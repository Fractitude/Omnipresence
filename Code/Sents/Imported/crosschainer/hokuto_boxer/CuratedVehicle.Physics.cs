using Sandbox;
using System;

namespace CuratedVehicles;

public sealed partial class CuratedVehicle
{
	private const float MaxRaise = 3 / 2.54f;
	private const float MaxDrop = 6 / 2.54f;
	private readonly BackfireTrigger backfireTrigger = new();
	private readonly SceneTraceResult[] contacts = new SceneTraceResult[4];

	protected override void OnFixedUpdate()
	{
		if ( !ready || GameObject.IsDestroyed || !Networking.IsHost || !Body.IsValid() || !Body.MotionEnabled ) return;
		PreserveHandlingInertia();
		// The Sandbox spawn/physgun systems can change ownership. Keep car physics on the host.
		if ( GameObject.Network.Owner is not null ) GameObject.Network.DropOwnership();
		var connection = Driver.IsValid() ? Driver.Network.Owner : null;
		var throttle = connection?.Down( "Forward" ) == true ? 1f : 0;
		var brake = connection?.Down( "Backward" ) == true ? 1f : 0;
		var handbrake = connection?.Down( "Jump" ) == true;
		var targetSteer = (connection?.Down( "Left" ) == true ? 1f : 0)
			- (connection?.Down( "Right" ) == true ? 1f : 0);
		Steering = Steering.LerpTo( targetSteer, Time.Delta * 7 );
		var forwardSpeed = Vector3.Dot( Body.Velocity, WorldRotation.Forward );
		var pedals = AutomaticPedals.Resolve( Gear, forwardSpeed, throttle, brake, Speed < (Burnout ? 14 : 8) );
		Gear = pedals.Gear;
		throttle = pedals.Throttle;
		brake = pedals.Brake;
		Braking = brake > 0 || handbrake;
		var grounded = 0;
		for ( var i = 0; i < 4; i++ )
		{
			contacts[i] = TraceWheel( i );
			if ( contacts[i].Hit && contacts[i].Normal.z >= 0.6f ) grounded++;
		}
		Burnout = Gear > 0 && throttle > 0.45f && brake > 0.45f && !handbrake
			&& Speed < (Burnout ? 14 : 8) && grounded == 4;
		var ratio = Gear == -1 ? 2.8f : Gear == 0 ? 0 : Gears[Math.Clamp( Gear - 1, 0, Gears.Length - 1 )];
		var radius = WheelRadius / InchesPerMeter;
		var wheelRpm = GearingSpeed / 3.6f / (2 * MathF.PI * radius) * 60;
		var finalRatio = MaxRpm * 0.92f / (wheelRpm * Gears[^1]);
		var roadRpm = MathF.Abs( forwardSpeed ) / InchesPerMeter / (2 * MathF.PI * radius) * 60 * ratio * finalRatio;
		var idleRpm = Burnout ? MaxRpm * (0.48f + 0.24f * throttle) : 1000 + throttle * 700;
		Rpm = Math.Clamp( Rpm.LerpTo( Math.Max( idleRpm, roadRpm ), Time.Delta * 9 ), 900, MaxRpm );
		if ( Gear > 0 && sinceShift > 0.3f )
		{
			if ( Rpm > MaxRpm * 0.92f && Gear < Gears.Length ) { Gear++; sinceShift = 0; }
			else if ( Rpm < MaxRpm * 0.36f && Gear > 1 ) { Gear--; sinceShift = 0; }
		}
		if ( backfireTrigger.Update( Driver.IsValid(), ExhaustOutlets?.Length > 0, Speed, Rpm,
			MaxRpm, Gear, throttle > 0.1f, Time.Delta, Burnout ) ) BackfireSequence++;
		var torqueFactor = Rpm < MaxRpm * 0.6f ? 0.5f + 0.5f * Rpm / (MaxRpm * 0.6f)
			: 1 - 0.4f * (Rpm / MaxRpm - 0.6f) / 0.4f;
		var drive = Torque * torqueFactor * ratio * finalRatio * 0.95f / radius * throttle;
		if ( Rpm >= MaxRpm * 0.995f || sinceShift < 0.16f ) drive = 0;
		if ( Gear == -1 ) drive = -drive;
		if ( TryPark( throttle > 0 || brake > 0 || handbrake, contacts ) ) { Slip = 0; return; }
		var coastBrake = throttle <= 0 && brake <= 0 && !handbrake ? Math.Clamp( 0.22f - Speed / 350, 0.04f, 0.22f ) : 0;
		var lateralSpeed = Vector3.Dot( Body.Velocity, WorldRotation.Left );
		var driftAngle = MathF.Atan2( MathF.Abs( lateralSpeed ), MathF.Max( 80, forwardSpeed ) ) * 180 / MathF.PI;
		if ( Drift && forwardSpeed > 120 && (handbrake || driftAngle > 25 || rearSlide > 0.5f && driftAngle > 12) )
			rearSlideHold = 0.6f;
		else rearSlideHold = Math.Max( 0, rearSlideHold - Time.Delta );
		rearSlide = Drift ? rearSlide.LerpTo( rearSlideHold > 0 ? 1 : 0, Time.Delta * 10 ) : 0;
		var frontAuthority = Drift && forwardSpeed > 120 ? 1 - 0.65f * Math.Clamp( (driftAngle - 10) / 30, 0, 1 ) : 1;
		var gravity = Body.Gravity ? Scene.PhysicsWorld.Gravity.Length * Body.GravityScale : 0;
		var burnoutForce = Burnout ? Math.Min( Math.Abs( drive ) * InchesPerMeter, Body.Mass * gravity * 0.16f ) : 0;
		Slip = 0;
		var travel = Vector4.Zero;
		for ( var i = 0; i < 4; i++ )
		{
			travel[i] = MaxDrop;
			var trace = contacts[i];
			if ( !trace.Hit || trace.Normal.z < 0.3f ) continue;
			var hub = WorldTransform.PointToWorld( hubs[i] );
			var mount = hub + WorldRotation.Up * MaxRaise;
			var displacement = Vector3.DistanceBetween( mount, trace.EndPosition ) - WheelRadius - MaxRaise;
			travel[i] = Math.Clamp( displacement, -MaxRaise, MaxDrop );
			var velocity = Body.GetVelocityAtPoint( hub );
			var sprungMass = Body.Mass * LoadFraction( i );
			var omega = 2 * MathF.PI * 2.25f;
			var stiffness = sprungMass * omega * omega;
			var damping = 2 * 0.8f * sprungMass * omega;
			var force = sprungMass * gravity - displacement * stiffness - Vector3.Dot( velocity, trace.Normal ) * damping;
			force += Math.Max( 0, -displacement - MaxRaise ) * stiffness * 4;
			var normalForce = Math.Clamp( force, 0, sprungMass * Math.Max( gravity, 1 ) * 8 );
			Body.ApplyForceAt( hub, trace.Normal * normalForce );
			var front = i < 2;
			var orientation = Burnout ? WorldRotation : WorldRotation * Rotation.FromYaw( front ? FrontWheelAngle : 0 );
			var lateral = Vector3.Dot( velocity, orientation.Left );
			var longitudinal = Vector3.Dot( velocity, orientation.Forward );
			var slipAngle = MathF.Atan2( lateral, Math.Max( 80, Math.Abs( longitudinal ) ) );
			Slip = Math.Max( Slip, Math.Abs( slipAngle ) );
			var slide = Burnout ? 1 : rearSlide;
			var tireStiffness = Drift ? front ? 2.7f : 3.2f - 2.15f * slide : front ? 6 : 7;
			var grip = normalForce * (Drift ? front ? 1.2f : 0.95f - 0.1f * slide : 1.4f);
			var driveForce = front || handbrake ? 0 : drive * InchesPerMeter / 2;
			var rearAuthority = 1f;
			if ( Drift && !front && !Burnout )
			{
				var driveLoad = Math.Clamp( Math.Abs( driveForce ) / Math.Max( normalForce * 2, 1 ), 0, 0.8f );
				rearAuthority -= (1 - MathF.Sqrt( 1 - driveLoad * driveLoad )) * slide;
			}
			if ( Burnout && !front ) { tireStiffness *= 0.3f; grip *= 0.3f; }
			if ( handbrake && !front ) grip *= 0.2f;
			var sideways = Math.Clamp( -slipAngle * normalForce * tireStiffness, -grip, grip )
				* (front ? frontAuthority : rearAuthority);
			var longitudinalForce = Burnout ? front ? -burnoutForce / 2 : burnoutForce / 2 : driveForce;
			longitudinalForce -= Math.Clamp( longitudinal * Mass * ((brake + coastBrake) * 2
				+ (handbrake && !front ? 4 : 0) + 0.008f), -normalForce * 1.8f, normalForce * 1.8f );
			Body.ApplyForceAt( hub, orientation.Left * sideways + orientation.Forward * longitudinalForce );
		}
		WheelTravel = travel;
		Body.ApplyForce( -Body.Velocity * Body.Velocity.Length * 0.003f );
	}

	private SceneTraceResult TraceWheel( int index )
	{
		var hub = WorldTransform.PointToWorld( hubs[index] );
		return Scene.Trace.Ray( hub + WorldRotation.Up * MaxRaise,
			hub - WorldRotation.Up * (WheelRadius + MaxDrop) ).IgnoreGameObjectHierarchy( GameObject ).Run();
	}

	private float LoadFraction( int index )
	{
		var frontX = (hubs[0].x + hubs[1].x) * 0.5f;
		var rearX = (hubs[2].x + hubs[3].x) * 0.5f;
		var center = Body.MassCenter;
		var frontShare = Math.Clamp( (center.x - rearX) / (frontX - rearX), 0.05f, 0.95f );
		var axle = index < 2 ? 0 : 2;
		var leftShare = Math.Clamp( (center.y - hubs[axle + 1].y) / (hubs[axle].y - hubs[axle + 1].y), 0.05f, 0.95f );
		return (index < 2 ? frontShare : 1 - frontShare) * (index % 2 == 0 ? leftShare : 1 - leftShare);
	}

	private bool TryPark( bool input, SceneTraceResult[] contacts )
	{
		if ( input || Body.Velocity.Length > 8 || Body.AngularVelocity.Length > 0.12f || WorldRotation.Up.z < 0.7f )
		{
			if ( Body.Sleeping ) Body.Sleeping = false;
			return false;
		}
		var travel = Vector4.Zero;
		for ( var i = 0; i < 4; i++ )
		{
			var trace = contacts[i];
			var mount = WorldTransform.PointToWorld( hubs[i] ) + WorldRotation.Up * MaxRaise;
			var displacement = Vector3.DistanceBetween( mount, trace.EndPosition ) - WheelRadius - MaxRaise;
			if ( !trace.Hit || trace.Normal.z < 0.7f || Math.Abs( displacement ) > 0.4f
				|| trace.Body.IsValid() && (trace.Body.Velocity.Length > 1 || trace.Body.AngularVelocity.Length > 0.01f) )
			{
				if ( Body.Sleeping ) Body.Sleeping = false;
				return false;
			}
			travel[i] = displacement;
		}
		WheelTravel = travel;
		if ( !Body.Sleeping ) Body.Sleeping = true;
		return true;
	}
}
