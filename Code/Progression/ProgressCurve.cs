namespace Sandbox;

/// <summary>
/// How much XP each level takes. Level 1 starts at 0 XP, level 2 at 100, level 3 at 400, level 4 at 900, and so on.
/// </summary>
public static class ProgressCurve
{
	public const int MaxLevel = 100;
	const int Scale = 100;

	public static long MaxXp => XpForLevel( MaxLevel );

	/// <summary>
	/// The XP a player needs to be at <paramref name="level"/>.
	/// </summary>
	public static long XpForLevel( int level )
	{
		level = System.Math.Clamp( level, 1, MaxLevel );
		return (long)Scale * (level - 1) * (level - 1);
	}

	public static int LevelFor( long xp )
	{
		var level = (int)System.Math.Sqrt( System.Math.Max( xp, 0 ) / (double)Scale ) + 1;
		return System.Math.Min( level, MaxLevel );
	}

	/// <summary>
	/// How far through the current level, from 0 to 1. A player at the top level is always at 1.
	/// </summary>
	public static float Fraction( long xp )
	{
		var level = LevelFor( xp );
		if ( level >= MaxLevel )
			return 1f;

		var start = XpForLevel( level );
		return (float)(xp - start) / (XpForLevel( level + 1 ) - start);
	}
}
