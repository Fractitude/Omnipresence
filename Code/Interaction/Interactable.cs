namespace Sandbox;

/// <summary>
/// Makes a GameObject something the player can look at and press Use on. Put it on any GameObject that has a collider the
/// player's look can hit (or on its parent). While they look at it, <c>Crosshair.razor</c> lists its <see cref="Options"/>
/// under the crosshair; the mouse wheel picks one and Use does it.
/// <para>
/// What happens on use: the <see cref="IInteractionHandler"/> components that answer to this Interactable are told, then the
/// option's <see cref="InteractionOption.OnInteract"/> action (wired in the editor) runs, then <see cref="Interacted"/> (for
/// code). An option's label can be plain properties, set by code, or taken from a handler's
/// <see cref="IInteractionHandler.Describe"/>.
/// </para>
/// It is built on the engine's <see cref="Component.IPressable"/>, so <see cref="PlayerController"/> finds and presses it.
/// </summary>
public sealed class Interactable : Component, Component.IPressable
{
	/// <summary>
	/// What the player can do with it, in the order they are listed. Change it from code the same way on every machine: the
	/// host takes the option as its place in this list.
	/// </summary>
	[Property] public List<InteractionOption> Options { get; set; } = [new()];

	/// <summary>
	/// The farthest the player's eye can be, in world units, from this object's colliders (or its origin, if it has none).
	/// Beyond it the prompt is hidden and the press does nothing. The host checks every press from a client again, from the
	/// player's eye: it must be within this range and see the object, with nothing in the way. The engine finds what the
	/// player looks at within <see cref="PlayerController.ReachLength"/>, so that is the most this can reach. 0 turns the
	/// checks off.
	/// </summary>
	[Property] public float Range { get; set; } = 50f;

	/// <summary>
	/// The GameObject whose <see cref="IInteractionHandler"/> components answer to this Interactable (it and its children).
	/// Empty searches near this one instead: see <see cref="GetHandlers"/>.
	/// </summary>
	[Property, Group( "Events" )] public GameObject HandlerObject { get; set; }

	/// <summary>
	/// Raised in code when the player uses an option, after the handlers.
	/// </summary>
	public event System.Action<InteractionEvent> Interacted;

	/// <summary>
	/// The Interactable the local player looks at, or null. The crosshair draws its menu.
	/// </summary>
	public static Interactable Focused { get; private set; }

	/// <summary>
	/// A row of the menu the local player sees.
	/// </summary>
	public readonly record struct Entry( int Index, InteractionOption Option, InteractionInfo Info );

	/// <summary>
	/// Index in <see cref="Options"/> of the option the wheel has picked, for the local player.
	/// </summary>
	int selected;

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
	/// The player's eye, or their feet if they have no controller.
	/// </summary>
	static Vector3 EyeOf( GameObject player ) => player.Components.Get<PlayerController>()?.EyePosition ?? player.WorldPosition;

	/// <summary>
	/// The player's eye is within <see cref="Range"/> of this object. Cheap: what the crosshair asks every frame.
	/// </summary>
	bool IsInRange( GameObject player )
	{
		if ( Range <= 0f )
			return true;

		if ( !player.IsValid() )
			return false;

		var eye = EyeOf( player );
		var nearest = Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants )
			.Select( x => x.FindClosestPoint( eye ).Distance( eye ) )
			.DefaultIfEmpty( WorldPosition.Distance( eye ) )
			.Min();

		return nearest <= Range;
	}

	/// <summary>
	/// The player is within <see cref="Range"/> and can see the object: a ray from their eye to one of its colliders hits that
	/// collider first, so a wall in between blocks it. What a press has to pass, on the host as well as the client.
	/// </summary>
	bool CanReach( GameObject player )
	{
		if ( Range <= 0f )
			return true;

		if ( !IsInRange( player ) )
			return false;

		var colliders = Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants ).ToList();
		if ( colliders.Count == 0 )
			return true;

		var eye = EyeOf( player );
		foreach ( var collider in colliders )
		{
			var point = collider.FindClosestPoint( eye );
			var direction = point - eye;
			if ( direction.Length < 1f )
				return true;

			var trace = Scene.Trace.Ray( eye, point + direction.Normal * 2f )
				.IgnoreGameObjectHierarchy( player )
				.Run();

			if ( trace.Hit && colliders.Contains( trace.Collider ) )
				return true;
		}

		return false;
	}

	/// <summary>
	/// The player who looks at or presses this: the engine says so, else the local player.
	/// </summary>
	GameObject Who( Component.IPressable.Event e ) => e.Source?.GameObject ?? LocalPlayer;

	/// <summary>
	/// The local player's GameObject, or null.
	/// </summary>
	GameObject LocalPlayer => Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy )?.GameObject;

	/// <summary>
	/// What the local player would see for an option right now: the option's own values, a handler's description in place of
	/// them, and whether it can be used.
	/// </summary>
	public InteractionInfo Describe( int index, GameObject interactor = null )
	{
		if ( index < 0 || index >= Options.Count || Options[index] is not { } option )
			return new InteractionInfo( "", Enabled: false );

		var e = new InteractionEvent( this, interactor, option, index );
		var info = new InteractionInfo( option.Label, option.Icon, option.Description );
		var handlers = GetHandlers();

		foreach ( var handler in handlers )
		{
			if ( handler.Describe( e ) is { } described )
			{
				info = described;
				break;
			}
		}

		var enabled = info.Enabled && option.Available && Time.Now >= option.NextUse && handlers.All( x => x.CanInteract( e ) );
		return info with { Enabled = enabled };
	}

	/// <summary>
	/// The rows the local player sees: every option that has a label, in order.
	/// </summary>
	public List<Entry> GetEntries()
	{
		var player = LocalPlayer;
		var entries = new List<Entry>();

		for ( var i = 0; i < Options.Count; i++ )
		{
			if ( Options[i] is null )
				continue;

			var info = Describe( i, player );
			if ( !string.IsNullOrEmpty( info.Label ) )
				entries.Add( new Entry( i, Options[i], info ) );
		}

		return entries;
	}

	/// <summary>
	/// Which row is picked: the wheel's choice, or the first row if that one is hidden.
	/// </summary>
	public int GetSelected( List<Entry> entries ) =>
		entries.Any( x => x.Index == selected ) ? selected : entries.Count > 0 ? entries[0].Index : -1;

	/// <summary>
	/// The mouse wheel moves the pick up or down the rows, stopping at the first and last. Not while a menu has the cursor.
	/// </summary>
	void ScrollSelection()
	{
		var wheel = Input.MouseWheel.y;
		if ( wheel == 0f )
			return;

		if ( Mouse.Visibility == MouseVisibility.Visible )
			return;

		var entries = GetEntries();
		if ( entries.Count < 2 )
			return;

		var position = entries.FindIndex( x => x.Index == GetSelected( entries ) );
		position = System.Math.Clamp( position + (wheel > 0f ? -1 : 1), 0, entries.Count - 1 );
		selected = entries[position].Index;
	}

	void Component.IPressable.Hover( Component.IPressable.Event e ) => selected = 0;

	void Component.IPressable.Look( Component.IPressable.Event e )
	{
		// Too far to offer: the prompt hides, and the press does nothing (CanPress).
		if ( IsInRange( Who( e ) ) )
			Focused = this;
		else if ( Focused == this )
			Focused = null;
	}

	protected override void OnUpdate()
	{
		if ( Focused == this )
			ScrollSelection();
	}

	void Component.IPressable.Blur( Component.IPressable.Event e )
	{
		if ( Focused == this )
			Focused = null;
	}

	protected override void OnDisabled()
	{
		if ( Focused == this )
			Focused = null;
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e )
	{
		if ( !IsInRange( Who( e ) ) )
			return false;

		var entries = GetEntries();
		return GetSelected( entries ) is var index and >= 0 && Describe( index, Who( e ) ).Enabled;
	}

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		var index = GetSelected( GetEntries() );
		var interactor = Who( e );

		if ( index < 0 || !Describe( index, interactor ).Enabled )
			return false;

		var option = Options[index];
		if ( !CanReach( interactor ) )
			return false;

		option.NextUse = Time.Now + option.Cooldown;

		if ( option.RunOn == InteractionSide.Host && Networking.IsActive && !Networking.IsHost )
		{
			if ( !GameObject.Network.Active && !warnedNotNetworked )
			{
				warnedNotNetworked = true;
				Log.Warning( $"Interactable: '{GameObject.Name}' runs on the host but isn't a network object, so the press is lost. "
					+ "Set its GameObject's Network Mode to Object." );
			}

			HostInteract( index );
			return true;
		}

		Execute( new InteractionEvent( this, interactor, option, index ) );
		return true;
	}

	/// <summary>
	/// The picked row, for tools that read the engine's tooltips instead of drawing the menu.
	/// </summary>
	Component.IPressable.Tooltip? Component.IPressable.GetTooltip( Component.IPressable.Event e )
	{
		var entries = GetEntries();
		if ( !IsInRange( Who( e ) ) || entries.FirstOrDefault( x => x.Index == GetSelected( entries ) ) is not { Option: not null } entry )
			return null;

		return new Component.IPressable.Tooltip( entry.Info.Label, entry.Info.Icon, entry.Info.Description, entry.Info.Enabled, this );
	}

	/// <summary>
	/// On the host: a client pressed Use on an option. The caller is the network's, never the client's word: its player must
	/// be in range and see the object, the option must exist and be usable for it, and the option must be one that runs on the host.
	/// </summary>
	[Rpc.Host]
	void HostInteract( int index )
	{
		var caller = Rpc.Caller;
		var player = Scene.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => caller is null ? !x.IsProxy : x.Network.Owner == caller )?.GameObject;

		if ( !player.IsValid() || index < 0 || index >= Options.Count || Options[index] is not { RunOn: InteractionSide.Host } option )
			return;

		if ( !CanReach( player ) || !Describe( index, player ).Enabled )
			return;

		option.NextUse = Time.Now + option.Cooldown;
		Execute( new InteractionEvent( this, player, option, index ) );
	}

	/// <summary>
	/// Use an option from code, as if the player pressed Use on it. Runs here, on the side you call it on, and skips the
	/// checks.
	/// </summary>
	public void Execute( InteractionEvent e )
	{
		foreach ( var handler in GetHandlers() )
			Guard( () => handler.OnInteract( e ) );

		Guard( () => e.Option?.OnInteract?.Invoke( e.Interactor ) );
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
