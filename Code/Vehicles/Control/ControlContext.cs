/// <summary>
/// Who is driving while <see cref="IPlayerControllable.OnControl"/> runs, so <see cref="ClientInput"/> can read that
/// player's keys.
/// </summary>
public static class ControlContext
{
	internal readonly record struct State( PlayerController Player, Connection Connection );

	[ThreadStatic]
	private static State _current;

	/// <summary>
	/// The player in control of whatever is currently running, or null.
	/// </summary>
	public static PlayerController Player => _current.Player;

	internal static Connection Connection => _current.Connection;

	/// <summary>
	/// Restores whoever was in control before when disposed, so nested pushes unwind cleanly.
	/// </summary>
	internal readonly struct Scope : IDisposable
	{
		private readonly State _previous;

		internal Scope( State state )
		{
			_previous = _current;
			_current = state;
		}

		public void Dispose()
		{
			_current = _previous;
		}
	}

	internal static Scope Push( PlayerController player, Connection connection = null ) => new( new State( player, connection ) );
}
