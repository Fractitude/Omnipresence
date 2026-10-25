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

		var transform = new Transform( position, rotation );
		var go = GameObject.Clone( entity.Prefab, new CloneConfig { Transform = transform, StartEnabled = false } );

		// The engine keeps a prefab's scene once loaded. If it was loaded before this game had the code of its
		// components (a sent imported while the editor was open), the clone has them as missing: build it from the
		// prefab's data instead, which looks the types up again.
		// A component whose type is in a package that failed to compile (a second sent of an author who shares code,
		// whose own build broke) is not even missing: it is left out of the clone. Fewer components than the prefab's
		// data lists means the same thing, so build it from the data as well.
		var components = go.Components.GetAll( FindMode.EverythingInSelfAndDescendants ).ToList();
		var listed = Regex.Matches( entity.Prefab.RootObject.ToJsonString(), "\"__type\"" ).Count;

		if ( components.Any( x => x is MissingComponent ) || components.Count < listed )
		{
			go.Destroy();
			go = new GameObject( false, entity.Title ?? ident );
			go.Deserialize( entity.Prefab.RootObject.AsObject() );
			go.WorldTransform = transform;
		}

		go.NetworkSpawn( true, null );

		Log.Info( $"spawn_sent: {caller.DisplayName} spawned {entity.Title ?? ident}" );
	}
}
