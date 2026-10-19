namespace Sandbox;

/// <summary>
/// How much hacking XP each hacking level takes. Level 1 starts at 0, level 2 at 50, level 3 at 200, level 4 at 450, and
/// so on. Separate from <see cref="ProgressCurve"/>: hacking XP comes only from hacking and doesn't move the character
/// level.
/// </summary>
public static class HackingCurve
{
	public const int MaxLevel = 30;
	const int Scale = 50;

	public static long MaxXp => XpForLevel( MaxLevel );

	/// <summary>
	/// The hacking XP a player needs to be at <paramref name="level"/>.
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

	/// <summary>
	/// What the alert of an operation is multiplied by: a better hacker is quieter, 2% per level above the first, down to
	/// 70%.
	/// </summary>
	public static double NoiseScale( int level ) => 1.0 - System.Math.Min( System.Math.Max( level - 1, 0 ), 15 ) * 0.02;
}
