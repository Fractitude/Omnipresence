using Sandbox;
using System;
using System.Collections.Generic;

namespace CuratedVehicles;

/// <summary>Gamejam car-body impacts, selected from the actual collision's closing speed.</summary>
public sealed class VehicleCollisionAudio : Component, Component.ICollisionListener
{
	private TimeSince sinceImpact = 100;
	private readonly List<SoundHandle> impacts = [];

	protected override void OnStart() => ConfigureBody();
	protected override void OnFixedUpdate() => ConfigureBody();

	private void ConfigureBody()
	{
		if ( GameObject.IsDestroyed ) return;
		var body = GetComponent<Rigidbody>();
		if ( !body.IsValid() ) return;
		if ( !body.CollisionEventsEnabled ) body.CollisionEventsEnabled = true;
		if ( !body.CollisionUpdateEventsEnabled ) body.CollisionUpdateEventsEnabled = true;
		if ( !body.RigidbodyFlags.Contains( RigidbodyFlags.DisableCollisionSounds ) )
			body.RigidbodyFlags |= RigidbodyFlags.DisableCollisionSounds;
	}

	public void OnCollisionStart( Collision collision ) => TryPlayImpact( collision );
	public void OnCollisionUpdate( Collision collision ) => TryPlayImpact( collision );

	private void TryPlayImpact( Collision collision )
	{
		if ( !Networking.IsHost || GameObject.IsDestroyed || sinceImpact < 0.15f ) return;
		var contact = collision.Contact;
		var speed = MathF.Abs( contact.NormalSpeed );
		if ( !float.IsFinite( speed ) || speed < 85 ) return;
		// Preserve the source game's filter for ordinary road and suspension contact.
		if ( MathF.Abs( contact.Normal.z ) > 0.75f && speed < 320 ) return;
		sinceImpact = 0;
		ImpactBurst( speed < 170 ? 0 : speed < 350 ? 1 : 2, contact.Point );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void ImpactBurst( int severity, Vector3 point )
	{
		if ( Application.IsDedicatedServer || GameObject.IsDestroyed || severity < 0 || severity > 2 ) return;
		var sound = severity switch
		{
			0 => "drift_district/audio/collision/bump.sound",
			1 => "drift_district/audio/collision/hit.sound",
			_ => "drift_district/audio/collision/crash.sound"
		};
		var handle = Sound.Play( sound, point );
		if ( handle is null ) return;
		handle.Parent = GameObject;
		handle.FollowParent = true;
		handle.LocalTransform = new global::Transform( WorldTransform.PointToLocal( point ) );
		handle.SpacialBlend = 1;
		if ( GetComponent<CuratedVehicle>()?.LocalDriver == true )
		{
			handle.Volume *= 0.8f;
			handle.DistanceAttenuation = false;
		}
		impacts.RemoveAll( h => !h.IsPlaying );
		impacts.Add( handle );
	}

	private void StopImpacts()
	{
		foreach ( var handle in impacts ) handle.Stop();
		impacts.Clear();
	}

	protected override void OnDisabled() => StopImpacts();
	protected override void OnDestroy() => StopImpacts();
}
