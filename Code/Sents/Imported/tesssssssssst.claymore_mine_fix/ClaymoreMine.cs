using Sandbox;

public sealed class Claymore : Component, Component.ITriggerListener
{
	[Property] public GameObject ExplosionPrefab { get; set; }

	[Property, Title("Distance devant la mine")] 
	public float ForwardDistance { get; set; } = 20f;

	private Prop _propComponent;
	private bool _hasExploded;

	protected override void OnStart()
	{
		_propComponent = GetComponent<Prop>();
	}

	protected override void OnUpdate()
	{
		if ( _hasExploded ) return;

		// Explose si la santé du Prop descend sous 450
		if ( _propComponent != null && _propComponent.Health < 450f )
		{
			Explode();
		}
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( _hasExploded ) return;

		// Explose si le joueur franchit la zone
		if ( other.GameObject.Name == "Player Body" || other.GameObject.Tags.Has( "player" ) )
		{
			Explode();
		}
	}

	public void OnTriggerExit( Collider other ) { }

	private void Explode()
	{
		if ( _hasExploded ) return;
		_hasExploded = true;

		if ( ExplosionPrefab.IsValid() )
		{
			// Positionne l'explosion juste devant le modèle (vers les lasers)
			Vector3 spawnPos = WorldPosition + (WorldRotation.Forward * ForwardDistance);

			// Pivote la prefab pour que les effets d'explosion soient orientés vers l'avant
			Rotation correctedRotation = WorldRotation * Rotation.From( 0, 180, 0 );

			ExplosionPrefab.Clone( spawnPos, correctedRotation );
		}

		GameObject.Destroy();
	}
}