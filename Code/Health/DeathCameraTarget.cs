namespace Sandbox;

/// <summary>
/// Goes on a dead player's ragdoll, so the observer can look at it.
/// </summary>
public sealed class DeathCameraTarget : Component
{
	[Sync( SyncFlags.FromHost )] public Connection Connection { get; set; }
	public DateTime Created { get; set; }

	protected override void OnEnabled()
	{
		Invoke( 60.0f, GameObject.Destroy );
	}
}
