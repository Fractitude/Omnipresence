using Sandbox.UI;
using Inventory = Sandbox.Services.Inventory;

namespace Sandbox;

/// <summary>
/// Something the player can wear: a loaded clothing resource, an owned inventory item that's still in the cloud,
/// or a clothing package from the workshop.
/// </summary>
public sealed class CatalogItem
{
	/// <summary>
	/// The clothing resource, or a placeholder until it's downloaded.
	/// </summary>
	public Clothing Clothing { get; set; }

	/// <summary>
	/// The Steam inventory definition, for items that are sold.
	/// </summary>
	public Inventory.ItemDefinition Definition { get; init; }

	/// <summary>
	/// The workshop package, for community clothing that isn't sold.
	/// </summary>
	public string WorkshopIdent { get; init; }

	public bool IsLoaded => !string.IsNullOrEmpty( Clothing?.ResourcePath );

	public bool IsLoading { get; set; }

	/// <summary>
	/// Comes in its own package from the workshop rather than with the game.
	/// </summary>
	public bool IsWorkshop => !string.IsNullOrEmpty( PackageIdent );

	public bool IsPurchased => DefinitionId != 0 && Inventory.HasItem( DefinitionId );

	/// <summary>
	/// Sold, but not owned. Sandbox lets you wear it anyway, it only gets a lock on its icon.
	/// </summary>
	public bool IsLocked => DefinitionId != 0 && !Inventory.HasItem( DefinitionId );

	public int DefinitionId => Clothing?.SteamItemDefinitionId ?? Definition?.Id ?? 0;

	public string PackageIdent => Definition?.PackageIdent ?? WorkshopIdent;

	public string Title => string.IsNullOrWhiteSpace( Clothing?.Title ) ? Definition?.Name : Clothing.Title;

	/// <summary>
	/// The local icon when there is one, else the one on the inventory cdn or the package thumbnail.
	/// </summary>
	public string Icon { get; init; }

	public bool IsWornIn( ClothingContainer outfit )
	{
		var id = DefinitionId;
		return outfit.Clothing.Any( x => (IsLoaded && x.Clothing == Clothing) || (id != 0 && x.ItemDefinitionId == id) );
	}

	public void RemoveFrom( ClothingContainer outfit )
	{
		var id = DefinitionId;
		outfit.Clothing.RemoveAll( x => (IsLoaded && x.Clothing == Clothing) || (id != 0 && x.ItemDefinitionId == id) );
	}

	/// <summary>
	/// Add a loaded item. The definition id goes with it, so other clients can download it too.
	/// </summary>
	public void AddTo( ClothingContainer outfit )
	{
		var entry = outfit.Add( Clothing );

		if ( DefinitionId != 0 )
			entry.ItemDefinitionId = DefinitionId;
	}

	/// <summary>
	/// Download the clothing package.
	/// </summary>
	public async Task<bool> Load()
	{
		if ( IsLoaded ) return true;
		if ( string.IsNullOrEmpty( PackageIdent ) ) return false;

		IsLoading = true;

		try
		{
			var clothing = await Cloud.Load<Clothing>( PackageIdent );
			if ( clothing is not null )
			{
				Clothing = clothing;

				if ( WorkshopIdent is not null )
					WorkshopClothing.Register( clothing, WorkshopIdent, Icon );
			}
		}
		finally
		{
			IsLoading = false;
		}

		return IsLoaded;
	}
}

/// <summary>
/// Everything the local player can wear: free local clothing, and the inventory items they own.
/// </summary>
public static class ClothingCatalog
{
	public static List<CatalogItem> Build()
	{
		var definitionsByAsset = Inventory.Definitions
			.Where( x => !string.IsNullOrEmpty( x.Asset ) )
			.GroupBy( x => x.Asset, StringComparer.OrdinalIgnoreCase )
			.ToDictionary( x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase );

		List<CatalogItem> items = [];

		foreach ( var clothing in ResourceLibrary.GetAll<Clothing>() )
		{
			if ( string.IsNullOrWhiteSpace( clothing.Title ) ) continue;

			// a downloaded package doesn't know its definition, but the definition knows its asset
			var definition = clothing.SteamItemDefinitionId is int id
				? Inventory.FindDefinition( id )
				: definitionsByAsset.GetValueOrDefault( clothing.ResourcePath );

			var workshop = WorkshopClothing.Find( clothing );

			items.Add( new CatalogItem
			{
				Clothing = clothing,
				Definition = definition,
				WorkshopIdent = workshop?.Ident,
				Icon = GetIcon( clothing.Icon.Path, definition?.IconUrl ?? workshop?.Thumb )
			} );
		}

		foreach ( var definition in Inventory.Definitions )
		{
			if ( string.IsNullOrEmpty( definition.PackageIdent ) ) continue;
			if ( !Inventory.HasItem( definition.Id ) ) continue;
			if ( items.Any( x => x.DefinitionId == definition.Id ) ) continue;

			items.Add( new CatalogItem
			{
				Clothing = CreatePlaceholder( definition ),
				Definition = definition,
				Icon = definition.IconUrl
			} );
		}

		return items.OrderBy( x => x.Title ).ToList();
	}

	public static string GetIcon( string localPath, string fallback )
	{
		var path = localPath?.Trim( '/' );
		if ( !string.IsNullOrWhiteSpace( path ) && FileSystem.Mounted.FileExists( path ) )
			return path;

		return fallback ?? path;
	}

	// the same placeholder the avatar menu makes for items it hasn't downloaded
	static Clothing CreatePlaceholder( Inventory.ItemDefinition definition )
	{
		return new Clothing
		{
			Title = definition.Name,
			Category = Enum.TryParse<Clothing.ClothingCategory>( definition.Category, out var category ) ? category : Clothing.ClothingCategory.None,
			SteamItemDefinitionId = definition.Id
		};
	}
}

/// <summary>
/// Community clothing packages. An outfit only stores resource paths, so this remembers which package each one came from,
/// and the package idents travel with the outfit for other clients to download first.
/// </summary>
public static class WorkshopClothing
{
	public record Source( string Ident, string Thumb );

	static readonly Dictionary<string, Source> _byPath = new( StringComparer.OrdinalIgnoreCase );

	public static void Register( Clothing clothing, string ident, string thumb )
	{
		if ( string.IsNullOrEmpty( clothing?.ResourcePath ) ) return;

		// clients that only downloaded it for someone else's outfit don't know the thumbnail
		if ( thumb is null && _byPath.TryGetValue( clothing.ResourcePath, out var known ) )
			thumb = known.Thumb;

		_byPath[clothing.ResourcePath] = new Source( ident, thumb );
	}

	public static Source Find( Clothing clothing ) => clothing?.ResourcePath is { } path ? _byPath.GetValueOrDefault( path ) : null;

	/// <summary>
	/// The inventory definition of a package Facepunch sells, or has sold. Those are the official, verified ones.
	/// </summary>
	public static Inventory.ItemDefinition FindSold( string ident )
	{
		return Inventory.Definitions.FirstOrDefault( x => string.Equals( x.PackageIdent, ident, StringComparison.OrdinalIgnoreCase ) );
	}

	public static bool IsSold( string ident ) => FindSold( ident ) is not null;

	public static CatalogItem FromPackage( Package package )
	{
		// sold items go by their definition, so outfits carry the item id like the avatar menu's do
		var definition = FindSold( package.FullIdent );
		if ( definition is not null )
		{
			var resource = string.IsNullOrEmpty( definition.Asset ) ? null : ResourceLibrary.Get<Clothing>( definition.Asset );

			return new CatalogItem
			{
				Clothing = resource ?? new Clothing { Title = package.Title, SteamItemDefinitionId = definition.Id },
				Definition = definition,
				Icon = package.Thumb ?? definition.IconUrl
			};
		}

		var loaded = _byPath.FirstOrDefault( x => x.Value.Ident == package.FullIdent ).Key;
		var clothing = loaded is null ? null : ResourceLibrary.Get<Clothing>( loaded );

		return new CatalogItem
		{
			Clothing = clothing ?? new Clothing { Title = package.Title },
			WorkshopIdent = package.FullIdent,
			Icon = package.Thumb
		};
	}

	/// <summary>
	/// The packages an outfit needs, to send along with it.
	/// </summary>
	public static string GetIdents( ClothingContainer outfit )
	{
		var idents = outfit?.Clothing.Select( x => Find( x.Clothing )?.Ident ).Where( x => x is not null ).Distinct().ToArray();
		return idents is { Length: > 0 } ? string.Join( ",", idents ) : null;
	}

	/// <summary>
	/// Download the packages, so the outfit's paths resolve once it's read.
	/// </summary>
	public static async Task Mount( string idents )
	{
		if ( string.IsNullOrWhiteSpace( idents ) ) return;

		foreach ( var ident in idents.Split( ',', StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( IsSold( ident ) ) continue;

			var clothing = await Cloud.Load<Clothing>( ident );
			Register( clothing, ident, null );
		}
	}
}
