namespace Sandbox;

/// <summary>
/// Console commands for testing the crime loop. They only ask: the host decides (see <see cref="PlayerCrime"/>) and
/// refuses outside the editor.
/// </summary>
public static class CrimeCommands
{
	[ConCmd( "crime_give" )]
	public static void Give( string kind, int count = 1, int quality = 50 )
	{
		if ( ItemCatalog.Find( kind ) is null )
		{
			Log.Info( $"Unknown kind. Use one of: {string.Join( ", ", ItemCatalog.Items.Select( x => x.Id ) )}" );
			return;
		}

		PlayerCrime.Local?.DebugGive( kind, count, quality );
	}

	[ConCmd( "crime_tray_ready" )]
	public static void TrayReady() => PlayerCrime.Local?.DebugTraysReady();

	[ConCmd( "crime_postal_now" )]
	public static void PostalNow() => PlayerCrime.Local?.DebugPostalNow();

	[ConCmd( "crime_orders" )]
	public static void Orders( int count = 3 ) => PlayerCrime.Local?.DebugOrders( count );
}
