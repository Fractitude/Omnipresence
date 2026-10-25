namespace Sandbox;

/// <summary>
/// What the world file remembers about an item placed by hand in the editor, by the scene it is in and its object id (the
/// GameObject's guid in the scene file, which does not change between sessions). It is only written once the item is no
/// longer as the map has it: picked up (<see cref="Collected"/>), part picked up (<see cref="Item"/> is what is left).
/// </summary>
public sealed class HandPlacedRecord
{
	/// <summary>The scene's key (<see cref="WorldItemRegistry.SceneKey"/>).</summary>
	public string Scene { get; set; } = "";

	/// <summary>The item object's id in that scene.</summary>
	public string Id { get; set; } = "";

	/// <summary>All of it was taken (or it left its place, or fell out of the world): it is not made again.</summary>
	public bool Collected { get; set; }

	/// <summary>What is left after a partial pickup, shown the next time instead of what the editor says. Null otherwise.</summary>
	public ItemData Item { get; set; }
}

/// <summary>
/// What a hand-placed item does when its scene loads.
/// </summary>
public enum HandPlacedAction
{
	/// <summary>Nothing is recorded: build the item from what the author set in the editor.</summary>
	Build,

	/// <summary>A pickup left part of it: use <see cref="HandPlacedDecision.Item"/>.</summary>
	Remaining,

	/// <summary>It was collected: remove it from the scene.</summary>
	Destroy,
}

public readonly record struct HandPlacedDecision( HandPlacedAction Action, ItemData Item );

/// <summary>
/// The pure rules for items placed by hand: turning what the author set in the inspector into an item, the label shown
/// in the scene view, and the record of what has been taken (<see cref="HandPlacedRecord"/>). No scene, no files.
/// </summary>
public static class HandPlacedRules
{
	/// <summary>The most hand-placed records the file keeps.</summary>
	public const int MaxRecords = 2000;

	/// <summary>
	/// What building an item from the inspector's fields gave: the item, or why there is none, and anything that had to be
	/// cut to fit.
	/// </summary>
	public readonly record struct Authored( ItemData Item, string Error, string Warning );

	/// <summary>
	/// Turn the author's values into an item, using only the fields the kind declares (<see cref="ItemAuthoringRules.FieldsOf"/>):
	/// the others are ignored. A count is cut to the kind's stack size and a quality to 0 to 100 (a cut is a warning, not an
	/// error); an amount to 1 up to the wallet's cap. A custom item (no kind) needs a name. An unknown kind, or one the game
	/// makes itself (the parcel), gives an error and no item.
	/// </summary>
	public static Authored Build(
		string kind, int count, int quality, long amount, string name = "", string description = "" )
	{
		kind = (kind ?? "").Trim();

		var fields = ItemAuthoringRules.FieldsOf( kind );
		var warning = "";
		ItemData item;

		if ( kind.Length == 0 )
		{
			if ( string.IsNullOrWhiteSpace( name ) )
				return new( null, "a custom item needs a name", "" );

			item = new ItemData { Name = Cut( name.Trim(), 100 ), Count = 1 };
		}
		else if ( ItemCatalog.Find( kind ) is not { } def )
		{
			return new( null, $"unknown kind '{kind}'", "" );
		}
		else if ( !def.PlaceableByHand )
		{
			return new( null, $"{def.Name} is made by the game, not placed", "" );
		}
		else
		{
			var cap = System.Math.Max( def.MaxStack, 1 );
			var wanted = fields.HasFlag( ItemField.Count ) ? count : 1;

			item = kind == ItemCatalog.Cash
				? ItemCatalog.MakeCash( System.Math.Max( amount, 1 ) )
				: ItemCatalog.Make( kind, System.Math.Max( wanted, 1 ),
					fields.HasFlag( ItemField.Quality ) ? quality : 0, "" );

			if ( kind != ItemCatalog.Cash && fields.HasFlag( ItemField.Amount ) )
				item.Amount = System.Math.Clamp( amount, ItemAuthoringRules.MinAmount, ItemAuthoringRules.MaxAmount );

			if ( fields.HasFlag( ItemField.Amount ) )
			{
				if ( amount < 1 )
					warning = "Amount below one dollar, made it $1";
				else if ( item.Amount < amount )
					warning = $"Amount above the wallet's cap, cut to {CashRules.Format( item.Amount )}";
			}
			else if ( fields.HasFlag( ItemField.Count ) )
			{
				if ( count > cap )
					warning = $"Count {count} is above the stack size {cap}, cut to {cap}";
				else if ( count < 1 )
					warning = "Count below one, made it 1";
			}

			if ( fields.HasFlag( ItemField.Quality ) && (quality < 0 || quality > 100) && warning == "" )
				warning = $"Quality {quality} is outside 0 to 100, cut to {item.Quality}";
		}

		if ( fields.HasFlag( ItemField.Name ) && !string.IsNullOrWhiteSpace( name ) )
			item.Name = Cut( name.Trim(), 100 );

		if ( fields.HasFlag( ItemField.Description ) && !string.IsNullOrWhiteSpace( description ) )
			item.Description = Cut( description.Trim(), 1000 );

		return new( item, "", warning );
	}

	/// <summary>
	/// The short text the scene view shows over the item: "Static tabs x12 q61", "$1,250", or what is wrong.
	/// </summary>
	public static string Label(
		string kind, int count, int quality, long amount, out bool valid, string name = "", string description = "" )
	{
		var made = Build( kind, count, quality, amount, name, description );
		valid = made.Item is not null;

		if ( !valid )
			return $"Item: {made.Error}";

		var item = made.Item;

		if ( string.IsNullOrEmpty( item.Kind ) )
			return item.Name;

		if ( item.Kind == ItemCatalog.Cash )
			return CashRules.Format( item.Amount );

		return ItemCatalog.QualityMattersOf( item.Kind )
			? $"{item.Name} x{item.Count} q{item.Quality}"
			: $"{item.Name} x{item.Count}";
	}

	// -- The record of what was taken --------------------------------------------------------------------------------

	/// <summary>
	/// The record of a hand-placed item, or null when nothing was taken from it.
	/// </summary>
	public static HandPlacedRecord Find( List<HandPlacedRecord> records, string scene, string id ) =>
		records.FirstOrDefault( x => x.Scene == scene && x.Id == id );

	/// <summary>
	/// What an item does when its scene loads: nothing recorded builds it from the editor's fields, a partial pickup uses
	/// what was left, a collected one is removed.
	/// </summary>
	public static HandPlacedDecision Decide( List<HandPlacedRecord> records, string scene, string id ) =>
		Find( records, scene, id ) switch
		{
			null => new( HandPlacedAction.Build, null ),
			{ Collected: true } => new( HandPlacedAction.Destroy, null ),
			{ Item: { } rest } => new( HandPlacedAction.Remaining, rest.Clone() ),
			_ => new( HandPlacedAction.Build, null ),
		};

	/// <summary>
	/// Record that all of it is gone. Returns whether anything changed.
	/// </summary>
	public static bool Collect( List<HandPlacedRecord> records, string scene, string id )
	{
		if ( string.IsNullOrEmpty( scene ) || string.IsNullOrEmpty( id ) )
			return false;

		var record = Find( records, scene, id );

		if ( record is null )
			records.Add( new HandPlacedRecord { Scene = scene, Id = id, Collected = true } );
		else if ( record.Collected )
			return false;
		else
		{
			record.Collected = true;
			record.Item = null;
		}

		return true;
	}

	/// <summary>
	/// Record what is left of it after a partial pickup. Returns whether anything changed.
	/// </summary>
	public static bool SetRemaining( List<HandPlacedRecord> records, string scene, string id, ItemData rest )
	{
		if ( string.IsNullOrEmpty( scene ) || string.IsNullOrEmpty( id ) || rest is null )
			return false;

		var record = Find( records, scene, id );

		if ( record is null )
			records.Add( new HandPlacedRecord { Scene = scene, Id = id, Item = rest.Clone() } );
		else if ( record.Collected )
			return false;
		else
			record.Item = rest.Clone();

		return true;
	}

	/// <summary>
	/// Forget everything taken in a scene (or in every scene, when <paramref name="scene"/> is null), so its items are as
	/// the editor has them the next time it loads. Returns how many records went.
	/// </summary>
	public static int Reset( List<HandPlacedRecord> records, string scene = null ) =>
		records.RemoveAll( x => scene is null || x.Scene == scene );

	/// <summary>
	/// Clean the loaded records: malformed ones go, a repeated (scene, id) keeps its first, an item that is left is made
	/// valid, and the list is cut to <see cref="MaxRecords"/> (the oldest go). Returns how many were dropped.
	/// </summary>
	public static int Sanitise( List<HandPlacedRecord> records )
	{
		var before = records.Count;
		var seen = new HashSet<(string, string)>();
		var kept = new List<HandPlacedRecord>();

		foreach ( var record in records )
		{
			if ( record is null || string.IsNullOrWhiteSpace( record.Scene ) || string.IsNullOrWhiteSpace( record.Id )
			     || record.Scene.Length > WorldItemRules.MaxText || record.Id.Length > WorldItemRules.MaxText
			     || !seen.Add( (record.Scene, record.Id) ) )
				continue;

			if ( record.Collected )
				record.Item = null;
			else
			{
				record.Item = WorldItemRules.CleanItem( record.Item );

				if ( record.Item is null )
					continue;
			}

			kept.Add( record );
		}

		records.Clear();
		records.AddRange( kept.TakeLast( MaxRecords ) );
		return before - records.Count;
	}

	static string Cut( string text, int max ) => text.Length > max ? text[..max] : text;
}
