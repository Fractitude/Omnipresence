using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Sandbox;

/// <summary>
/// What a player may wear, decided by the host. An outfit is only a list of clothing resource paths and a few numbers,
/// so before the host keeps or shows one (<see cref="PlayerCustomization"/>) it is read again here, and what it holds
/// that is not allowed is taken out. Never trust the client's outfit: it is whatever its player sent.
/// <para>
/// <b>Rules</b> are one file on the host, <c>customization/rules.json</c>: a mode (<c>off</c>: anything,
/// <c>allow</c>: only the listed items, <c>block</c>: anything but the listed items) and the list. An item is a
/// clothing's resource path (<c>models/citizen_clothes/hat/beanie/beanie.clothing</c>), or a folder ending in
/// <c>*</c> (<c>hl2_metrocop/*</c>, which is how a whole workshop package is named: its files are in a folder of its
/// own). Paths are compared without regard to case. Change them with the <c>customization_*</c> commands.
/// </para>
/// </summary>
public static class CustomizationRules
{
	public const string Off = "off";
	public const string Allow = "allow";
	public const string Block = "block";

	/// <summary>
	/// The most clothing an outfit may have, and the longest outfit, colors or package list the host accepts.
	/// </summary>
	public const int MaxItems = 40;
	public const int MaxJson = 32 * 1024;
	public const int MaxColors = 64;

	const string Folder = "customization";
	const string RulesPath = "customization/rules.json";

	static readonly Regex Ident = new( @"^[a-z0-9_]{1,64}\.[a-z0-9_]{1,64}$", RegexOptions.Compiled );

	sealed class RulesFile
	{
		public string Mode { get; set; } = Off;
		public List<string> Items { get; set; } = [];
	}

	// Not readonly: a hot reload would reset a readonly field that holds only engine and BCL types.
	static RulesFile rules;

	static RulesFile Rules => rules ??= Load();

	public static string Mode => Rules.Mode;

	public static IReadOnlyList<string> Items => Rules.Items;

	static RulesFile Load()
	{
		var loaded = FileSystem.Data is { } data ? FileProgressStore.TryReadSafely<RulesFile>( data, RulesPath ) : null;
		loaded ??= new RulesFile();

		if ( loaded.Mode is not (Off or Allow or Block) )
			loaded.Mode = Off;

		loaded.Items ??= [];
		return loaded;
	}

	static void Save()
	{
		if ( FileSystem.Data is { } data )
			FileProgressStore.WriteSafely( data, Folder, RulesPath, Json.Serialize( Rules ) );
	}

	static string Normal( string item ) => item?.Trim().Replace( '\\', '/' ).TrimStart( '/' ).ToLowerInvariant();

	/// <summary>
	/// Whether a clothing path may be worn under the rules.
	/// </summary>
	public static bool IsAllowed( string path )
	{
		path = Normal( path );

		if ( string.IsNullOrEmpty( path ) )
			return false;

		if ( Rules.Mode == Off )
			return true;

		var listed = Rules.Items.Any( x => Matches( x, path ) );
		return Rules.Mode == Allow ? listed : !listed;
	}

	static bool Matches( string item, string path ) =>
		item.EndsWith( '*' ) ? path.StartsWith( item[..^1], StringComparison.Ordinal ) : item == path;

	/// <summary>
	/// An outfit as a player sent it, cleaned up: only items the rules allow, no more than <see cref="MaxItems"/>, and
	/// every other field a number or a flag. Null if it is not an outfit at all. <paramref name="removed"/> says what was
	/// taken out. Without <paramref name="applyRules"/> only the shape is cleaned: how the host writes it when nothing
	/// is taken out, so the owner's screen can put on the same text.
	/// </summary>
	public static string CleanOutfit( string json, List<string> removed, bool applyRules = true )
	{
		if ( string.IsNullOrWhiteSpace( json ) || json.Length > MaxJson )
			return null;

		JsonObject outfit;

		try
		{
			outfit = JsonNode.Parse( json ) as JsonObject;
		}
		catch ( Exception )
		{
			return null;
		}

		if ( outfit is null || outfit["Items"] is not JsonArray items )
			return null;

		var kept = new JsonArray();

		foreach ( var item in items.ToList() )
		{
			// One entry is an object with the clothing's path ("p").
			var path = (item as JsonObject)?["p"] is JsonValue value && value.TryGetValue<string>( out var text ) ? text : null;

			if ( path is null || (applyRules && !IsAllowed( path )) || kept.Count >= MaxItems )
			{
				removed?.Add( path ?? "an item without a path" );
				continue;
			}

			kept.Add( JsonNode.Parse( item.ToJsonString() ) );
		}

		outfit["Items"] = kept;

		// Everything else is appearance (height, age, sizes): numbers and flags, nothing a string could smuggle in.
		foreach ( var field in outfit.Where( x => x.Key != "Items" ).ToList() )
		{
			if ( field.Value is not JsonValue number || !(IsNumber( number ) || number.TryGetValue<bool>( out _ )) )
				outfit.Remove( field.Key );
		}

		return outfit.ToJsonString();
	}

	static bool IsNumber( JsonValue value ) => value.TryGetValue<double>( out var number ) && double.IsFinite( number );

	/// <summary>
	/// The colors of an outfit, as they were sent: at most <see cref="MaxColors"/>, keyed by short names or paths.
	/// Null for none or a damaged list.
	/// </summary>
	public static string CleanColors( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) || json.Length > MaxJson )
			return null;

		try
		{
			var colors = Json.Deserialize<Dictionary<string, Color>>( json );

			if ( colors is null || colors.Count == 0 || colors.Count > MaxColors || colors.Keys.Any( x => x.Length > 200 ) )
				return null;

			return Json.Serialize( colors );
		}
		catch ( Exception )
		{
			return null;
		}
	}

	/// <summary>
	/// The workshop packages that come with an outfit (<c>org.package</c>, comma separated): only well-formed idents, and
	/// only a few, because every client downloads them.
	/// </summary>
	public static string CleanIdents( string idents )
	{
		if ( string.IsNullOrWhiteSpace( idents ) || idents.Length > 2048 )
			return null;

		var clean = idents.Split( ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Select( x => x.ToLowerInvariant() )
			.Where( x => Ident.IsMatch( x ) )
			.Distinct()
			.Take( 16 )
			.ToArray();

		return clean.Length == 0 ? null : string.Join( ",", clean );
	}

	// The commands: the host's alone.

	static bool CheckHost( string command )
	{
		if ( Connection.Local?.IsHost == true || !Networking.IsActive )
			return true;

		Log.Warning( $"{command}: only the host may change what players can wear" );
		return false;
	}

	[ConCmd( "customization_mode" )]
	public static void SetMode( string mode = null )
	{
		if ( !CheckHost( "customization_mode" ) )
			return;

		mode = mode?.Trim().ToLowerInvariant();

		if ( mode is not (Off or Allow or Block) )
		{
			Log.Info( $"customization_mode is {Mode} ({Items.Count} item(s) listed). Use off, allow or block." );
			return;
		}

		Rules.Mode = mode;
		Changed();
		Log.Info( $"customization_mode: {mode}" );
	}

	[ConCmd( "customization_add" )]
	public static void AddItem( string item )
	{
		if ( !CheckHost( "customization_add" ) || Normal( item ) is not { Length: > 0 } clean )
			return;

		if ( !Rules.Items.Contains( clean ) )
			Rules.Items.Add( clean );

		Changed();
		Log.Info( $"customization_add: {clean} ({Mode} mode, {Items.Count} listed)" );
	}

	[ConCmd( "customization_remove" )]
	public static void RemoveItem( string item )
	{
		if ( !CheckHost( "customization_remove" ) )
			return;

		var removed = Rules.Items.Remove( Normal( item ) );
		Changed();
		Log.Info( removed ? $"customization_remove: {Normal( item )}" : $"customization_remove: {item} was not listed" );
	}

	[ConCmd( "customization_list" )]
	public static void List()
	{
		Log.Info( $"customization: mode {Mode}, {Items.Count} item(s)" + (Items.Count > 0 ? "\n  " + string.Join( "\n  ", Items ) : "") );
	}

	// Saves, and cleans the outfits of the players here under the new rules.
	static void Changed()
	{
		Save();

		if ( Game.ActiveScene is { } scene )
		{
			foreach ( var customization in scene.GetAll<PlayerCustomization>() )
				customization.Recheck();
		}
	}
}
