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
		SentLocal.SetRemoved( ident, false );

		if ( wrote )
			File.WriteAllText( prefabFile, PrefabText( entity, namespaces ) );

		// The archive the code was read from, so what we keep can be traced back to one published build.
		var code = SentImporter.Fingerprint( ident );

		File.WriteAllText( Path.Combine( folder, "source.json" ), SourceText( ident, entity, package, namespaces, code ) );

		// The assets become files of the project too, so the workshop is not needed to see the sent.
		await SentAssets.ImportAsync( ident );

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

		return PrettyText( file );
	}

	// Where the sent came from, and what it still needs from the workshop.
	static string SourceText( string ident, ScriptedEntity entity, Package package, List<string> namespaces,
		SentImporter.CodeArchive code )
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
			["codeFile"] = code?.File,
			["codeSha256"] = code?.Sha256,
			["codeSize"] = code?.Size,
			["namespaces"] = new JsonArray( namespaces.Select( x => (JsonNode)x ).ToArray() ),
			["description"] = entity.Description,
			["assetsFromWorkshop"] = new JsonArray( assets.Select( x => (JsonNode)x ).ToArray() ),
		};

		return PrettyText( source );
	}

	/// <summary>
	/// <c>sent_provenance [org.package]</c>: write the code archive's SHA-256 and size into the <c>source.json</c> of
	/// every integrated sent, or of one, from the <c>.cll</c> the engine has downloaded now. For the sents integrated
	/// before that was kept; <c>sent_integrate</c> writes it from the start. Nothing else in the file changes.
	/// </summary>
	[ConCmd( "sent_provenance" )]
	public static void Refresh( string ident = null ) => Provenance( ident );

	/// <summary>
	/// The number of <c>source.json</c> files written, for a probe. A sent without code has no archive to fingerprint
	/// and is left alone; one whose code is not downloaded is named and skipped, so the gap is known rather than
	/// mistaken for a clean file.
	/// </summary>
	public static int Provenance( string ident )
	{
		var root = Path.Combine( Project.Current.GetRootPath(), "Assets", "sents" );

		if ( !Directory.Exists( root ) )
		{
			Log.Warning( "sent_provenance: there is no Assets/sents: nothing is integrated." );
			return 0;
		}

		IEnumerable<string> folders = string.IsNullOrWhiteSpace( ident )
			? Directory.EnumerateDirectories( root )
			: [Path.Combine( root, ident.Trim().ToLowerInvariant() )];

		var written = 0;
		var missing = new List<string>();

		foreach ( var folder in folders )
		{
			var file = Path.Combine( folder, "source.json" );

			if ( !File.Exists( file ) )
			{
				if ( !string.IsNullOrWhiteSpace( ident ) )
					Log.Warning( $"sent_provenance: {Path.GetFileName( folder )} has no source.json: it is not integrated." );

				continue;
			}

			var source = JsonNode.Parse( File.ReadAllText( file ) ) as JsonObject;
			var name = source?["ident"]?.GetValue<string>() ?? Path.GetFileName( folder );

			if ( source?["includesCode"]?.GetValue<bool>() != true )
				continue;

			var code = SentImporter.Fingerprint( name );

			if ( code is null )
			{
				missing.Add( name );
				continue;
			}

			File.WriteAllText( file, PrettyText( WithCodeArchive( source, code ) ) );
			written++;
		}

		Log.Info( $"sent_provenance: {written} source.json file(s) carry the SHA-256 of the code archive they came from"
		          + (missing.Count > 0
			          ? $"; {missing.Count} skipped, the engine has not downloaded their code ({string.Join( ", ", missing )}): import one, then run this again."
			          : ".") );

		return written;
	}

	// source.json with the archive's file, hash and size, after "code": every other key keeps its place.
	static JsonObject WithCodeArchive( JsonObject source, SentImporter.CodeArchive code )
	{
		var result = new JsonObject();

		foreach ( var (key, value) in source )
		{
			result[key] = value?.DeepClone();

			if ( key == "code" )
				AddCodeArchive( result, code );
		}

		if ( !result.ContainsKey( "codeFile" ) )
			AddCodeArchive( result, code );

		return result;
	}

	static void AddCodeArchive( JsonObject json, SentImporter.CodeArchive code )
	{
		json["codeFile"] = code.File;
		json["codeSha256"] = code.Sha256;
		json["codeSize"] = code.Size;
	}

	// The file as we write it: two-space indented JSON, CRLF, no trailing newline.
	static string PrettyText( JsonObject json ) =>
		json.ToJsonString( Pretty ).Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" );
}
