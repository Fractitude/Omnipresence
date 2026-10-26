using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Sandbox;

/// <summary>
/// <c>sent_remove org.package</c>: takes an integrated or imported sent out of the project again. Its prefab folder
/// (<c>Assets/sents/&lt;ident&gt;/</c>) is only its own and goes whole. In its code folder
/// (<c>Code/Sents/Imported/&lt;ident&gt;/</c>) a file stays if a type it declares is named by anything else that is
/// kept: another import's code, <c>Code/Sents/Ported/</c> or another sent's prefab. That is how a library one author's
/// sents share (imported once, into the first sent's folder, see <see cref="SentImporter"/>) survives the removal of
/// the sent that carried it. A file that only a kept file needs stays too.
/// <para>
/// The test is by name, so it errs towards keeping: a file stays if its type's name appears anywhere in the others.
/// </para>
/// </summary>
public static class SentRemover
{
	static readonly Regex TypePattern = new( @"\b(?:class|struct|enum|interface|record)\s+([A-Za-z_]\w*)" );
	static readonly Regex Word = new( @"\b[A-Za-z_]\w*\b" );

	[ConCmd( "sent_remove" )]
	public static void Remove( string ident ) => RemoveSent( ident );

	/// <summary>
	/// True when something of the sent was removed.
	/// </summary>
	public static bool RemoveSent( string ident )
	{
		ident = ident?.Trim().ToLowerInvariant();

		if ( !Sents.IsIdent( ident ) )
		{
			Log.Warning( "sent_remove: give a cloud ident, e.g. sent_remove hl2npcs.metrocop" );
			return false;
		}

		var root = Project.Current.GetRootPath();
		var prefabs = Path.Combine( root, "Assets", "sents" );
		var importedRoot = Path.Combine( root, "Code", "Sents", "Imported" );
		var portedRoot = Path.Combine( root, "Code", "Sents", "Ported" );
		var prefabFolder = Path.Combine( prefabs, ident );
		var codeFolder = Path.Combine( importedRoot, ident );

		if ( !Directory.Exists( prefabFolder ) && !Directory.Exists( codeFolder ) )
		{
			Log.Warning( $"sent_remove: {ident} is not in the project." );
			return false;
		}

		// Everything the rest of the project names: the words of every other sent's code and prefabs.
		var used = new HashSet<string>();

		foreach ( var folder in SiblingFolders( importedRoot, codeFolder ).Append( portedRoot ) )
			AddWords( used, folder, "*.cs", "*.razor" );

		foreach ( var folder in SiblingFolders( prefabs, prefabFolder ) )
			AddWords( used, folder, "*.prefab" );

		var kept = new List<string>();
		var removed = 0;

		if ( Directory.Exists( codeFolder ) )
		{
			var files = Directory.EnumerateFiles( codeFolder, "*", SearchOption.AllDirectories )
				.Where( x => !Path.GetFileName( x ).Equals( "SentPackage.cs", StringComparison.OrdinalIgnoreCase ) )
				.ToDictionary( x => x, Declared );

			// A kept file brings the words it uses into play, so what it needs stays as well.
			bool changed;

			do
			{
				changed = false;

				foreach ( var (file, declared) in files.ToList() )
				{
					if ( kept.Contains( file ) || !IsStyleOf( file, kept ) && !declared.Any( used.Contains ) )
						continue;

					kept.Add( file );
					changed = true;

					if ( Path.GetExtension( file ) is ".cs" or ".razor" )
						AddWords( used, File.ReadAllText( file ) );
				}
			} while ( changed );

			foreach ( var file in files.Keys.Except( kept ) )
			{
				File.Delete( file );
				removed++;
			}

			File.Delete( Path.Combine( codeFolder, "SentPackage.cs" ) );
			DeleteEmptyFolders( codeFolder );
		}

		if ( Directory.Exists( prefabFolder ) )
		{
			Directory.Delete( prefabFolder, true );
			removed++;
		}

		var message = $"sent_remove: {ident} removed ({removed} file(s) and folder(s) deleted)";

		if ( kept.Count > 0 )
		{
			var names = kept.Select( x => Path.GetRelativePath( codeFolder, x ).Replace( '\\', '/' ) );
			message += $"; kept {kept.Count} code file(s) that other sents use, still in Code/Sents/Imported/{ident}/: "
			           + string.Join( ", ", names );
		}

		Log.Info( message + "." );
		return true;
	}

	static IEnumerable<string> SiblingFolders( string parent, string own ) =>
		Directory.Exists( parent )
			? Directory.GetDirectories( parent ).Where( x => !SamePath( x, own ) )
			: [];

	static bool SamePath( string a, string b ) =>
		string.Equals( Path.GetFullPath( a ).TrimEnd( '\\', '/' ), Path.GetFullPath( b ).TrimEnd( '\\', '/' ),
			StringComparison.OrdinalIgnoreCase );

	static void AddWords( HashSet<string> words, string folder, params string[] patterns )
	{
		foreach ( var pattern in patterns )
		{
			if ( !Directory.Exists( folder ) )
				continue;

			foreach ( var file in Directory.EnumerateFiles( folder, pattern, SearchOption.AllDirectories ) )
				AddWords( words, File.ReadAllText( file ) );
		}
	}

	static void AddWords( HashSet<string> words, string text )
	{
		foreach ( Match match in Word.Matches( text ) )
			words.Add( match.Value );
	}

	// The names a file stands for: the types it declares, and a razor panel's file name.
	static List<string> Declared( string file )
	{
		var extension = Path.GetExtension( file ).ToLowerInvariant();

		if ( extension is not (".cs" or ".razor") )
			return [];

		var names = TypePattern.Matches( File.ReadAllText( file ) ).Select( m => m.Groups[1].Value ).ToList();

		if ( extension == ".razor" )
			names.Add( Path.GetFileNameWithoutExtension( file ) );

		return names;
	}

	// A stylesheet follows the razor panel of the same name.
	static bool IsStyleOf( string file, List<string> kept ) =>
		file.EndsWith( ".scss", StringComparison.OrdinalIgnoreCase ) || file.EndsWith( ".css", StringComparison.OrdinalIgnoreCase )
			? kept.Any( x => x.EndsWith( ".razor", StringComparison.OrdinalIgnoreCase )
			                 && Path.GetFileNameWithoutExtension( file ).StartsWith( Path.GetFileNameWithoutExtension( x ),
				                 StringComparison.OrdinalIgnoreCase ) )
			: false;

	static void DeleteEmptyFolders( string folder )
	{
		foreach ( var child in Directory.GetDirectories( folder ) )
			DeleteEmptyFolders( child );

		if ( !Directory.EnumerateFileSystemEntries( folder ).Any() )
			Directory.Delete( folder );
	}
}
