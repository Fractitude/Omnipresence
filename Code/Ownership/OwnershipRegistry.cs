namespace Sandbox;

/// <summary>
/// Who owns what, kept in <c>ownership.json</c> (the data folder, like <c>placed_objects.json</c>). One record per
/// thing: its key, its owner, since when, and the last few changes of hands.
/// </summary>
/// <remarks>
/// Host only, and the single source of truth for a title. <see cref="OwnershipComponent"/> is a synced copy of it on
/// an object so a client can read the owner without the file, and <see cref="PlacedObject"/> and the rest seed it once;
/// after that nobody writes a title anywhere but here, which is what stops two of them disagreeing.
/// <para>
/// A thing's key is <see cref="KeyOf(GameObject)"/> for an object in a scene, and any key the caller makes for a thing
/// that is not one (a business, a licence, a bank account). Not <c>readonly</c> (see AGENTS.md, "Static state and hot
/// reload"): loaded the first time it is asked, and a hot reload keeps it.
/// </para>
/// <para>
/// This writes titles, it does not police them: <see cref="Transfer"/> is what game code calls for a purchase, a
/// licence or a repossession, and a player's own request goes through <see cref="OwnershipComponent.Transfer"/>, which
/// checks <see cref="OwnershipRules"/> first.
/// </para>
/// </remarks>
public static class OwnershipRegistry
{
	const string DefaultFile = "ownership.json";

	/// <summary>How many changes of hands a record keeps. The rest are dropped, oldest first.</summary>
	public const int MaxHistory = 12;

	static OwnershipFile file;
	static string fileName;

	/// <summary>The file in the data folder. A test uses another one so it never touches the real one.</summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>
	/// Keep the records in another file from now on, and read it again. Anything not yet written is dropped. For
	/// tests. Host only.
	/// </summary>
	public static void UseFile( string name )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		fileName = name;
		file = null;
	}

	/// <summary>Everything the registry holds. Change it only through the methods here.</summary>
	public static OwnershipFile Data => Loaded;

	/// <summary>Goes up on every change, so a panel can tell it must draw again.</summary>
	public static int Version { get; private set; }

	static OwnershipFile Loaded => file ??= Load();

	// -- Keys ---------------------------------------------------------------------------------------------------------

	/// <summary>
	/// The key a thing in the world is kept under: the scene's key and the object's permanent id. A scene-placed object
	/// is known by its object id, which the scene file keeps for ever; a bought one by its <see cref="PlacedObject"/>
	/// id, which is the same in every session. A thing that carries an <see cref="OwnershipComponent"/> with a
	/// <see cref="OwnershipComponent.ThingId"/> is known by that instead, so a car or a business can have a name of its
	/// own. Look from the object itself or from a component on it.
	/// </summary>
	public static string KeyOf( GameObject go )
	{
		if ( !go.IsValid() )
			return "";

		var thing = go.Components.Get<OwnershipComponent>( FindMode.EverythingInSelfAndParent );

		if ( thing.IsValid() && !string.IsNullOrWhiteSpace( thing.ThingId ) )
			return thing.ThingId.Trim();

		return Key( WorldItemRegistry.SceneKey( go.Scene ), PlacedObject.IdOf( go ) );
	}

	/// <summary>
	/// A key from a scope and an id: the scope is <see cref="WorldItemRegistry.SceneKey"/> for a thing in a scene, and
	/// anything stable for a thing that is not in one.
	/// </summary>
	public static string Key( string scope, string id )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			return "";

		return string.IsNullOrWhiteSpace( scope ) ? id.Trim() : $"{scope.Trim()}/{id.Trim()}";
	}

	// -- Reading ------------------------------------------------------------------------------------------------------

	/// <summary>The record of a thing, or null when nobody ever wrote one down.</summary>
	public static OwnershipRecord Find( string key ) =>
		string.IsNullOrEmpty( key ) ? null : Loaded.Records.FirstOrDefault( x => x.Id == key );

	/// <summary>Who owns the thing with this key, or nobody.</summary>
	public static Ownership At( string key ) => Ownership.Parse( Find( key )?.Owner );

	/// <summary>Who owns an object in the world, or nobody.</summary>
	public static Ownership OwnerOf( GameObject go ) => At( KeyOf( go ) );

	/// <summary>Every record of this owner, oldest first.</summary>
	public static IReadOnlyList<OwnershipRecord> RecordsOf( Ownership owner ) =>
		owner is null || !owner.IsOwned
			? []
			: Loaded.Records.Where( x => Ownership.Parse( x.Owner ).Equals( owner ) ).ToList();

	/// <summary>Everything this player owns.</summary>
	public static IReadOnlyList<OwnershipRecord> OfPlayer( ulong steamId ) => RecordsOf( Ownership.Player( steamId ) );

	/// <summary>Everything this organization owns.</summary>
	public static IReadOnlyList<OwnershipRecord> OfOrg( string orgId ) => RecordsOf( Ownership.Organization( orgId ) );

	/// <summary>The object in the world a key belongs to, or null when it is not in the scene now.</summary>
	public static OwnershipComponent Live( string key )
	{
		if ( string.IsNullOrEmpty( key ) || !Game.ActiveScene.IsValid() )
			return null;

		return Game.ActiveScene.GetAllComponents<OwnershipComponent>().FirstOrDefault( x => x.Key == key );
	}

	// -- Writing ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// Give a thing a new owner, and write it down at once. <paramref name="by"/> is the player who did it (0 for the
	/// game itself) and <paramref name="reason"/> says why, for the record's history. A transfer to nobody releases
	/// it. Returns whether anything changed. Host only.
	/// </summary>
	public static bool Transfer( string key, Ownership to, ulong by = 0, string reason = "" )
	{
		if ( string.IsNullOrEmpty( key ) || to is null || OwnershipRules.Check( to ) is not null )
			return false;

		if ( Networking.IsActive && !Networking.IsHost )
			return false;

		var record = Find( key );
		var from = Ownership.Parse( record?.Owner );

		if ( from.Equals( to ) )
			return false;

		var now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		if ( record is null )
		{
			record = new OwnershipRecord { Id = key };
			Loaded.Records.Add( record );
		}

		record.Owner = to.Key;
		record.Since = now;
		record.History ??= new();
		record.History.Add( new() { From = from.Key, To = to.Key, By = by, At = now, Reason = reason ?? "" } );

		if ( record.History.Count > MaxHistory )
			record.History.RemoveRange( 0, record.History.Count - MaxHistory );

		Save();
		Announce( record, from, to, by, now, reason );
		return true;
	}

	/// <summary>
	/// Forget the title of a thing, so it is nobody's again. The same as <see cref="Transfer"/> to nobody, for the
	/// callers that read better that way. Host only.
	/// </summary>
	public static bool Release( string key, ulong by = 0, string reason = "" ) =>
		Transfer( key, Ownership.Nobody, by, reason );

	/// <summary>
	/// Forget every title of a scene, or every title at all when no scene is given, and clear the copies on the
	/// objects. Returns how many records were removed. For the wipe of a season and the dev command. Host only.
	/// </summary>
	public static int Wipe( Scene scene = null )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return 0;

		if ( scene.IsValid() )
		{
			foreach ( var thing in scene.GetAllComponents<OwnershipComponent>() )
				thing.Forget();

			var prefix = WorldItemRegistry.SceneKey( scene );

			if ( string.IsNullOrEmpty( prefix ) )
				return 0;

			prefix += "/";
			var removedInScene = Loaded.Records.RemoveAll(
				x => x.Id.StartsWith( prefix, System.StringComparison.Ordinal ) );
			Save();
			return removedInScene;
		}

		var removed = Loaded.Records.Count;
		Loaded.Records.Clear();

		if ( Game.ActiveScene.IsValid() )
		{
			foreach ( var thing in Game.ActiveScene.GetAllComponents<OwnershipComponent>() )
				thing.Forget();
		}

		Save();
		return removed;
	}

	// -- The file -----------------------------------------------------------------------------------------------------

	static OwnershipFile Load()
	{
		var loaded = new OwnershipFile();

		try
		{
			if ( FileSystem.Data is { } data
			     && FileProgressStore.TryReadSafely<OwnershipFile>( data, FileName ) is { } saved )
				loaded = saved;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Could not read {FileName}: {e.Message}" );
		}

		var dropped = Sanitise( loaded );

		if ( dropped > 0 )
			Log.Warning( $"Ownership: {dropped} entries of {FileName} were invalid and are dropped" );

		return loaded;
	}

	/// <summary>
	/// Clean a loaded file in place: a record without a key, an owner that is not an owner (a player without an id, an
	/// organization without one), a repeated key, and more history than a record keeps. Returns how many records were
	/// dropped.
	/// </summary>
	static int Sanitise( OwnershipFile loaded )
	{
		loaded.Records ??= new();
		var before = loaded.Records.Count;
		var keys = new HashSet<string>();

		loaded.Records = loaded.Records
			.Where( x => x is not null && !string.IsNullOrWhiteSpace( x.Id ) && keys.Add( x.Id ) )
			.ToList();

		loaded.Records.RemoveAll( x =>
		{
			var owner = Ownership.Parse( x.Owner );

			if ( owner.IsOwned && OwnershipRules.Check( owner ) is null )
				return false;

			// A record that says nobody owns the thing says nothing: the thing is free either way.
			return true;
		} );

		foreach ( var record in loaded.Records )
		{
			record.History ??= new();

			if ( record.History.Count > MaxHistory )
				record.History.RemoveRange( 0, record.History.Count - MaxHistory );
		}

		loaded.Version = OwnershipFile.CurrentVersion;
		return before - loaded.Records.Count;
	}

	static void Save()
	{
		Version++;

		try
		{
			if ( FileSystem.Data is { } data )
				FileProgressStore.WriteSafely( data, "", FileName, Json.Serialize( Loaded ) );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Could not save {FileName}: {e.Message}" );
		}
	}

	/// <summary>Tell the object in the world and the scene that the thing changed hands.</summary>
	static void Announce( OwnershipRecord record, Ownership from, Ownership to, ulong by, long at, string reason )
	{
		if ( !Game.ActiveScene.IsValid() )
			return;

		var thing = Live( record.Id );
		thing?.Adopt( to );

		var change = new OwnershipChange { From = from.Key, To = to.Key, By = by, At = at, Reason = reason ?? "" };
		Global.IOwnershipEvents.Post( x => x.OnOwnershipChanged( change, thing ) );
	}
}
