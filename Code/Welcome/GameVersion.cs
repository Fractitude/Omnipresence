namespace Sandbox;

/// <summary>
/// One release in the changelog: the version number it carries and what changed.
/// </summary>
public sealed record ChangelogEntry( int Version, string Title, string[] Changes );

/// <summary>
/// The game's version number and its changelog. The Welcome screen compares <see cref="Current"/> with the last version
/// the player saw and lists what is newer. **Every finished task adds one to <see cref="Current"/> and one entry on top
/// of <see cref="Changelog"/>** (see AGENTS.md).
/// </summary>
public static class GameVersion
{
	/// <summary>
	/// The version of this build. A whole number, one more for every finished task.
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
