namespace Sandbox;

/// <summary>
/// The knobs an item can have when it is placed by hand in the editor. A kind declares which of them it has
/// (<see cref="ItemDef.Fields"/>) and the inspector draws exactly those, so a new kind that needs a knob declares it in
/// data and a kind that does not need one never shows it.
/// </summary>
[System.Flags]
public enum ItemField
{
	None = 0,

	/// <summary>How many are in the stack, 1 to the kind's stack size.</summary>
	Count = 1,

	/// <summary>How good the batch is, 0 to 100.</summary>
	Quality = 2,

	/// <summary>Dollars, for cash.</summary>
	Amount = 4,

	/// <summary>A name the author gives the item (a custom item must have one).</summary>
	Name = 8,

	/// <summary>A description the author gives the item.</summary>
	Description = 16,
}

/// <summary>
/// What the author set for an item placed by hand: the one piece of data the custom inspector of <see cref="WorldItem"/>
/// edits (a hidden member of the component, so the default inspector never shows a wall of knobs). Only the fields the
/// kind declares (<see cref="ItemAuthoringRules.FieldsOf"/>) mean anything; the others are kept at their defaults.
/// </summary>
public sealed class ItemAuthoring
{
	public const int DefaultCount = 1;
	public const int DefaultQuality = 50;
	public const long DefaultAmount = 100;

	/// <summary>The id of the item's kind in the <see cref="ItemCatalog"/>, or empty for a custom item.</summary>
	[Property]
	public string Kind { get; set; } = "";

	[Property]
	public int Count { get; set; } = DefaultCount;

	[Property]
	public int Quality { get; set; } = DefaultQuality;

	[Property]
	public long Amount { get; set; } = DefaultAmount;

	[Property]
	public string Name { get; set; } = "";

	[Property]
	public string Description { get; set; } = "";

	/// <summary>
	/// Whether nothing was set: the values a new component starts with.
	/// </summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsDefault => string.IsNullOrEmpty( Kind ) && Count == DefaultCount && Quality == DefaultQuality
	                         && Amount == DefaultAmount && string.IsNullOrEmpty( Name )
	                         && string.IsNullOrEmpty( Description );

	public ItemAuthoring Clone() => new()
	{
		Kind = Kind ?? "", Count = Count, Quality = Quality, Amount = Amount, Name = Name ?? "",
		Description = Description ?? "",
	};
}

/// <summary>
/// The pure rules behind the item inspector: which kinds can be placed, which fields a kind has and their limits, what
/// changing the kind does to the other values, and how the loose fields of an older component carry over. No component, no
/// editor.
/// </summary>
public static class ItemAuthoringRules
{
	/// <summary>
	/// One entry of the type picker.
	/// </summary>
	public readonly record struct KindChoice( string Kind, string Label, string Icon, string Color );

	/// <summary>
	/// The fields a kind has. An empty kind is a custom item (a name and a description). A kind that is not in the catalog
	/// has none. Otherwise what the kind declares, or, when it declares nothing, what follows from it: a count if it stacks,
	/// a quality if quality matters for it.
	/// </summary>
	public static ItemField FieldsOf( string kind )
	{
		if ( string.IsNullOrEmpty( kind ) )
			return ItemField.Name | ItemField.Description;

		if ( ItemCatalog.Find( kind ) is not { } def )
			return ItemField.None;

		return def.Fields
		       ?? ((def.MaxStack > 1 ? ItemField.Count : 0) | (def.QualityMatters ? ItemField.Quality : 0));
	}

	/// <summary>
	/// Whether a kind can be placed by hand: a custom item, or a catalog kind that allows it (a parcel does not).
	/// </summary>
	public static bool IsPlaceable( string kind ) =>
		string.IsNullOrEmpty( kind ) || ItemCatalog.Find( kind ) is { PlaceableByHand: true };

	/// <summary>
	/// The type picker's entries: every kind that can be placed, by its display name, then "Custom item".
	/// </summary>
	public static List<KindChoice> Choices()
	{
		var choices = ItemCatalog.Items
			.Where( x => x.PlaceableByHand )
			.Select( x => new KindChoice( x.Id, x.Name, x.Icon, x.Color ) )
			.ToList();

		choices.Add( new KindChoice( "", "Custom item", "inventory_2", "" ) );
		return choices;
	}

	/// <summary>The most a count can be for a kind: its stack size.</summary>
	public static int MaxCount( string kind ) => ItemCatalog.MaxStackOf( kind );

	/// <summary>The least dollars a pile of cash holds.</summary>
	public const long MinAmount = 1;

	/// <summary>The most dollars a pile of cash holds: the wallet's cap.</summary>
	public static long MaxAmount => ProgressRecord.MaxMoney;

	/// <summary>
	/// The values made valid for their kind: a field the kind has is clamped to its limits, a field it does not have goes
	/// back to its default, and an unknown kind becomes a custom item. Returns a new object.
	/// </summary>
	public static ItemAuthoring Normalise( ItemAuthoring source )
	{
		var kind = (source?.Kind ?? "").Trim();

		if ( kind.Length > 0 && ItemCatalog.Find( kind ) is null )
			kind = "";

		var fields = FieldsOf( kind );

		return new ItemAuthoring
		{
			Kind = kind,
			Count = fields.HasFlag( ItemField.Count )
				? System.Math.Clamp( source?.Count ?? ItemAuthoring.DefaultCount, 1, MaxCount( kind ) )
				: ItemAuthoring.DefaultCount,
			Quality = fields.HasFlag( ItemField.Quality )
				? System.Math.Clamp( source?.Quality ?? ItemAuthoring.DefaultQuality, 0, 100 )
				: ItemAuthoring.DefaultQuality,
			Amount = fields.HasFlag( ItemField.Amount )
				? System.Math.Clamp( source?.Amount ?? ItemAuthoring.DefaultAmount, MinAmount, MaxAmount )
				: ItemAuthoring.DefaultAmount,
			Name = fields.HasFlag( ItemField.Name ) ? (source?.Name ?? "") : "",
			Description = fields.HasFlag( ItemField.Description ) ? (source?.Description ?? "") : "",
		};
	}

	/// <summary>
	/// The authoring of a component made before the knobs moved into <see cref="ItemAuthoring"/>, from its loose fields
	/// (kind, count, quality, amount, name and description overrides). Values the kind does not use are dropped.
	/// </summary>
	public static ItemAuthoring FromLegacy(
		string kind, int count, int quality, long amount, string name, string description ) =>
		Normalise( new ItemAuthoring
		{
			Kind = kind ?? "", Count = count, Quality = quality, Amount = amount, Name = name ?? "",
			Description = description ?? "",
		} );
}
