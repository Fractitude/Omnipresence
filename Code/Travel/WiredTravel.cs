namespace Sandbox;

/// <summary>
/// Going between the real world and the Wired, which are in the same scene: the Wired is under the map. Connecting
/// teleports the player to a random object tagged <see cref="SpawnTag"/>; disconnecting takes them back to where they
/// were. Each player is in one or the other (<see cref="PlayerData.InWired"/>).
/// </summary>
public static class WiredTravel
{
	/// <summary>
	/// The tag on the objects in the scene where a player arrives in the Wired.
	/// </summary>
	public const string SpawnTag = "spawn_point_wired";

	/// <summary>Approximate standing-player hull, used to find a spawn nobody is standing on.</summary>
	static readonly BBox PlayerHull = new( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 72f ) );

	/// <summary>
	/// Where a player is: the real world, or the Wired. Someone without a body or data yet is in the real world.
	/// </summary>
	public static WiredPlace PlaceOf( Connection connection ) =>
		PlayerData.For( connection ) is { InWired: true } ? WiredPlace.Wired : WiredPlace.Home;

	/// <summary>
	/// Who is crossing now, so a second press can't start another while the screen fades.
	/// </summary>
	static HashSet<Guid> crossing = new();

	/// <summary>
	/// Connect the player to the Wired from home, or disconnect them back home from the Wired. Only the host can; does
	/// nothing for someone without a body. The screen fades to black, the player is moved while it is black, and it fades
	/// back in.
	/// </summary>
	public static void Cross( Connection connection )
	{
		if ( !Networking.IsHost || connection is null || !crossing.Add( connection.Id ) )
			return;

		_ = CrossAsync( connection );
	}

	static async Task CrossAsync( Connection connection )
	{
		var manager = GameManager.Current;

		try
		{
			if ( manager is null || !CanCross( connection ) )
				return;

			manager.Fade( connection, true );
			await GameTask.DelaySeconds( ScreenFade.Duration );

			// They may have died or left while the screen faded.
			if ( CanCross( connection ) )
				Move( manager, connection );

			// The world swaps on their client a frame after they move; hold the black until it has.
			await GameTask.DelaySeconds( 0.15f );
		}
		finally
		{
			crossing.Remove( connection.Id );

			if ( manager is not null && Networking.IsHost )
				manager.Fade( connection, false );
		}
	}

	static bool CanCross( Connection connection ) =>
		connection is not null && PlayerData.For( connection ).IsValid()
		&& PlayerHealth.FindForConnection( connection ).IsValid();

	static void Move( GameManager manager, Connection connection )
	{
		var data = PlayerData.For( connection );
		var player = PlayerHealth.FindForConnection( connection );

		if ( !data.InWired )
		{
			var destination = FindWiredSpawn();
			if ( destination is null )
			{
				Log.Warning( $"WiredTravel: no object in the scene has the tag '{SpawnTag}'" );
				return;
			}

			data.HomeTransform = player.WorldTransform;
			data.InWired = true;
			manager.Teleport( connection, destination.Value );
			return;
		}

		// Back to where they were, or to a spawn if they have no record (they were put in the Wired some other way).
		var home = data.HomeTransform ?? manager.FindSpawnLocation();
		data.InWired = false;
		data.HomeTransform = null;
		manager.Teleport( connection, home );
	}

	/// <summary>
	/// A random Wired spawn that nobody is standing on, or null when the scene has none.
	/// </summary>
	static Transform? FindWiredSpawn()
	{
		var spawns = Game.ActiveScene?.GetAllObjects( true )
			.Where( x => x.Tags.Has( SpawnTag ) )
			.OrderBy( _ => Random.Shared.Next() )
			.Select( x => x.WorldTransform )
			.ToArray();

		if ( spawns is null || spawns.Length == 0 )
			return null;

		return SpawnPlacement.FindSpawnPosition( spawns, PlayerHull );
	}
}

public enum WiredPlace
{
	Home,
	Wired,
}
