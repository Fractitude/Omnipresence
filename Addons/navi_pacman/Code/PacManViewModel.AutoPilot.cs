using Sandbox;
using System.Collections.Generic;

namespace NaviPacMan;

/// <summary>
/// The autopilot: the "Auto" button hands Pac-Man's steering to it. It plays to win, which is to clear every maze.
///
/// It knows the ghosts' rules because it uses them: to decide, it plays the game forward in its head. It tries every
/// route Pac-Man could take for the next few seconds, moving copies of the ghosts with the very code that moves the
/// real ones (<see cref="AdvanceGhost"/>, <see cref="ChooseDirection"/>, <see cref="Target"/> and the scatter/chase and
/// power pellet clocks). So it knows that:
/// <list type="bullet">
/// <item>Blinky aims at Pac-Man, Pinky four tiles ahead of him, Inky at the far side of the tile two ahead of him from
/// Blinky, and Clyde at him until he is eight tiles away, then at his own corner.</item>
/// <item>In scatter, every ghost aims at its corner; the modes switch on a clock, and every switch turns the ghosts around.</item>
/// <item>A ghost never turns back by itself, and picks the way that ends nearest its target at every junction. That is
/// what makes a ghost's route predictable, and a power pellet's about-face the only surprise.</item>
/// <item>Frightened ghosts wander at random and are slow: they are food, and only while the pellet lasts.</item>
/// <item>Eaten ghosts are harmless eyes racing home; ghosts in the house or leaving it are dangerous once they are out.</item>
/// </list>
/// A route that gets Pac-Man caught is thrown out, however many dots it passes. Of the rest, it takes the one that
/// earns the most: dots soon, ghosts while frightened, power pellets only when a ghost is close enough to need one, and
/// a clear maze above all. When every route ends in a catch, it takes the one that lasts longest.
/// </summary>
public sealed partial class PacManViewModel
{
	/// <summary>The action the Auto button sends.</summary>
	public const string AutoAction = "auto";

	/// <summary>Seconds on Game Over before the autopilot starts the next game.</summary>
	const float AutoRestartTime = 3f;

	/// <summary>How many tiles of route to try, and the most routes' steps to try before it stops looking.</summary>
	const int LookAhead = 14, MaxNodes = 3000;

	/// <summary>Seconds between new plans, besides every time Pac-Man reaches a new tile.</summary>
	const float ReplanTime = 0.1f;

	/// <summary>
	/// How close a ghost has to get to count as a catch. The game's is 0.6: the rest is room for the look-ahead being a
	/// few steps and frames off.
	/// </summary>
	const float HitRadius = 0.8f;

	const float DeadValue = -100000f;
	const float ReverseCost = 25f;
	const float LevelClearValue = 3000f;

	bool auto;
	float replanTimer;
	Vector2Int plannedTile;
	int nodes;

	// Dots the route being tried has eaten so far. Marked on the way in, cleared on the way out.
	readonly bool[,] routeEaten = new bool[Width, Height];

	// Per depth of the route, the copies of the ghosts as they would be there.
	Ghost[][] ghostCopies;

	// Scratch for the search for the nearest dot.
	readonly int[] queue = new int[Width * Height];
	readonly int[] queueDistance = new int[Width * Height];
	readonly int[] seen = new int[Width * Height];
	int seenStamp;

	/// <summary>The route's world: the clocks, and what the route has eaten.</summary>
	struct World
	{
		public Timers Timers;
		public int DotsEaten;
		public int GhostsEaten;
	}

	void ToggleAuto()
	{
		auto = !auto;
		ResetPilot();
	}

	void ResetPilot()
	{
		replanTimer = 0;
		plannedTile = new Vector2Int( -1, -1 );
	}

	/// <summary>
	/// Steer for this frame: plan again when Pac-Man is on a new tile or the last plan is old.
	/// </summary>
	void Steer( float dt )
	{
		if ( (replanTimer -= dt) > 0 && pac.Tile == plannedTile )
			return;

		Plan();
	}

	void Plan()
	{
		replanTimer = ReplanTime;
		plannedTile = pac.Tile;
		nodes = 0;

		if ( ghostCopies is null )
		{
			ghostCopies = new Ghost[LookAhead + 3][];

			for ( var i = 0; i < ghostCopies.Length; i++ )
			{
				ghostCopies[i] = new Ghost[ghosts.Count];
				for ( var j = 0; j < ghosts.Count; j++ )
					ghostCopies[i][j] = new Ghost();
			}
		}

		CopyGhosts( ghosts, ghostCopies[0] );
		var world = new World { Timers = timers, GhostsEaten = ghostsEatenThisFright };

		// Standing still: just pick the best way to set off.
		if ( pac.Dir == Vector2Int.Zero )
		{
			Explore( pac.Tile, Vector2Int.Zero, 0f, 0, world, out var start );
			if ( start != Vector2Int.Zero ) wanted = start;
			return;
		}

		// Keep going and turn at the next tile, or about face right now (Pac-Man may turn around mid-tile). Turning
		// around costs a little, so a plan doesn't flip back and forth over nothing.
		var ahead = Branch( pac.Tile, pac.Dir, pac.Progress, 0f, 0, world, out var turn );
		var back = Branch( Wrap( pac.Tile + pac.Dir ), -pac.Dir, 1f - pac.Progress, 0f, 0, world, out _ ) - ReverseCost;

		if ( back > ahead ) wanted = -pac.Dir;
		else wanted = turn != Vector2Int.Zero ? turn : pac.Dir;
	}

	static void CopyGhosts( IReadOnlyList<Ghost> from, Ghost[] to )
	{
		for ( var i = 0; i < to.Length; i++ )
			to[i].CopyFrom( from[i] );
	}

	/// <summary>
	/// Pac-Man stands on <paramref name="tile"/> at <paramref name="time"/> seconds from now, having come by
	/// <paramref name="arrived"/>, and the ghosts in <c>ghostCopies[depth]</c> are where they would be. Returns the worth
	/// of the best way on, and which way that is in <paramref name="best"/>.
	/// </summary>
	float Explore( Vector2Int tile, Vector2Int arrived, float time, int depth, World world, out Vector2Int best )
	{
		best = Vector2Int.Zero;
		nodes++;

		if ( depth >= LookAhead || nodes > MaxNodes )
			return Leaf( tile, world, ghostCopies[depth] );

		var bestValue = float.NegativeInfinity;

		// Pac-Man doesn't turn back by choice on a route, except where there is no other way.
		for ( var allowBack = false; bestValue == float.NegativeInfinity; allowBack = true )
		{
			foreach ( var d in Directions )
			{
				if ( (!allowBack && d == -arrived) || !IsOpen( tile + d ) )
					continue;

				var value = Branch( tile, d, 0f, time, depth, world, out _ );

				if ( value > bestValue )
				{
					bestValue = value;
					best = d;
				}
			}

			if ( allowBack )
				break;
		}

		return bestValue == float.NegativeInfinity ? Leaf( tile, world, ghostCopies[depth] ) : bestValue;
	}

	/// <summary>
	/// What going from <paramref name="from"/> in direction <paramref name="d"/> is worth, from
	/// <paramref name="startFrac"/> of the way along it, with everything after it. Moves the ghosts of
	/// <c>ghostCopies[depth]</c> into <c>ghostCopies[depth + 1]</c> on the way.
	/// </summary>
	float Branch( Vector2Int from, Vector2Int d, float startFrac, float time, int depth, World world, out Vector2Int next )
	{
		next = Vector2Int.Zero;

		var ghostsHere = ghostCopies[depth + 1];
		CopyGhosts( ghostCopies[depth], ghostsHere );

		var arrival = time + (1f - startFrac) / PacSpeed;
		var gain = 0f;

		if ( !Travel( ref world, ghostsHere, from, d, startFrac, ref gain ) )
			return DeadValue + arrival * 1000f; // caught: the later the better, if every way is

		var tile = Wrap( from + d );
		var ate = Arrive( ref world, ghostsHere, tile, ref gain );

		// Dots sooner are worth more.
		var value = gain / (1f + 0.12f * arrival);

		if ( dotsLeft - world.DotsEaten <= 0 ) value += LevelClearValue;
		else value += Explore( tile, d, arrival, depth + 1, world, out next );

		if ( ate ) routeEaten[tile.x, tile.y] = false;

		return value;
	}

	/// <summary>
	/// Pac-Man goes along an edge of the maze, and the ghosts go with him, by the game's own rules. False if a ghost
	/// catches him; if he catches a frightened ghost, that is gain.
	/// </summary>
	bool Travel( ref World world, Ghost[] all, Vector2Int from, Vector2Int d, float startFrac, ref float gain )
	{
		const int steps = 3;
		var dt = (1f - startFrac) / PacSpeed / steps;

		for ( var i = 1; i <= steps; i++ )
		{
			StepTimers( ref world.Timers, all, dt );

			var context = new GhostContext( from, d, all[0].Tile, IsChasing( world.Timers ) );
			foreach ( var g in all )
				AdvanceGhost( g, dt, context );

			var frac = startFrac + (1f - startFrac) * i / steps;
			var pacPosition = new Vector2( from.x + d.x * frac, from.y + d.y * frac );

			foreach ( var g in all )
			{
				if ( g.Mode is GhostMode.InHouse or GhostMode.Eaten )
					continue;

				if ( Gap( g.Position, pacPosition ) > HitRadius )
					continue;

				if ( !g.Frightened )
					return false;

				g.Mode = GhostMode.Eaten;
				g.Frightened = false;
				gain += 0.7f * (200 << world.GhostsEaten); // a frightened ghost's way is a guess: not quite certain
				world.GhostsEaten = System.Math.Min( world.GhostsEaten + 1, 3 );
			}
		}

		return true;
	}

	/// <summary>
	/// Pac-Man reaches a tile: eat what is there. True if he ate something, which the caller puts back after the route.
	/// </summary>
	bool Arrive( ref World world, Ghost[] all, Vector2Int tile, ref float gain )
	{
		if ( !dots[tile.x, tile.y] || routeEaten[tile.x, tile.y] )
			return false;

		routeEaten[tile.x, tile.y] = true;
		world.DotsEaten++;

		if ( !pellets[tile.x, tile.y] )
		{
			gain += 10f;
			return true;
		}

		// A power pellet is the way out of a tight spot: worth a lot when a ghost is near, and not worth wasting.
		var pressed = false;

		if ( world.Timers.Fright <= 0 )
		{
			var position = new Vector2( tile.x, tile.y );

			foreach ( var g in all )
				if ( g.Mode == GhostMode.Active && Gap( g.Position, position ) < 8f )
					pressed = true;
		}

		gain += pressed ? 60f : 8f;
		world.GhostsEaten = 0;
		PowerUp( ref world.Timers, all );
		return true;
	}

	/// <summary>
	/// What a route is worth where it stops looking: how far the next dot is, how close the ghosts are.
	/// </summary>
	float Leaf( Vector2Int tile, in World world, Ghost[] all )
	{
		var value = -0.8f * DistanceToDot( tile );
		var position = new Vector2( tile.x, tile.y );

		foreach ( var g in all )
		{
			if ( g.Mode is GhostMode.InHouse or GhostMode.Eaten )
				continue;

			var gap = Gap( g.Position, position );

			if ( g.Frightened )
			{
				// Worth chasing while the pellet lasts long enough to get there.
				if ( world.Timers.Fright > gap / PacSpeed * 1.5f ) value += 150f / (1f + gap);
			}
			else if ( gap < 6f )
			{
				value -= (6f - gap) * 12f;
			}
		}

		return value;
	}

	/// <summary>
	/// Steps to the nearest dot the route hasn't eaten.
	/// </summary>
	int DistanceToDot( Vector2Int start )
	{
		seenStamp++;

		var head = 0;
		var tail = 0;
		var first = start.y * Width + start.x;
		queue[tail] = first;
		queueDistance[tail++] = 0;
		seen[first] = seenStamp;

		while ( head < tail )
		{
			var index = queue[head];
			var distance = queueDistance[head++];
			var tile = new Vector2Int( index % Width, index / Width );

			if ( dots[tile.x, tile.y] && !routeEaten[tile.x, tile.y] )
				return distance;

			foreach ( var d in Directions )
			{
				var next = Wrap( tile + d );
				if ( !IsOpen( next ) ) continue;

				var nextIndex = next.y * Width + next.x;
				if ( seen[nextIndex] == seenStamp ) continue;

				seen[nextIndex] = seenStamp;
				queue[tail] = nextIndex;
				queueDistance[tail++] = distance + 1;
			}
		}

		return 0;
	}

	/// <summary>
	/// How far apart two points of the maze are in a straight line, the tunnel counted.
	/// </summary>
	static float Gap( Vector2 a, Vector2 b )
	{
		var dx = System.MathF.Abs( a.x - b.x );
		dx = System.MathF.Min( dx, Width - dx );
		var dy = a.y - b.y;
		return System.MathF.Sqrt( dx * dx + dy * dy );
	}
}
