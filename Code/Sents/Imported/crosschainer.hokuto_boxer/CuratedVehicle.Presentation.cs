using Sandbox;
using System;
using System.Collections.Generic;

namespace CuratedVehicles;

public sealed partial class CuratedVehicle
{
	private readonly List<(Material Material, bool Secondary)> paints = [];
	private readonly List<Material> headlights = [];
	private readonly List<Material> taillights = [];
	private readonly List<SpotLight> beams = [];
	private SoundHandle engine;
	private SoundHandle tires;
	private SoundHandle backfire;
	private float roll;
	private float popupOpening;
	private int observedBackfire;
	private float tireVolume;
	private GameObject hud;

	private void SetupPresentation()
	{
		if ( Application.IsDedicatedServer ) return;
		observedBackfire = BackfireSequence;
		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			if ( renderer.Model is null || renderer.Model.IsError ) continue;
			for ( var i = 0; i < renderer.Model.Materials.Length; i++ )
			{
				var original = renderer.Model.Materials[i];
				var name = original.Name.ToLowerInvariant();
				if ( !name.Contains( "primary" ) && !name.Contains( "secondary" )
					&& !name.Contains( "/headlight_" ) && !name.Contains( "/taillight_" ) ) continue;
				var copy = original.CreateCopy();
				renderer.Materials.SetOverride( i, copy );
				if ( name.Contains( "/headlight_" ) ) headlights.Add( copy );
				else if ( name.Contains( "/taillight_" ) ) taillights.Add( copy );
				else paints.Add( (copy, name.Contains( "secondary" )) );
			}
		}
		foreach ( var mount in HeadlightMounts ?? [] )
		{
			var go = new GameObject( GameObject, true, "Headlight beam" ) { NetworkMode = NetworkMode.Never };
			go.Flags |= GameObjectFlags.NotSaved;
			go.LocalPosition = mount;
			go.LocalRotation = Rotation.From( new Angles( 4, 0, 0 ) );
			var beam = go.Components.Create<SpotLight>();
			beam.LightColor = new Color( 0.92f, 0.96f, 1 ) * 4;
			beam.Radius = 1600;
			beam.ConeInner = 18;
			beam.ConeOuter = 38;
			beam.Shadows = true;
			beam.Enabled = false;
			beams.Add( beam );
		}
	}

	private void UpdatePresentation()
	{
		if ( Application.IsDedicatedServer ) return;
		var body = Body;
		if ( GameObject.IsDestroyed || !body.IsValid() ) { StopPresentation(); return; }
		var dt = Math.Min( Time.Delta, 0.1f );
		roll += Vector3.Dot( body.Velocity, WorldRotation.Forward ) / WheelRadius * dt * 180 / MathF.PI;
		if ( Burnout ) roll += Rpm / 60 * 360 * dt / 10;
		roll %= 360;
		for ( var i = 0; i < 4; i++ )
		{
			if ( !WheelObjects[i].IsValid() ) continue;
			WheelObjects[i].LocalPosition = hubs[i] + Vector3.Down * WheelTravel[i];
			WheelObjects[i].LocalRotation = Rotation.FromYaw( i < 2 ? FrontWheelAngle : 0 )
				* Rotation.FromPitch( roll ) * Rotation.FromYaw( i % 2 == 0 ? 180 : 0 );
		}
		if ( PopupLights.IsValid() )
		{
			popupOpening += Math.Clamp( (Lights ? PopupAngle : 0) - popupOpening, -dt * 60, dt * 60 );
			PopupLights.LocalRotation = Rotation.FromPitch( -popupOpening );
		}
		var lampsOn = Lights && (!PopupLights.IsValid() || popupOpening >= PopupAngle - 0.1f);
		foreach ( var (material, secondary) in paints ) material.Set( "g_vColorTint", secondary ? new Color( 0.098f, 0.11f, 0.125f ) : Paint );
		foreach ( var material in headlights ) material.Set( "LampEmission", lampsOn ? 5f : 0 );
		foreach ( var material in taillights ) material.Set( "LampEmission", Braking || Gear == -1 ? 8f : Lights ? 2f : 0 );
		foreach ( var beam in beams ) if ( beam.IsValid() ) beam.Enabled = lampsOn;
		UpdateSmoke( dt );
		UpdateAudio( dt );
		UpdateBackfire( dt );
		if ( LocalDriver && !hud.IsValid() )
		{
			hud = new GameObject( true, "Vehicle driving HUD" ) { NetworkMode = NetworkMode.Never };
			hud.Flags |= GameObjectFlags.NotSaved;
			var screen = hud.Components.Create<ScreenPanel>();
			screen.ZIndex = 20;
			hud.Components.Create<VehicleHud>().Vehicle = this;
		}
		else if ( !LocalDriver && hud.IsValid() ) { hud.Destroy(); hud = null; }
	}

	private void UpdateAudio( float dt )
	{
		var occupied = Driver.IsValid();
		if ( occupied && (engine is null || !engine.IsPlaying) )
		{
			engine = Sound.Play( EngineSound, WorldPosition );
			ConfigureSound( engine );
		}
		if ( !occupied && engine is not null ) { engine.Stop( 0.15f ); engine = null; }
		if ( engine is not null )
		{
			engine.Pitch = 0.55f + Rpm / MaxRpm * 1.1f;
			engine.Volume = LocalDriver ? 0.0253125f : 0.009f + 0.051f * Math.Clamp( (Rpm - 1200) / (MaxRpm - 1200), 0, 1 );
			engine.SpacialBlend = LocalDriver ? 0 : 1;
			engine.DistanceAttenuation = !LocalDriver;
		}
		var target = smokingTires > 0
			? (0.08f + 0.1f * (Burnout ? 0.8f : Math.Clamp( (Slip - 0.14f) / 0.65f, 0, 1 ))) * (smokingTires == 1 ? 0.7f : 1) : 0;
		tireVolume = tireVolume.LerpTo( target, dt * (target > tireVolume ? 12 : 20) );
		if ( tireVolume > 0.001f )
		{
			if ( tires is null || !tires.IsPlaying )
			{
				tires = Sound.Play( "drift_district/audio/tires.sound", WorldPosition, fadeInTime: 0.05f );
				ConfigureSound( tires );
				if ( tires is not null ) tires.LocalTransform = new global::Transform( (hubs[2] + hubs[3]) * 0.5f + Vector3.Down * WheelRadius );
			}
			if ( tires is not null ) { tires.Volume = tireVolume; tires.Pitch = 0.94f + 0.04f * MathF.Sin( Time.Now * 1.3f ); }
		}
		else if ( tires is not null ) { tires.Stop(); tires = null; }
		if ( observedBackfire != BackfireSequence )
		{
			observedBackfire = BackfireSequence;
			FlashBackfire();
		}
	}

	private void ConfigureSound( SoundHandle sound )
	{
		if ( sound is null ) return;
		sound.Parent = GameObject;
		sound.FollowParent = true;
		sound.LocalTransform = global::Transform.Zero;
		sound.Distance = 2400;
		sound.SpacialBlend = 1;
		sound.OcclusionEnabled = false;
		sound.ReverbEnabled = false;
	}

	private void StopPresentation()
	{
		StopSmoke();
		StopBackfire();
		engine?.Stop(); engine = null;
		tires?.Stop(); tires = null;
		backfire?.Stop(); backfire = null;
		if ( hud.IsValid() ) hud.Destroy();
		hud = null;
		foreach ( var beam in beams ) if ( beam.IsValid() ) beam.Enabled = false;
	}
}
