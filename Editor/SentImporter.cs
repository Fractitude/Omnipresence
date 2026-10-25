using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Sandbox;

/// <summary>
/// Brings the code of a sent into this game. A sent that carries code (<c>IncludeCode</c>) was compiled against the
/// sandbox game, so it cannot compile here and its components load as missing. Its source is inside the package the
/// engine downloaded, so <c>sent_import org.package</c> writes it to <c>Code/Sents/Imported/org.package/</c>, where the
/// game compiles it like its own. Spawn the sent once first, so the engine has downloaded it. See docs/sents.md.
/// </summary>
public static class SentImporter
{
	[ConCmd( "sent_import" )]
	public static void Import( string ident )
	{
		ident = ident?.Trim().ToLowerInvariant();

		if ( string.IsNullOrEmpty( ident ) || !ident.Contains( '.' ) )
		{
			Log.Warning( "sent_import: give a cloud ident, e.g. sent_import crosschainer.hokuto_boxer" );
			return;
		}

		var bin = Path.Combine( Path.GetDirectoryName( Environment.ProcessPath ), "download", "assets", "_bin" );
		var cll = Directory.Exists( bin )
			? new DirectoryInfo( bin ).GetFiles( $"package_{ident.Replace( '.', '_' )}.*.cll" )
				.OrderByDescending( x => x.LastWriteTimeUtc ).FirstOrDefault()
			: null;

		if ( cll is null )
		{
			Log.Warning( $"sent_import: {ident} is not downloaded. Spawn it once (spawn_sent {ident}) and try again." );
			return;
		}

		string text;

		using ( var zip = new GZipStream( File.OpenRead( cll.FullName ), CompressionMode.Decompress ) )
		using ( var reader = new StreamReader( zip, Encoding.UTF8 ) )
			text = reader.ReadToEnd();

		var target = Path.Combine( Project.Current.GetRootPath(), "Code", "Sents", "Imported", ident );
		var imported = Path.Combine( Project.Current.GetRootPath(), "Code", "Sents", "Imported" );
		var written = 0;
		var shared = 0;
		var position = 0;

		// The documents are JSON arrays of { Text, LocalPath } inside the package's binary container.
		while ( true )
		{
			var start = text.IndexOf( "[{\"Text\":", position, StringComparison.Ordinal );

			if ( start < 0 )
				break;

			JsonDocument doc;

			try
			{
				doc = JsonDocument.Parse( ExtractArray( text, start, out var length ) );
				position = start + length;
			}
			catch ( Exception e )
			{
				Log.Warning( $"sent_import: could not read the documents of {ident}: {e.Message}" );
				break;
			}

			foreach ( var item in doc.RootElement.EnumerateArray() )
			{
				var local = item.GetProperty( "LocalPath" ).GetString().Replace( '\\', '/' );
				var source = item.GetProperty( "Text" ).GetString();

				// Build output and a path that tries to leave the folder are not source.
				if ( local.StartsWith( "/.obj/" ) || local.StartsWith( ".obj/" ) || local.Contains( ".." ) )
					continue;

				// A library several sents carry (one author's shared vehicle code) is imported once.
				if ( IdenticalElsewhere( imported, target, Path.GetFileName( local ), source ) )
				{
					shared++;
					continue;
				}

				var path = Path.Combine( target, local.Replace( '/', Path.DirectorySeparatorChar ) );
				Directory.CreateDirectory( Path.GetDirectoryName( path ) );
				File.WriteAllText( path, source.Replace( "\r\n", "\n" ).Replace( "\n", "\r\n" ) );
				written++;
			}
		}

		Log.Info( $"sent_import: {ident}: {written} files written to Code/Sents/Imported/{ident}, {shared} already imported "
			+ "by another sent. Read the code before you keep it: it runs in the game with no sandbox." );
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

	static bool IdenticalElsewhere( string imported, string own, string fileName, string source )
	{
		if ( !Directory.Exists( imported ) )
			return false;

		foreach ( var file in Directory.EnumerateFiles( imported, fileName, SearchOption.AllDirectories ) )
		{
			if ( file.StartsWith( own, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( File.ReadAllText( file ).Replace( "\r\n", "\n" ) == source.Replace( "\r\n", "\n" ) )
				return true;
		}

		return false;
	}
}
