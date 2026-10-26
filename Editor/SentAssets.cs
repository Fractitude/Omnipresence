using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// <c>sent_assets org.package</c>: makes a sent's assets (models, materials, textures, sounds) files of the project, so
/// it does not need the workshop to look right. A package ships its assets compiled, and once it is mounted the engine
/// serves those compiled files (<c>FileSystem.Mounted</c>, <c>drift_district/x/body.vmdl_c</c>; not all of them are loose
/// files in <c>download/assets</c>), and a compiled file placed in <c>Assets/</c> under its own path is loaded like one
/// the editor compiled. The files are found by following the paths written inside them from the paths the prefab and
/// the sent's code name, and written to the project. The
/// ones copied are listed in <c>source.json</c> (<c>assetFiles</c>), which is how <c>sent_remove</c> knows what is
/// the sent's own. Run by <c>sent_integrate</c>; safe to run again.
/// <para>
/// A path a file names that the package does not have (the engine's own materials, shaders) is left alone.
/// </para>
/// </summary>
public static class SentAssets
{
	// A path of an asset, as it is written inside a compiled file or a prefab.
	static readonly Regex Reference = new(
		@"[A-Za-z0-9_\-./]+\.(?:vmdl|vmat|vtex|vsnd|sound|vanmgrph|vpcf|vmorph|vagrp|vphys|vseq|vrmap|clip)",
		RegexOptions.IgnoreCase );

	// Folders of the engine's own assets (and of cloud packages the engine mounts): a copy here would shadow the real ones.
	static readonly string[] EngineRoots = ["models", "textures", "materials", "shaders", "particles", "effects"];

	[ConCmd( "sent_assets" )]
	public static void Run( string ident ) => _ = ImportAsync( ident );

	/// <summary>
	/// The number of files now in the project for this sent, or -1 if it could not be done.
	/// </summary>
	public static async Task<int> ImportAsync( string ident )
	{
		ident = ident?.Trim().ToLowerInvariant();

		if ( !Sents.IsIdent( ident ) )
		{
			Log.Warning( "sent_assets: give a cloud ident, e.g. sent_assets crosschainer.nishida_sable_s14" );
			return -1;
		}

		var root = Project.Current.GetRootPath();
		var folder = Path.Combine( root, "Assets", "sents", ident );
		var sourceFile = Path.Combine( folder, "source.json" );

		if ( !Directory.Exists( folder ) )
		{
			Log.Warning( $"sent_assets: {ident} is not integrated (sent_integrate {ident})." );
			return -1;
		}

		// Has the engine fetch the package, so its files are in the download folder.
		await SentLocal.Mount( ident );

		var starts = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var prefab in Directory.EnumerateFiles( folder, "*.prefab" ) )
			AddReferences( starts, File.ReadAllText( prefab ) );

		// Code names assets too (a sound, a particle) in strings.
		var imported = Path.Combine( root, "Code", "Sents", "Imported" );

		if ( Directory.Exists( imported ) )
		{
			foreach ( var code in Directory.EnumerateFiles( imported, "*.cs", SearchOption.AllDirectories ) )
				AddReferences( starts, File.ReadAllText( code ) );
		}

		var found = new Dictionary<string, byte[]>( StringComparer.OrdinalIgnoreCase );
		var queue = new Queue<string>( starts );

		while ( queue.Count > 0 )
		{
			var reference = queue.Dequeue().TrimStart( '/' );

			var name = reference + "_c";

			if ( found.ContainsKey( name ) || !FileSystem.Mounted.FileExists( name ) )
				continue;

			var bytes = FileSystem.Mounted.ReadAllBytes( name ).ToArray();
			found[name] = bytes;

			foreach ( Match match in Reference.Matches( System.Text.Encoding.Latin1.GetString( bytes ) ) )
				queue.Enqueue( match.Value );
		}

		// The code's strings reach every sent's assets; only what is under a folder the prefab itself uses is this sent's.
		var prefabRoots = starts.Where( x => IsInPrefab( folder, x ) )
			.Select( x => x.Split( '/' )[0] )
			.Where( x => !EngineRoots.Contains( x, StringComparer.OrdinalIgnoreCase ) )
			.ToHashSet( StringComparer.OrdinalIgnoreCase );

		var files = found.Where( x => prefabRoots.Contains( x.Key.Split( '/' )[0] ) ).OrderBy( x => x.Key ).ToList();
		var assets = Path.Combine( root, "Assets" );
		var written = 0;

		foreach (var (name, bytes) in files)
		{
			var target = Path.Combine( assets, name.Replace( '/', Path.DirectorySeparatorChar ) );

			if ( File.Exists( target ) && new FileInfo( target ).Length == bytes.Length )
				continue;

			Directory.CreateDirectory( Path.GetDirectoryName( target ) );
			File.WriteAllBytes( target, bytes );
			written++;
		}

		// *_c is git-ignored (compiled assets); these are not the editor's output, so they are kept.
		foreach ( var top in prefabRoots )
		{
			var ignore = Path.Combine( assets, top, ".gitignore" );

			if ( Directory.Exists( Path.GetDirectoryName( ignore ) ) && !File.Exists( ignore ) )
				File.WriteAllText( ignore, "!*_c\r\n" );
		}

		WriteList( sourceFile, files.Select( x => x.Key ).ToList() );
		Log.Info( $"sent_assets: {ident}: {files.Count} asset file(s) in the project, {written} copied, under "
		          + $"{string.Join( ", ", prefabRoots.Select( x => $"Assets/{x}/" ) )}. The workshop is no longer needed for them." );

		return files.Count;
	}

	/// <summary>
	/// Removes the files <c>sent_assets</c> copied for the sent, except those another sent's list also has. Called by
	/// <c>sent_remove</c> before the sent's folder goes.
	/// </summary>
	public static int Remove( string ident )
	{
		var root = Project.Current.GetRootPath();
		var sents = Path.Combine( root, "Assets", "sents" );
		var own = ListOf( Path.Combine( sents, ident, "source.json" ) );
		var others = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		if ( Directory.Exists( sents ) )
		{
			foreach ( var other in Directory.GetDirectories( sents ) )
			{
				if ( !string.Equals( Path.GetFileName( other ), ident, StringComparison.OrdinalIgnoreCase ) )
					others.UnionWith( ListOf( Path.Combine( other, "source.json" ) ) );
			}
		}

		var removed = 0;

		foreach ( var name in own.Where( x => !others.Contains( x ) ) )
		{
			var file = Path.Combine( root, "Assets", name.Replace( '/', Path.DirectorySeparatorChar ) );

			if ( !File.Exists( file ) )
				continue;

			File.Delete( file );
			removed++;

			for ( var dir = Path.GetDirectoryName( file ); IsEmpty( dir, root ); dir = Path.GetDirectoryName( dir ) )
				Directory.Delete( dir );
		}

		return removed;
	}

	// An empty folder under Assets/ (one that holds only the .gitignore this tool wrote counts as empty).
	static bool IsEmpty( string dir, string root )
	{
		if ( !dir.StartsWith( Path.Combine( root, "Assets" ) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase )
		     || !Directory.Exists( dir ) )
			return false;

		var left = Directory.EnumerateFileSystemEntries( dir ).ToList();

		if ( left.Count == 1 && Path.GetFileName( left[0] ) == ".gitignore" )
			File.Delete( left[0] );
		else if ( left.Count > 0 )
			return false;

		return true;
	}

	static bool IsInPrefab( string folder, string reference ) =>
		Directory.EnumerateFiles( folder, "*.prefab" ).Any( x => File.ReadAllText( x ).Contains( reference, StringComparison.OrdinalIgnoreCase ) );

	static void AddReferences( HashSet<string> into, string text )
	{
		foreach ( Match match in Reference.Matches( text ) )
		{
			// A path has a folder; a bare name in a string is something else.
			if ( match.Value.Contains( '/' ) )
				into.Add( match.Value );
		}
	}

	static List<string> ListOf( string sourceFile )
	{
		if ( !File.Exists( sourceFile ) )
			return [];

		try
		{
			return JsonNode.Parse( File.ReadAllText( sourceFile ) )?["assetFiles"]?.AsArray()
				       .Select( x => x.GetValue<string>() ).ToList() ?? [];
		}
		catch ( Exception )
		{
			return [];
		}
	}

	static void WriteList( string sourceFile, List<string> names )
	{
		var json = File.Exists( sourceFile ) ? JsonNode.Parse( File.ReadAllText( sourceFile ) ) as JsonObject : null;
		json ??= new JsonObject();
		json["assetFiles"] = new JsonArray( names.Select( x => (JsonNode)x ).ToArray() );
		json["assetsInProject"] = true;

		File.WriteAllText( sourceFile, json.ToJsonString( new JsonSerializerOptions { WriteIndented = true } )
			.Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" ) );
	}
}
