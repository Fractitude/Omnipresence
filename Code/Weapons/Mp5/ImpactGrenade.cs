/// <summary>
/// A 40mm launcher round - flies on a ballistic arc, nose along its velocity, and explodes on the
/// first thing it touches. Shooting it mid-flight sets it off too. Like a real launcher it must clear
/// an arming distance before it will go off; anything it hits closer than that leaves a dud.
/// </summary>
public sealed class ImpactGrenade : ProjectileEntity, Component.IDamageable
{
	/// <summary>
	/// How far the round has to get from the barrel, in inches, before it will go off - a real grenade
	/// launcher's arming distance. A hit closer than this leaves a dud, so a wall or a teammate at
	/// point-blank cannot kill the player who fired. Zero arms the round at once.
	/// </summary>
	[Property] public float ArmingDistance { get; set; } = 300f;

	/// <summary>Radius of the blast, in inches.</summary>
	[Property] public float ExplosionRadius { get; set; } = 150f;

	/// <summary>Damage dealt to everything within <see cref="ExplosionRadius"/>.</summary>
	[Property] public float ExplosionDamage { get; set; } = 100f;

	/// <summary>How hard the blast throws what it catches, as the force's scale.</summary>
	[Property] public float ExplosionForce { get; set; } = 0.5f;

	/// <summary>Seconds before a round that never hit anything is removed.</summary>
	[Property] public float Lifetime { get; set; } = 10f;

	Vector3 _launchPosition;
	bool _exploded;

	/// <summary>
	/// True once the round has cleared <see cref="ArmingDistance"/> from where it left the barrel.
	/// </summary>
	bool Armed => ( WorldPosition - _launchPosition ).Length >= ArmingDistance;

	protected override void OnStart()
	{
		base.OnStart();

		// The round is spawned at the muzzle, so where it starts is where the barrel was.
		_launchPosition = WorldPosition;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		if ( TimeSinceCreated > Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		var velocity = Rigidbody.Velocity;
		if ( velocity.LengthSquared > 1f )
			WorldRotation = Rotation.LookAt( velocity, Vector3.Up );
	}

	protected override void OnHit( Collision collision = default )
	{
		// Still inside the arming distance, so a dud: the round stays inert. The collision itself still
		// happened - nothing here touches the body - so it bounces off and Lifetime clears it.
		if ( !Armed ) return;

		Detonate();
	}

	void Component.IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || damage.Damage <= 0f ) return;

		// An unarmed round is inert, so shooting it does not set it off either.
		if ( !Armed ) return;

		Detonate();
	}

	void Detonate()
	{
		if ( _exploded ) return;
		_exploded = true;

		Explode( ExplosionRadius, ExplosionDamage, ExplosionForce );
	}
}
