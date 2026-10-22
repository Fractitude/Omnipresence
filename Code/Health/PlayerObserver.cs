namespace Sandbox;

/// <summary>
/// Dead players become these. They try to observe their last corpse.
/// </summary>
internal sealed class PlayerObserver : Component
{
	Angles eyeAngles;
	TimeSince timeSinceStarted;
	DeathCameraTarget cachedCorpse;
	float currentDistance;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		eyeAngles = Scene.Camera.WorldRotation;
		timeSinceStarted = 0;
		currentDistance = 32;

		cachedCorpse = Scene.GetAllComponents<DeathCameraTarget>()
			.Where( x => x.Connection == Network.Owner )
			.OrderByDescending( x => x.Created )
			.FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		// Don't allow immediate respawn
		if ( timeSinceStarted < 1 )
			return;

		// If pressed a button, or has been too long, ask the host to respawn us. The host destroys
		// this observer once the spawn goes through; if a listener vetoed it we stay here and try
		// again after the same delay.
		if ( Input.Pressed( "attack1" ) || Input.Pressed( "jump" ) || timeSinceStarted > 4f )
		{
			GameManager.RequestRespawn();
			timeSinceStarted = 0;
		}
	}

	protected override void OnPreRender()
	{
		if ( IsProxy )
			return;

		if ( cachedCorpse.IsValid() )
			RotateAround( cachedCorpse );
	}

	void RotateAround( Component target )
	{
		// Find the corpse eyes
		if ( !target.Components.Get<SkinnedModelRenderer>().TryGetBoneTransform( "pelvis", out var tx ) )
			return;

		tx.Position += Vector3.Up * 25;

		var e = eyeAngles;
		e += Input.AnalogLook;
		e.pitch = e.pitch.Clamp( -90, 90 );
		e.roll = 0.0f;
		eyeAngles = e;

		currentDistance = currentDistance.LerpTo( 150, Time.Delta * 5 );

		var center = tx.Position;
		var targetPos = center - eyeAngles.Forward * currentDistance;

		var tr = Scene.Trace.FromTo( center, targetPos ).Radius( 1.0f ).WithoutTags( "ragdoll", "effect" ).Run();

		Scene.Camera.WorldPosition = tr.EndPosition;
		Scene.Camera.WorldRotation = eyeAngles;
	}
}
