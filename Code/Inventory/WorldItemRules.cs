namespace Sandbox;

/// <summary>
/// One dropped item as the world file keeps it: the whole item, where it lies and in which scene.
/// </summary>
public sealed class WorldItemRecord
{
	/// <summary>The unique id of this world item. It is how a restore knows an item is already in the scene.</summary>
	public string Id { get; set; } = "";

	/// <summary>The scene it was dropped in: the scene's resource path (see <see cref="WorldItemRegistry.SceneKey"/>).</summary>
	public string Scene { get; set; } = "";

	/// <summary>The prefab it was made from. Kept for the record: a restore chooses the prefab again.</summary>
	public string Prefab { get; set; } = "";

	public ItemData Item { get; set; }

	public Vector3 Position { get; set; }

	public Angles Angles { get; set; }

	/// <summary>
	/// Frozen with the Physgun (its body does not move until it is unfrozen). Additive: a file without it loads as false.
	/// </summary>
	public bool Frozen { get; set; }
}

/// <summary>
/// The world file (<c>world_items.json</c>). The list is in the order the items were dropped, oldest first.
/// </summary>
public sealed class WorldItemFile
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;

	public List<WorldItemRecord> Items { get; set; } = new();
}

/// <summary>
/// The pure rules for the world file: what a loaded entry may be, and how many a scene keeps. No scene, no files.
/// </summary>
public static class WorldItemRules
{
	/// <summary>The longest id, scene key and prefab path kept.</summary>
	public const int MaxText = 260;

	/// <summary>No coordinate of a place may be beyond this (the map is far smaller).</summary>
	public const float MaxCoordinate = 100000f;

	/// <summary>
	/// Clean a loaded file in place: malformed entries are dropped (never thrown on), items are made valid, an entry with
	/// no id gets one, a repeated id is dropped, and a scene keeps at most <paramref name="maxPerScene"/> (the oldest go
	/// first). Returns how many entries were dropped.
	/// </summary>
	public static int Sanitise( WorldItemFile file, int maxPerScene )
	{
		file.Items ??= new();
		var before = file.Items.Count;
		var ids = new HashSet<string>();
		var clean = new List<WorldItemRecord>();

		foreach ( var record in file.Items )
		{
			if ( record is null || string.IsNullOrWhiteSpace( record.Scene ) || !IsPlace( record.Position )
			     || !IsAngles( record.Angles ) )
				continue;

			var item = CleanItem( record.Item );

			if ( item is null )
				continue;

			record.Item = item;
			record.Scene = Limit( record.Scene );
			record.Prefab = CleanPrefab( record.Prefab );
			record.Id = string.IsNullOrWhiteSpace( record.Id ) ? NewId() : Limit( record.Id );

			if ( ids.Add( record.Id ) )
				clean.Add( record );
		}

		file.Items = clean;
		file.Version = WorldItemFile.CurrentVersion;
		Cap( file.Items, maxPerScene );
		return before - file.Items.Count;
	}

	/// <summary>
	/// Keep at most <paramref name="max"/> entries for each scene: the oldest (first in the list) are removed. Returns how
	/// many were removed.
	/// </summary>
	public static int Cap( List<WorldItemRecord> items, int max )
	{
		var removed = 0;

		foreach ( var scene in items.Select( x => x.Scene ).Distinct().ToList() )
		{
			var extra = items.Count( x => x.Scene == scene ) - max;

			for ( var i = 0; i < items.Count && extra > 0; )
			{
				if ( items[i].Scene != scene )
				{
					i++;
					continue;
				}

				items.RemoveAt( i );
				extra--;
				removed++;
			}
		}

		return removed;
	}

	/// <summary>
	/// A copy of a loaded item that is safe to spawn, or null when it is not an item. A known kind has its count clamped
	/// to the kind's stack size and its quality to 0..100. An unknown kind is kept as an ordinary item (one, no kind) only
	/// when it has a name. Texts are cut short, a parcel's contents are capped.
	/// </summary>
	public static ItemData CleanItem( ItemData item )
	{
		if ( item is null )
			return null;

		var clean = new ItemData
		{
			Name = Limit( item.Name, 100 ),
			Description = Limit( item.Description, 1000 ),
			PrefabPath = CleanPrefab( item.PrefabPath ),
			ModelPath = Limit( item.ModelPath ),
			Kind = Limit( item.Kind, 64 ),
			Count = item.Count,
			Quality = System.Math.Clamp( item.Quality, 0, 100 ),
			Amount = System.Math.Clamp( item.Amount, 0, ProgressRecord.MaxMoney ),
			OrderId = Limit( item.OrderId, 64 ),
			To = Limit( item.To, 64 ),
			Contents = CrimeRecord.CleanContents( item.Contents ),
		};

		clean.CopySenderFrom( item );

		if ( clean.Kind == ItemCatalog.Cash )
		{
			// Cash is its amount: a note with no dollars is nothing, and it is never more than one stack.
			if ( clean.Amount < 1 )
				return null;

			clean.Count = 1;
			clean.Quality = 0;
			clean.Name = ItemCatalog.Find( ItemCatalog.Cash ).Name;
			return clean;
		}

		clean.Amount = 0;

		if ( ItemCatalog.Find( clean.Kind ) is { } def )
		{
			clean.Count = System.Math.Clamp( clean.Count, 1, System.Math.Max( def.MaxStack, 1 ) );

			if ( string.IsNullOrWhiteSpace( clean.Name ) )
				clean.Name = def.Name;

			return clean;
		}

		// An unknown kind has no definition to say what it is: it stays only when it brought its own name.
		if ( string.IsNullOrWhiteSpace( clean.Name ) )
			return null;

		clean.Kind = "";
		clean.Count = 1;
		clean.Quality = 0;
		clean.OrderId = "";
		clean.To = "";
		clean.Contents = new();
		clean.CopySenderFrom( new ItemData() );
		return clean;
	}

	/// <summary>
	/// A prefab path from a file: empty unless it looks like a prefab path in the project (no parent folders, no drive,
	/// ends in .prefab). Whether it exists is not asked here: a spawn falls back when it cannot be made.
	/// </summary>
	public static string CleanPrefab( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) || path.Length > MaxText || path.Contains( ".." ) || path.Contains( ':' )
		     || path.Contains( '\\' ) || !path.EndsWith( ".prefab", System.StringComparison.OrdinalIgnoreCase ) )
			return "";

		return path;
	}

	/// <summary>
	/// Whether a position is one a world item can lie at: finite and inside the bounds of the map. It is not a height
	/// test: The Wired lies under the map, in the same scene.
	/// </summary>
	public static bool IsPlace( Vector3 position ) =>
		float.IsFinite( position.x ) && float.IsFinite( position.y ) && float.IsFinite( position.z )
		&& System.Math.Abs( position.x ) <= MaxCoordinate && System.Math.Abs( position.y ) <= MaxCoordinate
		&& System.Math.Abs( position.z ) <= MaxCoordinate;

	static bool IsAngles( Angles angles ) =>
		float.IsFinite( angles.pitch ) && float.IsFinite( angles.yaw ) && float.IsFinite( angles.roll );

	public static string NewId() => System.Guid.NewGuid().ToString( "N" );

	static string Limit( string text, int max = MaxText ) =>
		string.IsNullOrEmpty( text ) ? "" : text.Length > max ? text[..max] : text;
}
