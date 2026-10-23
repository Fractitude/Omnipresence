namespace Sandbox;

/// <summary>
/// A grow tray. Plant a Static culture, wait for the game clock, harvest raw Static. What is in a tray belongs to the
/// player who planted it (<see cref="PlayerCrime"/>, saved in their <see cref="CrimeRecord"/> by <see cref="TrayId"/>), so
/// two players see their own tray in the same place. The tray has one option whose label follows the player's own state:
/// "Plant", "Growing, 1:30 left", "Harvest". Put it with an <see cref="Interactable"/> (network mode Object) and a collider.
/// <para>
/// Above the tray floats a text with the local player's own tray: "Growing 62%  0:46", or "Ready to harvest". It is made
/// in code on each machine while the game runs, is never networked or saved, and shows nothing while the tray is empty or
/// the player is far from it.
/// </para>
/// </summary>
public sealed class GrowTray : Component, IInteractionHandler
{
	const string OptionId = "tray";

	/// <summary>How far the camera can be, in world units, for the progress text to show.</summary>
	const float LabelRange = 250f;

	/// <summary>How often the tray's state is read again, in seconds. The text itself changes once a second.</summary>
	const float LabelRefresh = 0.25f;

	TextRenderer label;
	PlayerCrime crime;
	TimeUntil nextRefresh;
	long shownKey = long.MinValue;

	/// <summary>
	/// The tray's id in the save: its object's id, which is stable in the scene.
	/// </summary>
	public string TrayId => GameObject.Id.ToString();

	protected override void OnStart() =>
		InteractionSetup.EnsureOption( this, OptionId, "Grow tray", "grass", "Grow Static in a tray." );

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId )
			return null;

		var crime = (e.Interactor?.Components.Get<PlayerCrime>()) ?? PlayerCrime.Local;
		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;

		if ( crime is null )
			return new( "Grow tray", "grass", "", false );

		var (planted, readyAt) = crime.TrayState( TrayId );

		if ( !planted )
		{
			var has = inventory?.CountOf( ItemCatalog.StaticCulture ) > 0;

			var hint = has ? "Uses one culture." : "You need a Static culture.";

			return new( "Plant a Static culture", "grass", hint, has );
		}

		var left = readyAt - WorldClock.Now;

		return left > 0
			? new( $"Growing, {InteractionSetup.Countdown( left )} left", "hourglass_top", "", false )
			: new( "Harvest", "agriculture", "Gives raw Static." );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		if ( e.Option?.Id != OptionId || !Networking.IsHost )
			return;

		if ( e.Interactor?.Components.Get<PlayerCrime>() is not { } crime )
			return;

		var planted = crime.TrayState( TrayId ).Planted;
		crime.Tell( planted ? crime.Harvest( TrayId ) : crime.Plant( TrayId ) );
	}

	protected override void OnUpdate()
	{
		if ( Application.IsHeadless )
			return;

		if ( !label.IsValid() )
			label = MakeLabel();

		var camera = Scene.Camera;
		var near = camera.IsValid() && camera.WorldPosition.Distance( label.WorldPosition ) <= LabelRange;

		if ( !near )
		{
			label.Enabled = false;
			shownKey = long.MinValue;
			return;
		}

		if ( !nextRefresh )
			return;

		nextRefresh = LabelRefresh;
		RefreshLabel();
	}

	/// <summary>
	/// The text above the tray: a child that exists only on this machine and is never saved, so the placed tray and its
	/// prefab need no change. It turns to face the camera by itself.
	/// </summary>
	TextRenderer MakeLabel()
	{
		var bounds = GameObject.GetBounds();
		var position = bounds.Size.Length > 0.1f
			? new Vector3( bounds.Center.x, bounds.Center.y, bounds.Maxs.z + 8f )
			: WorldPosition + Vector3.Up * 30f;

		var child = new GameObject( false, "Grow status" )
		{
			Parent = GameObject,
			WorldPosition = position,
			NetworkMode = NetworkMode.Never,
			Flags = GameObjectFlags.NotSaved,
		};

		var text = child.AddComponent<TextRenderer>();
		text.Billboard = TextRenderer.BillboardMode.YOnly;
		text.Scale = 0.06f;
		text.FontSize = 48f;
		text.FontWeight = 600;
		text.Enabled = false;
		child.Enabled = true;

		return text;
	}

	/// <summary>
	/// Reads the local player's tray and sets the text, only when what it says changes.
	/// </summary>
	void RefreshLabel()
	{
		if ( !crime.IsValid() )
			crime = PlayerCrime.Local;

		var (planted, readyAt) = crime.IsValid() ? crime.TrayState( TrayId ) : (false, 0);

		if ( !planted )
		{
			label.Enabled = false;
			shownKey = long.MinValue;
			return;
		}

		var left = System.Math.Max( readyAt - WorldClock.Now, 0 );

		if ( label.Enabled && left == shownKey )
			return;

		shownKey = left;

		if ( left > 0 )
		{
			var percent = System.Math.Clamp( (CrimeCatalog.GrowSeconds - left) * 100 / CrimeCatalog.GrowSeconds, 0, 99 );
			label.Text = $"Growing {percent}%  {InteractionSetup.Countdown( left )}";
			label.Color = new Color( 0.85f, 0.8f, 0.55f, 0.75f );
		}
		else
		{
			label.Text = "Ready to harvest";
			label.Color = new Color( 0.55f, 1f, 0.6f, 1f );
		}

		label.Enabled = true;
	}
}
