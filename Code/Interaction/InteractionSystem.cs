namespace Sandbox;

/// <summary>
/// Finds the <see cref="Interactable"/> the local player looks at, and presses it on Use. A ray from the player's eye along
/// their view must hit one of its colliders first (nothing in between), within its <see cref="Interactable.Range"/>. The
/// Interactable's colliders are what make it appear and what let it be used. Runs by itself: nothing to add to the scene.
/// </summary>
public sealed class InteractionSystem : GameObjectSystem<InteractionSystem>
{
	/// <summary>
	/// The farthest the look ray goes, for an Interactable whose range is 0 (no limit).
	/// </summary>
	const float MaxTrace = 2000f;

	public InteractionSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, Update, "InteractionSystem" );
	}

	void Update()
	{
		if ( !Game.IsPlaying )
			return;

		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );

		// Not while something else has the player: a menu with the cursor, or a Navi screen (which turns the controls off).
		if ( !player.IsValid() || !player.UseInputControls || Mouse.Visibility == MouseVisibility.Visible )
		{
			Interactable.SetFocus( null );
			return;
		}

		var interactable = Look( player );
		Interactable.SetFocus( interactable );

		if ( interactable.IsValid() && Input.Pressed( player.UseButton ) )
			interactable.TryPress( player.GameObject );
	}

	/// <summary>
	/// The Interactable the player's look hits, if it is within its range.
	/// </summary>
	Interactable Look( PlayerController player )
	{
		var interactables = Scene.GetAllComponents<Interactable>().Where( x => x.Enabled ).ToList();
		if ( interactables.Count == 0 )
			return null;

		var reach = interactables.Max( x => x.Range <= 0f ? MaxTrace : x.Range );
		var eye = player.EyePosition;
		var direction = player.EyeAngles.ToRotation().Forward;

		var trace = Scene.Trace.Ray( eye, eye + direction * System.MathF.Min( reach + 1f, MaxTrace ) )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		if ( !trace.Hit || !trace.Collider.IsValid() )
			return null;

		var hit = trace.GameObject?.Components.GetInAncestorsOrSelf<Interactable>();
		return hit.IsValid() && hit.Enabled && hit.OwnsCollider( trace.Collider ) && hit.IsInRange( player.GameObject ) ? hit : null;
	}
}
