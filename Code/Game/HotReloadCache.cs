namespace Sandbox;

/// <summary>
/// Keeps a table built from the game's own types for a short while, and builds it again after that. A static field that
/// holds the assembly's own record type survives a hot reload with its old values (an edit to the table never shows
/// until the editor restarts), and building the table on every call allocates each time. Nothing tells the game code
/// when a hot reload happens (the engine's hotload callbacks only reach the types that changed), so the cache simply
/// expires: a table is at most <see cref="Lifetime"/> seconds old, and an edit shows within that long. There is no
/// version number to remember.
/// </summary>
public static class HotReloadCache
{
	/// <summary>Seconds a built table is kept.</summary>
	public const float Lifetime = 1f;

	// Not readonly, and holding only BCL types, so a hot reload leaves it alone.
	static Dictionary<string, (RealTimeSince Age, object Value)> entries = new();

	/// <summary>
	/// The value for a key, built by <paramref name="build"/> the first time and again once it is older than
	/// <see cref="Lifetime"/>. Pass a static method, not a lambda that captures anything: the builder runs from the
	/// current code.
	/// </summary>
	public static T Get<T>( string key, System.Func<T> build )
	{
		if ( entries.TryGetValue( key, out var entry ) && entry.Age < Lifetime && entry.Value is T cached )
			return cached;

		var built = build();
		entries[key] = (0, built);
		return built;
	}
}
