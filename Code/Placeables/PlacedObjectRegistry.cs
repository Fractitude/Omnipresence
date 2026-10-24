namespace Sandbox;

/// <summary>
/// The Depot's objects, kept in <c>placed_objects.json</c> (the data folder, like <c>world_items.json</c>;
/// <c>placed_objects_test.json</c> under <c>progress_test_data on</c>): every object a player bought that stands in the
/// world, with its permanent id, its buyer, the scene and where it came to rest, and every purchase still on its way.
/// <see cref="PlacedObjectSystem"/> makes the deliveries arrive, restores the objects when the scene starts, and writes
/// the file.
/// </summary>
/// <remarks>
/// Host only. Not <c>readonly</c> (see AGENTS.md, "Static state and hot reload"): the file is read the first time it is
/// asked, and a hot reload keeps it. Changes are written <see cref="SaveDelay"/> seconds after the last one;
/// <see cref="Flush"/> writes at once. Deliveries live here and not in a player's record so that an absent buyer's
/// order still arrives; the price of that is that a season wipe of the crime state does not clear them, so the wipe
/// must call <see cref="Wipe"/> (like <see cref="WorldItemRegistry.Wipe"/>).
/// </remarks>
public static class PlacedObjectRegistry
{
	const string DefaultFile = "placed_objects.json";

	/// <summary>Seconds after the last change before the file is written.</summary>
	public const float SaveDelay = 3f;

	static PlacedObjectFile file;
	static string fileName;
	static bool dirty;
	static RealTimeSince sinceChange;

	/// <summary>
	/// The file in the data folder. A test uses another one so it never touches the real one.
	/// </summary>
	public static string FileName => string.IsNullOrEmpty( fileName ) ? DefaultFile : fileName;

	/// <summary>
	/// Keep the objects in another file from now on, and read it again. Anything not yet written is dropped. For tests.
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
	/// Everything the registry holds. Change it only through the registry's methods.
	/// </summary>
	public static PlacedObjectFile Data => Loaded;

	static PlacedObjectFile Loaded => file ??= Load();

	// -- Orders and deliveries ----------------------------------------------------------------------------------------

	/// <summary>
	/// The purchases of a player that are still on their way, soonest first.
	/// </summary>
	public static List<PlacedDeliveryRecord> OnTheWay( ulong owner ) => Loaded.Deliveries
		.Where( x => x.Owner == owner && !x.Arrived )
		.OrderBy( x => x.ArriveAt )
		.ToList();

	/// <summary>
	/// Put an order on its way. The caller takes the money after this succeeded. Returns the delivery, or null when the
	/// player has too many on their way. Host only.
	/// </summary>
	public static PlacedDeliveryRecord Order( ulong owner, Placeable item, long now )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return null;

		var delivery = PlacedObjectRules.Order( Loaded, owner, item, now );

		if ( delivery is not null )
			Changed();

		return delivery;
	}

	/// <summary>
	/// Take back an order that was just put on its way (the payment failed). Host only.
	/// </summary>
	public static void Cancel( PlacedDeliveryRecord delivery )
	{
		if ( Loaded.Deliveries.Remove( delivery ) )
			Changed();
	}

	/// <summary>
	/// Whether the player may have another delivery on its way.
	/// </summary>
	public static bool CanOrder( ulong owner ) =>
		PlacedObjectRules.InTransit( Loaded, owner ) < PlaceableCatalog.MaxInTransit
		&& Loaded.Deliveries.Count < PlacedObjectRules.MaxDeliveries;

	/// <summary>
	/// A delivery arrived: write down where. Host only.
	/// </summary>
	public static void MarkArrived( PlacedDeliveryRecord delivery, string where )
	{
		delivery.Arrived = true;
		delivery.Where = where ?? "";
		Changed();
	}

	/// <summary>
	/// A delivery's buyer has been told: forget it. Host only.
	/// </summary>
	public static void Told( PlacedDeliveryRecord delivery )
	{
		if ( Loaded.Deliveries.Remove( delivery ) )
			Changed();
	}

	// -- Objects ------------------------------------------------------------------------------------------------------

	/// <summary>
	/// The record of an object by id, or null.
	/// </summary>
	public static PlacedObjectRecord Find( string id ) =>
		string.IsNullOrEmpty( id ) ? null : Loaded.Objects.FirstOrDefault( x => x.Id == id );

	/// <summary>
	/// Make an object (a copy of the catalog's prefab with a <see cref="PlacedObject"/> carrying <paramref name="id"/>)
	/// and network-spawn it; write it down if it is not in the file yet, or move its record if it is. This is the one
	/// way a placed object comes into the world: a delivery, an unload from a car's cargo. (A restore at start uses
	/// <see cref="Restore"/>.) Returns null when it cannot be made. Host only.
	/// </summary>
	public static GameObject Create(
		Placeable item, string id, ulong owner, Vector3 position, Rotation rotation, Scene scene = null )
	{
		if ( item is null || !PlacedObjectRules.IsId( id ) || Networking.IsActive && !Networking.IsHost )
			return null;

		scene ??= Game.ActiveScene;
		var go = Make( item, id, owner, position, rotation, scene );

		if ( !go.IsValid() )
			return null;

		var record = Find( id );

		if ( record is null )
		{
			record = new PlacedObjectRecord { Id = id, Placeable = item.Id, Owner = owner };
			Loaded.Objects.Add( record );
		}

		record.Prefab = PlacedObjectRules.CleanPrefab( item.Prefab );
		record.Scene = WorldItemRegistry.SceneKey( scene );
		record.Position = position;
		record.Angles = rotation.Angles();

		Changed();
		return go;
	}

	/// <summary>
	/// The object itself, without touching the file. Disabled while it is set up, then network-spawned: a physics body
	/// for the Physgun (the prefabs' colliders are static, as a scene-placed object should be), the id, and the host as
	/// the keeper of it if the player who last held it leaves.
	/// </summary>
	static GameObject Make( Placeable item, string id, ulong owner, Vector3 position, Rotation rotation, Scene scene )
	{
		GameObject go;

		try
		{
			using ( scene.Push() )
				go = GameObject.Clone( item.Prefab, new Transform( position, rotation ), null, false );
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Placed objects: could not make {item.Name}: {e.Message}" );
			return null;
		}

		if ( !go.IsValid() )
			return null;

		go.Name = item.Name;

		var placed = go.Components.GetOrCreate<PlacedObject>();
		placed.ObjectId = id;
		placed.PlaceableId = item.Id;
		placed.BuyerId = owner;
		placed.Rest = position;

		if ( !go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants ).IsValid() )
			go.Components.Create<Rigidbody>();

		foreach ( var collider in go.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			collider.Static = false;

		go.Enabled = true;
		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn();
		go.Network.SetOrphanedMode( NetworkOrphaned.Host );
		return go;
	}

	/// <summary>
	/// Write down where an object came to rest. Host only.
	/// </summary>
	public static void SetPlace( string id, Vector3 position, Angles angles )
	{
		if ( !WorldItemRules.IsPlace( position ) || Find( id ) is not { } record )
			return;

		record.Position = position;
		record.Angles = angles;
		Changed();
	}

	/// <summary>
	/// Forget an object for good. Host only.
	/// </summary>
	public static void Forget( string id )
	{
		if ( string.IsNullOrEmpty( id ) || Loaded.Objects.RemoveAll( x => x.Id == id ) < 1 )
			return;

		Changed();
	}

	/// <summary>
	/// Remove every placed object of the scene, and every record and delivery of the file. Returns how many objects were
	/// removed. For the wipe of a season and the dev command. Host only.
	/// </summary>
	public static int Wipe( Scene scene )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return 0;

		var removed = 0;

		if ( scene.IsValid() )
		{
			foreach ( var go in scene.GetAllComponents<PlacedObject>().Select( x => x.GameObject ).ToList() )
			{
				go.Destroy();
				removed++;
			}
		}

		Loaded.Objects.Clear();
		Loaded.Deliveries.Clear();
		Changed();
		Flush();
		return removed;
	}

	/// <summary>
	/// Make the saved objects of this scene again, where they last stood. An object already in the scene (by id) is left
	/// alone, so calling this twice, or after a hot reload, never duplicates anything. Returns how many were made. Host
	/// only.
	/// </summary>
	public static int Restore( Scene scene )
	{
		if ( !scene.IsValid() || Networking.IsActive && !Networking.IsHost )
			return 0;

		var key = WorldItemRegistry.SceneKey( scene );
		var live = scene.GetAllComponents<PlacedObject>().Select( x => x.ObjectId ).ToHashSet();
		var made = 0;

		foreach ( var record in Loaded.Objects.Where( x => x.Scene == key && !live.Contains( x.Id ) ).ToList() )
		{
			var go = Make( PlaceableCatalog.Find( record.Placeable ), record.Id, record.Owner, record.Position,
				record.Angles.ToRotation(), scene );

			if ( go.IsValid() )
				made++;
			else
				Log.Warning( $"Placed objects: could not restore {record.Placeable} ({record.Id})" );
		}

		return made;
	}

	// -- The file -----------------------------------------------------------------------------------------------------

	/// <summary>
	/// Write the file if a change waits and the last one is old enough. Called by <see cref="PlacedObjectSystem"/>.
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
			Log.Warning( e, $"Could not save {FileName}: {e.Message}" );
		}
	}

	/// <summary>
	/// Delete the file and its backup. For tests.
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
			Log.Warning( e, $"Could not delete {FileName}: {e.Message}" );
		}
	}

	static void Changed()
	{
		dirty = true;
		sinceChange = 0;
	}

	static PlacedObjectFile Load()
	{
		var loaded = new PlacedObjectFile();

		try
		{
			if ( FileSystem.Data is { } data
			     && FileProgressStore.TryReadSafely<PlacedObjectFile>( data, FileName ) is { } saved )
				loaded = saved;
		}
		catch ( System.Exception e )
		{
			Log.Warning( e, $"Could not read {FileName}: {e.Message}" );
		}

		var dropped = PlacedObjectRules.Sanitise( loaded );

		if ( dropped > 0 )
			Log.Warning( $"Placed objects: {dropped} entries of {FileName} were invalid and are dropped" );

		return loaded;
	}
}
