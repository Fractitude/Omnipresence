namespace Sandbox;

/// <summary>
/// A grow tray. Plant a drug (the player chooses which in the planting panel, <see cref="PlantingMenu"/>), wait for the
/// game clock, harvest its raw. A tray holds one drug at a time. What is in a tray belongs to the player who planted it
/// (<see cref="PlayerCrime"/>, saved in their <see cref="CrimeRecord"/> by <see cref="TrayId"/>), so two players see
/// their own tray in the same place. The tray has two options, of which one shows at a time, following the player's
/// own state: "Plant" while it is empty (it runs on the player's own client and only opens the panel; the panel's
/// Plant button asks the host with <see cref="PlayerCrime.RequestPlant"/>), and "Growing Haze, 0:25 left" then "Harvest
/// Haze" once planted (this one runs on the host). Put it with an <see cref="Interactable"/> (network mode Object) and
/// a collider.
/// <para>
/// Above the tray floats a text with the local player's own tray: "Growing Haze 62%  0:46", or "Haze ready to
/// harvest". It is made in code on each machine while the game runs, is never networked or saved, and shows nothing
/// while the tray is empty or the player is far from it.
/// </para>
/// </summary>
public sealed class GrowTray : Component, IInteractionHandler
{
	const string OptionId = "tray";
	const string PlantId = "plant";

	/// <summary>How close, in world units, a player must be to a tray for the host to plant in it.</summary>
	public const float Reach = 300f;

	/// <summary>
	/// Whether the tray with this id stands within <see cref="Reach"/> of this position.
	/// </summary>
	public static bool IsNear( string trayId, Vector3 position ) =>
		Game.ActiveScene?.GetAllComponents<GrowTray>()
			.Any( x => x.TrayId == trayId && x.WorldPosition.Distance( position ) <= Reach ) ?? false;

	/// <summary>How far the camera can be, in world units, for the progress text to show.</summary>
	const float LabelRange = 250f;

	/// <summary>How often the tray's state is read again, in seconds. The text itself changes once a second.</summary>
	const float LabelRefresh = 0.25f;

	TextRenderer label;
	PlayerCrime crime;
	TimeUntil nextRefresh;
	string shownKey = "";

	/// <summary>
	/// The tray's id in the save: a bought tray's permanent <see cref="PlacedObject.ObjectId"/>, else its object's
	/// id, which is stable in the scene.
	/// </summary>
	public string TrayId => PlacedObject.IdOf( this );

	protected override void OnStart()
	{
		InteractionSetup.EnsureOption( this, OptionId, "Grow tray", "grass", "Harvest what grows in the tray." );
		var plant = InteractionSetup.EnsureOption( this, PlantId, "Plant", "grass", "Choose what to plant." );

		// The press only opens the panel on the player's own screen.
		if ( plant is not null )
			plant.RunOn = InteractionSide.Local;
	}

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e )
	{
		if ( e.Option?.Id is not (OptionId or PlantId) )
			return null;

		var crime = (e.Interactor?.Components.Get<PlayerCrime>()) ?? PlayerCrime.Local;
		var inventory = e.Interactor?.Components.Get<PlayerInventory>() ?? PlayerInventory.Local;

		if ( crime is null )
			return new( e.Option.Id == OptionId ? "" : "Grow tray", "grass", "", false );

		var (planted, readyAt, kind) = crime.TrayState( TrayId );

		// One row at a time: Plant while the tray is empty, the growing and harvest row once it is planted. An empty
		// label hides a row.
		if ( e.Option.Id == PlantId )
		{
			if ( planted )
				return new( "" );

			var has = ProductCatalog.Products.Any( x => inventory?.CountOf( x.Seed.Id ) > 0 );

			return has
				? new( "Plant", "grass", "Choose what to plant." )
				: new( "Plant", "grass", "You have no seeds. Buy some in Hush.", false );
		}

		if ( !planted )
			return new( "" );

		var product = ProductCatalog.OfTray( kind );
		var left = readyAt - WorldClock.Now;

		return left > 0
			? new( $"Growing {product.Name}, {InteractionSetup.Countdown( left )} left", "hourglass_top", "", false )
			: new( $"Harvest {product.Name}", "agriculture", $"Gives {product.HarvestRaw} {product.Raw.Name}." );
	}

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		// Planting only opens the panel, on the player's own screen. The panel's button asks the host.
		if ( e.Option?.Id == PlantId )
		{
			if ( !Application.IsHeadless )
				PlantingMenu.Open( this );

			return;
		}

		if ( e.Option?.Id != OptionId || !Networking.IsHost )
			return;

		if ( e.Interactor?.Components.Get<PlayerCrime>() is not { } crime )
			return;

		if ( crime.TrayState( TrayId ).Planted )
			crime.Tell( crime.Harvest( TrayId ) );
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
			shownKey = "";
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

		var (planted, readyAt, kind) = crime.IsValid() ? crime.TrayState( TrayId ) : (false, 0, "");

		if ( !planted )
		{
			label.Enabled = false;
			shownKey = "";
			return;
		}

		var left = System.Math.Max( readyAt - WorldClock.Now, 0 );
		var key = $"{kind}/{left}";

		if ( label.Enabled && key == shownKey )
			return;

		shownKey = key;

		var product = ProductCatalog.OfTray( kind );

		if ( left > 0 )
		{
			var percent = System.Math.Clamp( (product.GrowSeconds - left) * 100 / product.GrowSeconds, 0, 99 );
			label.Text = $"Growing {product.Name} {percent}%  {InteractionSetup.Countdown( left )}";
			label.Color = new Color( 0.85f, 0.8f, 0.55f, 0.75f );
		}
		else
		{
			label.Text = $"{product.Name} ready to harvest";
			label.Color = new Color( 0.55f, 1f, 0.6f, 1f );
		}

		label.Enabled = true;
	}
}
