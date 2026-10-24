namespace Sandbox;

/// <summary>
/// What a <see cref="PlayerInventory"/> keeps of an item: the facts the inventory shows. The item's GameObject is gone
/// while it is in the inventory. It is saved as JSON and synced from the host, so keep it to plain values.
/// </summary>
public sealed class ItemData
{
	public string Name { get; set; } = "";

	public string Description { get; set; } = "";

	/// <summary>
	/// The resource path of the prefab drawn as the icon, or empty. It wins over <see cref="ModelPath"/>.
	/// </summary>
	public string PrefabPath { get; set; } = "";

	/// <summary>
	/// The resource path of the icon's model, or empty for none.
	/// </summary>
	public string ModelPath { get; set; } = "";

	/// <summary>
	/// The id of the <see cref="ItemCatalog"/> entry this is a batch of, or empty for an ordinary item (something picked up
	/// from the world). Items from before batches load with no kind.
	/// </summary>
	public string Kind { get; set; } = "";

	/// <summary>
	/// How many are in this batch. A batch is the unit: one slot holds "40 tabs, quality 72".
	/// </summary>
	public int Count { get; set; } = 1;

	/// <summary>
	/// How good the batch is, 0 to 100. Only batches of a kind have one.
	/// </summary>
	public int Quality { get; set; }

	/// <summary>
	/// For a parcel: the id of the order it was packed for. Empty for everything else.
	/// </summary>
	public string OrderId { get; set; } = "";

	/// <summary>
	/// For a parcel: the nickname of the buyer it is addressed to (the order's handle, which the player sees in Hush).
	/// Empty for everything else, for a parcel packed before this was kept whose order is gone. Plain text only.
	/// </summary>
	public string To { get; set; } = "";

	/// <summary>
	/// For a parcel: the Steam id of the player who packed it, who is paid when it is delivered, whoever posts it. 0 for
	/// everything else, and for a parcel packed before this was kept (it is posted against the poster's own orders).
	/// </summary>
	public ulong SenderId { get; set; }

	/// <summary>
	/// For a parcel: the packing player's display name, shown as "From: name". Never the Steam id. Empty when unknown.
	/// </summary>
	public string SenderName { get; set; } = "";

	/// <summary>
	/// For a parcel: a description of the order it was packed for (kind, ordered count, agreed total, deadline in game
	/// seconds). Only a description for the player and a check: delivery and payment follow the sender's own order record
	/// (see <see cref="ParcelPostRules"/>), never these numbers.
	/// </summary>
	public string OrderKind { get; set; } = "";

	public int OrderCount { get; set; }

	public long OrderPrice { get; set; }

	public long OrderDeadline { get; set; }

	/// <summary>
	/// For a parcel: what is inside, one entry per batch packed. Empty for everything else, and for parcels from before
	/// contents were kept.
	/// </summary>
	public List<ItemContent> Contents { get; set; } = new();

	/// <summary>
	/// Whether this is a batch from the <see cref="ItemCatalog"/> (the crime items).
	/// </summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsBatch => !string.IsNullOrEmpty( Kind );

	/// <summary>
	/// The most one stack of this item holds: its kind's <see cref="ItemDef.MaxStack"/>, and 1 for an ordinary item.
	/// </summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public int MaxStack => ItemCatalog.MaxStackOf( Kind );

	public ItemData Clone() => new()
	{
		Name = Name,
		Description = Description,
		PrefabPath = PrefabPath,
		ModelPath = ModelPath,
		Kind = Kind,
		Count = Count,
		Quality = Quality,
		OrderId = OrderId,
		To = To,
		SenderId = SenderId,
		SenderName = SenderName,
		OrderKind = OrderKind,
		OrderCount = OrderCount,
		OrderPrice = OrderPrice,
		OrderDeadline = OrderDeadline,
		Contents = (Contents ?? []).Select( x => x with { } ).ToList(),
	};

	/// <summary>
	/// Copy a parcel's sender and order facts from <paramref name="source"/> (a loaded item) into this one, cut to safe
	/// sizes. For code that rebuilds an item from a file and would otherwise drop them.
	/// </summary>
	public void CopySenderFrom( ItemData source )
	{
		if ( source is null )
			return;

		SenderId = source.SenderId;
		SenderName = Cut( source.SenderName, 64 );
		OrderKind = Cut( source.OrderKind, 64 );
		OrderCount = System.Math.Clamp( source.OrderCount, 0, ItemCatalog.MaxCount );
		OrderPrice = System.Math.Clamp( source.OrderPrice, 0, 1_000_000_000 );
		OrderDeadline = System.Math.Max( source.OrderDeadline, 0 );
	}

	static string Cut( string text, int max ) =>
		string.IsNullOrEmpty( text ) ? "" : text.Length > max ? text[..max] : text;

	/// <summary>
	/// Whether both hold exactly the same facts (every field, and a parcel's contents in order). What a drop and a pickup
	/// must keep.
	/// </summary>
	public bool SameAs( ItemData other ) => other is not null && Json.Serialize( this ) == Json.Serialize( other );

	/// <summary>
	/// The prefab the object was made from: its own, else the nearest parent's. Empty if it is not from a prefab.
	/// </summary>
	public static string NearestPrefab( GameObject go )
	{
		for ( ; go.IsValid(); go = go.Parent )
		{
			if ( !string.IsNullOrEmpty( go.PrefabInstanceSource ) )
				return go.PrefabInstanceSource;
		}

		return "";
	}

	public static ItemData From( IItem item ) => new()
	{
		Name = item.ItemName ?? "",
		Description = item.ItemDescription ?? "",
		PrefabPath = item.ItemPrefab ?? "",
		ModelPath = item.ItemModel?.ResourcePath ?? "",
	};
}

/// <summary>
/// One batch inside a parcel: plain data, so it syncs and saves as JSON.
/// </summary>
public sealed record ItemContent( string Kind, int Count, int Quality );
