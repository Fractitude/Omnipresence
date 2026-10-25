namespace Sandbox;

/// <summary>
/// One release in the changelog: the version number it carries and what changed.
/// </summary>
public sealed record ChangelogEntry( int Version, string Title, string[] Changes );

/// <summary>
/// The Welcome screen's version and its changelog. The screen opens by itself for a player who has not seen
/// <see cref="Current"/> yet, and lists the entries newer than the version they saw. **Only the host raises
/// <see cref="Current"/>, by hand, when the Welcome screen's contents change** and returning players should read it
/// again (see AGENTS.md). Finishing a task does not.
/// </summary>
public static class GameVersion
{
	/// <summary>
	/// The version of the Welcome screen's contents. A whole number, raised by hand by the host.
	/// </summary>
	public const int Current = 1;

	/// <summary>
	/// What changed, newest first. Write for players: what they can do or see now, not how the code works.
	/// </summary>
	public static IReadOnlyList<ChangelogEntry> Changelog { get; } = new ChangelogEntry[]
	{
		new( 1, "Welcome screen",
		[
			"A welcome screen opens when you join. It tells you about the game, the rules and what changed since you last played.",
			"Type welcome in the console to open it again.",
		] ),
	};

	/// <summary>
	/// The entries newer than a version, newest first.
	/// </summary>
	public static IEnumerable<ChangelogEntry> Since( int version ) => Changelog.Where( x => x.Version > version );
}
