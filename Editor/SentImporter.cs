using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Brings the code of a sent into this game. A sent that carries code (<c>IncludeCode</c>) was compiled against the
/// sandbox game, so it cannot compile here and its components load as missing. Its source is inside the package the
/// engine downloads, so <c>sent_import org.package</c> fetches the package if it is not there yet, writes the source to
/// <c>Code/Sents/Imported/org.package/</c>, where the game compiles it like its own, and then says what did not compile
/// and what to do about it (<see cref="SentDiagnosis"/>). See docs/sents.md.
/// <para>
/// Safe to run again: the first run writes the files, and every later run only reports what the package has that the
/// folder has not (<c>new</c>) or has changed (<c>edited</c>) and writes nothing but the manifest. <c>sent_import
/// org.package update</c> also writes the new files. A file that was edited here is never overwritten.
/// </para>
/// </summary>
public static class SentImporter
{
	/// <summary>
	/// The files of a package that are source. A package is a container with more in it (build output, project files).
	/// </summary>
	static readonly string[] SourceExtensions = [".cs", ".razor", ".scss", ".css"];

	static readonly Regex NamespacePattern = new( @"^\s*namespace\s+([A-Za-z_][\w.]*)\s*[;{]", RegexOptions.Multiline );

	/// <summary>
	/// One source file of a package.
	/// </summary>
	internal sealed record Document( string Path, string Text );

	enum Fate
	{
		/// <summary>Not in the folder yet.</summary>
		New,

		/// <summary>In the folder, the same.</summary>
		Same,

		/// <summary>In the folder, changed here.</summary>
		Edited,

		/// <summary>Another import has this very file (a library one author's sents share).</summary>
		Shared,

		/// <summary>Another import has a file of that name declaring the same types, changed here: that one stays.</summary>
		Superseded,
	}

	/// <summary>
	/// What an import found: whether the package was got and read, the namespaces its code declares, and whether the
	/// game built with it (null when nothing was written, so no build was waited for).
	/// </summary>
	public sealed record Result( bool Ok, IReadOnlyList<string> Namespaces, bool? Built );

	[ConCmd( "sent_import" )]
	public static void Import( string ident, string mode = null ) => _ = ImportAsync( ident, mode );

	public static async Task<Result> ImportAsync( string ident, string mode )
	{
		var failed = new Result( false, [], null );
		ident = ident?.Trim().ToLowerInvariant();

		if ( !Sents.IsIdent( ident ) )
		{
			Log.Warning( "sent_import: give a cloud ident, e.g. sent_import crosschainer.hokuto_boxer" );
			return failed;
		}

		var package = FindPackage( ident ) ?? await Download( ident );

		if ( package is null )
		{
			Log.Warning( $"sent_import: could not get {ident} from the cloud: is it a sent, and is the ident right?" );
			return failed;
		}

		List<Document> documents;

		try
		{
			documents = ReadDocuments( package );
		}
		catch ( Exception e )
		{
			Log.Warning( $"sent_import: could not read {package.Name}: {e.Message}" );
			return failed;
		}

		var importedRoot = Path.Combine( Project.Current.GetRootPath(), "Code", "Sents", "Imported" );
		var target = Path.Combine( importedRoot, ident );
		var firstImport = !Directory.Exists( target );
		var update = string.Equals( mode, "update", StringComparison.OrdinalIgnoreCase );

		var fates = documents.Select( x => (Document: x, Fate: FateOf( x, importedRoot, target )) ).ToList();
		var written = 0;

		if ( firstImport || update )
		{
			foreach ( var (document, _) in fates.Where( x => x.Fate == Fate.New ) )
			{
				WriteSource( target, document );
				written++;
			}
		}

		var namespaces = NamespacesOf( documents );
		await WriteManifest( target, ident, namespaces );

		var summary = $"sent_import: {ident}: {written} file(s) written to Code/Sents/Imported/{ident}, "
		              + $"{fates.Count( x => x.Fate == Fate.Same )} already there, "
		              + $"{fates.Count( x => x.Fate == Fate.Shared )} shared with another import";

		var edited = fates.Where( x => x.Fate == Fate.Edited ).Select( x => x.Document.Path ).ToList();
		var waiting = fates.Where( x => x.Fate == Fate.New && !(firstImport || update) ).Select( x => x.Document.Path ).ToList();

		if ( edited.Count > 0 )
			summary += $", {edited.Count} edited here and kept ({string.Join( ", ", edited )})";

		var superseded = fates.Where( x => x.Fate == Fate.Superseded ).Select( x => x.Document.Path ).ToList();

		if ( superseded.Count > 0 )
			summary += $", {superseded.Count} left out because another import has a changed copy ({string.Join( ", ", superseded )})";

		if ( waiting.Count > 0 )
			summary += $", {waiting.Count} new in the package and not written ({string.Join( ", ", waiting )}): sent_import {ident} update";

		Log.Info( summary + ". Read the code before you keep it: it runs in the game with no sandbox." );

		bool? built = null;

		if ( written > 0 )
			built = await Diagnose( ident, target );

		return new Result( true, namespaces, built );
	}

	/// <summary>
	/// Has the engine download the package (as spawning it would), then finds the file.
	/// </summary>
	static async Task<FileInfo> Download( string ident )
	{
		Log.Info( $"sent_import: downloading {ident}..." );

		try
		{
			await Cloud.Load<ScriptedEntity>( ident, true );
		}
		catch ( Exception e )
		{
			Log.Warning( $"sent_import: the download of {ident} failed: {e.Message}" );
			return null;
		}

		// The file is written as the load finishes, or a moment after.
		for ( var i = 0; i < 20; i++ )
		{
			if ( FindPackage( ident ) is { } package )
				return package;

			await Task.Delay( 500 );
		}

		return null;
	}

	/// <summary>
	/// Waits for the game to build with the new code, and says what is wrong with it.
	/// </summary>
	static async Task<bool> Diagnose( string ident, string target )
	{
		var problems = await SentDiagnosis.WaitForErrors( target );

		if ( problems is null )
		{
			Log.Warning( $"sent_import: the game did not finish building in time; check the console for {ident}." );
			return false;
		}

		if ( problems.Count == 0 )
		{
			Log.Info( $"sent_import: {ident} builds with the game. Try it with sent_test {ident}." );
			return true;
		}

		var report = string.Join( Environment.NewLine, SentDiagnosis.Report( problems ) );
		var files = problems.Select( x => x.File ).Distinct().Count();

		Log.Warning( $"sent_import: {ident} does not build yet: {problems.Count} error(s) in {files} file(s).{Environment.NewLine}{report}" );
		return false;
	}

	/// <summary>
	/// The newest download of a package: the file the engine keeps the package's code in.
	/// </summary>
	internal static FileInfo FindPackage( string ident )
	{
		var bin = Path.Combine( Path.GetDirectoryName( Environment.ProcessPath ), "download", "assets", "_bin" );

		if ( !Directory.Exists( bin ) )
			return null;

		return new DirectoryInfo( bin ).GetFiles( $"package_{ident.Replace( '.', '_' )}.*.cll" )
			.OrderByDescending( x => x.LastWriteTimeUtc )
			.FirstOrDefault();
	}

	/// <summary>
	/// The source files inside a package. They are JSON arrays of { Text, LocalPath } in the package's container.
	/// </summary>
	internal static List<Document> ReadDocuments( FileInfo package )
	{
		string text;

		using ( var zip = new GZipStream( File.OpenRead( package.FullName ), CompressionMode.Decompress ) )
		using ( var reader = new StreamReader( zip, Encoding.UTF8 ) )
			text = reader.ReadToEnd();

		var documents = new List<Document>();
		var position = 0;

		while ( true )
		{
			var start = text.IndexOf( "[{\"Text\":", position, StringComparison.Ordinal );

			if ( start < 0 )
				break;

			using var list = JsonDocument.Parse( ExtractArray( text, start, out var length ) );
			position = start + length;

			foreach ( var item in list.RootElement.EnumerateArray() )
			{
				var path = item.GetProperty( "LocalPath" ).GetString().Replace( '\\', '/' ).TrimStart( '/' );

				// Build output and anything that is not source are not for this game.
				if ( path.StartsWith( ".obj/" ) || !SourceExtensions.Contains( Path.GetExtension( path ).ToLowerInvariant() ) )
					continue;

				documents.Add( new Document( path, item.GetProperty( "Text" ).GetString() ) );
			}
		}

		return documents;
	}

	// The array that starts at <start>: the text up to its matching close bracket, outside strings.
	static string ExtractArray( string text, int start, out int length )
	{
		var depth = 0;
		var inString = false;

		for ( var i = start; i < text.Length; i++ )
		{
			var c = text[i];

			if ( inString )
			{
				if ( c == '\\' )
					i++;
				else if ( c == '"' )
					inString = false;

				continue;
			}

			if ( c == '"' )
				inString = true;
			else if ( c == '[' )
				depth++;
			else if ( c == ']' && --depth == 0 )
			{
				length = i - start + 1;
				return text.Substring( start, length );
			}
		}

		throw new InvalidDataException( "unterminated document list" );
	}

	static Fate FateOf( Document document, string importedRoot, string target )
	{
		var path = SafePath( target, document.Path );

		if ( path is null )
			return Fate.Same;

		if ( File.Exists( path ) )
			return Same( File.ReadAllText( path ), document.Text ) ? Fate.Same : Fate.Edited;

		// The folder was reorganised here (a file moved into another subfolder): that file is the package's, not a new one.
		var moved = Directory.Exists( target )
			? Directory.EnumerateFiles( target, Path.GetFileName( path ), SearchOption.AllDirectories ).FirstOrDefault()
			: null;

		if ( moved is not null )
			return Same( File.ReadAllText( moved ), document.Text ) ? Fate.Same : Fate.Edited;

		// The game has its own copy of this file, declaring the same types (a HUD piece it shares): that one stays.
		var codeRoot = Path.Combine( Project.Current.GetRootPath(), "Code" );

		if ( Directory.EnumerateFiles( codeRoot, Path.GetFileName( path ), SearchOption.AllDirectories )
			    .Where( x => !x.StartsWith( importedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) )
			    .Any( x => path.EndsWith( ".razor", StringComparison.OrdinalIgnoreCase )
			    || DeclaresSameTypes( File.ReadAllText( x ), document.Text ) ) )
			return Fate.Superseded;

		// A library several sents carry (one author's shared vehicle code) is imported once. A second copy would
		// declare the same types again, so it is left out, whether it is the same or was adapted in the first import.
		if ( !Directory.Exists( importedRoot ) )
			return Fate.New;

		var others = Directory.EnumerateFiles( importedRoot, Path.GetFileName( path ), SearchOption.AllDirectories )
			.Where( x => !x.StartsWith( target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) )
			.Select( File.ReadAllText )
			.ToList();

		if ( others.Any( x => Same( x, document.Text ) ) )
			return Fate.Shared;

		return others.Any( x => DeclaresSameTypes( x, document.Text ) ) ? Fate.Superseded : Fate.New;
	}

	/// <summary>
	/// Where a package's file goes inside <paramref name="target"/>, or null if its path would leave the folder.
	/// </summary>
	static string SafePath( string target, string relative )
	{
		var folder = Path.GetFullPath( target ) + Path.DirectorySeparatorChar;
		var path = Path.GetFullPath( Path.Combine( target, relative.Replace( '/', Path.DirectorySeparatorChar ) ) );

		return path.StartsWith( folder, StringComparison.OrdinalIgnoreCase ) ? path : null;
	}

	// Equal apart from whitespace (line endings, indentation, blank lines).
	static bool Same( string a, string b ) => Squeezed( a ) == Squeezed( b );

	static string Squeezed( string text ) => Regex.Replace( text, @"\s+", "" );

	static readonly Regex TypePattern = new( @"\b(?:class|struct|enum|interface|record)\s+([A-Za-z_]\w*)" );

	// The namespaces and type names a source file declares: tells two copies of a library file from two files that
	// only share a name.
	static string Declarations( string text )
	{
		var namespaces = NamespacePattern.Matches( text ).Select( m => m.Groups[1].Value );
		var types = TypePattern.Matches( text ).Select( m => m.Groups[1].Value );

		return string.Join( ",", namespaces.Concat( types ).Distinct().OrderBy( x => x ) );
	}

	static bool DeclaresSameTypes( string a, string b )
	{
		var declared = Declarations( a );
		return declared.Contains( ',' ) && declared == Declarations( b );
	}

	static void WriteSource( string target, Document document )
	{
		var path = SafePath( target, document.Path );

		if ( path is null )
			return;

		Directory.CreateDirectory( Path.GetDirectoryName( path ) );
		File.WriteAllText( path, document.Text.Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" ) );
	}

	/// <summary>
	/// <c>SentPackage.cs</c>: tells the game which package the folder came from and which namespaces the package's
	/// source declares, for <see cref="SentPackages"/>. Written on every run, from the whole package.
	/// </summary>
	static List<string> NamespacesOf( List<Document> documents ) => documents
		.SelectMany( x => NamespacePattern.Matches( x.Text ).Select( m => m.Groups[1].Value ) )
		.Distinct()
		.OrderBy( x => x )
		.ToList();

	static async Task WriteManifest( string target, string ident, List<string> namespaces )
	{
		var package = await SentSource.Fetch( ident );
		var arguments = string.Join( ", ", namespaces.Prepend( ident ).Select( x => $"\"{x}\"" ) );
		var name = "SentPackage_" + Regex.Replace( ident, @"\W", "_" );

		Directory.CreateDirectory( target );
		File.WriteAllText( Path.Combine( target, "SentPackage.cs" ), string.Join( "\r\n",
			"// Written by sent_import: which package this folder came from and which namespaces its code declares.",
			"// The game reads it to name a sent's components in full (Sents/SentPackage.cs). Written again on every import.",
			$"// Source: {SentSource.Describe( package )}",
			"namespace Sandbox;",
			"",
			$"[SentPackage( {arguments} )]",
			$"public sealed class {name} {{ }}",
			"" ) );
	}
}
