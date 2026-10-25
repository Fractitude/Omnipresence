namespace Sandbox;

/// <summary>
/// The dropped items of the world, kept in <c>world_items.json</c> so they are still there in the next session. Every
/// <see cref="WorldItem"/> a player dropped (and every ordinary item that dropped as its own prefab) is tracked here with
/// its whole <see cref="ItemData"/>, the prefab, the scene it lies in and where it lies. <see cref="WorldItemSystem"/>
/// restores them when the scene starts on the host.
/// </summary>
/// <remarks>
/// Host only. Not <c>readonly</c> (see AGENTS.md, "Static state and hot reload"): the file is read the first time it is
/// asked, and a hot reload keeps it. Changes are written <see cref="SaveDelay"/> seconds after the last one, never every
/// frame; <see cref="Flush"/> writes at once.
/// </remarks>
public static class WorldItemRegistry
{
	const string DefaultFile = "world_items.json";

	/// <summary>Seconds after the last change before the file is written.</summary>
	public const float SaveDelay = 3f;

	static WorldItemFile file;
	static string fileName;
	static bool dirty;
	static RealTimeSince sinceChange;

	/// <summary>
	/// The file in the data folder. A test uses another one so it never touches the real one.
	/// </summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>
	/// Keep the items in another file from now on, and read it again. Anything not yet written is dropped. For tests.
	/// Host only.
	/// </summary>
	public static void UseFile( string name )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return;

		fileName = name;
		file = null;
		dirty = false;
	}

	/// <summary>
	/// Whether a change waits to be written.
	/// </summary>
	public static bool IsDirty => dirty;

	/// <summary>
	/// What the registry holds, oldest first. Do not change the list.
	/// </summary>
	public static IReadOnlyList<WorldItemRecord> Records => Loaded.Items;

	static WorldItemFile Loaded => file ??= Load();

	/// <summary>
	/// The key of a scene in the file: its resource path, else its name.
	/// </summary>
	public static string SceneKey( Scene scene )
	{
		if ( !scene.IsValid() )
			return "";

		return !string.IsNullOrEmpty( scene.Source?.ResourcePath ) ? scene.Source.ResourcePath : scene.Name ?? "";
	}

	/// <summary>
	/// Start keeping a world item that was just made. A restored one passes its saved id and keeps its record. Returns the
	/// id. Host only.
	/// </summary>
	public static string Track( GameObject go, ItemData item, string prefab, string restoreId = null )
	{
		if ( !go.IsValid() || item is null || Networking.IsActive && !Networking.IsHost )
			return null;

		var key = SceneKey( go.Scene );
		var record = string.IsNullOrEmpty( restoreId ) ? null : Loaded.Items.FirstOrDefault( x => x.Id == restoreId );
		var known = record is not null;

		if ( !known )
		{
			record = new WorldItemRecord { Id = string.IsNullOrEmpty( restoreId ) ? WorldItemRules.NewId() : restoreId };
			Loaded.Items.Add( record );
		}

		record.Scene = key;
		record.Prefab = WorldItemRules.CleanPrefab( prefab );
		record.Item = item.Clone();
		record.Position = go.WorldPosition;
		record.Angles = go.WorldRotation.Angles();

		if ( WorldItemRules.Cap( Loaded.Items, WorldItem.MaxInScene ) > 0 )
			Log.Warning( $"World items: more than {WorldItem.MaxInScene} in a scene, the oldest were forgotten" );

		if ( go.Components.Get<WorldItemKeeper>() is not { } keeper )
			keeper = go.Components.Create<WorldItemKeeper>();

		keeper.ItemId = record.Id;
		keeper.Rest = go.WorldPosition;

		// A restored item is already in the file as it is.
		if ( !known )
			Changed();

		return record.Id;
	}

	/// <summary>
	/// The id this object is kept under, or empty if it is not a tracked world item.
	/// </summary>
	public static string IdOf( GameObject go ) =>
		go.IsValid() && go.Components.Get<WorldItemKeeper>() is { } keeper ? keeper.ItemId : "";

	/// <summary>
	/// Write down where an item came to rest. Host only.
	/// </summary>
	public static void SetPlace( string id, Vector3 position, Angles angles )
	{
		if ( !WorldItemRules.IsPlace( position ) || Find( id ) is not { } record )
			return;

		record.Position = position;
		record.Angles = angles;
		Changed();
	}

	// -- Items placed by hand -----------------------------------------------------------------------------------------

	/// <summary>
	/// What a hand-placed item does as its scene loads (<see cref="HandPlacedRules.Decide"/>). Host only.
	/// </summary>
	public static HandPlacedDecision DecideHandPlaced( Scene scene, string id ) =>
		HandPlacedRules.Decide( Loaded.HandPlaced, SceneKey( scene ), id );

	/// <summary>
	/// Record that a hand-placed item is gone for good: picked up, taken from its place, or fallen out of the world. It is
	/// removed from the scene on every later load. Host only.
	/// </summary>
	public static void MarkCollected( Scene scene, string id )
	{
		if ( HandPlacedRules.Collect( Loaded.HandPlaced, SceneKey( scene ), id ) )
			Changed();
	}

	/// <summary>
	/// Forget what was taken from the hand-placed items of a scene (or of every scene, with null), so they are as the
	/// editor has them the next time the scene loads. The objects already gone from the running scene come back then, not
	/// now. Returns how many records went. Host only.
	/// </summary>
	public static int ResetHandPlaced( Scene scene )
	{
		var removed = HandPlacedRules.Reset( Loaded.HandPlaced, scene.IsValid() ? SceneKey( scene ) : null );

		if ( removed > 0 )
			Changed();

		return removed;
	}

	/// <summary>
	/// The hand-placed item id of this object, or empty if it is not one (or is no longer, once it has been moved).
	/// </summary>
	public static string HandPlacedIdOf( GameObject go ) =>
		go.IsValid() && go.Components.Get<WorldItem>() is { } item ? item.HandPlacedId : "";

	/// <summary>
	/// Write down that the Physgun froze or unfroze an item, and where it stands now. Nothing is written when the state
	/// is what the file already says. Host only.
	/// </summary>
	public static void SetFrozen( string id, bool frozen, Vector3 position, Angles angles )
	{
		if ( Find( id ) is not { } record )
			return;

		if ( WorldItemRules.IsPlace( position ) )
		{
			record.Position = position;
			record.Angles = angles;
		}

		if ( record.Frozen == frozen && !WorldItemRules.IsPlace( position ) )
			return;

		record.Frozen = frozen;
		Changed();
	}

	/// <summary>
	/// Whether the file says this item is frozen.
	/// </summary>
	public static bool IsFrozen( string id ) => Find( id )?.Frozen ?? false;

	/// <summary>
	/// Write down that an item changed (a partial pickup left fewer). Host only.
	/// </summary>
	public static void SetItem( GameObject go, ItemData item )
	{
		// What is left of a hand-placed item is what the scene shows next time.
		if ( item is not null && HandPlacedIdOf( go ) is { Length: > 0 } handId )
		{
			if ( HandPlacedRules.SetRemaining( Loaded.HandPlaced, SceneKey( go.Scene ), handId, item ) )
				Changed();

			return;
		}

		if ( item is null || Find( IdOf( go ) ) is not { } record )
			return;

		record.Item = item.Clone();
		Changed();
	}

	/// <summary>
	/// Forget a world item that is gone for good (picked up, out of bounds). Host only.
	/// </summary>
	public static void Forget( GameObject go )
	{
		// A hand-placed item that is taken is recorded as collected, so the scene does not bring it back.
		if ( HandPlacedIdOf( go ) is { Length: > 0 } handId )
			MarkCollected( go.Scene, handId );

		Forget( IdOf( go ) );
	}

	/// <summary>
	/// Forget the item with this id. Host only.
	/// </summary>
	public static void Forget( string id )
	{
		if ( string.IsNullOrEmpty( id ) || Loaded.Items.RemoveAll( x => x.Id == id ) < 1 )
			return;

		Changed();
	}

	/// <summary>
	/// Remove every world item of the scene, and every entry of the file. Returns how many objects were removed. For the
	/// wipe of a season and the dev command. Host only.
	/// </summary>
	public static int Wipe( Scene scene )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return 0;

		var removed = 0;

		if ( scene.IsValid() )
		{
			foreach ( var go in scene.GetAllComponents<WorldItemKeeper>().Select( x => x.GameObject ).ToList() )
			{
				go.Destroy();
				removed++;
			}
		}

		Loaded.Items.Clear();

		// The same wipe gives the hand-placed items back: they are as the editor has them at the next load.
		Loaded.HandPlaced.Clear();
		Changed();
		Flush();
		return removed;
	}

	/// <summary>
	/// Make the saved items of this scene again, at their saved places. An item already in the scene (by id) is left alone,
	/// so calling this twice, or after a hot reload, never duplicates anything. Returns how many were made. Host only.
	/// </summary>
	public static int Restore( Scene scene )
	{
		if ( !scene.IsValid() || Networking.IsActive && !Networking.IsHost )
			return 0;

		var key = SceneKey( scene );
		var live = scene.GetAllComponents<WorldItemKeeper>().Select( x => x.ItemId ).ToHashSet();
		var made = 0;

		foreach ( var record in Loaded.Items.Where( x => x.Scene == key && !live.Contains( x.Id ) ).ToList() )
		{
			var go = WorldItem.Spawn( record.Item.Clone(), record.Position, record.Angles.ToRotation(), Vector3.Zero,
				record.Id, record.Frozen );

			if ( go.IsValid() )
				made++;
			else
				Log.Warning( $"World items: couldn't restore {record.Item.Name} ({record.Id})" );
		}

		return made;
	}

	/// <summary>
	/// Write the file if a change waits and the last one is old enough. Called by <see cref="WorldItemSystem"/>.
	/// </summary>
	public static void Tick()
	{
		if ( dirty && sinceChange >= SaveDelay )
			Flush();
	}

	/// <summary>
	/// Write the file now if a change waits.
	/// </summary>
	public static void Flush()
	{
		if ( !dirty )
			return;

		dirty = false;

		try
		{
			if ( FileSystem.Data is { } data )
				FileProgressStore.WriteSafely( data, "", FileName, Json.Serialize( Loaded ) );
		}
		catch ( System.Exception e )
		{
			dirty = true;
			Log.Warning( e, $"Couldn't save {FileName}: {e.Message}" );
		}
	}

	/// <summary>
	/// Delete the file and its backup. For the dev command and tests.
	/// </summary>
	public static void DeleteFile()
	{
		try
		{
			if ( FileSystem.Data is not { } data )
				return;

			foreach ( var path in new[] { FileName, FileName + ".bak" } )
			{
				if ( data.FileExists( path ) )
					data.DeleteFile( path );
			}
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't delete {FileName}: {e.Message}" );
		}
	}

	static WorldItemRecord Find( string id ) =>
		string.IsNullOrEmpty( id ) ? null : Loaded.Items.FirstOrDefault( x => x.Id == id );

	static void Changed()
	{
		dirty = true;
		sinceChange = 0;
	}

	static WorldItemFile Load()
	{
		var loaded = new WorldItemFile();

		try
		{
			if ( FileSystem.Data is { } data
			     && FileProgressStore.TryReadSafely<WorldItemFile>( data, FileName ) is { } saved )
				loaded = saved;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Couldn't read {FileName}: {e.Message}" );
		}

		var dropped = WorldItemRules.Sanitise( loaded, WorldItem.MaxInScene );

		if ( dropped > 0 )
			Log.Warning( $"World items: {dropped} entries of {FileName} were invalid or over the limit and are dropped" );

		return loaded;
	}
}
