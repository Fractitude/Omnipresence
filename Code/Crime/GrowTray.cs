namespace Sandbox;

/// <summary>
/// A grow tray. Plant a drug (the player chooses which in the planting panel, <see cref="PlantingMenu"/>), wait for the
/// game clock, harvest its raw. A tray holds one crop at a time, and the crop is the tray's, not a player's: what is in
/// it is synced from the host (<see cref="PlantedKind"/>, <see cref="ReadyAt"/>), so everyone who looks at the same
/// tray sees the same drug at the same stage, and whoever harvests it gets the raw (<see cref="PlayerCrime.Harvest"/>).
/// The crop is kept in <see cref="TrayRegistry"/> so a restart does not empty it.
/// <para>
/// The tray has two options, of which one shows at a time, the same for every player: "Plant" while it is empty (it
/// runs on the player's own client and only opens the panel; the panel's Plant button asks the host with
/// <see cref="PlayerCrime.RequestPlant"/>) and "Growing Haze, 0:25 left" then "Harvest Haze" once planted (this one
/// runs on the host). Put it with an <see cref="Interactable"/> (network mode Object) and a collider, or the press
/// never reaches the host.
/// </para>
/// <para>
/// Above the tray floats a text with the same crop: "Growing Haze 62%  0:46", or "Haze ready to harvest". It is made in
/// code on each machine while the game runs, is never networked or saved, and shows nothing while the tray is empty or
/// the player is far from it.
/// </para>
/// </summary>
public sealed class GrowTray : Component, IInteractionHandler
{
	const string OptionId = "tray";
	const string PlantId = "plant";

	/// <summary>How close, in world units, a player must be to a tray for the host to plant in it.</summary>
	public const float Reach = 300f;

	/// <summary>How far the camera can be, in world units, for the progress text to show.</summary>
	const float LabelRange = 250f;

	/// <summary>How often the tray's state is read again, in seconds. The text itself changes once a second.</summary>
	const float LabelRefresh = 0.25f;

	/// <summary>
	/// The drug planted (<see cref="ProductDef.Id"/>), or empty while the tray is empty. Synced from the host: the crop
	/// is one state every player sees, not a copy each.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public string PlantedKind { get; set; } = "";

	/// <summary>
	/// Game seconds (<see cref="WorldClock.Now"/>) when the crop is ready. Synced from the host with the drug, so the
	/// countdown is the same for everyone.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public long ReadyAt { get; set; }

	/// <summary>
	/// Whether something grows in the tray.
	/// </summary>
	public bool Planted => !string.IsNullOrEmpty( PlantedKind );

	/// <summary>
	/// Whether the crop is ready to harvest now.
	/// </summary>
	public bool IsReady => Planted && WorldClock.Now >= ReadyAt;

	/// <summary>
	/// The quality of the crop, fixed when it was planted. Host only and not synced: the harvest runs on the host, so
	/// a client never needs it and never learns it.
	/// </summary>
	public int Quality { get; set; }

	/// <summary>
	/// The tray's id in the save: a bought tray's permanent <see cref="PlacedObject.ObjectId"/>, else its object's
	/// id, which is stable in the scene.
	/// </summary>
	public string TrayId => PlacedObject.IdOf( this );

	TextRenderer label;
	TimeUntil nextRefresh;
	string shownKey = "";

	/// <summary>
	/// The grow tray with this id in the scene, or null. The host works it out from its own world, never from what a
	/// client says about a tray.
	/// </summary>
	public static GrowTray Find( string trayId ) => string.IsNullOrEmpty( trayId ) || !Game.ActiveScene.IsValid()
		? null
		: Game.ActiveScene.GetAllComponents<GrowTray>().FirstOrDefault( x => x.TrayId == trayId );

	/// <summary>
	/// Whether the tray with this id stands within <see cref="Reach"/> of this position.
	/// </summary>
	public static bool IsNear( string trayId, Vector3 position ) =>
		Find( trayId ) is { } tray && tray.WorldPosition.Distance( position ) <= Reach;

	protected override void OnStart()
	{
		InteractionSetup.EnsureOption( this, OptionId, "Grow tray", "grass", "Harvest what grows in the tray." );
		var plant = InteractionSetup.EnsureOption( this, PlantId, "Plant", "grass", "Choose what to plant." );

		// The press only opens the panel on the player's own screen.
		if ( plant is not null )
			plant.RunOn = InteractionSide.Local;

		// A restart forgets what grows where: the host reads it back, so the crop is still there and still ripening.
		if ( Networking.IsHost && Game.IsPlaying && !Planted )
			RestoreCrop();
	}

	/// <summary>
	/// Put a crop in the tray. Host only; a client's tray learns it from the sync.
	/// </summary>
	public void SetCrop( string kind, long readyAt, int quality )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		PlantedKind = kind ?? "";
		ReadyAt = readyAt;
		Quality = quality;
		TrayRegistry.Set( TrayId, PlantedKind, ReadyAt, Quality );
	}

	/// <summary>
	/// Take the crop out of the tray, which empties it. Host only.
	/// </summary>
	public void ClearCrop()
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		PlantedKind = "";
		ReadyAt = 0;
		Quality = 0;
		TrayRegistry.Forget( TrayId );
	}

	/// <summary>
	/// Make the crop in the tray ripe now, keeping it planted. Host only. For the dev command.
	/// </summary>
	public void RipenCrop()
	{
		if ( !IsReady )
			SetCrop( PlantedKind, WorldClock.Now, Quality );
	}

	/// <summary>
	/// The crop saved for this tray, if there is one. Only read for an empty tray (a restart), and only where
	/// <see cref="TrayId"/> is known: a bought tray's id is put on it before it starts.
	/// </summary>
	void RestoreCrop()
	{
		if ( string.IsNullOrEmpty( TrayId ) || TrayRegistry.Crop( TrayId ) is not { } saved )
			return;

		PlantedKind = saved.Kind;
		ReadyAt = saved.ReadyAt;
		Quality = saved.Quality;
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id is not (OptionId or PlantId) )
			return null;

		// One row at a time: Plant while the tray is empty, the growing and harvest row once it is planted. An empty
		// label hides a row.
		if ( e.Option.Id == PlantId )
		{
			if ( Planted )
				return new( "" );

			var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;
			var has = ProductCatalog.Products.Any( x => inventory?.CountOf( x.Seed.Id ) > 0 );

			return has
				? new( "Plant", "grass", "Choose what to plant." )
				: new( "Plant", "grass", "You have no seeds. Buy some in Hush.", false );
		}

		if ( !Planted )
			return new( "" );

		var product = ProductCatalog.OfTray( PlantedKind );
		var left = ReadyAt - WorldClock.Now;

		return left > 0
			? new( $"Growing {product.Name}, {InteractionSetup.Countdown( left )} left", "hourglass_top", "", false )
			: new( $"Harvest {product.Name}", "agriculture", $"Gives {product.HarvestRaw} {product.Raw.Name}." );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		// Planting only opens the panel, on the player's own screen. The panel's button asks the host.
		if ( e.Option?.Id == PlantId )
		{
			if ( !Application.IsHeadless )
				PlantingMenu.Open( this );

			return;
		}

		if ( e.Option?.Id != OptionId || !Networking.IsHost )
			return;

		if ( e.Interactor?.Components.Get<PlayerCrime>() is not { } crime )
			return;

		if ( Planted )
			crime.Tell( crime.Harvest( TrayId ) );
	}

	protected override void OnUpdate()
	{
		if ( Application.IsHeadless )
			return;

		if ( !label.IsValid() )
			label = MakeLabel();

		var camera = Scene.Camera;
		var near = camera.IsValid() && camera.WorldPosition.Distance( label.WorldPosition ) <= LabelRange;

		if ( !near )
		{
			label.Enabled = false;
			shownKey = "";
			return;
		}

		if ( !nextRefresh )
			return;

		nextRefresh = LabelRefresh;
		RefreshLabel();
	}

	/// <summary>
	/// The text above the tray: a child that exists only on this machine and is never saved, so the placed tray and its
	/// prefab need no change. It turns to face the camera by itself.
	/// </summary>
	TextRenderer MakeLabel()
	{
		var bounds = GameObject.GetBounds();
		var position = bounds.Size.Length > 0.1f
			? new Vector3( bounds.Center.x, bounds.Center.y, bounds.Maxs.z + 8f )
			: WorldPosition + Vector3.Up * 30f;

		var child = new GameObject( false, "Grow status" )
		{
			Parent = GameObject,
			WorldPosition = position,
			NetworkMode = NetworkMode.Never,
			Flags = GameObjectFlags.NotSaved,
		};

		var text = child.AddComponent<TextRenderer>();
		text.Billboard = TextRenderer.BillboardMode.YOnly;
		text.Scale = 0.06f;
		text.FontSize = 48f;
		text.FontWeight = 600;
		text.Enabled = false;
		child.Enabled = true;

		return text;
	}

	/// <summary>
	/// Reads the tray's crop and sets the text, only when what it says changes.
	/// </summary>
	void RefreshLabel()
	{
		if ( !Planted )
		{
			label.Enabled = false;
			shownKey = "";
			return;
		}

		var left = System.Math.Max( ReadyAt - WorldClock.Now, 0 );
		var key = $"{PlantedKind}/{left}";

		if ( label.Enabled && key == shownKey )
			return;

		shownKey = key;

		var product = ProductCatalog.OfTray( PlantedKind );

		if ( left > 0 )
		{
			var percent = System.Math.Clamp( (product.GrowSeconds - left) * 100 / product.GrowSeconds, 0, 99 );
			label.Text = $"Growing {product.Name} {percent}%  {InteractionSetup.Countdown( left )}";
			label.Color = new Color( 0.85f, 0.8f, 0.55f, 0.75f );
		}
		else
		{
			label.Text = $"{product.Name} ready to harvest";
			label.Color = new Color( 0.55f, 1f, 0.6f, 1f );
		}

		label.Enabled = true;
	}
}
