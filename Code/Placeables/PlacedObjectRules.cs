namespace Sandbox;

/// <summary>
/// One placed object as the world file keeps it: what it is, who bought it, where it stands.
/// </summary>
public sealed class PlacedObjectRecord
{
	/// <summary>
	/// The unique id of the object. It never changes, however the object moves, so what is saved under it (a tray's
	/// crop, a storage box's contents) follows it.
	/// </summary>
	public string Id { get; set; } = "";

	/// <summary>The <see cref="Placeable.Id"/> it was bought as.</summary>
	public string Placeable { get; set; } = "";

	/// <summary>The prefab it is made from. Kept for the record: a restore asks the catalog first.</summary>
	public string Prefab { get; set; } = "";

	/// <summary>The Steam id of the buyer, kept for the ownership design. It grants nothing yet.</summary>
	public ulong Owner { get; set; }

	/// <summary>The scene it stands in (<see cref="WorldItemRegistry.SceneKey"/>).</summary>
	public string Scene { get; set; } = "";

	public Vector3 Position { get; set; }

	public Angles Angles { get; set; }

	/// <summary>
	/// Frozen with the Physgun (its body does not move until it is unfrozen). Additive: a file without it loads as false.
	/// </summary>
	public bool Frozen { get; set; }
}

/// <summary>
/// A purchase on its way. It is in the world file, not in a player's record, so it arrives on time whether the buyer
/// is playing or away; the buyer is told when they are next there.
/// </summary>
public sealed class PlacedDeliveryRecord
{
	/// <summary>The id the object will have once it stands in the world.</summary>
	public string Id { get; set; } = "";

	public string Placeable { get; set; } = "";

	public ulong Owner { get; set; }

	/// <summary>Game seconds (<see cref="WorldClock.Now"/>) when it arrives.</summary>
	public long ArriveAt { get; set; }

	/// <summary>The object stands in the world.</summary>
	public bool Arrived { get; set; }

	/// <summary>Where it was put, in words ("beside your mailbox"). Set when it arrives.</summary>
	public string Where { get; set; } = "";
}

/// <summary>
/// The file (<c>placed_objects.json</c>).
/// </summary>
public sealed class PlacedObjectFile
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;

	public List<PlacedObjectRecord> Objects { get; set; } = new();

	public List<PlacedDeliveryRecord> Deliveries { get; set; } = new();
}

/// <summary>
/// The pure rules of the placed objects and their deliveries: what a loaded file may hold, what a player may order and
/// what is due. No scene, no files, no connection.
/// </summary>
public static class PlacedObjectRules
{
	public const int MaxText = 260;

	/// <summary>The most placed objects one scene keeps.</summary>
	public const int MaxInScene = 200;

	/// <summary>The most deliveries (arrived and waiting to be told, or on their way) the file keeps.</summary>
	public const int MaxDeliveries = 200;

	/// <summary>
	/// A fresh id for an object: letters and digits only, so every save that keys on it accepts it.
	/// </summary>
	public static string NewId() => System.Guid.NewGuid().ToString( "N" );

	/// <summary>
	/// Whether text is a plain id (letters, digits, - and _, at most 64), the shape the saves accept.
	/// </summary>
	public static bool IsId( string id ) => !string.IsNullOrWhiteSpace( id ) && id.Length <= CrimeRecord.MaxIdLength
		&& id.All( x => char.IsAsciiLetterOrDigit( x ) || x is '-' or '_' );

	/// <summary>
	/// Clean a loaded file in place. Entries with no known placeable, a bad id, a repeated id or a bad place are
	/// dropped (never thrown on). A scene keeps at most <see cref="MaxInScene"/> objects, the oldest go first. Returns
	/// how many entries were dropped.
	/// </summary>
	public static int Sanitise( PlacedObjectFile file )
	{
		file.Objects ??= new();
		file.Deliveries ??= new();
		var before = file.Objects.Count + file.Deliveries.Count;
		var ids = new HashSet<string>();

		file.Objects = file.Objects
			.Where( x => x is not null && IsId( x.Id ) && PlaceableCatalog.Find( x.Placeable ) is not null
			             && !string.IsNullOrWhiteSpace( x.Scene ) && IsPlace( x.Position ) && IsAngles( x.Angles )
			             && ids.Add( x.Id ) )
			.ToList();

		foreach ( var record in file.Objects )
		{
			record.Scene = Limit( record.Scene );
			record.Prefab = CleanPrefab( record.Prefab );
		}

		foreach ( var scene in file.Objects.Select( x => x.Scene ).Distinct().ToList() )
		{
			var extra = file.Objects.Count( x => x.Scene == scene ) - MaxInScene;

			for ( var i = 0; i < file.Objects.Count && extra > 0; )
			{
				if ( file.Objects[i].Scene != scene )
				{
					i++;
					continue;
				}

				file.Objects.RemoveAt( i );
				extra--;
			}
		}

		file.Deliveries = file.Deliveries
			.Where( x => x is not null && IsId( x.Id ) && PlaceableCatalog.Find( x.Placeable ) is not null
			             && x.Owner != 0 && ids.Add( x.Id ) )
			.Take( MaxDeliveries )
			.ToList();

		foreach ( var delivery in file.Deliveries )
			delivery.Where = Limit( delivery.Where );

		file.Version = PlacedObjectFile.CurrentVersion;
		return before - file.Objects.Count - file.Deliveries.Count;
	}

	/// <summary>
	/// How many deliveries of this player are still on their way.
	/// </summary>
	public static int InTransit( PlacedObjectFile file, ulong owner ) =>
		file.Deliveries.Count( x => x.Owner == owner && !x.Arrived );

	/// <summary>
	/// Add an order to the file, if the player may have another one on its way. Returns the delivery, or null. Taking the
	/// money is the caller's job and comes after this succeeded.
	/// </summary>
	public static PlacedDeliveryRecord Order( PlacedObjectFile file, ulong owner, Placeable item, long now )
	{
		if ( owner == 0 || item is null || InTransit( file, owner ) >= PlaceableCatalog.MaxInTransit
		     || file.Deliveries.Count >= MaxDeliveries )
			return null;

		var delivery = new PlacedDeliveryRecord
		{
			Id = NewId(), Placeable = item.Id, Owner = owner, ArriveAt = now + item.DeliverSeconds,
		};

		file.Deliveries.Add( delivery );
		return delivery;
	}

	/// <summary>
	/// The deliveries whose time is up and that have not arrived, oldest first.
	/// </summary>
	public static List<PlacedDeliveryRecord> Due( PlacedObjectFile file, long now ) => file.Deliveries
		.Where( x => !x.Arrived && now >= x.ArriveAt )
		.OrderBy( x => x.ArriveAt )
		.ToList();

	/// <summary>
	/// A place is a real place: every coordinate finite and inside the map's bounds.
	/// </summary>
	public static bool IsPlace( Vector3 place ) =>
		IsInside( place.x ) && IsInside( place.y ) && IsInside( place.z );

	static bool IsAngles( Angles angles ) =>
		float.IsFinite( angles.pitch ) && float.IsFinite( angles.yaw ) && float.IsFinite( angles.roll );

	static bool IsInside( float value ) =>
		float.IsFinite( value ) && System.Math.Abs( value ) <= WorldItemRules.MaxCoordinate;

	/// <summary>
	/// A prefab path that is a plain relative <c>.prefab</c> path, else empty.
	/// </summary>
	public static string CleanPrefab( string path ) => WorldItemRules.CleanPrefab( path );

	static string Limit( string text ) =>
		string.IsNullOrEmpty( text ) ? "" : text.Length > MaxText ? text[..MaxText] : text;
}
