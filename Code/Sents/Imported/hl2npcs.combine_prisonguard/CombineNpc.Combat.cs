using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;
using Sandbox.Npcs.Layers;

namespace Hl2CombinePrisonGuard;

public enum LineOfFire
{
	Clear,
	Blocked,
	Friendly,
}

public sealed partial class CombineNpc
{
	/// <summary>Custom awareness bit: a live grenade is close by.</summary>
	public const NpcAwareness GrenadeDanger = (NpcAwareness)(1 << 8);

	[Property, Group( "Sounds" )] public SoundEvent Smg1FireSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent Smg1ReloadSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent Ar2FireSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent Ar2ReloadSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent ShotgunFireSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent ShotgunReloadSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent ShotgunPumpSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent ShotgunShellSound { get; set; }

	public SoundEvent FireSound
	{
		get
		{
			var sound = ActiveWeapon switch
			{
				CombineWeaponType.Ar2 => Ar2FireSound,
				CombineWeaponType.Shotgun => ShotgunFireSound,
				_ => Smg1FireSound,
			};
			OtherWeaponSound( false, ref sound );
			return sound;
		}
	}

	public SoundEvent ReloadSound
	{
		get
		{
			var sound = ActiveWeapon switch
			{
				CombineWeaponType.Ar2 => Ar2ReloadSound,
				CombineWeaponType.Shotgun => ShotgunReloadSound,
				_ => Smg1ReloadSound,
			};
			OtherWeaponSound( true, ref sound );
			return sound;
		}
	}
	[Property, Group( "Sounds" )] public SoundEvent PainSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent DeathSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent GearSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent GrenadeTickSound { get; set; }

	private TimeUntil _nextShot;
	private TimeUntil _nextOwnGrenade;
	private TimeSince _sinceGunshotStimulus = 10f;

	public Vector3 EyePosition => WorldPosition + Vector3.Up * (Crouched ? 40f : 64f);

	/// <summary>
	/// Where his shots start: the end of the barrel - or, when standing against a wall has pushed
	/// the barrel through it, this side of the wall, so bullets can't come out the other side.
	/// </summary>
	public Vector3 MuzzlePosition
	{
		get
		{
			var tip = GunTip;
			var body = WorldPosition.WithZ( tip.z );
			var tr = Scene.Trace.Ray( body, tip )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
				.Run();

			return tr.Hit ? tr.EndPosition - (tip - body).Normal * 2f : tip;
		}
	}

	private Vector3 GunTip
	{
		get
		{
			if ( Renderer.IsValid() && Renderer.GetAttachment( "muzzle_" + Gun.Name ) is { } t )
				return t.Position;

			return WorldPosition + Vector3.Up * (Crouched ? 36f : 52f) + WorldRotation.Forward * 24f;
		}
	}

	/// <summary>Upper chest of the target - HL2 soldiers aim for the body. Something with no head (the
	/// sandbox's Roller, a ball) is aimed at in the middle.</summary>
	public static Vector3 AimPoint( GameObject target )
	{
		var eyes = EyesOf( target, out var guessed );
		return guessed ? target.GetBounds().Center : eyes - Vector3.Up * 14f;
	}

	/// <summary>
	/// Where the target's eyes are. The sandbox finds them on the model ("eyes"); a model without that
	/// gets a guess 60 units up - over the top of anything small, so every shot at it went high.
	/// That guess is kept inside the target's bounds.
	/// </summary>
	public static Vector3 EyesOf( GameObject target, out bool guessed )
	{
		guessed = false;
		var eyes = AnimationLayer.GetEyePosition( target );
		if ( !target.IsValid() ) return eyes;

		var bounds = target.GetBounds();
		if ( bounds.Size.z < 1f || eyes.z <= bounds.Maxs.z + 2f ) return eyes;

		guessed = true;
		return bounds.Center.WithZ( bounds.Mins.z + bounds.Size.z * 0.75f );
	}

	public static Vector3 EyesOf( GameObject target ) => EyesOf( target, out _ );

	public bool IsFacing( Vector3 position, float maxAngle )
	{
		var direction = (position - WorldPosition).WithZ( 0 );
		if ( direction.LengthSquared < 1f ) return true;
		return Vector3.GetAngle( WorldRotation.Forward.WithZ( 0 ), direction.Normal ) <= maxAngle;
	}

	public void TurnTowards( Vector3 position, float degreesPerSecond )
	{
		var direction = (position - WorldPosition).WithZ( 0 );
		if ( direction.LengthSquared < 1f ) return;

		var target = Rotation.LookAt( direction.Normal, Vector3.Up );
		var remaining = WorldRotation.Distance( target );
		if ( remaining < 0.5f ) return;

		var step = MathF.Min( 1f, degreesPerSecond * Time.Delta / remaining );
		WorldRotation = Rotation.Slerp( WorldRotation, target, step );
	}

	/// <summary>Would a shot at the target hit it, a wall, or a friend?</summary>
	public LineOfFire CheckLineOfFire( GameObject target ) => CheckLineOfFire( AimPoint( target ), target );

	public string LastLineOfFireHit { get; private set; }

	// ---------------------------------------------------------------- AR2 energy balls

	[Property, Group( "Weapon" )] public int MaxBalls { get; set; } = 2;
	[Property, Group( "Weapon" )] public int StartBalls { get; set; } = 1;

	/// <summary>Seconds to charge one more ball.</summary>
	[Property, Group( "Weapon" )] public float BallRecharge { get; set; } = 100f;

	[Property, Group( "Sounds" )] public SoundEvent BallFireSound { get; set; }

	public int Balls { get; set; }
	private TimeSince _sinceBallCharge;

	private void RechargeBalls()
	{
		if ( ActiveWeapon != CombineWeaponType.Ar2 || Balls >= MaxBalls )
		{
			_sinceBallCharge = 0;
			return;
		}

		if ( _sinceBallCharge > BallRecharge )
		{
			_sinceBallCharge = 0;
			Balls++;
		}
	}

	/// <summary>Is this a good moment for an energy ball at that spot?</summary>
	public bool WantsBall( Vector3 position, GameObject target, bool visible )
	{
		if ( ActiveWeapon != CombineWeaponType.Ar2 || Balls <= 0 || HasGivenWeapon ) return false;
		if ( Squad is not null && !Squad.BallReady ) return false;

		var distance = WorldPosition.Distance( position );
		if ( distance < 220f || distance > 1500f ) return false;

		// It kills whatever it touches - never with a friend near the target or in the way
		foreach ( var npc in Scene.GetAllComponents<Npc>() )
		{
			if ( npc == this || npc.Health < 1f || GetDisposition( npc.GameObject ) == Disposition.Hostile ) continue;
			if ( npc.WorldPosition.Distance( position ) < 300f ) return false;
		}
		if ( CheckLineOfFire( position + Vector3.Up * 45f, target ) == LineOfFire.Friendly ) return false;
		var aimAt = position + Vector3.Up * 45f;
		var pass = CombineBall.ClosestPassToFriends( this, MuzzlePosition, aimAt - MuzzlePosition );
		if ( pass < 110f )
		{
			if ( CombineSquad.Debug ) Log.Info( $"[ball] {GameObject.Id.ToString()[..4]} holds the ball: a friend {pass:0} units from its path" );
			return false;
		}

		// The ball has to get there: a clear line, or at most a wall right next to the target to bounce off
		var path = Scene.Trace.Ray( MuzzlePosition, position + Vector3.Up * 45f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
			.Radius( CombineBall.Radius )
			.Run();
		if ( path.Hit && path.EndPosition.Distance( position ) > 300f ) return false;

		if ( visible )
		{
			// A clean straight line to the target: one ball and he's gone
			if ( !path.Hit ) return Game.Random.Float() < 0.6f;

			// Several enemies bunched together, or the fight is going badly, or just a player worth it
			var bunched = 0;
			foreach ( var other in Senses.VisibleTargets )
			{
				if ( other.IsValid() && other.WorldPosition.Distance( position ) < 300f ) bunched++;
			}

			if ( bunched >= 2 ) return true;
			if ( Squad is not null && Squad.Strength < 1f ) return Game.Random.Float() < 0.5f;
			return target.IsValid() && target.GetComponent<Player>().IsValid() && Game.Random.Float() < 0.2f;
		}

		// Hiding behind something close by: bounce it in there
		var tr = Scene.Trace.Ray( MuzzlePosition, position + Vector3.Up * 45f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
			.Run();
		return tr.Hit && tr.EndPosition.Distance( position ) < 300f && Game.Random.Float() < 0.4f;
	}

	/// <summary>Fire an energy ball. Careful (the AI): not when it would pass close to a friend.</summary>
	public void FireBall( Vector3 aim, bool careful = true )
	{
		if ( Balls <= 0 ) return;
		if ( CombineSquad.Debug ) Log.Info( $"[ball] {GameObject.Id.ToString()[..4]} fires a ball" );
		if ( careful && CombineBall.ClosestPassToFriends( this, MuzzlePosition, aim - MuzzlePosition ) < 110f ) return;
		Balls--;
		Squad?.OnBallFired();
		OtherBrainBallFired();

		var from = MuzzlePosition;
		CombineBall.Fire( this, from, aim - from );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void BallChargeEffects()
	{
		if ( BallFireSound is not null ) Sound.Play( BallFireSound, MuzzlePosition );
		PlayLocalAction( "shoot_ar2_alt", 1f, restart: true );
	}

	private TimeSince _sinceShotBlocked = 100f;

	/// <summary>Couldn't get a clean shot from here: the next plan should be a better spot.</summary>
	public void ShotBlocked() => _sinceShotBlocked = 0;
	public bool RecentlyBlocked => _sinceShotBlocked < 3f;

	/// <summary>A short step sideways or forward to a spot with a clean line of fire at the target.</summary>
	public Vector3? FindFiringPosition( GameObject target )
	{
		var aim = AimPoint( target );
		var forward = (target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
		var left = new Vector3( -forward.y, forward.x, 0f );

		foreach ( var offset in new[] { left * 80f, -left * 80f, left * 160f, -left * 160f, forward * 120f + left * 60f, forward * 120f - left * 60f } )
		{
			if ( Scene.NavMesh.GetClosestPoint( WorldPosition + offset ) is not { } point ) continue;
			if ( MathF.Abs( point.z - WorldPosition.z ) > 20f ) continue;

			var tr = Scene.Trace.Ray( point + Vector3.Up * 52f, aim )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "ragdoll", "playercontroller" )
				.UseHitboxes()
				.Run();

			if ( !tr.Hit || tr.GameObject?.Root == target.Root )
				return point;
		}

		return null;
	}

	public LineOfFire CheckLineOfFire( Vector3 to, GameObject target )
	{
		var tr = Scene.Trace.Ray( MuzzlePosition, to )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll", "playercontroller" )
			.UseHitboxes()
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() ) return LineOfFire.Clear;

		var root = tr.GameObject.Root;
		LastLineOfFireHit = $"{tr.GameObject.Name} / root {root.Name} / target root {(target.IsValid() ? target.Root.Name : "-")}";
		if ( target.IsValid() && root == target.Root ) return LineOfFire.Clear;

		if ( root.GetComponent<Npc>().IsValid() || root.GetComponent<Player>().IsValid() )
		{
			if ( GetDisposition( root ) == Disposition.Hostile ) return LineOfFire.Clear;
			// Our own manhacks dart about the target all the time: fire past them, not hold for them
			return root.Tags.Has( "manhack" ) ? LineOfFire.Clear : LineOfFire.Friendly;
		}

		// Loose physics junk (a dropped gun, a can) doesn't stop a bullet
		if ( tr.Body is { } body && body.MotionEnabled && body.Mass < 60f )
			return LineOfFire.Clear;

		// Shooting at someone: a wall is a wall, however close behind it he stands.
		// Shooting at a spot (suppression): hitting right next to it is the point.
		if ( target.IsValid() ) return LineOfFire.Blocked;
		return tr.EndPosition.Distance( to ) < 24f ? LineOfFire.Clear : LineOfFire.Blocked;
	}

	/// <summary>
	/// Where to put suppressing fire: the enemy's last spot if the gun reaches it, otherwise just
	/// over or beside their cover - where they'd have to show themselves. Null when there's
	/// nothing but solid wall to shoot at from here.
	/// </summary>
	public Vector3? FindSuppressionAim( Vector3 point ) => FindSuppressionAim( point, MuzzlePosition );

	public Vector3? FindSuppressionAim( Vector3 point, Vector3 from )
	{
		var forward = (point - from).WithZ( 0 ).Normal;
		var side = new Vector3( -forward.y, forward.x, 0f ) * (Game.Random.Int( 0, 1 ) == 0 ? 1f : -1f);
		var up = Vector3.Up * 30f;

		// Further out than the edge of his cover is just empty air - nobody there to keep down
		foreach ( var offset in new[] { Vector3.Zero, up, side * 50f, -side * 50f, up + side * 50f, up - side * 50f } )
		{
			if ( SuppressionReaches( from, point + offset ) )
				return point + offset;
		}

		return null;
	}

	/// <summary>
	/// Where to stand to suppress: right here if standing up is enough, otherwise a short step out
	/// from behind the wall or pillar he's using - so he fires round it, not through it.
	/// </summary>
	public Vector3? FindSuppressionSpot( Vector3 point )
	{
		var forward = (point - WorldPosition).WithZ( 0 ).Normal;
		var left = new Vector3( -forward.y, forward.x, 0f );

		foreach ( var offset in new[] { Vector3.Zero, left * 80f, -left * 80f, left * 160f, -left * 160f, forward * 120f + left * 60f, forward * 120f - left * 60f } )
		{
			if ( Scene.NavMesh.GetClosestPoint( WorldPosition + offset ) is not { } spot ) continue;
			if ( MathF.Abs( spot.z - WorldPosition.z ) > 20f ) continue;

			// Judged from about where his barrel will be once he faces that way, not from his chest
			var barrel = spot + Vector3.Up * 52f + (point - spot).WithZ( 0 ).Normal * 20f;
			if ( FindSuppressionAim( point, barrel ) is not null )
				return spot;
		}

		return null;
	}

	/// <summary>
	/// Is the spot he was seen at out of sight from here - behind something, the way the senses look
	/// (eyes to chest)? A spot in plain view with nobody seen on it is empty: nothing there to keep down.
	/// </summary>
	public bool SpotHidden( Vector3 seenPosition )
	{
		var tr = Scene.Trace.Ray( WorldPosition + Vector3.Up * 64f, seenPosition + Vector3.Up * 32f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
			.Run();

		return tr.Hit;
	}

	/// <summary>
	/// Does a shot from here get all the way to the spot? A wall in between is a wall being shot,
	/// even one right in front of where he's crouching.
	/// </summary>
	public bool SuppressionReaches( Vector3 from, Vector3 aim )
	{
		var tr = Scene.Trace.Ray( from, aim )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
			.Run();

		return !tr.Hit || from.Distance( tr.EndPosition ) > from.Distance( aim ) - 4f;
	}

	public bool TryShoot( GameObject target ) => TryShootAt( AimPoint( target ), 1f );

	/// <param name="target">Who the shot's for, when it's someone: a weapon handed to him aims at them itself.</param>
	public bool TryShootAt( Vector3 point, float spreadScale, GameObject target = null )
	{
		// A weapon handed to him (CombineNpc.GivenWeapon.cs) fires itself
		if ( HasGivenWeapon ) return FireGiven( point, target );
		if ( !_nextShot || Clip <= 0 ) return false;

		var gun = Gun;
		_nextShot = gun.FireInterval;
		Clip--;

		if ( CombineSquad.Debug && Clip % 5 == 0 )
			Log.Info( $"[shot] {GameObject.Id.ToString()[..4]} {gun.Name} at {WorldPosition.Distance( point ):0} units" );

		var from = MuzzlePosition;
		if ( CombineSquad.Debug && from.Distance( GunTip ) > 4f )
			Log.Info( $"[shot] {GameObject.Id.ToString()[..4]} barrel is through a wall, fires from this side of it" );

		var far = 1f + MathF.Max( 0f, from.Distance( point ) - 1000f ) / 6000f;
		var spread = gun.Spread * spreadScale * far * (Crouched ? 0.6f : 1f) * (MovingNow ? 1.6f : 1f);
		var forward = Rotation.LookAt( (point - from).Normal );

		for ( var i = 0; i < gun.Pellets; i++ )
		{
			var aim = forward * Rotation.From( Game.Random.Float( -spread, spread ), Game.Random.Float( -spread, spread ), 0f );

			var tr = Scene.Trace.Ray( new Ray( from, aim.Forward ), gun.Range * 1.5f )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithCollisionRules( "bullet" )
				.WithoutTags( "playercontroller" )
				.Radius( 1f )
				.UseHitboxes()
				.Run();

			// Combine bullets don't hurt other Combine on the same side (crossfire around a target) - the elite too
			var hitSoldier = tr.GameObject.IsValid() ? tr.GameObject.Root.GetComponent<Npc>() : null;
			var friendly = hitSoldier.IsValid() && hitSoldier != this && CombineSquad.Allied( this, hitSoldier );

			// ...and go on past a friendly manhack to whatever is behind it
			if ( friendly && hitSoldier.GameObject.Tags.Has( "manhack" ) )
			{
				tr = Scene.Trace.Ray( new Ray( from, aim.Forward ), gun.Range * 1.5f )
					.IgnoreGameObjectHierarchy( GameObject )
					.IgnoreGameObjectHierarchy( hitSoldier.GameObject )
					.WithCollisionRules( "bullet" )
					.WithoutTags( "playercontroller" )
					.Radius( 1f )
					.UseHitboxes()
					.Run();
				hitSoldier = tr.GameObject.IsValid() ? tr.GameObject.Root.GetComponent<Npc>() : null;
				friendly = hitSoldier.IsValid() && hitSoldier != this && CombineSquad.Allied( this, hitSoldier );
			}
			if ( !friendly )
				BaseCombatWeapon.ShootBullet( tr, gun.Damage, 200f, GameObject, GameObject );
			ShotEffects( from, tr.EndPosition, tr.Hit, i == 0 );
		}

		if ( _sinceGunshotStimulus > 1f )
		{
			_sinceGunshotStimulus = 0;
			EmitStimulus( StimulusKind.Gunshot, radius: 2048f, lifetime: 1.5f );
		}

		return true;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Unreliable )]
	private void ShotEffects( Vector3 from, Vector3 to, bool hit, bool first )
	{
		var gun = Gun;

		// The player playing as him sees the shot leave the gun in front of his view
		from = FirstPersonMuzzle ?? from;

		if ( first )
		{
			if ( FireSound is not null )
				Sound.Play( FireSound, from );

			CombineTracer.MuzzleFlash( from, (to - from).Normal, gun );

			// Rack the slide once the blast has settled
			if ( gun.PumpAction && ShotgunPumpSound is not null )
				Invoke( 0.35f, () => { if ( IsValid ) Sound.Play( ShotgunPumpSound, MuzzlePosition ); } );
			PlayLocalAction( Crouched ? gun.ShootCrouchAnim : gun.ShootAnim, 1f, restart: true );
		}

		CombineTracer.Create( from, to, gun.TracerColor );

		if ( hit )
		{
			var dir = (to - from).Normal;
			var tr = Scene.Trace.Ray( to - dir * 4f, to + dir * 4f )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "playercontroller" )
				.UseHitboxes()
				.Run();

			if ( tr.Hit ) BaseCombatWeapon.ImpactEffect( tr );
		}
	}

	// ---------------------------------------------------------------- rifle butt

	[Property, Group( "Balance" )] public float MeleeDamage { get; set; } = 15f;
	[Property, Group( "Balance" )] public float MeleeRange { get; set; } = 72f;

	private TimeSince _sinceMelee = 10f;

	/// <summary>Close enough to hit with the stock, roughly in front, and not straight after the last swing.</summary>
	public bool CanMelee( GameObject target )
	{
		if ( _sinceMelee < 1.2f || !target.IsValid() ) return false;
		var flat = (target.WorldPosition - WorldPosition).WithZ( 0 );
		if ( flat.Length > MeleeRange || MathF.Abs( target.WorldPosition.z - WorldPosition.z ) > 50f ) return false;
		return IsFacing( target.WorldPosition, 70f );
	}

	/// <summary>The moment the stock lands: hurts whoever is still right in front.</summary>
	public void MeleeHit( GameObject target )
	{
		_sinceMelee = 0;
		if ( !target.IsValid() || IsDeadTarget( target ) ) return;

		var flat = (target.WorldPosition - WorldPosition).WithZ( 0 );
		if ( flat.Length > MeleeRange + 16f || !IsFacing( target.WorldPosition, 80f ) ) return;

		var damage = new DamageInfo( MeleeDamage, GameObject, GameObject )
		{
			Position = AimPoint( target ),
			Origin = WorldPosition,
		};

		foreach ( var damageable in target.Root.Components.GetAll<Component.IDamageable>() )
			damageable.OnDamage( damage );

		if ( CombineSquad.Debug ) Log.Info( $"[melee] {GameObject.Id.ToString()[..4]} hits {target.Root.Name} for {MeleeDamage}" );

		// A shove away, as in HL2
		if ( target.Root.GetComponent<Rigidbody>() is { } body && body.MotionEnabled )
			body.Velocity += flat.Normal * 250f + Vector3.Up * 80f;

		if ( GearSound is not null ) Sound.Play( GearSound, target.WorldPosition + Vector3.Up * 40f );
	}

	public bool CanThrowGrenadeAt( Vector3 position )
	{
		if ( GrenadesLeft <= 0 || !_nextOwnGrenade ) return false;
		if ( Squad is not null && !Squad.GrenadeReady ) return false;

		// Far enough to be outside his own blast
		var distance = WorldPosition.Distance( position );
		if ( distance < GrenadeRadius + 60f || distance > 1100f ) return false;

		// Never into a friend's lap
		var safe = GrenadeRadius + 100f;
		foreach ( var npc in Scene.GetAllComponents<Npc>() )
		{
			if ( npc == this || npc.Health < 1f ) continue;
			if ( npc.WorldPosition.Distance( position ) > safe ) continue;
			if ( GetDisposition( npc.GameObject ) != Disposition.Hostile ) return false;
		}

		foreach ( var player in Scene.GetAllComponents<Player>() )
		{
			if ( player.WorldPosition.Distance( position ) > safe ) continue;
			if ( GetDisposition( player.GameObject ) != Disposition.Hostile ) return false;
		}

		return ThrowArcReaches( position, false ) || ThrowArcReaches( position, true );
	}

	private Vector3 ThrowStart => WorldPosition + Vector3.Up * 72f + WorldRotation.Forward * 12f;

	/// <summary>Follow the throw through the air: does it land near the target without hitting anything on the way?</summary>
	public bool ThrowArcReaches( Vector3 target, bool flat )
	{
		var start = ThrowStart;
		var velocity = BallisticVelocity( start, target, flat );
		var gravity = Scene.PhysicsWorld.Gravity;
		var previous = start;

		for ( var t = 0.05f; t < 3f; t += 0.05f )
		{
			var point = start + velocity * t + gravity * (0.5f * t * t);
			var tr = Scene.Trace.Ray( previous, point )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll", "grenade" )
				.Radius( 3f )
				.Run();

			if ( tr.Hit ) return tr.EndPosition.Distance( target ) < 120f;
			if ( point.z < target.z - 20f ) return point.WithZ( target.z ).Distance( target ) < 120f;
			previous = point;
		}

		return false;
	}

	public void ThrowGrenade( Vector3 target, bool flat = false )
	{
		// A lob that would hit the ceiling or a wall goes in low instead (and the other way round)
		if ( !ThrowArcReaches( target, flat ) && ThrowArcReaches( target, !flat ) )
			flat = !flat;

		var start = ThrowStart;
		var model = Model.Load( "hl2_combine_prisonguard/models/weapons/w_grenade.vmdl" );
		if ( model is null || model.IsError ) return;

		// The HL2 frag grenade: model, physics, fuse light
		var go = new GameObject( true, "combine grenade" );
		go.WorldPosition = start;
		go.WorldRotation = Rotation.Random;
		go.Tags.Add( "grenade" );
		go.AddComponent<ModelRenderer>().Model = model;
		go.AddComponent<ModelCollider>().Model = model;
		go.AddComponent<Rigidbody>();

		var explosive = go.GetOrAddComponent<TimedExplosive>();
		explosive.Lifetime = GrenadeFuse;
		explosive.Radius = GrenadeRadius;
		explosive.Damage = GrenadeDamage;
		explosive.Force = 1f;
		explosive.Attacker = GameObject;

		go.AddComponent<CombineGrenadeBeacon>().TickSound = GrenadeTickSound;

		var filter = go.AddComponent<PhysicsFilter>();
		filter.Body = GameObject;

		if ( go.GetComponent<Rigidbody>() is { } rb )
		{
			rb.Velocity = BallisticVelocity( start, target, flat );
			rb.AngularVelocity = Vector3.Random * 10f;
		}

		go.NetworkSpawn();

		GrenadesLeft--;
		_nextOwnGrenade = 8f;
		Squad?.OnGrenadeThrown();
		OtherBrainGrenadeThrown();
	}

	private Vector3 BallisticVelocity( Vector3 start, Vector3 target, bool low )
	{
		var gravity = Scene.PhysicsWorld.Gravity.z;
		var flat = (target - start).WithZ( 0 );
		var time = low ? Math.Clamp( flat.Length / 700f, 0.35f, 0.7f ) : Math.Clamp( flat.Length / 550f, 0.6f, 1.6f );
		var vertical = (target.z - start.z - 0.5f * gravity * time * time) / time;
		return flat / time + Vector3.Up * vertical;
	}

	public GameObject FindDangerousGrenade()
	{
		foreach ( var explosive in Scene.GetAllComponents<TimedExplosive>() )
		{
			if ( !explosive.IsValid() ) continue;
			if ( GrenadeLandsAt( explosive ).Distance( WorldPosition ) < explosive.Radius + 30f )
				return explosive.GameObject;
		}

		return null;
	}

	/// <summary>
	/// A grenade in flight is judged by where it comes down, not where it is right now -
	/// so nobody runs from the one he just threw at the enemy.
	/// </summary>
	private Vector3 GrenadeLandsAt( TimedExplosive explosive )
	{
		var position = explosive.WorldPosition;
		var body = explosive.GetComponent<Rigidbody>();
		if ( !body.IsValid() || body.Velocity.Length < 150f ) return position;

		var velocity = body.Velocity;
		var gravity = Scene.PhysicsWorld.Gravity;
		for ( var t = 0f; t < 2.5f; t += 0.1f )
		{
			velocity += gravity * 0.1f;
			var next = position + velocity * 0.1f;
			var tr = Scene.Trace.Ray( position, next )
				.IgnoreGameObjectHierarchy( explosive.GameObject )
				.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll", "grenade" )
				.Run();

			if ( tr.Hit ) return tr.EndPosition;
			position = next;
		}

		return position;
	}

	protected override NpcAwareness GatherAwareness()
	{
		var awareness = base.GatherAwareness();
		if ( FindDangerousGrenade().IsValid() )
			awareness |= GrenadeDanger;
		return awareness;
	}

	/// <summary>Is a crouched soldier at this point hidden from someone looking from threatEye?</summary>
	public bool IsCovered( Vector3 point, Vector3 threatEye )
	{
		var tr = Scene.Trace.Ray( point + Vector3.Up * 36f, threatEye )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "player", "npc", "playercontroller", "trigger", "ragdoll" )
			.Run();

		return tr.Hit && tr.EndPosition.Distance( threatEye ) > 40f;
	}

	public Vector3? FindCover( Vector3 threat, float maxDistance = 650f )
	{
		var threatEye = threat + Vector3.Up * 60f;

		if ( IsCovered( WorldPosition, threatEye ) && Squad?.IsCoverTaken( this, WorldPosition ) != true )
			return WorldPosition;

		Vector3? best = null;
		var bestScore = float.MaxValue;

		for ( var ring = 0; ring < 3; ring++ )
		{
			var radius = 140f + ring * (maxDistance - 140f) / 2f;

			for ( var i = 0; i < 12; i++ )
			{
				var candidate = WorldPosition + Rotation.FromYaw( i * 30f + ring * 15f ).Forward * radius;
				if ( Scene.NavMesh.GetClosestPoint( candidate ) is not { } point ) continue;
				if ( MathF.Abs( point.z - WorldPosition.z ) > 20f ) continue;
				if ( point.Distance( threat ) < 250f ) continue;
				if ( Squad?.IsCoverTaken( this, point ) == true ) continue;
				if ( !IsCovered( point, threatEye ) ) continue;

				// Close by, and not toward the enemy
				var score = point.Distance( WorldPosition ) - point.Distance( threat ) * 0.3f;
				if ( score >= bestScore ) continue;

				bestScore = score;
				best = point;
			}
		}

		return best;
	}

	/// <summary>A point off to the side of the enemy that still has a view of them.</summary>
	public Vector3? FindFlankPosition( Vector3 targetPosition, GameObject target, out float side )
	{
		var toTarget = (targetPosition - WorldPosition).WithZ( 0 );
		var distance = toTarget.Length;
		var forward = toTarget.Normal;
		var left = new Vector3( -forward.y, forward.x, 0f );
		var preferred = Game.Random.Float() < 0.5f ? 1f : -1f;

		for ( var attempt = 0; attempt < 6; attempt++ )
		{
			side = attempt % 2 == 0 ? preferred : -preferred;
			var candidate = WorldPosition
				+ left * side * Game.Random.Float( 300f, 600f )
				+ forward * Game.Random.Float( 0f, MathF.Min( 300f, distance * 0.4f ) );

			if ( Scene.NavMesh.GetClosestPoint( candidate ) is not { } point ) continue;
			if ( point.Distance( targetPosition ) < 200f ) continue;

			var look = target.IsValid() ? AimPoint( target ) : targetPosition + Vector3.Up * 50f;
			var tr = Scene.Trace.Ray( point + Vector3.Up * 64f, look )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "ragdoll", "playercontroller" )
				.UseHitboxes()
				.Run();

			if ( !tr.Hit || target.IsValid() && tr.GameObject?.Root == target.Root || tr.EndPosition.Distance( look ) < 40f )
				return point;
		}

		side = 0f;
		return null;
	}

	/// <summary>A short sidestep while keeping the gun on the target.</summary>
	public Vector3 StrafePosition( Vector3 target )
	{
		var forward = (target - WorldPosition).WithZ( 0 ).Normal;
		var left = new Vector3( -forward.y, forward.x, 0f );
		var side = Game.Random.Float() < 0.5f ? 1f : -1f;
		var candidate = WorldPosition + left * side * Game.Random.Float( 80f, 180f );
		return Scene.NavMesh.GetClosestPoint( candidate ) ?? candidate;
	}

	public Vector3 RetreatPosition( Vector3 threat, float distance = 350f )
	{
		var away = (WorldPosition - threat).WithZ( 0 );
		if ( away.LengthSquared < 1f ) away = Vector3.Random.WithZ( 0 );

		for ( var attempt = 0; attempt < 5; attempt++ )
		{
			var direction = Rotation.FromYaw( Game.Random.Float( -50f, 50f ) ) * away.Normal;
			var candidate = WorldPosition + direction * distance;
			if ( Scene.NavMesh.GetClosestPoint( candidate ) is { } point && point.Distance( threat ) > WorldPosition.Distance( threat ) )
				return point;
		}

		return WorldPosition + away.Normal * distance;
	}

	/// <summary>A covered spot that's clearly closer to the enemy than where we stand now.</summary>
	public Vector3? FindAdvanceCover( Vector3 threat )
	{
		var threatEye = threat + Vector3.Up * 60f;
		var current = WorldPosition.Distance( threat );

		Vector3? best = null;
		var bestScore = float.MaxValue;

		for ( var ring = 0; ring < 2; ring++ )
		{
			// Long legs: the next cover well forward, not a hop away
			var radius = 300f + ring * 250f;
			for ( var i = 0; i < 12; i++ )
			{
				var candidate = WorldPosition + Rotation.FromYaw( i * 30f + ring * 15f ).Forward * radius;
				if ( Scene.NavMesh.GetClosestPoint( candidate ) is not { } point ) continue;
				if ( MathF.Abs( point.z - WorldPosition.z ) > 20f ) continue;

				var toThreat = point.Distance( threat );
				if ( toThreat > current - 180f || toThreat < 220f ) continue;
				if ( Squad?.IsCoverTaken( this, point ) == true ) continue;
				if ( !IsCovered( point, threatEye ) ) continue;

				var score = toThreat + point.Distance( WorldPosition ) * 0.5f;
				if ( score >= bestScore ) continue;

				bestScore = score;
				best = point;
			}
		}

		return best;
	}

	private bool _prefersCrouch;
	private TimeSince _sinceCrouchChoice = 100f;

	/// <summary>Kneel or stand to shoot - decided now and then, not every burst.</summary>
	public bool WantsCrouch( float distance )
	{
		if ( _sinceCrouchChoice > 8f )
		{
			_sinceCrouchChoice = 0;
			_prefersCrouch = Game.Random.Float() < 0.5f;
		}

		return _prefersCrouch && distance > 350f;
	}

	private Vector3? _coverSpot;
	private TimeSince _sinceCoverSpot;
	private Vector3? _searchPoint;

	/// <summary>Already behind something good and haven't been here long: no need to move yet.</summary>
	public bool ShouldStayInCover( Vector3 threat )
	{
		if ( !IsCovered( WorldPosition, threat + Vector3.Up * 60f ) )
		{
			_coverSpot = null;
			return false;
		}

		if ( _coverSpot is not { } spot || spot.Distance( WorldPosition ) > 40f )
		{
			_coverSpot = WorldPosition;
			_sinceCoverSpot = 0;
		}

		return _sinceCoverSpot < Game.Random.Float( 1f, 2f );
	}

	public Vector3 CurrentSearchPoint()
	{
		// Got stuck on the way to it: pick another
		if ( SinceMoveStuck < 3f ) _searchPoint = null;
		_searchPoint ??= Squad?.SearchPoint( this, Scene ) ?? EnemyPosition;
		return _searchPoint.Value;
	}

	public void ClearSearchPoint() => _searchPoint = null;

	/// <summary>
	/// Who to keep facing while moving to <paramref name="destination"/>: the enemy, if he's in
	/// sight and the move is short - so the soldier backs off or sidesteps with his gun up
	/// instead of turning his back. Long runs are made facing forward, like in HL2.
	/// </summary>
	public GameObject FaceWhileMoving( Vector3 destination )
	{
		var enemy = Senses.GetBestTarget();
		if ( !enemy.IsValid() )
		{
			var known = Squad?.KnownEnemy( this );
			if ( known is null || known.SinceSeen > 2f ) return null;
			enemy = known.Target;
		}

		if ( !enemy.IsValid() ) return null;
		return WorldPosition.Distance( destination ) < 450f ? enemy : null;
	}

	/// <summary>Move in a fight: backing off or sidestepping is slower than a straight run.</summary>
	public Sandbox.Npcs.Tasks.MoveTo CombatMove( Vector3 destination, float stopDistance )
	{
		var face = FaceWhileMoving( destination );
		return new CombineMoveTo( destination, stopDistance )
		{
			Speed = face.IsValid() ? RunSpeed * 0.7f : RunSpeed,
			FaceTarget = face,
		};
	}

	/// <summary>Kneeling here blocks the shot: stand to fire for a while.</summary>
	public void StandToShoot()
	{
		_prefersCrouch = false;
		_sinceCrouchChoice = 0;
	}

	public void Reloaded()
	{
		Clip = ClipSize;
		_sinceReloaded = 0;
	}

	private TimeSince _sinceReloaded = 100f;

	/// <summary>Empty, or below this share of the magazine - but not straight after the last reload.</summary>
	public bool WantsReload( float fraction ) => Clip <= 0 || Clip < ClipSize * fraction && _sinceReloaded > 6f;

	/// <summary>Patrols walk; combat movement runs once the destination is a few steps away.</summary>
	public void SetRelaxed( bool relaxed )
	{
		Navigation.RunDistance = relaxed ? 100000f : 220f;
		Navigation.FaceMovementDirection = true;

		// On patrol the body follows the path closely, so the easy walk never goes sideways for long
		Navigation.TurnSpeed = relaxed ? 3.5f : 2.5f;
	}
}
