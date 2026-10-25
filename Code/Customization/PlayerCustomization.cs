using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// What a player's character wears and how it is tinted: their outfit, kept by the host and shown to everyone.
/// <para>
/// The host owns it. It reads the player's saved outfit when the player arrives (<c>customization/{steamId}.json</c> in
/// the data folder, so a player keeps it on any computer), checks it against <see cref="CustomizationRules"/>, and
/// shares it with <c>[Sync]</c>: every machine, including a player who joins later or a body that respawns, dresses the
/// body from it. A player asks to change it with <see cref="Submit"/>, which the host cleans, saves and applies. Nothing
/// a client sends is kept as it is.
/// </para>
/// <para>
/// It is made on the host by <see cref="Player"/> (the player prefab need not have it) and refreshed to the clients, as
/// <see cref="PlayerProgress"/> does for the wallet. The outfit has no place in <see cref="ProgressRecord"/>: it is
/// not progress, and it has its own file.
/// </para>
/// </summary>
public sealed class PlayerCustomization : Component
{
	/// <summary>
	/// The outfit as <c>ClothingContainer.Serialize</c> writes it, or null for the player's own avatar.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public string OutfitJson { get; private set; }

	/// <summary>
	/// The tints (<see cref="OutfitColors.Serialize"/>), or null.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public string ColorsJson { get; private set; }

	/// <summary>
	/// The workshop packages the outfit's clothing comes from, comma separated: clients download them first.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public string Workshop { get; private set; }

	/// <summary>
	/// The host has read the saved outfit, so what is here is the player's real one (empty or not).
	/// </summary>
	[Sync( SyncFlags.FromHost )] public bool Loaded { get; private set; }

	public bool HasOutfit => !string.IsNullOrEmpty( OutfitJson );

	/// <summary>
	/// How long after a body is first dressed it is dressed once more: the body's own <see cref="Dresser"/> may not have
	/// started yet, and puts the avatar on over the outfit if it starts late.
	/// </summary>
	const float DressAgainAfter = 0.5f;

	/// <summary>
	/// How long after a dressing the body's bone objects are held to the renderer's pose (see <see cref="OnPreRender"/>).
	/// </summary>
	const float DressBoneSync = 1.5f;

	const string Folder = "customization";

	sealed class SavedOutfit
	{
		public string Outfit { get; set; }
		public string Colors { get; set; }
		public string Workshop { get; set; }
	}

	// The host's.
	bool started;
	ulong steamId;

	// Every machine's: what this body wears now.
	string wornOutfit;
	string wornColors;
	string wornWorkshop;
	bool everDressed;
	bool dressedAgain;
	RealTimeSince sinceDressed;
	RealTimeUntil syncBonesUntil;

	/// <summary>
	/// Makes the component on a player's object, on the host, and has the clients get it.
	/// </summary>
	public static void EnsureOn( Player player )
	{
		if ( !player.IsValid() || player.Components.Get<PlayerCustomization>() is not null )
			return;

		var created = player.Components.Create<PlayerCustomization>();

		if ( player.GameObject.Network.Active )
			player.GameObject.Network.Refresh( created );
	}

	/// <summary>
	/// The local player's, or null before they have spawned.
	/// </summary>
	public static PlayerCustomization Local => Players.Local<PlayerCustomization>();

	protected override void OnUpdate()
	{
		if ( Networking.IsHost )
			HostUpdate();

		if ( Loaded )
			Dress();
	}

	/// <summary>
	/// While a body is being dressed its bone objects (the hand a weapon is held by, which follow the animation) can stay at
	/// the bind pose for a while, and the weapon in the hand was drawn in a T-pose until they caught up. For the time
	/// after a dressing they are put where the renderer says the bones are, just before drawing.
	/// </summary>
	protected override void OnPreRender()
	{
		if ( syncBonesUntil <= 0 )
			return;

		var body = GetComponentInChildren<Dresser>( true )?.BodyTarget;

		if ( !body.IsValid() )
			return;

		foreach ( var bone in body.GameObject.GetAllObjects( false ).Where( x => x.Flags.HasFlag( GameObjectFlags.Bone ) ) )
		{
			if ( body.TryGetBoneTransform( bone.Name, out var pose ) && (bone.WorldPosition - pose.Position).LengthSquared > 0.01f )
				bone.WorldTransform = bone.WorldTransform.WithPosition( pose.Position ).WithRotation( pose.Rotation );
		}
	}

	// The host

	void HostUpdate()
	{
		if ( started )
			return;

		// The owner is set just after the player spawns, so it can be missing on the first frame.
		if ( (Network.Owner ?? Connection.Local) is not { } owner )
			return;

		started = true;
		steamId = owner.SteamId;
		Read();
		Loaded = true;
	}

	string PathFor() => $"{Folder}/{steamId}.json";

	// The saved outfit, cleaned under the rules as they are now.
	void Read()
	{
		var data = FileSystem.Data;

		if ( data is null || FileProgressStore.TryReadSafely<SavedOutfit>( data, PathFor() ) is not { } saved )
			return;

		Set( saved.Outfit, saved.Colors, saved.Workshop, null );
	}

	void Save()
	{
		if ( FileSystem.Data is not { } data || steamId == 0 )
			return;

		try
		{
			FileProgressStore.WriteSafely( data, Folder, PathFor(),
				Json.Serialize( new SavedOutfit { Outfit = OutfitJson, Colors = ColorsJson, Workshop = Workshop } ) );
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"PlayerCustomization: couldn't save the outfit of {steamId}: {e.Message}" );
		}
	}

	// Keeps only what the host accepts, and says what it left out. Returns whether the outfit is the one that came.
	bool Set( string outfit, string colors, string workshop, List<string> removed )
	{
		var cleaned = CustomizationRules.CleanOutfit( outfit, removed );

		OutfitJson = cleaned;
		ColorsJson = cleaned is null ? null : CustomizationRules.CleanColors( colors );
		Workshop = cleaned is null ? null : CustomizationRules.CleanIdents( workshop );

		return cleaned is not null && (removed?.Count ?? 0) == 0;
	}

	/// <summary>
	/// Asks the host to make this the player's outfit (null: their own avatar). Asked by the owner. The host cleans it,
	/// saves it and shows it to everyone.
	/// </summary>
	public void Request( string outfit, string colors, string workshop )
	{
		// The owner's own screen does not wait for the host: it puts on the outfit as the host will write it, so the
		// host's copy coming back is the same text and nothing is dressed twice.
		Wear( CustomizationRules.CleanOutfit( outfit, null, applyRules: false ), CustomizationRules.CleanColors( colors ),
			CustomizationRules.CleanIdents( workshop ) );
		Submit( outfit, colors, workshop );
	}

	[Rpc.Host]
	void Submit( string outfit, string colors, string workshop )
	{
		if ( Rpc.Caller != (Network.Owner ?? Connection.Local) || !Loaded )
			return;

		var removed = new List<string>();

		if ( string.IsNullOrEmpty( outfit ) )
		{
			OutfitJson = ColorsJson = Workshop = null;
		}
		else if ( !Set( outfit, colors, workshop, removed ) && OutfitJson is null )
		{
			// Not an outfit at all: keep what the player had.
			Log.Warning( $"PlayerCustomization: {steamId} sent an outfit that could not be read" );
			return;
		}

		Save();

		if ( removed.Count > 0 )
			Tell( $"Some of that clothing is not allowed on this server and was left out ({removed.Count})." );
	}

	/// <summary>
	/// The rules changed: clean the outfit again, and save and show it if something went.
	/// </summary>
	public void Recheck()
	{
		if ( !Networking.IsHost || !Loaded || !HasOutfit )
			return;

		var removed = new List<string>();
		var before = OutfitJson;
		Set( OutfitJson, ColorsJson, Workshop, removed );

		if ( OutfitJson == before )
			return;

		Save();
		Tell( $"The server no longer allows some of your clothing ({removed.Count} item(s) taken off)." );
	}

	void Tell( string text )
	{
		if ( (Network.Owner ?? Connection.Local) is { } owner )
			GameManager.Current?.Notify( owner, text );
	}

	// Every machine

	void Dress()
	{
		var dresser = GetComponentInChildren<Dresser>( true );

		if ( !dresser.IsValid() )
			return;

		var changed = OutfitJson != wornOutfit || ColorsJson != wornColors || Workshop != wornWorkshop;

		if ( changed && (HasOutfit || everDressed) )
		{
			Wear( OutfitJson, ColorsJson, Workshop );
		}
		else if ( HasOutfit && !dressedAgain && sinceDressed > DressAgainAfter )
		{
			dressedAgain = true;
			_ = Put( dresser, OutfitJson, ColorsJson, Workshop );
		}
	}

	// Puts it on this body now, and remembers it, so the same outfit coming back from the host is not put on twice.
	void Wear( string outfit, string colors, string workshop )
	{
		wornOutfit = outfit;
		wornColors = colors;
		wornWorkshop = workshop;
		// Only the first dressing after the body appears is repeated; later changes are dressed once.
		dressedAgain = everDressed;
		everDressed = true;
		sinceDressed = 0;
		syncBonesUntil = DressBoneSync;

		if ( GetComponentInChildren<Dresser>( true ) is { IsValid: true } dresser )
			_ = Put( dresser, outfit, colors, workshop );
	}

	static async Task Put( Dresser dresser, string outfit, string colors, string workshop )
	{
		// The body's own Dresser may be putting the avatar on: not over this.
		dresser.CancelDressing();

		if ( string.IsNullOrEmpty( outfit ) )
			await PlayerCustomizer.DressDefault( dresser );
		else
			await PlayerCustomizer.Dress( dresser, outfit, OutfitColors.Deserialize( colors ), workshop );
	}
}
