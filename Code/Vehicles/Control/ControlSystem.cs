/// <summary>
/// Lets players drive things from a seat. Every fixed update on the host, this finds who is sitting in each
/// <see cref="BaseChair"/> and calls <see cref="IPlayerControllable.OnControl"/> on the object it belongs to.
/// </summary>
public sealed class ControlSystem : GameObjectSystem<ControlSystem>
{
	private static Dictionary<BaseChair, RealTimeSince> _occupiedSince = new();

	public ControlSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 10, OnTick, "ControlSystem" );
	}

	private void OnTick()
	{
		if ( Scene != Game.ActiveScene ) return;
		if ( !Networking.IsHost ) return;

		// Whoever sat down first claims the contraption: a second seat on it does nothing.
		var driven = new HashSet<GameObject>();

		foreach ( var chair in GetSortedSeats() )
		{
			if ( driven.Contains( chair.GameObject.Root ) ) continue;

			var linked = new LinkedGameObjectBuilder();
			linked.AddConnected( chair.GameObject );

			if ( linked.Objects.Any( driven.Contains ) ) continue;
			driven.UnionWith( linked.Objects );

			RunControl( chair, linked );
		}
	}

	private IEnumerable<BaseChair> GetSortedSeats()
	{
		var chairs = Scene.GetAll<BaseChair>().ToList();

		foreach ( var chair in chairs )
		{
			if ( !chair.IsValid() || !chair.IsOccupied )
				_occupiedSince.Remove( chair );
			else
				_occupiedSince.TryAdd( chair, 0 );
		}

		return chairs
			.Where( chair => chair.IsValid() && chair.IsOccupied )
			.OrderBy( chair => (float)_occupiedSince.GetValueOrDefault( chair, default ) );
	}

	private static void RunControl( BaseChair chair, LinkedGameObjectBuilder linked )
	{
		var player = chair.GetOccupant()?.GetComponent<PlayerController>();
		if ( !player.IsValid() ) return;

		var connection = player.Network?.Owner;
		if ( connection is null ) return;

		using var scope = ControlContext.Push( player, connection );

		foreach ( var gameObject in linked.Objects )
		{
			foreach ( var component in gameObject.GetComponentsInChildren<Component>() )
			{
				if ( !component.IsValid() ) continue;
				if ( component is not IPlayerControllable controllable ) continue;
				if ( !controllable.CanControl( player ) ) continue;

				controllable.OnControl();
			}
		}
	}
}
