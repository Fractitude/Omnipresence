using System;
using System.Linq;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Metrocop;

public sealed partial class CombineNpc
{
	public bool PlayerTooClose => _playerTooClose;
	public bool RestOver => _restUntil;

	// ---------------------------------------------------------------- the baton (weapon_stunstick.cpp)

	/// <summary>
	/// The swing lands (frame 13): a box 32 forward from the hand - stretched down to catch low
	/// things - aimed at the enemy if he's roughly in front. NPCs take 40; a player in the suit a
	/// quarter of that, and is thrown back 500, his screen flashed red, made to drop what he holds.
	/// </summary>
	/// <param name="stepped">The swing he stepped into (CopSwingTask): it reaches a little further.</param>
	public void BatonHit( GameObject aimedAt, bool stepped = false )
	{
		// At the enemy if he's roughly in front - level, not tilted down at him as HL2 does: s&box bodies
		// stand further apart than HL2's, and the tilt took just enough off the reach to miss. The box
		// reaches 40 down, so low things are still caught.
		var dir = WorldRotation.Forward.WithZ( 0 ).Normal;
		if ( aimedAt.IsValid() )
		{
			var to = (aimedAt.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
			if ( Vector3.Dot( to, dir ) > 0.5f ) dir = to;
		}

		// Swept from a little behind him: a sweep that starts already inside someone pressed right up
		// against him doesn't see that someone. The reach in front is HL2's 32 all the same.
		var start = WorldPosition + Vector3.Up * 55f;
		var hit = MeleeTrace( start - dir * 24f, start + dir * (stepped ? 52f : 32f), new BBox( new Vector3( -16, -16, -40 ), new Vector3( 16, 16, 16 ) ) );
		if ( hit is null )
		{
			PlayAt( BatonSwingSound, start );
			return;
		}

		PlayAt( BatonHitSound, hit.WorldPosition + Vector3.Up * 40f );

		var player = hit.GetComponent<PlayerController>();
		var criminal = player is null || PlayerIsCriminal( hit );
		if ( criminal ) Hurt( hit, player is not null ? BatonDamageNpc * SuitScale : BatonDamageNpc, start );

		if ( player is not null )
		{
			StunnedTarget( hit );
			var away = (hit.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
			if ( player.GroundObject?.Root == GameObject ) away = dir.WithZ( 0 ).Normal;
			Push( player, away * 500f );
			ScreenFlash( player, new Color( 0.5f, 0f, 0f, 0.5f ) );
			ForceDrop( player );
		}
	}

	/// <summary>The shove (frame 11 of pushplayer): a short box forward; a criminal takes a little damage.</summary>
	public void Shove( GameObject aimedAt )
	{
		var start = WorldPosition + Vector3.Up * 36f;
		var ahead = WorldRotation.Forward.WithZ( 0 ).Normal;
		var hit = MeleeTrace( start - ahead * 24f, start + ahead * 16f, new BBox( new Vector3( -16 ), new Vector3( 16 ) ) );
		if ( hit is null ) return;

		var player = hit.GetComponent<PlayerController>();
		if ( player is not null )
		{
			if ( PlayerIsCriminal( hit ) ) Hurt( hit, ShoveDamage * SuitScale, start );
			var forward = (hit.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
			Push( player, forward * 250f );
			ForceDrop( player );
		}
		else if ( GetDisposition( hit ) == Disposition.Hostile ) Hurt( hit, ShoveDamage, start );

		PlayAt( ShoveSound, hit.WorldPosition + Vector3.Up * 40f );
	}

	/// <summary>
	/// HL2's CTraceFilterMetroPolice: the first player, NPC he hates, or breakable thing in the box.
	/// Other NPCs (friends, the neutral) are passed over; loose things in the way just get hit.
	/// </summary>
	private GameObject MeleeTrace( Vector3 from, Vector3 to, BBox box )
	{
		var hits = Scene.Trace.Box( box, from, to )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll" )
			.RunAll();

		foreach ( var tr in hits )
		{
			if ( !tr.GameObject.IsValid() ) continue;
			var root = tr.GameObject.Root;
			if ( root.GetComponent<PlayerController>().IsValid() ) return root;
			if ( root.GetComponent<Npc>().IsValid() )
			{
				if ( GetDisposition( root ) == Disposition.Hostile ) return root;
				continue;
			}

			if ( root.GetComponent<Prop>() is { } prop )
			{
				Hurt( root, BatonDamageNpc, from );
				if ( prop.Health > 0f ) return root;
			}
		}

		return null;
	}

	private void Hurt( GameObject victim, float amount, Vector3 from )
	{
		if ( !victim.IsValid() || amount <= 0f ) return;
		var damage = new DamageInfo( amount, GameObject, GameObject )
		{
			Position = victim.WorldPosition + Vector3.Up * 40f,
			Origin = from,
		};

		foreach ( var damageable in victim.Root.Components.GetAll<Component.IDamageable>() )
			damageable.OnDamage( damage );
	}

	private static void Push( PlayerController player, Vector3 velocity )
	{
		// Not airborne: "if not on ground, then don't make them fly"
		if ( !player.IsOnGround ) velocity = velocity.WithZ( 0 );
		if ( player.GameObject.Root.GetComponent<Rigidbody>() is { } body ) body.Velocity += velocity;
	}

	/// <summary>Make the player let go of whatever his physgun holds (ForceDropOfCarriedPhysObjects).</summary>
	private static void ForceDrop( PlayerController player )
	{
		// The physgun's grab is the holder's to change: this reaches the host's own player, not a joined one's
		foreach ( var gun in player.GameObject.Root.GetComponentsInChildren<Physgun>( true ) )
		{
			if ( !gun.IsProxy && gun._state.Active ) gun._state = default;
		}
	}

	private void ScreenFlash( PlayerController player, Color color )
	{
		var owner = player.Network.Owner;
		if ( owner is null )
		{
			ScreenFlashRpc( color );
			return;
		}

		using ( Rpc.FilterInclude( owner ) )
			ScreenFlashRpc( color );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void ScreenFlashRpc( Color color )
	{
		var go = new GameObject( true, "metrocop flash" ) { NetworkMode = NetworkMode.Never };
		go.AddComponent<MetrocopScreenFlash>().Color = color;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void PlayAtRpc( SoundEvent sound, Vector3 position )
	{
		if ( sound is not null ) Sound.Play( sound, position );
	}

	private void PlayAt( SoundEvent sound, Vector3 position )
	{
		if ( sound is not null ) PlayAtRpc( sound, position );
	}

	// ---------------------------------------------------------------- burning, losing him

	/// <summary>TASK_METROPOLICE_DIE_INSTANTLY with DMG_BURN.</summary>
	public void BurnToDeath()
	{
		if ( !IsAlive ) return;
		var damage = new DamageInfo( Health + 1f, GameObject, null ) { Position = WorldPosition + Vector3.Up * 40f, Origin = WorldPosition };
		((Component.IDamageable)this).OnDamage( damage );
	}

	/// <summary>Got to where he was and he isn't there: the trail's gone cold till he's seen again.</summary>
	public void CopLostAt( GameObject enemy )
	{
		if ( !enemy.IsValid() || _copEluded.Contains( enemy ) ) return;
		_copEluded.Add( enemy );
		var longAgo = Squad?.Enemies.TryGetValue( enemy, out var record ) == true && record.SinceSeen > 10f;
		MetrocopVoice.LostEnemy( this, longAgo );
	}

	// ---------------------------------------------------------------- shooting on the move

	private TimeUntil _nextMoveShot;

	/// <summary>
	/// HL2's move-and-shoot: on schedules that allow it, a cop holding an attack turn fires at the
	/// enemy he can see while running - backing off to reload included ("run backwards + shoot").
	/// </summary>
	private void TickMoveAndShoot()
	{
		if ( ActiveSchedule is not CopSchedule { MoveAndShoot: true } || !Navigation.IsMoving ) return;
		if ( !HasGun || !WeaponDrawn || Clip <= 0 || !CanFireNow || ArrestEnemy ) return;

		var enemy = CopEnemy;
		if ( !enemy.IsValid() || !CanSee( enemy ) || !IsFacing( enemy.WorldPosition, 50f ) ) return;
		if ( WorldPosition.Distance( enemy.WorldPosition ) > EngageRange ) return;
		if ( !CopOccupy( enemy, CopSlot.Attack1, CopSlot.Attack2 ) ) return;

		if ( _burstLeft <= 0 && !RestOver ) return;
		EnsureBurst( enemy );

		if ( !_nextMoveShot ) return;
		var aim = AimPoint( enemy );
		if ( CheckLineOfFire( aim, enemy ) != LineOfFire.Clear ) return;
		if ( !TryShootAt( aim, 1f, enemy ) ) return;

		_nextMoveShot = BurstInterval;
		BurstShotFired();
	}
}
