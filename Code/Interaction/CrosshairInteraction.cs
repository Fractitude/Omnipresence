namespace Sandbox;

/// <summary>
/// Picks what the player uses from the crosshair, in third person. <see cref="PlayerController"/> traces its own look
/// from the eye, and in third person the camera sits behind and beside the eye, so the engine's ray meets the world at
/// a different point than the middle of the screen: an object under the crosshair can be ignored while one beside it is
/// picked. This traces the crosshair's own line and hovers and presses what it finds. In first person the engine's ray
/// is the crosshair, so it keeps the job.
/// </summary>
public sealed class CrosshairInteraction : GameObjectSystem<CrosshairInteraction>
{
	/// <summary>
	/// What this system last told the pressables the player is looking at, if anything.
	/// </summary>
	Component hovered;

	/// <summary>
	/// True while this system has the controller's own pressing turned off, so it can turn it back on.
	/// </summary>
	bool tookOver;

	public CrosshairInteraction( Scene scene ) : base( scene )
	{
		// Late in the frame, after the controller has done its own
		Listen( Stage.FinishUpdate, 0, Update, "CrosshairInteraction" );
	}

	void Update()
	{
		var controller = Players.LocalController;

		if ( !controller.IsValid() || !controller.ThirdPerson )
		{
			HandBack( controller );
			return;
		}

		var camera = controller.Scene.Camera;
		if ( !camera.IsValid() )
			return;

		// The engine's own pick would be the eye's, and it would fight this one
		if ( !tookOver )
		{
			tookOver = controller.EnablePressing;
			controller.EnablePressing = false;
		}

		var ray = CrosshairRay( controller, camera );
		var e = new Component.IPressable.Event( controller, ray );
		var found = FindPressable( controller, ray, e );

		SetHovered( found, e );

		// Use holds the press while the button is down, as it does in first person
		var useButton = controller.UseButton;

		if ( controller.Pressed.IsValid() )
		{
			if ( !Input.Down( useButton ) )
				controller.StopPressing();
		}
		else if ( found.IsValid() && Input.Pressed( useButton ) )
		{
			controller.StartPressing( found );
		}
	}

	/// <summary>
	/// The crosshair's line: the camera's own aim, which is what the middle of the screen shows. In third person the
	/// camera is behind and beside the eye, so the line starts beside the eye where the camera sits beside it, and the
	/// distance the player can reach is still measured from their eye, as in first person.
	/// </summary>
	static Ray CrosshairRay( PlayerController controller, CameraComponent camera )
	{
		var eye = controller.EyePosition;
		var forward = camera.WorldRotation.Forward;
		var offset = camera.WorldPosition - eye;

		return new Ray( eye + offset - forward * offset.Dot( forward ), forward );
	}

	/// <summary>
	/// Tells the pressables what the player is looking at: the new one starts being looked at, the old one stops.
	/// </summary>
	void SetHovered( Component found, in Component.IPressable.Event e )
	{
		if ( found == hovered )
		{
			if ( found is Component.IPressable still )
				still.Look( e );

			return;
		}

		if ( hovered is Component.IPressable was )
			was.Blur( e );

		hovered = found;

		if ( found is Component.IPressable now )
		{
			now.Hover( e );
			now.Look( e );
		}
	}

	/// <summary>
	/// Gives the job back to the engine: stops looking at anything, and turns the controller's own pressing on again.
	/// </summary>
	void HandBack( PlayerController controller )
	{
		if ( !tookOver )
			return;

		tookOver = false;
		SetHovered( null, new Component.IPressable.Event( controller, null ) );

		if ( !controller.IsValid() )
			return;

		controller.StopPressing();
		controller.EnablePressing = true;
	}

	/// <summary>
	/// What is on the crosshair's line and can be pressed right now, or null.
	/// </summary>
	Component FindPressable( PlayerController controller, Ray ray, in Component.IPressable.Event e )
	{
		var reach = controller.ReachLength;
		var tr = Scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * reach )
			.IgnoreGameObjectHierarchy( controller.GameObject )
			.Run();

		if ( !tr.Hit || controller.EyePosition.Distance( tr.HitPosition ) > reach )
			return null;

		// The pressable is on what was hit or on a parent of it (an Interactable may sit above the collider), and only one
		// that says it can be pressed right now counts, as with the engine's own look.
		for ( var go = tr.GameObject; go.IsValid() && go is not Sandbox.Scene; go = go.Parent )
		{
			foreach ( var pressable in go.Components.GetAll<Component.IPressable>( FindMode.EnabledInSelf ) )
			{
				if ( pressable is Component component && pressable.CanPress( e ) )
					return component;
			}
		}

		return null;
	}
}
