/// <summary>
/// Spawns a prefab at this object when it is enabled or disabled. The physgun's grab effects use it to add their sparks.
/// </summary>
public sealed class PrefabSpawner : Component
{
	[Property] public GameObject Prefab { get; set; }

	/// <summary>
	/// Parent the spawned object to this one, so it follows.
	/// </summary>
	[Property] public bool AsChild { get; set; }

	[Property] public bool SpawnOnEnabled { get; set; } = true;
	[Property] public bool SpawnOnDisabled { get; set; }

	protected override void OnEnabled()
	{
		if ( SpawnOnEnabled )
			Spawn();
	}

	protected override void OnDisabled()
	{
		if ( SpawnOnDisabled )
			Spawn();
	}

	void Spawn()
	{
		if ( !Prefab.IsValid() )
			return;

		var spawned = Prefab.Clone( new CloneConfig
		{
			Transform = WorldTransform,
			Parent = AsChild ? GameObject : null,
			StartEnabled = true
		} );

		spawned.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
	}
}
