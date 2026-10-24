using Sandbox.Diagnostics;

namespace Sandbox;

/// <summary>
/// The host's game flow, a small version of Facepunch's sandbox <c>GameManager</c>: keeps a <see cref="PlayerData"/> for
/// every connection, credits kills, and spawns, respawns and observes players after they die. The first spawn when a
/// player joins is still the scene's <see cref="NetworkHelper"/>; this takes over from there, using its player prefab
/// and spawn points.
/// </summary>
public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener,
	IScenePhysicsEvents
{
	public GameManager( Scene scene ) : base( scene )
	{
	}

	void Component.INetworkListener.OnActive( Connection channel )
	{
		GetOrCreatePlayerData( channel );
	}

	/// <summary>
	/// Called when someone leaves the server. This will only be called for the host.
	/// </summary>
	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		PlayerData.For( channel )?.GameObject.Destroy();
	}

	/// <summary>
	/// The data that outlives this connection's player, made on first use. Host only.
	/// </summary>
	public PlayerData GetOrCreatePlayerData( Connection channel )
	{
		Assert.True( Networking.IsHost, "Only the host makes player data" );

		var existing = PlayerData.For( channel );
		if ( existing.IsValid() )
			return existing;

		var go = new GameObject( true, $"PlayerInfo - {channel.DisplayName}" );
		var data = go.AddComponent<PlayerData>();

		go.NetworkSpawn( channel );
		go.Network.SetOwnerTransfer( OwnerTransfer.Fixed );

		return data;
	}

	/// <summary>
	/// Spawns a player for this connection if they don't already have one. Host only.
	/// Fires <see cref="Global.IPlayerEvents.OnPlayerRespawning"/> first, so listeners can move or
	/// cancel the spawn. Pass <paramref name="force"/> to skip that event. Returns the new player, or null if the
	/// connection already has one or a listener cancelled.
	/// </summary>
	public PlayerHealth SpawnPlayer( Connection connection, bool force = false )
	{
		Assert.True( Networking.IsHost, $"Client tried to SpawnPlayer: {connection?.DisplayName}" );

		var playerData = GetOrCreatePlayerData( connection );

		// does this connection already have a player?
		if ( PlayerHealth.FindForConnection( connection ).IsValid() )
			return null;

		var helper = Scene.GetAllComponents<NetworkHelper>().FirstOrDefault();
		if ( !helper.IsValid() || helper.PlayerPrefab is null )
		{
			Log.Warning( "No NetworkHelper with a player prefab in the scene, can't spawn a player." );
			return null;
		}

		// A new body is in the real world, wherever the last one died.
		playerData.InWired = false;
		playerData.HomeTransform = null;

		var startLocation = FindSpawnLocation( helper ).WithScale( 1 );

		// Fire pre-respawn event - listeners can move or cancel the spawn, unless we're forcing it
		if ( !force )
		{
			var respawnEvent = new PlayerRespawnEvent { PlayerData = playerData, SpawnLocation = startLocation };
			Global.IPlayerEvents.Post( x => x.OnPlayerRespawning( respawnEvent ) );

			if ( respawnEvent.Cancelled )
				return null;

			startLocation = respawnEvent.SpawnLocation;
		}

		// Spawn this object and make the client the owner
		var playerGo = helper.PlayerPrefab.Clone( new CloneConfig
		{
			Name = connection?.DisplayName, StartEnabled = false, Transform = startLocation
		} );

		var player = playerGo.Components.Get<PlayerHealth>( true );
		playerGo.NetworkSpawn( connection );

		Local.IPlayerEvents.PostToGameObject( player.GameObject, x => x.OnSpawned() );
		Global.IPlayerEvents.Post( x => x.OnPlayerSpawned( player ) );

		return player;
	}

	/// <summary>
	/// Forcefully respawns a player, whether they're dead or alive. Host only. Removes their current
	/// player object and any observer without a death, then spawns a fresh one without consulting
	/// <see cref="Global.IPlayerEvents.OnPlayerRespawning"/>.
	/// </summary>
	public PlayerHealth Respawn( Connection connection )
	{
		Assert.True( Networking.IsHost, "Only the host can respawn players" );

		PlayerHealth.FindForConnection( connection )?.GameObject.Destroy();
		DestroyObservers( connection );

		return SpawnPlayer( connection, force: true );
	}

	/// <summary>
	/// Clean up any lingering death observers for this connection.
	/// </summary>
	void DestroyObservers( Connection connection )
	{
		foreach ( var observer in Scene.GetAllComponents<PlayerObserver>().Where( x => x.Network.Owner == connection )
			         .ToArray() )
		{
			observer.GameObject.Destroy();
		}
	}

	/// <summary>
	/// Called by the client (via <see cref="PlayerObserver"/>) when they want to respawn. If a listener cancels the
	/// respawn the observer is kept, so the player stays spectating until <see cref="Respawn"/> or the next request
	/// goes through.
	/// </summary>
	[Rpc.Host]
	internal static void RequestRespawn()
	{
		var manager = Current;
		var connection = Rpc.Caller;
		if ( manager is null )
			return;

		var player = manager.SpawnPlayer( connection );
		if ( !player.IsValid() )
			return;

		manager.DestroyObservers( connection );
	}

	/// <summary>Approximate standing-player hull, used for spawn clearance and floor settling.</summary>
	static readonly BBox PlayerHull = new( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 72f ) );

	/// <summary>The last position we spawned a player at, so we can avoid reusing it.</summary>
	Vector3? lastSpawnPosition;

	/// <summary>
	/// A place to spawn a player in the real world: <see cref="FindSpawnLocation(NetworkHelper)"/> for the scene's
	/// <see cref="NetworkHelper"/>, or the origin without one.
	/// </summary>
	public Transform FindSpawnLocation()
	{
		var helper = Scene.GetAllComponents<NetworkHelper>().FirstOrDefault();
		return helper.IsValid() ? FindSpawnLocation( helper ) : Transform.Zero;
	}

	/// <summary>
	/// Find a place to respawn: one of the NetworkHelper's spawn points, settled onto the floor, that nobody is
	/// standing on, avoiding the last one used. Without spawn points it is the NetworkHelper's own position.
	/// </summary>
	Transform FindSpawnLocation( NetworkHelper helper )
	{
		var points = helper.SpawnPoints?.Where( x => x.IsValid() ).ToArray();
		if ( points is null || points.Length == 0 )
			return helper.WorldTransform;

		var transform = SpawnPlacement.FindSpawnPosition(
			points.OrderBy( _ => Random.Shared.Next() ).Select( x => x.WorldTransform ),
			PlayerHull,
			avoid: lastSpawnPosition );

		lastSpawnPosition = transform.Position;
		return transform;
	}

	/// <summary>
	/// Moves a player to <paramref name="destination"/> and stops them. Host only: the player's own machine does it,
	/// since that is where their movement is simulated.
	/// </summary>
	public void Teleport( Connection connection, Transform destination )
	{
		Assert.True( Networking.IsHost, "Only the host teleports players" );
		TeleportRpc( connection.Id, destination.Position, destination.Rotation.Angles().yaw );
	}

	/// <summary>
	/// Fades one player's screen to black, or back. Host only; the screen is theirs, so only their client does it.
	/// </summary>
	public void Fade( Connection connection, bool toBlack )
	{
		Assert.True( Networking.IsHost, "Only the host fades a player's screen" );
		FadeRpc( connection.Id, toBlack );
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void FadeRpc( Guid connection, bool toBlack )
	{
		if ( Connection.Local?.Id == connection )
			ScreenFade.IsBlack = toBlack;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void TeleportRpc( Guid connection, Vector3 position, float yaw )
	{
		if ( Connection.Local?.Id != connection )
			return;

		var controller = Players.LocalController;
		if ( !controller.IsValid() )
			return;

		controller.WorldPosition = position;
		controller.EyeAngles = new Angles( 0, yaw, 0 );

		if ( controller.Body.IsValid() )
			controller.Body.Velocity = Vector3.Zero;
	}

	/// <summary>
	/// Anything that falls out of the world is removed.
	/// </summary>
	void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body )
	{
		WorldItemRegistry.Forget( body.GameObject );
		body.DestroyGameObject();
	}

	/// <summary>
	/// Called on the host when a player is killed: credits the attacker and counts the death.
	/// </summary>
	internal void OnDeath( PlayerHealth player, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );
		Assert.True( player.IsValid(), "Player invalid" );

		var playerData = GetOrCreatePlayerData( player.Network.Owner );
		var source = dmg.Attacker?.GetComponentInParent<IKillSource>( true );

		playerData.Deaths++;

		if ( source is null )
		{
			Log.Info( $"{player.Network.Owner?.DisplayName ?? "Unknown"} died (tags: {dmg.Tags})" );
			return;
		}

		var isSuicide = source is PlayerHealth p && p == player;

		if ( !isSuicide )
			source.OnKill( player.GameObject );

		// Fire kill event on the killer if they're a player
		if ( !isSuicide && source is PlayerHealth killer )
		{
			var killEvent = new PlayerKillEvent { Player = killer, Victim = player.GameObject, DamageInfo = dmg };
			Local.IPlayerEvents.PostToGameObject( killer.GameObject, x => x.OnKill( killEvent ) );
			Global.IPlayerEvents.Post( x => x.OnPlayerKill( killEvent ) );
		}

		var victimName = player.Network.Owner?.DisplayName ?? "Unknown";
		Log.Info( isSuicide
			? $"{victimName} killed themself (tags: {dmg.Tags})"
			: $"{source.DisplayName} killed {victimName} (tags: {dmg.Tags})" );
	}
}
