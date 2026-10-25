using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Sandbox;

/// <summary>
/// Makes the object of a sent from its prefab, the one place that does. Two ways, because the engine's own clone is not
/// enough for everything a sent can be:
/// <list type="number">
/// <item>
/// <b>Clone</b> the prefab, as the sandbox game does. It is checked afterwards: a component the clone lacks means the
/// engine had the prefab cached before its code was imported (the components are missing), or the sent's own package
/// failed to compile (the components are left out without a trace).
/// </item>
/// <item>
/// <b>Build from the prefab's data</b>, which looks every type up again. Used when the clone fell short, and always
/// for a sent whose code is imported with namespaces (<see cref="SentPackages"/>), because the clone would find a plain
/// type name in the wrong sent.
/// </item>
/// </list>
/// </summary>
public static class SentBuilder
{
	/// <summary>
	/// The object of <paramref name="entity"/>, not yet enabled or networked, at <paramref name="transform"/>.
	/// </summary>
	public static GameObject Build( ScriptedEntity entity, string ident, Transform transform )
	{
		var prefab = entity.Prefab;
		var namespaces = SentPackages.NamespacesOf( ident );

		if ( namespaces.Count == 0 )
		{
			var clone = GameObject.Clone( prefab, new CloneConfig { Transform = transform, StartEnabled = false } );

			if ( IsComplete( clone, prefab ) )
				return clone;

			clone.Destroy();
		}

		var built = new GameObject( false, entity.Title ?? ident );
		built.Deserialize( DataOf( prefab, namespaces ) );
		built.WorldTransform = transform;
		return built;
	}

	/// <summary>
	/// The object of an integrated sent (<see cref="SentLocal"/>): its prefab is ours, with the component types already
	/// named in full, so a plain clone is all it takes.
	/// </summary>
	public static GameObject BuildLocal( PrefabFile prefab, Transform transform ) =>
		GameObject.Clone( prefab, new CloneConfig { Transform = transform, StartEnabled = false } );

	/// <summary>
	/// A prefab's data with each component type named in full, by the first of <paramref name="namespaces"/> that has it
	/// (<paramref name="exists"/> says whether a type does). What <c>sent_integrate</c> saves as the sent's own prefab.
	/// </summary>
	public static JsonObject QualifiedData( PrefabFile prefab, IReadOnlyList<string> namespaces, Func<string, bool> exists )
	{
		var data = JsonNode.Parse( prefab.RootObject.ToJsonString() ).AsObject();
		QualifyTypes( data, namespaces, exists );
		return data;
	}

	/// <summary>
	/// How many components of <paramref name="go"/> are missing: their type is not in this game.
	/// </summary>
	public static int MissingCount( GameObject go ) =>
		go.Components.GetAll( FindMode.EverythingInSelfAndDescendants ).Count( x => x is MissingComponent );

	/// <summary>
	/// Whether a clone has every component its prefab's data lists. Components of nested prefabs are not listed in the
	/// data, so a clone has more than the data names, never fewer.
	/// </summary>
	static bool IsComplete( GameObject clone, PrefabFile prefab )
	{
		var components = clone.Components.GetAll( FindMode.EverythingInSelfAndDescendants ).ToList();

		return components.All( x => x is not MissingComponent ) && components.Count >= TypeCount( prefab );
	}

	static int TypeCount( PrefabFile prefab ) => Regex.Matches( prefab.RootObject.ToJsonString(), "\"__type\"" ).Count;

	/// <summary>
	/// A prefab's data ready to build from: every object and component id replaced by a new one, and each component's
	/// type named in full where the sent's namespaces have it.
	/// <para>
	/// The ids matter. A prefab keeps its own, and its components refer to each other by them (an NPC's renderer).
	/// <c>GameObject.Clone</c> makes new ones; building from the data does not, so a second copy found the ids taken,
	/// got new ones, and its references pointed into the first copy: the second NPC in the world A-posed on the first
	/// one's renderer and took the first one's damage.
	/// </para>
	/// </summary>
	static JsonObject DataOf( PrefabFile prefab, IReadOnlyList<string> namespaces )
	{
		var json = prefab.RootObject.ToJsonString();

		var ids = Regex.Matches( json, @"""__guid"":\s*""([0-9a-fA-F-]{36})""" )
			.Select( x => x.Groups[1].Value )
			.Distinct( StringComparer.OrdinalIgnoreCase );

		foreach ( var id in ids )
			json = json.Replace( id, Guid.NewGuid().ToString(), StringComparison.OrdinalIgnoreCase );

		var data = JsonNode.Parse( json ).AsObject();

		if ( namespaces.Count > 0 )
			QualifyTypes( data, namespaces, x => TypeLibrary.GetType( x ) is not null );

		return data;
	}

	/// <summary>
	/// Names every component type in full, by the first namespace that has it.
	/// </summary>
	static void QualifyTypes( JsonNode node, IReadOnlyList<string> namespaces, Func<string, bool> exists )
	{
		switch ( node )
		{
			case JsonObject obj:
				if ( obj["__type"] is JsonValue type && type.TryGetValue<string>( out var name ) )
				{
					var full = namespaces.Select( x => $"{x}.{name}" ).FirstOrDefault( exists );

					if ( full is not null )
						obj["__type"] = full;
				}

				foreach ( var child in obj.Select( x => x.Value ).ToList() )
					QualifyTypes( child, namespaces, exists );

				break;

			case JsonArray array:
				foreach ( var child in array )
					QualifyTypes( child, namespaces, exists );

				break;
		}
	}
}
