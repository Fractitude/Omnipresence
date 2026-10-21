namespace Sandbox;

/// <summary>
/// Makes a GameObject something the player can look at and press Use on. Put it on any GameObject that has a collider the
/// player's look can hit (or on its parent). The label shows under the crosshair (<c>Crosshair.razor</c>).
/// <para>
/// What happens on use: the <see cref="IInteractionHandler"/> components on this GameObject and its children are told, then
/// the <see cref="OnInteract"/> actions (wired in the editor) run, then <see cref="Interacted"/> (for code). The label can be
/// plain properties, set by code, or taken from a handler's <see cref="IInteractionHandler.Describe"/>.
/// </para>
/// It is built on the engine's <see cref="Component.IPressable"/>, so <see cref="PlayerController"/> finds and presses it.
/// </summary>
public sealed class Interactable : Component, Component.IPressable
{
	/// <summary>
	/// The hint the player sees, e.g. "Open". Empty hides the hint, and the object can still be used.
	/// </summary>
	[Property] public string Label { get; set; } = "Use";

	/// <summary>
	/// Material icon shown beside the label.
	/// </summary>
	[Property] public string Icon { get; set; } = "";

	/// <summary>
	/// A second line under the label.
	/// </summary>
	[Property] public string Description { get; set; } = "";

	/// <summary>
	/// Turn off to stop it being used. The hint stays, greyed out. Set it from code to lock and unlock.
	/// </summary>
	[Property] public bool Available { get; set; } = true;

	/// <summary>
	/// Where the handlers and events run. See <see cref="InteractionSide"/>.
	/// </summary>
	[Property] public InteractionSide RunOn { get; set; } = InteractionSide.Local;

	/// <summary>
	/// Seconds before it can be used again, for the player who used it (<see cref="InteractionSide.Local"/>) or for everyone
	/// (<see cref="InteractionSide.Host"/>).
	/// </summary>
	[Property, Range( 0f, 10f )] public float Cooldown { get; set; }

	/// <summary>
	/// The farthest a player can be, in world units, for the host to accept their press. The look range is the player's; this
	/// is a generous limit for <see cref="InteractionSide.Host"/> so a modified client can't use things from across the map.
	/// </summary>
	[Property, Group( "Host" )] public float HostRange { get; set; } = 400f;

	/// <summary>
	/// The GameObject whose <see cref="IInteractionHandler"/> components answer to this Interactable (it and its children).
	/// Empty searches near this one instead: see <see cref="GetHandlers"/>.
	/// </summary>
	[Property, Group( "Events" )] public GameObject HandlerObject { get; set; }

	/// <summary>
	/// Runs when the player uses it, with the player's GameObject (null if it can't be told). Connect any component's method
	/// to it in the editor; one that doesn't need the player leaves the argument out.
	/// </summary>
	[Property, Group( "Events" )] public System.Action<GameObject> OnInteract { get; set; }

	/// <summary>
	/// Raised in code when the player uses it, after the handlers.
	/// </summary>
	public event System.Action<InteractionEvent> Interacted;

	float nextUse;
	bool warnedNotNetworked;

	/// <summary>
	/// The handlers that answer to this Interactable: those on <see cref="HandlerObject"/> and its children if one is set.
	/// Otherwise those on this GameObject and its children, or, if there are none, those on the nearest parent that has any.
	/// So the Interactable can sit on a child (where the collider is) while the component that handles it is on the object's
	/// root.
	/// </summary>
	List<IInteractionHandler> GetHandlers()
	{
		if ( HandlerObject.IsValid() )
			return HandlerObject.Components.GetAll<IInteractionHandler>( FindMode.EnabledInSelfAndDescendants ).ToList();

		var own = Components.GetAll<IInteractionHandler>( FindMode.EnabledInSelfAndDescendants ).ToList();
		if ( own.Count > 0 )
			return own;

		for ( var parent = GameObject.Parent; parent.IsValid() && parent is not Sandbox.Scene; parent = parent.Parent )
		{
			var found = parent.Components.GetAll<IInteractionHandler>( FindMode.EnabledInSelf ).ToList();
			if ( found.Count > 0 )
				return found;
		}

		return own;
	}

	/// <summary>
	/// What the local player would see right now: the Interactable's own values, a handler's description in place of them,
	/// and whether it can be used.
	/// </summary>
	public InteractionInfo Describe( GameObject interactor )
	{
		var e = new InteractionEvent( this, interactor );
		var info = new InteractionInfo( Label, Icon, Description );
		var handlers = GetHandlers();

		foreach ( var handler in handlers )
		{
			if ( handler.Describe( e ) is { } described )
			{
				info = described;
				break;
			}
		}

		var enabled = info.Enabled && Available && Time.Now >= nextUse && handlers.All( x => x.CanInteract( e ) );
		return info with { Enabled = enabled };
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e ) => Describe( e.Source?.GameObject ).Enabled;

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		var interactor = e.Source?.GameObject;
		if ( !Describe( interactor ).Enabled )
			return false;

		nextUse = Time.Now + Cooldown;

		if ( RunOn == InteractionSide.Host && Networking.IsActive && !Networking.IsHost )
		{
			if ( !GameObject.Network.Active && !warnedNotNetworked )
			{
				warnedNotNetworked = true;
				Log.Warning( $"Interactable: '{GameObject.Name}' runs on the host but isn't a network object, so the press is lost. "
					+ "Set its GameObject's Network Mode to Object." );
			}

			HostInteract();
			return true;
		}

		Execute( new InteractionEvent( this, interactor ) );
		return true;
	}

	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( Component.IPressable.Event e )
	{
		var info = Describe( e.Source?.GameObject );
		if ( string.IsNullOrEmpty( info.Label ) )
			return null;

		return new Component.IPressable.Tooltip( info.Label, info.Icon, info.Description, info.Enabled, this );
	}

	/// <summary>
	/// On the host: a client pressed Use. The caller is the network's, never the client's word: its player must be near
	/// enough and the object must be usable for it.
	/// </summary>
	[Rpc.Host]
	void HostInteract()
	{
		var caller = Rpc.Caller;
		var player = Scene.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => caller is null ? !x.IsProxy : x.Network.Owner == caller )?.GameObject;

		if ( !player.IsValid() || player.WorldPosition.Distance( WorldPosition ) > HostRange || !Describe( player ).Enabled )
			return;

		nextUse = Time.Now + Cooldown;
		Execute( new InteractionEvent( this, player ) );
	}

	/// <summary>
	/// Use it from code, as if the player pressed Use. Runs here, on the side you call it on, and skips the checks.
	/// </summary>
	public void Execute( InteractionEvent e )
	{
		foreach ( var handler in GetHandlers() )
			Guard( () => handler.OnInteract( e ) );

		Guard( () => OnInteract?.Invoke( e.Interactor ) );
		Guard( () => Interacted?.Invoke( e ) );
	}

	/// <summary>
	/// One failing handler mustn't stop the others.
	/// </summary>
	void Guard( System.Action action )
	{
		try
		{
			action();
		}
		catch ( System.Exception ex )
		{
			Log.Warning( ex, $"Interactable '{GameObject.Name}': a handler threw: {ex.Message}" );
		}
	}
}
