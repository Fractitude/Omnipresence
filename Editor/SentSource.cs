using System;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Where a sent came from: its title, author and the address of its page, kept beside everything we bring in from the
/// workshop so the origin is never lost (<c>source.json</c> next to an integrated prefab, a line in the code's
/// <c>SentPackage.cs</c>).
/// </summary>
public static class SentSource
{
	/// <summary>
	/// The package's description from the cloud, or null if it could not be fetched.
	/// </summary>
	public static async Task<Package> Fetch( string ident )
	{
		try
		{
			return await Package.FetchAsync( ident, false );
		}
		catch ( Exception e )
		{
			Log.Warning( $"sent: could not read the page of {ident}: {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// One line: <c>Title by Author, https://sbox.game/org/package (version 123)</c>.
	/// </summary>
	public static string Describe( Package package ) => package is null
		? "unknown (could not be fetched)"
		: $"{package.Title} by {Author( package )}, {Url( package )} (version {package.Revision?.VersionId})";

	public static string Author( Package package ) => package.Org?.Title ?? package.Org?.Ident ?? "unknown";

	public static string Url( Package package ) => string.IsNullOrEmpty( package.Url ) ? $"https://sbox.game/{package.FullIdent.Replace( '.', '/' )}" : package.Url;
}
