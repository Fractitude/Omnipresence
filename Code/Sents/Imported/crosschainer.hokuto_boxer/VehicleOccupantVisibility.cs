using Sandbox;
using System.Collections.Generic;

namespace CuratedVehicles;

/// <summary>Hide the driver's character, clothing and held models locally on each peer, restoring their exact render state on exit.</summary>
public sealed class VehicleOccupantVisibility : Component
{
	private BaseChair seat;
	private PlayerController occupant;
	private readonly Dictionary<ModelRenderer, (bool Game, ModelRenderer.ShadowRenderType Shadows)> original = [];

	protected override void OnStart() => seat = GetComponent<BaseChair>();
	protected override void OnUpdate()
	{
		var next = seat.IsValid() ? seat.GetOccupant() : null;
		if ( next != occupant ) { Restore(); occupant = next; }
		if ( !occupant.IsValid() ) return;
		foreach ( var renderer in occupant.GetComponentsInChildren<ModelRenderer>() )
		{
			if ( !original.ContainsKey( renderer ) ) original[renderer] = (renderer.RenderOptions.Game, renderer.RenderType);
			renderer.RenderOptions.Game = false;
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		}
	}

	private void Restore()
	{
		foreach ( var (renderer, state) in original )
		{
			if ( !renderer.IsValid() ) continue;
			renderer.RenderOptions.Game = state.Game;
			renderer.RenderType = state.Shadows;
		}
		original.Clear();
		occupant = null;
	}
	protected override void OnDisabled() => Restore();
	protected override void OnDestroy() => Restore();
}
