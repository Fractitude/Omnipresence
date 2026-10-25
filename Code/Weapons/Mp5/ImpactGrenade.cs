/// <summary>
/// A 40mm launcher round - flies on a ballistic arc, nose along its velocity, and explodes on the
/// first thing it touches. Shooting it mid-flight sets it off too. Like a real launcher it must fly an
/// arming distance before it will go off; anything it hits short of that leaves a dud.
/// </summary>
public sealed class ImpactGrenade : ProjectileEntity, Component.IDamageable
{
	/// <summary>
	/// How far the round has to fly - the length of its whole path, in inches, not how far it has got
	/// from the barrel - before it will go off, a real grenade launcher's arming distance. A hit short of
	/// it leaves a dud, so a wall or a teammate at point-blank cannot kill the player who fired. Zero
	/// arms the round at once.
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

	// Where the last chunk of path was charged to, and how much path has been charged so far.
	Vector3 _lastPosition;
	float _travelled;

	bool _exploded;

	/// <summary>
	/// True once the round has flown <see cref="ArmingDistance"/>. Measured along the path, so a round
	/// fired straight up and caught on the way down is armed like one that went the same distance out.
	/// </summary>
	bool Armed => _travelled >= ArmingDistance;

	protected override void OnStart()
	{
		base.OnStart();

		// The round is spawned at the muzzle, so where it starts is where the barrel was.
		_lastPosition = WorldPosition;
	}

	/// <summary>
	/// Adds the distance flown since the last charge to <see cref="_travelled"/> - one length, once a
	/// physics step, whatever direction the round went in.
	/// </summary>
	void ChargeDistance()
	{
		var position = WorldPosition;
		_travelled += ( position - _lastPosition ).Length;
		_lastPosition = position;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		if ( TimeSinceCreated > Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		ChargeDistance();

		var velocity = Rigidbody.Velocity;
		if ( velocity.LengthSquared > 1f )
			WorldRotation = Rotation.LookAt( velocity, Vector3.Up );
	}

	protected override void OnHit( Collision collision = default )
	{
		// A hit lands in the middle of a step, before the next fixed update charges it: count the run
		// since the last one, or a round fired into a wall in the same step it was fired measures zero.
		ChargeDistance();

		// Still inside the arming distance, so a dud: the round stays inert. The collision itself still
		// happened - nothing here touches the body - so it bounces off and Lifetime clears it.
		if ( !Armed ) return;

		Detonate();
	}

	void Component.IDamageable.OnDamage( in DamageInfo damage )
	{
		if ( IsProxy || damage.Damage <= 0f ) return;

		// A bullet lands in the middle of a step too, so charge the run since the last one before asking
		// whether the round is armed.
		ChargeDistance();

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
