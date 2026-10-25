using System;
using System.Collections.Generic;
using Sandbox;

namespace Hl2CombineElite;

/// <summary>
/// Animation is plain sequences, no anim graph. Locomotion is picked from the synced move
/// velocity on every client; one-shot actions (shooting, reloads, signals, flinches) are
/// broadcast by the host.
/// </summary>
public sealed partial class CombineNpc
{
	// Ground speed of each locomotion sequence at playback rate 1 (measured from the HL2 animations)
	private static readonly Dictionary<string, float> StrideSpeeds = new()
	{
		["walk"] = 71.8f, ["walk_easy"] = 77.9f, ["run"] = 214.8f, ["crouch_walk"] = 94.1f, ["crouch_run"] = 155.0f,
		["run_ne"] = 192.7f, ["run_e"] = 160.6f, ["run_se"] = 181.1f, ["run_s"] = 183.4f, ["run_sw"] = 186.5f, ["run_w"] = 161.7f, ["run_nw"] = 204.1f,
		["walk_ne"] = 62.4f, ["walk_e"] = 54.6f, ["walk_se"] = 53.2f, ["walk_s"] = 64.4f, ["walk_sw"] = 54.1f, ["walk_w"] = 61.3f, ["walk_nw"] = 71.7f,
		["crouch_walk_ne"] = 100.6f, ["crouch_walk_e"] = 73.6f, ["crouch_walk_se"] = 75.8f, ["crouch_walk_s"] = 72.7f,
		["crouch_walk_sw"] = 66.6f, ["crouch_walk_w"] = 84.8f, ["crouch_walk_nw"] = 76.2f,
		["walk_easy_ne"] = 62.4f, ["walk_easy_e"] = 54.6f, ["walk_easy_se"] = 59.1f, ["walk_easy_s"] = 64.4f, ["walk_easy_sw"] = 60.1f, ["walk_easy_w"] = 61.3f, ["walk_easy_nw"] = 71.7f,
	};

	// How long the aim is held after a burst before relaxing to the idle stance
	private const float AimHold = 0.8f;

	private string _sequence;
	private TimeSince _sequenceSwitched = 10f;
	private string _action;
	private bool _actionCrouched;
	private bool _leavingAction;
	private TimeSince _sinceStill;
	private float _actionDuration;
	private float _actionRate = 1f;
	private TimeSince _actionStarted;
	private float _speed;
	private bool _runAnim;
	private bool _leftFootUp, _rightFootUp;
	private int? _moveSector;

	public bool HasAction => _action is not null && _actionStarted < _actionDuration;
	public bool CurrentActionIsFlinch => _action?.StartsWith( "flinch" ) == true;
	public bool CurrentActionIsShooting => _action?.StartsWith( "shoot" ) == true;

	/// <summary>Host: play a one-shot sequence everywhere. Returns its length in seconds.</summary>
	public float PlayAction( string name, float rate = 1f )
	{
		PlayActionRpc( name, rate );
		return _actionDuration;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void PlayActionRpc( string name, float rate )
	{
		PlayLocalAction( name, rate, restart: true );
	}

	/// <summary>Host: cut a one-shot short everywhere (a reload broken off, say).</summary>
	public void StopAction( string name ) => StopActionRpc( name );

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void StopActionRpc( string name )
	{
		if ( _action != name ) return;
		_action = null;
		_leavingAction = true;
	}

	private void PlayLocalAction( string name, float rate, bool restart )
	{
		if ( !Renderer.IsValid() ) return;

		var shooting = name.StartsWith( "shoot" );

		// A shot doesn't cut a reload or a grenade throw short
		if ( HasAction && shooting && !CurrentActionIsShooting ) return;

		// Keep one shooting loop going through a burst instead of restarting it every shot
		var single = shooting && (Gun.PumpAction || Gun.SingleShotAnim);
		if ( shooting && !single && _action == name && HasAction )
		{
			_actionDuration = MathF.Max( _actionDuration, _actionStarted + Gun.FireInterval + 0.2f );
			return;
		}

		_action = name;
		_actionCrouched = Crouched;
		_actionRate = rate;
		_actionStarted = 0;
		SetSequence( name, shooting && !single, rate, restart, instant: shooting );

		var duration = Renderer.Sequence.Duration;
		_actionDuration = (duration > 0f ? duration : 1f) / rate;
		if ( shooting ) _actionDuration = MathF.Max( _actionDuration, Gun.FireInterval + 0.2f );
	}

	private void SetSequence( string name, bool loop, float rate, bool force = false, bool instant = false )
	{
		Renderer.UseAnimGraph = false;
		Renderer.PlaybackRate = rate;
		OtherAnimName( ref name );

		if ( !force && _sequence == name ) return;
		var startOrStop = _sequence is not null && (_sequence.Contains( "idle" ) != name.Contains( "idle" ));
		if ( !force && !startOrStop && !_leavingAction && _sequenceSwitched < 0.25f ) return;
		_leavingAction = false;

		_sequence = name;
		_sequenceSwitched = 0;
		// Always blend; how fast is set per animation in the model (shots blend in almost at once)
		Renderer.Sequence.Blending = true;
		Renderer.Sequence.Name = name;
		Renderer.Sequence.Looping = loop;
		if ( force ) Renderer.Sequence.Time = 0f;
	}

	private CombineWeaponType? _shownWeapon;

	/// <summary>Log what every soldier is doing five times a second (movement debugging).</summary>
	[ConVar( "combine_elite_trace" )]
	public static bool Trace { get; set; }

	private TimeSince _sinceTrace;
	private static readonly System.Text.StringBuilder TraceBuffer = new();
	private static TimeSince _sinceTraceFlush;

	[ConCmd( "combine_elite_trace_clear" )]
	public static void ClearTrace() => TraceBuffer.Clear();

	private void TraceState()
	{
		if ( !Trace || IsProxy || _sinceTrace < 0.2f ) return;
		_sinceTrace = 0;

		var velocity = Animation.IsValid() ? Animation.MoveVelocity.WithZ( 0 ) : Vector3.Zero;
		var local = WorldRotation.Inverse * velocity;
		var moveYaw = velocity.Length > 5f ? MathF.Atan2( local.y, local.x ).RadianToDegree() : 0f;
		var schedule = ActiveSchedule?.GetDebugString() ?? "-";
		TraceBuffer.AppendLine( $"TR {GameObject.Id.ToString()[..4]} t={Time.Now:F1} p={WorldPosition.x:F0},{WorldPosition.y:F0} yaw={WorldRotation.Yaw():F0} v={velocity.Length:F0} my={moveYaw:F0} seq={Renderer.Sequence.Name} r={Renderer.PlaybackRate:F2} c={(Crouched ? 1 : 0)} cl={Clip} a={_action ?? "-"} s={schedule} sq={Squad?.Posture}" );

		// Static, so it outlives a play restart while the clock starts over
		if ( _sinceTraceFlush > 1f || _sinceTraceFlush < 0f )
		{
			_sinceTraceFlush = 0;
			if ( TraceBuffer.Length > 400000 ) TraceBuffer.Remove( 0, TraceBuffer.Length - 300000 );
			try
			{
				FileSystem.Data.WriteAllText( "combine_trace.txt", TraceBuffer.ToString() );
			}
			catch ( Exception e )
			{
				Log.Warning( $"combine_trace: can't write ({e.Message})" );
			}
		}
	}

	private void UpdateAnimation()
	{
		if ( !Renderer.IsValid() ) return;
		TraceState();

		if ( _shownWeapon != ActiveWeapon )
		{
			_shownWeapon = ActiveWeapon;
			Renderer.SetBodyGroup( "weapon", Gun.BodyGroup );

			// HL2's shotgunners wear their own colours (skin 1 of combine_soldier.mdl)
			Renderer.MaterialGroup = ActiveWeapon == CombineWeaponType.Shotgun ? "shotgun" : "default";
		}

		OtherAnimationUpdate();

		var velocity = Animation.IsValid() ? Animation.MoveVelocity.WithZ( 0 ) : Vector3.Zero;
		_speed = MathX.Lerp( _speed, velocity.Length, 0.3f );

		// Run above 60% of his run speed, walk below 50% (the soldier's 210: 125 and 105, as before) - so a
		// slower runner backing off at 70% of his speed (the Metrocop) isn't caught on the switch
		if ( _speed > RunSpeed * 0.6f ) _runAnim = true;
		else if ( _speed < RunSpeed * 0.5f ) _runAnim = false;

		UpdateFootsteps();

		if ( velocity.Length < 20f ) _sinceStill = 0;

		// Any one-shot (a shot, a reload, a flinch...) gives way the moment he sets off - not just
		// fast running - instead of sliding along in it. Still coasting to a stop when it began
		// doesn't count: that's how a reload in cover starts.
		if ( _action is not null && velocity.Length > 30f && (float)_sinceStill < (float)_actionStarted )
		{
			_action = null;
			_leavingAction = true;
		}

		if ( _action is not null )
		{
			if ( _actionStarted < _actionDuration )
			{
				Renderer.PlaybackRate = _actionRate;
				return;
			}

			// Between bursts the gun stays up on target, as in HL2, instead of dropping to idle and back
			if ( CurrentActionIsShooting && _speed < 12f && Crouched == _actionCrouched && _actionStarted < _actionDuration + AimHold )
			{
				Renderer.PlaybackRate = 0f;
				return;
			}

			_action = null;
		}

		if ( _speed < 12f )
		{
			SetSequence( Crouched ? "crouch_idle" : Alert ? Gun.CombatIdle : Gun.RelaxedIdle, true, 1f );
			return;
		}

		// Pick the direction of travel relative to where the body faces, like HL2's move_yaw
		var local = WorldRotation.Inverse * velocity;
		var yaw = MathF.Atan2( local.y, local.x ).RadianToDegree();
		var sector = (int)MathF.Round( yaw / 45f );
		if ( _moveSector is { } previous && MathF.Abs( Angles.NormalizeAngle( yaw - previous * 45f ) ) < 32f )
			sector = previous;
		_moveSector = sector;

		var suffix = sector switch
		{
			0 => "",
			1 => "_nw",
			2 => "_w",
			3 => "_sw",
			-1 => "_ne",
			-2 => "_e",
			-3 => "_se",
			_ => "_s",
		};

		string name;
		if ( Crouched )
			name = suffix == "" && _runAnim ? "crouch_run" : "crouch_walk" + suffix;
		else if ( !Alert )
			// Relaxed: HL2's easy walk in every direction, gun down - the combat walks lean forward
			name = _runAnim ? "run" : "walk_easy" + suffix;
		else if ( _runAnim )
			name = "run" + suffix;
		else
			name = "walk" + suffix;

		var stride = StrideSpeeds.GetValueOrDefault( name, 100f );
		OtherStrideSpeed( name, ref stride );
		SetSequence( name, true, Math.Clamp( _speed / stride, 0.6f, 1.35f ) );
	}

	private void UpdateFootsteps()
	{
		if ( _speed < 20f || Renderer.Model is null ) return;

		CheckFoot( "ValveBiped_Bip01_L_Foot", ref _leftFootUp, 0 );
		CheckFoot( "ValveBiped_Bip01_R_Foot", ref _rightFootUp, 1 );
	}

	private void CheckFoot( string boneName, ref bool wasUp, int foot )
	{
		if ( !Renderer.TryGetBoneTransform( boneName, out var t ) ) return;

		var height = t.Position.z - WorldPosition.z;
		if ( height > 8f )
		{
			wasUp = true;
			return;
		}

		if ( wasUp && height < 6f )
		{
			wasUp = false;
			PlayFootstep( t.Position, foot );
		}
	}

	private void PlayFootstep( Vector3 position, int foot )
	{
		var trace = Scene.Trace.Ray( position + Vector3.Up * 20f, position - Vector3.Up * 20f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( trace.Hit && trace.Surface is not null )
		{
			var step = foot == 0 ? trace.Surface.SoundCollection.FootLeft : trace.Surface.SoundCollection.FootRight;
			if ( step is not null )
			{
				var handle = Sound.Play( step, position );
				if ( handle.IsValid() ) handle.Volume *= _runAnim ? 1f : 0.6f;
			}
		}

		// The rattle of webbing and armour, the HL2 soldier's signature footstep
		if ( _runAnim && GearSound is not null )
			Sound.Play( GearSound, position + Vector3.Up * 40f );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void PlaySoundAt( Vector3 position, int which )
	{
		var sound = which switch
		{
			0 => PainSound,
			1 => DeathSound,
			2 => ReloadSound,
			3 => ShotgunShellSound,
			_ => null,
		};

		if ( sound is not null )
			Sound.Play( sound, position );
	}

	private void PlayPainSound() => PlaySoundAt( WorldPosition + Vector3.Up * 60f, 0 );
	private void PlayDeathSound() => PlaySoundAt( WorldPosition + Vector3.Up * 60f, 1 );
	public void PlayReloadSound()
	{
		if ( !Gun.PumpAction || ShotgunShellSound is null )
		{
			PlaySoundAt( MuzzlePosition, 2 );
			return;
		}

		// Shells go in one at a time, then a final pump
		var shells = Math.Max( 1, ClipSize - Clip );
		for ( var i = 0; i < shells; i++ )
			Invoke( 0.15f + i * 0.28f, () => { if ( IsValid && IsAlive ) PlaySoundAt( MuzzlePosition, 3 ); } );
		Invoke( 0.15f + shells * 0.28f + 0.1f, () => { if ( IsValid && IsAlive ) PumpRpc(); } );
	}

	/// <summary>How long a reload takes: shotguns take longer the more shells are missing.</summary>
	public float ReloadDuration( float animation ) => Gun.PumpAction ? MathF.Max( animation, 0.25f + Math.Max( 1, ClipSize - Clip ) * 0.28f + 0.35f ) : animation;

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void PumpRpc()
	{
		if ( ShotgunPumpSound is not null ) Sound.Play( ShotgunPumpSound, MuzzlePosition );
	}

	/// <summary>
	/// The ragdoll gets a fresh renderer with no sequence playing - otherwise bones without
	/// physics (fingers, spine, feet) keep playing the last animation.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void CreateCombineRagdoll( Vector3 velocity, Vector3 origin )
	{
		if ( !Renderer.IsValid() ) return;

		var batch = Scene.BatchGroup();

		var go = new GameObject( true, "Ragdoll" );
		go.Tags.Add( "ragdoll" );
		go.WorldTransform = Renderer.WorldTransform;

		var body = go.Components.Create<SkinnedModelRenderer>();
		body.Model = Renderer.Model;
		body.MaterialGroup = Renderer.MaterialGroup;
		body.UseAnimGraph = false;
		body.PlaybackRate = 0f;
		body.SetBodyGroup( "weapon", CombineWeaponStats.NoWeaponBodyGroup );
		OtherRagdoll( body );

		var physics = go.Components.Create<ModelPhysics>();
		physics.Model = body.Model;
		physics.Renderer = body;
		batch.Dispose();

		physics.CopyBonesFrom( Renderer, true );

		// Bodies created after an explosion sometimes start asleep and freeze mid-air
		body.Invoke( 0.05f, () =>
		{
			if ( !physics.IsValid() ) return;

			foreach ( var b in physics.Bodies )
			{
				var rb = b.Component;
				if ( !rb.IsValid() ) continue;

				rb.PhysicsBody.Sleeping = false;
				if ( velocity.Length > 1f )
					rb.ApplyImpulse( Vector3.Direction( origin, rb.WorldPosition ) * velocity.Length * rb.Mass );
			}
		} );

		go.Components.Create<CombineRagdollBlast>().Physics = physics;
		body.Invoke( 60f, go.Destroy );
	}

	/// <summary>Host: the gun falls out of the hand as a physics prop, like in HL2.</summary>
	private void DropWeapon()
	{
		if ( !Renderer.IsValid() ) return;
		if ( !Renderer.TryGetBoneTransform( "ValveBiped_Bip01_R_Hand", out var hand ) ) return;

		var model = Model.Load( $"hl2_combine_elite/models/weapons/w_{Gun.Name}.vmdl" );
		if ( model is null || model.IsError ) return;

		// The prop is skinned to a lone hand bone at the same rest pose, so put that bone where the hand is
		var bind = model.Bones.GetBone( "ValveBiped_Bip01_R_Hand" )?.LocalTransform ?? global::Transform.Zero;
		var rotation = hand.Rotation * bind.Rotation.Inverse;
		var position = hand.Position - rotation * bind.Position;

		var go = new GameObject( true, $"w_{Gun.Name}" );
		go.Tags.Add( "prop" );
		go.WorldPosition = position;
		go.WorldRotation = rotation;

		var renderer = go.AddComponent<SkinnedModelRenderer>();
		renderer.Model = model;
		renderer.UseAnimGraph = false;

		var collider = go.AddComponent<ModelCollider>();
		collider.Model = model;

		var rb = go.AddComponent<Rigidbody>();
		rb.Velocity = Vector3.Up * 60f + Vector3.Random * 40f;
		rb.AngularVelocity = Vector3.Random * 4f;

		go.NetworkSpawn();
		renderer.Invoke( 90f, go.Destroy );
	}
}
