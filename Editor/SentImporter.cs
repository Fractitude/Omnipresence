using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sandbox;

/// <summary>
/// Brings the code of a sent into this game. A sent that carries code (<c>IncludeCode</c>) was compiled against the
/// sandbox game, so it cannot compile here and its components load as missing. Its source is inside the package the
/// engine downloaded, so <c>sent_import org.package</c> writes it to <c>Code/Sents/Imported/org.package/</c>, where the
/// game compiles it like its own. Spawn the sent once first, so the engine has downloaded it. See docs/sents.md.
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
	sealed record Document( string Path, string Text );

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
	}

	[ConCmd( "sent_import" )]
	public static void Import( string ident, string mode = null )
	{
		ident = ident?.Trim().ToLowerInvariant();

		if ( !Sents.IsIdent( ident ) )
		{
			Log.Warning( "sent_import: give a cloud ident, e.g. sent_import crosschainer.hokuto_boxer" );
			return;
		}

		var package = FindPackage( ident );

		if ( package is null )
		{
			Log.Warning( $"sent_import: {ident} is not downloaded. Spawn it once (spawn_sent {ident}) and try again." );
			return;
		}

		List<Document> documents;

		try
		{
			documents = ReadDocuments( package );
		}
		catch ( Exception e )
		{
			Log.Warning( $"sent_import: could not read {package.Name}: {e.Message}" );
			return;
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

		WriteManifest( target, ident, documents );

		var summary = $"sent_import: {ident}: {written} file(s) written to Code/Sents/Imported/{ident}, "
		              + $"{fates.Count( x => x.Fate == Fate.Same )} already there, "
		              + $"{fates.Count( x => x.Fate == Fate.Shared )} shared with another import";

		var edited = fates.Where( x => x.Fate == Fate.Edited ).Select( x => x.Document.Path ).ToList();
		var waiting = fates.Where( x => x.Fate == Fate.New && !(firstImport || update) ).Select( x => x.Document.Path ).ToList();

		if ( edited.Count > 0 )
			summary += $", {edited.Count} edited here and kept ({string.Join( ", ", edited )})";

		if ( waiting.Count > 0 )
			summary += $", {waiting.Count} new in the package and not written ({string.Join( ", ", waiting )}): sent_import {ident} update";

		Log.Info( summary + ". Read the code before you keep it: it runs in the game with no sandbox." );
	}

	/// <summary>
	/// The newest download of a package: the file the engine keeps the package's code in.
	/// </summary>
	static FileInfo FindPackage( string ident )
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
	static List<Document> ReadDocuments( FileInfo package )
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

		// A library several sents carry (one author's shared vehicle code) is imported once.
		var shared = Directory.Exists( importedRoot )
		             && Directory.EnumerateFiles( importedRoot, Path.GetFileName( path ), SearchOption.AllDirectories )
			             .Any( x => !x.StartsWith( target, StringComparison.OrdinalIgnoreCase ) && Same( File.ReadAllText( x ), document.Text ) );

		return shared ? Fate.Shared : Fate.New;
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

	static bool Same( string a, string b ) => a.Replace( "\r\n", "\n" ) == b.Replace( "\r\n", "\n" );

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
	static void WriteManifest( string target, string ident, List<Document> documents )
	{
		var namespaces = documents
			.SelectMany( x => NamespacePattern.Matches( x.Text ).Select( m => m.Groups[1].Value ) )
			.Distinct()
			.OrderBy( x => x )
			.ToList();

		var arguments = string.Join( ", ", namespaces.Prepend( ident ).Select( x => $"\"{x}\"" ) );
		var name = "SentPackage_" + Regex.Replace( ident, @"\W", "_" );

		Directory.CreateDirectory( target );
		File.WriteAllText( Path.Combine( target, "SentPackage.cs" ), string.Join( "\r\n",
			"// Written by sent_import: which package this folder came from and which namespaces its code declares.",
			"// The game reads it to name a sent's components in full (Sents/SentPackage.cs). Written again on every import.",
			"namespace Sandbox;",
			"",
			$"[SentPackage( {arguments} )]",
			$"public sealed class {name} {{ }}",
			"" ) );
	}
}
