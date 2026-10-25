using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Spawns sents (Facepunch sandbox entity packages from the cloud) in the world. The host loads the package and
/// clones its prefab, so a client only ever sends an ident.
/// <para>
/// A sent can carry its own code (<see cref="ScriptedEntity.IncludeCode"/>), which runs in the game with no sandbox
/// and no permissions. Spawning one is therefore the host's choice: only the host's own player may, unless the host
/// turned <see cref="AllowEveryone"/> on. See docs/sents.md.
/// </para>
/// </summary>
public static class Sents
{
	/// <summary>
	/// Whether any player may spawn a sent, not only the host. Read on the host.
	/// </summary>
	[ConVar( "omni.sent_everyone", ConVarFlags.Saved )]
	public static bool AllowEveryone { get; set; }

	// A cloud ident is "org.package". Anything else (a path to a local file) is refused.
	static readonly Regex IdentPattern = new( @"^[a-z0-9_]+\.[a-z0-9_]+$", RegexOptions.Compiled );

	/// <summary>
	/// Whether this machine's player may spawn a sent. The host checks again on every request.
	/// </summary>
	public static bool CanSpawn => AllowEveryone || Connection.Local?.IsHost == true;

	/// <summary>
	/// Spawns a sent from the cloud in front of you, e.g. <c>spawn_sent crosschainer.hokuto_boxer</c>.
	/// </summary>
	[ConCmd( "spawn_sent" )]
	public static void Spawn( string ident ) => SpawnOnHost( ident );

	[Rpc.Host]
	static void SpawnOnHost( string ident )
	{
		var caller = Rpc.Caller;

		if ( !AllowEveryone && !caller.IsHost )
		{
			Log.Warning( $"spawn_sent: {caller.DisplayName} may not spawn sents" );
			return;
		}

		ident = ident?.Trim().ToLowerInvariant();

		if ( string.IsNullOrEmpty( ident ) || !IdentPattern.IsMatch( ident ) )
		{
			Log.Warning( $"spawn_sent: '{ident}' is not a cloud ident (org.package)" );
			return;
		}

		var player = Players.For<PlayerController>( caller );

		if ( !player.IsValid() )
		{
			Log.Warning( "spawn_sent: no player" );
			return;
		}

		var rotation = player.EyeAngles.WithPitch( 0 ).ToRotation();
		var origin = player.EyePosition;
		var trace = player.Scene.Trace.Ray( origin, origin + player.EyeAngles.Forward * 500f )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		_ = SpawnAsync( ident, trace.EndPosition, rotation, caller );
	}

	static async Task SpawnAsync( string ident, Vector3 position, Rotation rotation, Connection caller )
	{
		var entity = await Cloud.Load<ScriptedEntity>( ident, true );

		if ( entity?.Prefab is null )
		{
			Log.Warning( $"spawn_sent: '{ident}' is not a sent, or could not be loaded" );
			return;
		}

		// Stand it on the floor under the point we looked at.
		var bounds = SceneUtility.GetPrefabScene( entity.Prefab ).GetLocalBounds();
		position += Vector3.Up * -bounds.Mins.z;

		var go = GameObject.Clone( entity.Prefab, new CloneConfig
		{
			Transform = new Transform( position, rotation ),
			StartEnabled = false
		} );
		go.NetworkSpawn( true, null );

		Log.Info( $"spawn_sent: {caller.DisplayName} spawned {entity.Title ?? ident}" );
	}
}
