namespace Sandbox;

/// <summary>
/// Whether a scene is far enough loaded for things saved at rest to be made in it: nothing would fall through a floor that
/// is not there yet. The saved objects used to wait a fixed second or two for this; they now wait only for the thing
/// itself, so they appear on the first tick the scene is ready and a house is never empty for longer than it must be.
/// </summary>
public static class SceneReadiness
{
	/// <summary>
	/// The longest, in seconds, a system waits for a scene to be ready before it goes ahead anyway (a scene with no
	/// colliders at all must not keep its saved objects away for ever).
	/// </summary>
	public const float MaxWait = 3f;

	/// <summary>
	/// The rule, on counts alone: every map that is in the scene has loaded, and there is something to stand on (a map, or at
	/// least one collider), or <paramref name="waited"/> has passed <see cref="MaxWait"/>.
	/// </summary>
	public static bool IsReady( int maps, int mapsLoaded, int colliders, float waited ) =>
		waited >= MaxWait || (mapsLoaded >= maps && (maps > 0 || colliders > 0));

	/// <summary>
	/// Whether this scene is ready now. <paramref name="waited"/> is how long the caller has been waiting.
	/// </summary>
	public static bool IsReady( Scene scene, float waited )
	{
		if ( !scene.IsValid() )
			return false;

		if ( waited >= MaxWait )
			return true;

		var maps = scene.GetAllComponents<MapInstance>().ToList();

		return IsReady( maps.Count, maps.Count( x => x.IsLoaded ),
			maps.Count > 0 ? 0 : scene.GetAllComponents<Collider>().Take( 1 ).Count(), waited );
	}
}
