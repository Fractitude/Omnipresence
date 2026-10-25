using Sandbox;
using System;
using System.Collections.Generic;

namespace CuratedVehicles;

public sealed partial class CuratedVehicle
{
	private const float BackfireDuration = 0.22f;
	private GameObject backfireRoot;
	private float backfireRemaining;
	private readonly List<(ModelRenderer Renderer, float Length, float Width)> flames = [];
	private readonly List<PointLight> exhaustLights = [];

	private void CreateBackfire()
	{
		if ( backfireRoot.IsValid() || ExhaustOutlets is not { Length: > 0 }
			|| !BackfireFlameModel.TryGet( out var orange, out var blue ) ) return;
		backfireRoot = new GameObject( GameObject, false, "Exhaust backfire" ) { NetworkMode = NetworkMode.Never };
		backfireRoot.Flags |= GameObjectFlags.NotSaved;
		for ( var i = 0; i < ExhaustOutlets.Length; i++ )
		{
			var direction = ExhaustDirections is not null && i < ExhaustDirections.Length
				? ExhaustDirections[i].Normal : Vector3.Backward;
			if ( direction.LengthSquared < 0.01f ) direction = Vector3.Backward;
			var tip = new GameObject( backfireRoot, true, "Exhaust flame outlet" );
			tip.LocalPosition = ExhaustOutlets[i];
			tip.LocalRotation = Rotation.LookAt( direction, Math.Abs( direction.z ) > 0.98f ? Vector3.Left : Vector3.Up );
			AddFlame( tip, orange, "Orange flame", 1.5f, 16, 6 );
			AddFlame( tip, blue, "Blue flame", 0.5f, 6, 4 );
			var lightObject = new GameObject( tip, true, "Backfire light" );
			lightObject.LocalPosition = Vector3.Forward * 5;
			var light = lightObject.Components.Create<PointLight>();
			light.Radius = 60;
			light.Shadows = false;
			exhaustLights.Add( light );
		}
	}

	private void AddFlame( GameObject tip, Model model, string name, float distance, float length, float width )
	{
		var go = new GameObject( tip, true, name );
		go.LocalPosition = Vector3.Forward * distance;
		go.LocalScale = new Vector3( length, width, width );
		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;
		renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		flames.Add( (renderer, length, width) );
	}

	private void FlashBackfire()
	{
		CreateBackfire();
		backfireRemaining = BackfireDuration;
		if ( backfireRoot.IsValid() ) backfireRoot.Enabled = true;
		backfire?.Stop();
		backfire = Sound.Play( "drift_district/audio/backfire.sound", WorldPosition );
		ConfigureSound( backfire );
		if ( backfire is null ) return;
		backfire.LocalTransform = new global::Transform( ExhaustOutlets is { Length: > 0 } ? ExhaustOutlets[0] : Vector3.Zero );
		backfire.Volume = LocalDriver ? 0.1f : 0.25f;
		backfire.SpacialBlend = LocalDriver ? 0 : 1;
		backfire.DistanceAttenuation = !LocalDriver;
	}

	private void UpdateBackfire( float dt )
	{
		if ( !backfireRoot.IsValid() ) return;
		backfireRemaining = Math.Max( 0, backfireRemaining - dt );
		backfireRoot.Enabled = backfireRemaining > 0;
		var fade = Math.Clamp( backfireRemaining / 0.06f, 0, 1 );
		for ( var i = 0; i < flames.Count; i++ )
		{
			var (renderer, length, width) = flames[i];
			if ( !renderer.IsValid() ) continue;
			var phase = Time.Now * 87 + i * 2.7f;
			length *= (0.85f + 0.19f * MathF.Sin( phase )) * fade;
			width *= (0.9f + 0.12f * MathF.Sin( phase * 1.61f )) * fade;
			renderer.GameObject.LocalScale = new Vector3( length, width, width );
		}
		foreach ( var light in exhaustLights )
		{
			if ( !light.IsValid() ) continue;
			light.LightColor = new Color( 1, 0.25f, 0.035f ) * (backfireRemaining / BackfireDuration * 2);
		}
	}

	private void StopBackfire()
	{
		if ( backfireRoot.IsValid() ) backfireRoot.Destroy();
		backfireRoot = null;
		backfireRemaining = 0;
		flames.Clear();
		exhaustLights.Clear();
	}
}
