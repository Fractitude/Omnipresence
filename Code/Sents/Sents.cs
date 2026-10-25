using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Spawns sents (Facepunch sandbox entity packages from the cloud) in the world. The host loads the package and builds
/// its prefab (<see cref="SentBuilder"/>), so a client only ever sends an ident.
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
	static readonly Regex IdentPattern = new( @"^[a-z0-9_]{1,64}\.[a-z0-9_]{1,64}$", RegexOptions.Compiled );

	/// <summary>
	/// How soon after one spawn the same player may ask for another, in seconds: a held key or a pasted line of commands
	/// would otherwise download and build the same package over and over.
	/// </summary>
	const float SpawnInterval = 0.5f;

	/// <summary>
	/// How far in front of the player's eyes a sent is put, at most.
	/// </summary>
	const float SpawnReach = 500f;

	// Not readonly: a hot reload would reset a readonly field that holds only engine and BCL types.
	static Dictionary<Guid, RealTimeSince> lastSpawn = new();

	/// <summary>
	/// Whether this machine's player may spawn a sent. The host checks again on every request.
	/// </summary>
	public static bool CanSpawn => AllowEveryone || Connection.Local?.IsHost == true;

	/// <summary>
	/// Whether <paramref name="ident"/> is a cloud ident (<c>org.package</c>), already trimmed and lower case.
	/// </summary>
	public static bool IsIdent( string ident ) => !string.IsNullOrEmpty( ident ) && IdentPattern.IsMatch( ident );

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

		if ( !IsIdent( ident ) )
		{
			Log.Warning( $"spawn_sent: '{ident}' is not a cloud ident (org.package)" );
			return;
		}

		if ( lastSpawn.TryGetValue( caller.Id, out var since ) && since < SpawnInterval )
		{
			Log.Warning( "spawn_sent: too fast, wait a moment" );
			return;
		}

		var player = Players.For<PlayerController>( caller );

		if ( !player.IsValid() )
		{
			Log.Warning( "spawn_sent: no player" );
			return;
		}

		lastSpawn[caller.Id] = 0;

		var origin = player.EyePosition;
		var trace = player.Scene.Trace.Ray( origin, origin + player.EyeAngles.Forward * SpawnReach )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		_ = SpawnAsync( ident, trace.EndPosition, player.EyeAngles.WithPitch( 0 ).ToRotation(), caller );
	}

	static async Task SpawnAsync( string ident, Vector3 position, Rotation rotation, Connection caller )
	{
		try
		{
			// An integrated sent is ours: its prefab and code are in the project, the workshop only gives its assets.
			if ( SentLocal.Find( ident ) is { } local )
			{
				await SentLocal.Mount( ident );

				var placed = SentBuilder.BuildLocal( local, new Transform( StandOnFloor( local, position ), rotation ) );
				placed.NetworkSpawn( true, null );

				Log.Info( $"spawn_sent: {caller.DisplayName} spawned {ident} (integrated)" );
				WarnAboutCode( ident, ident, false, placed );
				return;
			}

			var entity = await Cloud.Load<ScriptedEntity>( ident, true );

			if ( entity?.Prefab is null )
			{
				Log.Warning( $"spawn_sent: '{ident}' is not a sent, or could not be loaded" );
				return;
			}

			var go = SentBuilder.Build( entity, ident, new Transform( StandOnFloor( entity.Prefab, position ), rotation ) );
			go.NetworkSpawn( true, null );

			Log.Info( $"spawn_sent: {caller.DisplayName} spawned {entity.Title ?? ident}" );
			WarnAboutCode( entity.Title ?? ident, ident, entity.IncludeCode, go );
		}
		catch ( Exception e )
		{
			Log.Warning( $"spawn_sent: could not spawn {ident}: {e.Message}" );
		}
	}

	/// <summary>
	/// The point moved up so the sent's lowest part rests on it.
	/// </summary>
	static Vector3 StandOnFloor( PrefabFile prefab, Vector3 position ) =>
		position + Vector3.Up * -SceneUtility.GetPrefabScene( prefab ).GetLocalBounds().Mins.z;

	/// <summary>
	/// Says why a sent with code does nothing, and what to do about it.
	/// </summary>
	static void WarnAboutCode( string title, string ident, bool includeCode, GameObject go )
	{
		var missing = SentBuilder.MissingCount( go );

		if ( missing == 0 )
			return;

		Log.Warning( includeCode && !SentPackages.IsImported( ident )
			? $"spawn_sent: {title} has {missing} component(s) from its own code, which this game does not have. "
			  + $"Import it with sent_import {ident} (docs/sents.md)."
			: $"spawn_sent: {title} has {missing} component(s) this game does not have, so parts of it will not work." );
	}
}
