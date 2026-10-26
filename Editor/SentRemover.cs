using System;
using System.IO;

namespace Sandbox;

/// <summary>
/// <c>sent_remove org.package</c>: takes an integrated or imported sent out of the project again. Its prefab folder
/// (<c>Assets/sents/&lt;author&gt;/&lt;package&gt;/</c>) is only its own and goes whole. In its code folder
/// (<c>Code/Sents/Imported/&lt;author&gt;/&lt;package&gt;/</c>) a file stays if another import relies on it: a library
/// one author's sents share is imported once, into the first sent's folder, and the later sents leave those files out
/// (<see cref="SentImporter"/>). So for every other import, the files its package has and its folder has not are the
/// ones it needs from elsewhere, and a file here with such a name stays. This is read from the packages, not guessed
/// from the code: a name test misses extension methods and namespaces. If another import's package is not downloaded
/// the removal is refused, as its needs are not known.
/// </summary>
public static class SentRemover
{
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
		var importedRoot = Path.Combine( root, "Code", "Sents", "Imported" );
		var authorName = Sents.AuthorOf( ident );
		var packageName = Sents.PackageOf( ident );
		var prefabFolder = Path.Combine( root, "Assets", "sents", authorName, packageName );
		var codeFolder = Path.Combine( importedRoot, authorName, packageName );

		if ( !Directory.Exists( prefabFolder ) && !Directory.Exists( codeFolder ) )
		{
			Log.Warning( $"sent_remove: {ident} is not in the project." );
			return false;
		}

		var needed = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		if ( Directory.Exists( importedRoot ) )
		{
			foreach ( var other in ImportedFolders( importedRoot ) )
			{
				var otherIdent = other.Ident;

				if ( otherIdent.Equals( ident, StringComparison.OrdinalIgnoreCase ) )
					continue;

				var package = SentImporter.FindPackage( otherIdent );

				if ( package is null )
				{
					Log.Warning( $"sent_remove: nothing was removed. The package of {otherIdent} is not downloaded, so what it "
					             + $"needs from {ident} is not known: spawn it once (spawn_sent {otherIdent}), then try again." );
					return false;
				}

				foreach ( var document in SentImporter.ReadDocuments( package ) )
				{
					var local = Path.Combine( other.Folder, document.Path.Replace( '/', Path.DirectorySeparatorChar ) );

					if ( !File.Exists( local ) )
						needed.Add( Path.GetFileName( document.Path ) );
				}
			}
		}

		var kept = new List<string>();
		var removed = 0;

		if ( Directory.Exists( codeFolder ) )
		{
			foreach ( var file in Directory.EnumerateFiles( codeFolder, "*", SearchOption.AllDirectories ).ToList() )
			{
				if ( needed.Contains( Path.GetFileName( file ) ) )
				{
					kept.Add( file );
					continue;
				}

				File.Delete( file );
				removed++;
			}

			DeleteEmptyFolders( codeFolder );
			DeleteIfEmpty( Path.GetDirectoryName( codeFolder ) );
		}

		if ( Directory.Exists( prefabFolder ) )
		{
			removed += SentAssets.Remove( ident );
			Directory.Delete( prefabFolder, true );
			removed++;
			DeleteIfEmpty( Path.GetDirectoryName( prefabFolder ) );
			SentLocal.SetRemoved( ident, true );
		}

		var message = $"sent_remove: {ident} removed ({removed} file(s) and folder(s) deleted)";

		if ( kept.Count > 0 )
		{
			var names = kept.Select( x => Path.GetRelativePath( codeFolder, x ).Replace( '\\', '/' ) );
			message += $"; kept {kept.Count} code file(s) that other sents use, still in Code/Sents/Imported/{Sents.Folder( ident )}/: "
			           + string.Join( ", ", names );
		}

		Log.Info( message + "." );
		return true;
	}

	static void DeleteEmptyFolders( string folder )
	{
		foreach ( var child in Directory.GetDirectories( folder ) )
			DeleteEmptyFolders( child );

		if ( !Directory.EnumerateFileSystemEntries( folder ).Any() )
			Directory.Delete( folder );
	}

	// An author's folder goes with its last sent.
	static void DeleteIfEmpty( string folder )
	{
		if ( folder is not null && Directory.Exists( folder ) && !Directory.EnumerateFileSystemEntries( folder ).Any() )
			Directory.Delete( folder );
	}

	// Every imported sent's code folder under Code/Sents/Imported, with the ident its two folders make
	// (crosschainer/hokuto_boxer is crosschainer.hokuto_boxer).
	static IEnumerable<(string Ident, string Folder)> ImportedFolders( string importedRoot ) =>
		Directory.GetDirectories( importedRoot )
			.SelectMany( author => Directory.GetDirectories( author )
				.Select( folder => ($"{Path.GetFileName( author )}.{Path.GetFileName( folder )}", folder) ) );
}
