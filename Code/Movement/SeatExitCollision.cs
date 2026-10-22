using Sandbox.Movement;

/// <summary>
/// Keeps another player's collider off for a moment after they get up from a seat. <see cref="BaseChair.Eject"/> moves
/// the player to the exit point only on their own machine; everywhere else the parent change arrives before the new
/// position, so <see cref="SitMoveMode"/> turned the collider back on inside the seat. On the host that collider shoved
/// the vehicle (and the player with it) into the air.
/// </summary>
public sealed class SeatExitCollision : GameObjectSystem<SeatExitCollision>
{
	/// <summary>
	/// How long the collider stays off after getting up: longer than the network interpolation, so the player has
	/// reached the exit point by then.
	/// </summary>
	private const float GhostTime = 0.5f;

	private Dictionary<PlayerController, RealTimeSince> _leftSeat = new();
	private HashSet<PlayerController> _seated = new();

	public SeatExitCollision( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 0, OnTick, "SeatExitCollision.Start" );
		Listen( Stage.FinishFixedUpdate, 0, OnTick, "SeatExitCollision.Finish" );
	}

	private void OnTick()
	{
		foreach ( var player in Scene.GetAll<PlayerController>() )
		{
			if ( !player.IsValid() || !player.IsProxy ) continue;

			var seated = player.GetComponentInParent<ISitTarget>() is not null;

			if ( seated )
			{
				_seated.Add( player );
				_leftSeat.Remove( player );
				continue;
			}

			if ( _seated.Remove( player ) )
				_leftSeat[player] = 0;

			if ( !_leftSeat.TryGetValue( player, out var since ) ) continue;

			var ghost = since < GhostTime;
			SetCollision( player, !ghost );

			if ( !ghost )
				_leftSeat.Remove( player );
		}

		_seated.RemoveWhere( x => !x.IsValid() );

		foreach ( var player in _leftSeat.Keys.Where( x => !x.IsValid() ).ToList() )
			_leftSeat.Remove( player );
	}

	private static void SetCollision( PlayerController player, bool enabled )
	{
		if ( player.Body.IsValid() )
			player.Body.Enabled = enabled;

		if ( player.ColliderObject.IsValid() )
			player.ColliderObject.Enabled = enabled;
	}
}
