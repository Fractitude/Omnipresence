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

	private readonly List<(ModelRenderer Renderer, int Index)> windows = [];
	private bool glassApplied;

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
				if ( IsWindow( original ) )
				{
					windows.Add( (renderer, i) );
					continue;
				}
				if ( !name.Contains( "primary" ) && !name.Contains( "secondary" )
					&& !name.Contains( "/headlight_" ) && !name.Contains( "/taillight_" ) ) continue;
				var copy = original.CreateCopy();
				renderer.Materials.SetOverride( i, copy );
				if ( name.Contains( "/headlight_" ) ) headlights.Add( copy );
				else if ( name.Contains( "/taillight_" ) ) taillights.Add( copy );
				else paints.Add( (copy, name.Contains( "secondary" )) );
			}
		}
		ApplyClearGlass();
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
		// A car placed in the scene is set up in OnStart, before its model has loaded, so the window pass finds nothing
		// then and has to be retried here until the model is there. Cars spawned at run time get it on the first frame.
		if ( !glassApplied ) ApplyClearGlass();
		UpdateSmoke( dt );
		UpdateAudio( dt );
		UpdateBackfire( dt );
		// The speedometer is this game's own (Code/Vehicles/Hud), fed by the IVehicleGauges members in CuratedVehicle.cs.
		VehicleHud.Update( this, ref hud );
	}

	/// <summary>
	/// Which materials are the car's windows. crosschainer's window glass uses the drift_glass shader (cabin.vmat, and
	/// windscreen_ok on the cars that split the screen off), but three of the cars — the Hokuto Boxer, the Takeda Ascend
	/// and the Mizuno Rotary C — also draw a rear window with a plain opaque material, dark_cabin.vmat on the ordinary
	/// complex shader. That one is a window like the rest wherever it appears: the drift_glass shader is only ever used
	/// for glass, and nothing else in these models is called dark_cabin.
	/// </summary>
	private static bool IsWindow( Material material )
	{
		if ( (material.ShaderName ?? "").Contains( "drift_glass" ) ) return true;
		return (material.Name ?? "").ToLowerInvariant().EndsWith( "/dark_cabin.vmat" );
	}

	/// <summary>
	/// One glass for every car, and it is clear. crosschainer's own window material (cabin.vmat) is opaque near-black and
	/// only looked see-through from the seat because the backface of a single-skin window mesh was culled; two of the cars
	/// model their glass as a closed slab, so not even that happened. Every window slot is therefore put on a transparent
	/// copy of the car's own lens glass. The lens shader is the only translucent one these models ship, but its material
	/// carries the headlight lens' texture, which drew a fluted pattern across the window, so the copy is given a plain
	/// white texture and a barely-there tint. A material override on this client only: the models and their own materials
	/// are untouched, and every car ends up with the same glass.
	/// </summary>
	private void ApplyClearGlass()
	{
		if ( windows.Count == 0 ) CollectWindows();
		if ( windows.Count == 0 ) return;
		Material clear = null;
		foreach ( var (renderer, _) in windows )
		{
			if ( !renderer.IsValid() ) continue;
			var lens = renderer.Model.Materials.FirstOrDefault( m => (m.ShaderName ?? "").Contains( "drift_lens" ) );
			if ( lens is null ) continue;
			clear = lens.CreateCopy();
			clear.Set( "g_tLens", Texture.Load( "materials/default/default_color.tga" ) );
			clear.Set( "LensTint", new Color( 0.9f, 0.9f, 0.9f, 1f ) );
			clear.Set( "LensOpacity", new Vector4( 0.05f, 0.05f, 0.05f, 0.05f ) );
			break;
		}
		if ( clear is null ) return;
		foreach ( var (renderer, index) in windows )
		{
			if ( !renderer.IsValid() ) continue;
			renderer.Materials.SetOverride( index, clear );
		}
		glassApplied = true;
	}

	/// <summary>Find the window slots again — used when a car's model was not loaded yet the first time around.</summary>
	private void CollectWindows()
	{
		windows.Clear();
		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			if ( renderer.Model is null || renderer.Model.IsError ) continue;
			for ( var i = 0; i < renderer.Model.Materials.Length; i++ )
			{
				if ( IsWindow( renderer.Model.Materials[i] ) ) windows.Add( (renderer, i) );
			}
		}
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
