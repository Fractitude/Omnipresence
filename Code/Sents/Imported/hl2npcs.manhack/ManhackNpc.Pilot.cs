using System;
using Sandbox;

namespace Hl2Manhack;

/// <summary>
/// A player flying the manhack (Warbox's "Play as", its NPC_COMPATIBILITY.md): found by these property
/// names and set on the host. While controlled it picks no targets, keeps no attack slot, circles and
/// wanders nowhere - it flies where the player steers, and the attack key is HL2's attack run toward
/// what the player aims at. The blades still only cut what it's hostile to.
/// </summary>
public sealed partial class ManhackNpc
{
	private const float PilotSpeed = 250f;
	private const float PilotFastSpeed = 400f;
	private const float PilotClimb = 220f;
	private const float PilotBurstRest = 0.6f;

	private bool _controlled;

	/// <summary>True while a player flies it.</summary>
	public bool WarboxControlled
	{
		get => _controlled;
		set
		{
			if ( IsProxy || _controlled == value ) return;
			_controlled = value;

			// Whatever it was doing on its own stops; back on its own, it stays round where it was left
			_target = null;
			_wander = null;
			_path.Clear();
			ReleaseSlot();
			StopBurst();
			Attacking = false;
			_home = WorldPosition;
		}
	}

	/// <summary>Where to fly, flat, world space, 0 to 1 long.</summary>
	public Vector3 WarboxMove { get; set; }
	public Vector3 WarboxAim { get; set; }

	/// <summary>The attack run toward the aim.</summary>
	public bool WarboxFire { get; set; }

	/// <summary>Brake.</summary>
	public bool WarboxFire2 { get; set; }
	public bool WarboxJump { get; set; }
	public bool WarboxDuck { get; set; }
	public bool WarboxRun { get; set; }

	/// <summary>
	/// The camera: behind it and a little above, looking past it - from inside the body all you'd see is
	/// the inside of the model. Read on the player's own screen, so it trails behind wherever that camera
	/// looks, and stops short of walls.
	/// </summary>
	public Transform WarboxEyes
	{
		get
		{
			var camera = Scene?.Camera;
			var forward = camera.IsValid() ? camera.WorldRotation.Forward : WorldRotation.Forward;
			var pos = WorldPosition;
			var wanted = pos - forward * 44f + Vector3.Up * 14f;
			var tr = Scene.Trace.Sphere( 4f, pos, wanted ).IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "manhack", "player", "npc", "debris" ).Run();
			return new Transform( tr.Hit ? tr.EndPosition : wanted, Rotation.LookAt( forward ) );
		}
	}

	/// <summary>The player's flying, each physics tick on the host (in place of seeing, choosing and flying itself).</summary>
	private void PilotFly( float dt )
	{
		var pos = WorldPosition;
		var v = _rb.Velocity;
		var toAim = WarboxAim - pos;
		var aim = toAim.LengthSquared > 1f ? toAim.Normal : WorldRotation.Forward;

		// The attack run, held down for one after another: panels open, straight at the aim
		if ( WarboxFire && _nextBurst && _burstEnds && _stall )
		{
			_burstDir = aim;
			_burstEnds = 1f;
			_nextBurst = PilotBurstRest;
			ShowHostile( true );
			Eye = EyeLook.Charge;
		}

		if ( !_stall )
		{
			// Stalled (hit, batted away): no steering, it just coasts
		}
		else if ( !_burstEnds )
		{
			// Bends toward the aim as it goes, as the AI's run bends toward the enemy's eyes
			_burstDir = (_burstDir + aim * 4f * dt).Normal;
			v = MoveInDirection( v, _burstDir, 400f, 500f, dt );
		}
		else
		{
			var wish = WarboxMove.WithZ( 0f ).ClampLength( 1f ) * (WarboxRun ? PilotFastSpeed : PilotSpeed);
			wish.z = ((WarboxJump ? 1f : 0f) - (WarboxDuck ? 1f : 0f)) * PilotClimb;
			v = Vector3.Lerp( v, wish, 1f - MathF.Pow( 0.03f, dt ) );
			if ( WarboxFire2 ) v *= MathF.Pow( 0.02f, dt );
		}

		// Never bolted in place: the little bob
		v.z += 2f * MathF.Sin( _noiseMod * Time.Now + _noiseMod ) * dt * 10f;

		v = KeepOffGround( pos, v );
		v = KeepApart( pos, v );
		_rb.Velocity = v.ClampLength( MaxSpeed );
		EnemyDistance = 0f;

		Face( aim, dt );
		if ( _burstEnds && _hostile ) ShowHostile( false );
		PlayEngineNoise();
	}
}
