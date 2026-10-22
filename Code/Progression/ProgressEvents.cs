namespace Sandbox;

/// <summary>
/// What part of a player's progress changed. A change can be more than one.
/// </summary>
[System.Flags]
public enum ProgressChange
{
	None = 0,

	/// <summary>XP, hacking XP, dollars or credits.</summary>
	Stats = 1 << 0,

	/// <summary>A payment was added to the history.</summary>
	Payments = 1 << 1,

	/// <summary>A dead server was breached, or a blueprint found.</summary>
	Intrusions = 1 << 2,

	/// <summary>An operation was finished.</summary>
	Operations = 1 << 3,

	/// <summary>The hardware inventory, the installed hardware or the shipments.</summary>
	Hardware = 1 << 4,

	All = Stats | Payments | Intrusions | Operations | Hardware,
}

public static partial class Global
{
	/// <summary>
	/// Progress events, posted to the whole scene on the host when a player's <see cref="PlayerProgress"/> loads or
	/// changes. Implement it on a component to react to progress without polling it.
	/// </summary>
	public interface IProgressEvents : ISceneEvent<IProgressEvents>
	{
		/// <summary>
		/// The player's record has come from the store, and the progress can be read and changed.
		/// </summary>
		void OnProgressLoaded( PlayerProgress progress ) { }

		/// <summary>
		/// The player's progress changed. <paramref name="change"/> says which parts.
		/// </summary>
		void OnProgressChanged( PlayerProgress progress, ProgressChange change ) { }
	}
}
