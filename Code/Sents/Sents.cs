using System.Text.Json.Nodes;
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

	/// <summary>
	/// The namespace of an imported sent's code, for the sents whose type names another import also has. In the sandbox
	/// game every sent is its own package, so four Combine sents can each have a <c>CombineNpc</c>; here they share one
	/// assembly, and a prefab's plain <c>"CombineNpc"</c> would find only one of them. These namespaces are written in
	/// the sent's own source (<c>namespace Hl2Metrocop;</c>). Add a sent here when it is imported and its prefab names
	/// a component that exists in more than one namespace.
	/// </summary>
	static readonly Dictionary<string, string> TypeNamespaces = new()
	{
		["hl2npcs.metrocop"] = "Hl2Metrocop",
		["hl2npcs.combine_soldier"] = "Hl2Combine",
		["hl2npcs.combine_elite"] = "Hl2CombineElite",
		["hl2npcs.combine_prisonguard"] = "Hl2CombinePrisonGuard",
	};

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

		// A sent whose type names are shared with another import is built from its data, with each component's type
		// named in full, as the engine's own clone would look them up by the plain name and find the wrong one.
		if ( TypeNamespaces.TryGetValue( ident, out var typeNamespace ) )
		{
			var data = FreshGuids( entity.Prefab.RootObject.ToJsonString() );
			QualifyTypes( data, typeNamespace );

			var built = new GameObject( false, entity.Title ?? ident );
			built.Deserialize( data );
			built.WorldTransform = transform;
			built.NetworkSpawn( true, null );

			Log.Info( $"spawn_sent: {caller.DisplayName} spawned {entity.Title ?? ident}" );
			return;
		}

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
			go.Deserialize( FreshGuids( entity.Prefab.RootObject.ToJsonString() ) );
			go.WorldTransform = transform;
		}

		go.NetworkSpawn( true, null );

		Log.Info( $"spawn_sent: {caller.DisplayName} spawned {entity.Title ?? ident}" );
	}

	/// <summary>
	/// A prefab's data with every object and component id replaced by a new one, and every reference to them with it.
	/// Building an object from the data (not <c>GameObject.Clone</c>, which does this) keeps the prefab's ids, so a
	/// second copy would find them taken, get new ones, and its components that refer to each other by id (an NPC's
	/// renderer) would point into the first copy: a second NPC in the world A-posed on the first one's renderer and
	/// took the first one's damage.
	/// </summary>
	static JsonObject FreshGuids( string json )
	{
		var ids = Regex.Matches( json, @"""__guid"":\s*""([0-9a-fA-F-]{36})""" )
			.Select( x => x.Groups[1].Value )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();

		foreach ( var id in ids )
			json = json.Replace( id, Guid.NewGuid().ToString(), StringComparison.OrdinalIgnoreCase );

		return JsonNode.Parse( json ).AsObject();
	}

	/// <summary>
	/// Puts the sent's namespace in front of every component type that exists there.
	/// </summary>
	static void QualifyTypes( JsonNode node, string typeNamespace )
	{
		if ( node is JsonObject obj )
		{
			if ( obj["__type"] is JsonValue type && type.TryGetValue<string>( out var name )
			     && TypeLibrary.GetType( $"{typeNamespace}.{name}" ) is not null )
				obj["__type"] = $"{typeNamespace}.{name}";

			foreach ( var child in obj.Select( x => x.Value ).ToList() )
			{
				if ( child is not null )
					QualifyTypes( child, typeNamespace );
			}
		}
		else if ( node is JsonArray array )
		{
			foreach ( var child in array )
			{
				if ( child is not null )
					QualifyTypes( child, typeNamespace );
			}
		}
	}
}
