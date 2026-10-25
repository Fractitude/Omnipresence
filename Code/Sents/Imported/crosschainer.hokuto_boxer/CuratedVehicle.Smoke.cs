using Sandbox;
using System;

namespace CuratedVehicles;

public sealed partial class CuratedVehicle
{
	private GameObject smokeRoot;
	private ParticleEffect smoke;
	private float smokeTime;
	private bool sliding;
	private int smokingTires;
	private readonly Vector3?[] lastSmokeContact = new Vector3?[2];

	private void CreateSmoke()
	{
		smokeRoot = new GameObject( false, "Tire smoke" ) { NetworkMode = NetworkMode.Never };
		smokeRoot.Flags |= GameObjectFlags.NotSaved;
		smoke = smokeRoot.Components.Create<ParticleEffect>();
		smoke.MaxParticles = 600;
		smoke.Lifetime = new ParticleFloat( 1.4f, 2.2f );
		smoke.ApplyShape = true;
		smoke.InitialScale = new ParticleFloat( 16, 24 );
		smoke.Scale = new ParticleFloat { Type = ParticleFloat.ValueType.Range,
			Evaluation = ParticleFloat.EvaluationType.Life, ConstantA = 1, ConstantB = 3.5f };
		smoke.ApplyAlpha = true;
		smoke.Alpha = new ParticleFloat { Type = ParticleFloat.ValueType.Range,
			Evaluation = ParticleFloat.EvaluationType.Life, ConstantA = 0.65f, ConstantB = 0 };
		smoke.ApplyRotation = true;
		smoke.InitialRoll = new ParticleFloat( 0, 360 );
		smoke.Damping = 0.6f;
		smoke.LocalSpace = 0;
		var sprites = smokeRoot.Components.Create<ParticleSpriteRenderer>();
		sprites.Sprite = Sprite.FromTexture( Texture.Load( "drift_district/ui/smoke.png" ) );
		sprites.Shadows = false;
		sprites.Lighting = true;
		sprites.SortMode = ParticleSpriteRenderer.ParticleSortMode.ByDistance;
		sprites.DepthFeather = 12;
		sprites.CameraFadeNear = 8;
		sprites.CameraFadeFar = 40;
		smokeRoot.Enabled = true;
	}

	private void UpdateSmoke( float dt )
	{
		// Each peer reconstructs world-space smoke from synced telemetry and its own contact traces.
		sliding = Driver.IsValid() && (Burnout || (sliding ? Speed > 20 && Slip > 0.14f : Speed > 25 && Slip > 0.2f));
		smokingTires = 0;
		if ( !sliding )
		{
			smokeTime = 0;
			lastSmokeContact[0] = lastSmokeContact[1] = null;
			return;
		}
		if ( !smoke.IsValid() ) CreateSmoke();
		smokeTime = Math.Min( smokeTime + dt, 0.15f );
		var ticks = (int)(smokeTime / 0.025f);
		smokeTime -= ticks * 0.025f;
		for ( var i = 2; i < 4; i++ )
		{
			var trace = TraceWheel( i );
			if ( !trace.Hit || trace.Normal.z < 0.6f ) { lastSmokeContact[i - 2] = null; continue; }
			smokingTires++;
			if ( ticks == 0 ) continue;
			var contact = trace.EndPosition;
			var previous = lastSmokeContact[i - 2] ?? contact;
			var distance = Vector3.DistanceBetween( previous, contact );
			if ( distance > 192 ) { previous = contact; distance = 0; }
			var count = Math.Clamp( Math.Max( ticks, (int)MathF.Ceiling( distance / 12 ) ), 1, 12 );
			for ( var sample = 1; sample <= count && !smoke.IsFull; sample++ )
			{
				var particle = smoke.Emit( previous.LerpTo( contact, sample / (float)count ) + trace.Normal * 6, 0 );
				particle.Velocity = Vector3.Up * 18 + Vector3.Random * 9 + Body.Velocity * 0.06f;
				particle.Color = new Color( 0.8f, 0.8f, 0.8f );
			}
			lastSmokeContact[i - 2] = contact;
		}
	}

	private void StopSmoke()
	{
		if ( smokeRoot.IsValid() ) smokeRoot.Destroy();
		smokeRoot = null;
		smoke = null;
		smokeTime = 0;
		sliding = false;
		smokingTires = 0;
		lastSmokeContact[0] = lastSmokeContact[1] = null;
	}
}
