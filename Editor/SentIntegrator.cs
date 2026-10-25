using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// <c>sent_integrate org.package</c>: makes a workshop sent ours. Its code is brought in as <c>sent_import</c> does
/// (<c>Code/Sents/Imported/&lt;ident&gt;/</c>), its prefab is saved as a file of the project
/// (<c>Assets/sents/&lt;ident&gt;/&lt;name&gt;.prefab</c>, the name being the ident after the author's, component types
/// named in full) and <c>source.json</c> beside it says
/// who made it and where its page is. From then on <c>spawn_sent</c> uses our copy and an update by the author changes
/// nothing; we are free to edit the prefab and the code. See <see cref="SentLocal"/> for what still comes from the
/// workshop (the assets).
/// <para>
/// Safe to run again: it never overwrites a prefab or code file that was edited here (the prefab is only written if
/// it is not there; delete it to take the workshop's again).
/// </para>
/// </summary>
public static class SentIntegrator
{
	static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

	static readonly Regex AssetPath = new(
		"\"([^\"]+\\.(?:vmdl|vmat|vtex|sound|vsnd|vpcf|vanmgrph|prefab|clip|vmap|png|jpg|jpeg|wav|mp3))\"",
		RegexOptions.IgnoreCase );

	[ConCmd( "sent_integrate" )]
	public static void Integrate( string ident ) => _ = IntegrateAsync( ident );

	/// <summary>
	/// True when the sent is now ours: its prefab is in the project (and its code, if it has any, built).
	/// </summary>
	public static async Task<bool> IntegrateAsync( string ident )
	{
		ident = ident?.Trim().ToLowerInvariant();

		if ( !Sents.IsIdent( ident ) )
		{
			Log.Warning( "sent_integrate: give a cloud ident, e.g. sent_integrate hl2npcs.metrocop" );
			return false;
		}

		ScriptedEntity entity;

		try
		{
			entity = await Cloud.Load<ScriptedEntity>( ident, true );
		}
		catch ( Exception e )
		{
			Log.Warning( $"sent_integrate: could not get {ident} from the cloud: {e.Message}" );
			return false;
		}

		if ( entity?.Prefab is null )
		{
			Log.Warning( $"sent_integrate: '{ident}' is not a sent, or could not be loaded." );
			return false;
		}

		var package = await SentSource.Fetch( ident );
		var namespaces = new List<string>();
		var built = true;

		if ( entity.IncludeCode )
		{
			var imported = await SentImporter.ImportAsync( ident, null );

			if ( !imported.Ok )
				return false;

			namespaces.AddRange( imported.Namespaces );
			built = imported.Built ?? true;
		}

		var folder = Path.Combine( Project.Current.GetRootPath(), "Assets", "sents", ident );
		var prefabFile = Path.Combine( folder, $"{SentLocal.PrefabName( ident )}.prefab" );
		var wrote = !File.Exists( prefabFile );

		Directory.CreateDirectory( folder );

		if ( wrote )
			File.WriteAllText( prefabFile, PrefabText( entity, namespaces ) );

		File.WriteAllText( Path.Combine( folder, "source.json" ), SourceText( ident, entity, package, namespaces ) );

		Log.Info( $"sent_integrate: {ident}: {(wrote ? "prefab saved to" : "prefab already in")} Assets/sents/{ident}/ "
		          + $"(source: {SentSource.Describe( package )})."
		          + (built ? "" : " Its code does not build yet (see the report above): fix it, then spawn it.")
		          + $" Try it with sent_test {ident}." );

		return true;
	}

	// The prefab file: the workshop's prefab data with every component named in full, so a plain clone finds its types.
	static string PrefabText( ScriptedEntity entity, List<string> namespaces )
	{
		var data = SentBuilder.QualifiedData( entity.Prefab, namespaces, x => EditorTypeLibrary.GetType( x ) is not null );

		var file = new JsonObject
		{
			["RootObject"] = data,
			["ResourceVersion"] = 2,
			["ShowInMenu"] = false,
			["MenuPath"] = null,
			["MenuIcon"] = null,
			["DontBreakAsTemplate"] = false,
			["__references"] = new JsonArray(),
			["__version"] = 2,
		};

		return file.ToJsonString( Pretty ).Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" );
	}

	// Where the sent came from, and what it still needs from the workshop.
	static string SourceText( string ident, ScriptedEntity entity, Package package, List<string> namespaces )
	{
		var assets = AssetPath.Matches( entity.Prefab.RootObject.ToJsonString() )
			.Select( x => x.Groups[1].Value )
			.Distinct()
			.OrderBy( x => x )
			.ToList();

		var source = new JsonObject
		{
			["ident"] = ident,
			["title"] = entity.Title ?? package?.Title ?? ident,
			["author"] = package is null ? null : SentSource.Author( package ),
			["authorIdent"] = package?.Org?.Ident,
			["page"] = package is null ? $"https://sbox.game/{ident.Replace( '.', '/' )}" : SentSource.Url( package ),
			["workshopVersion"] = package?.Revision?.VersionId,
			["workshopUpdated"] = package?.Updated.ToString( "yyyy-MM-dd" ),
			["integrated"] = DateTime.Now.ToString( "yyyy-MM-dd" ),
			["includesCode"] = entity.IncludeCode,
			["code"] = entity.IncludeCode ? $"Code/Sents/Imported/{ident}/" : null,
			["namespaces"] = new JsonArray( namespaces.Select( x => (JsonNode)x ).ToArray() ),
			["description"] = entity.Description,
			["assetsFromWorkshop"] = new JsonArray( assets.Select( x => (JsonNode)x ).ToArray() ),
		};

		return source.ToJsonString( Pretty ).Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" );
	}
}
