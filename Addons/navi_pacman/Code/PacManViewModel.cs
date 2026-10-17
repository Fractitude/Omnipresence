using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.Linq;

namespace NaviPacMan;

/// <summary>
/// The Pac-Man game. Runs a step every frame the window has the player's controls, and turns the result into
/// <see cref="UiState"/>, which the board draws. Input comes from the Forward/Backward/Left/Right actions (WASD), read
/// through Navi so that in multiplayer it is the input of whoever uses the computer.
/// </summary>
public sealed class PacManViewModel
{
	// # wall, . dot, o power pellet, - ghost door, P Pac-Man start, G ghost start (first outside the house, the rest inside).
	public static readonly string[] Layout =
	{
		"###################",
		"#........#........#",
		"#o##.###.#.###.##o#",
		"#.................#",
		"#.##.#.#####.#.##.#",
		"#....#...#...#....#",
		"####.### # ###.####",
		"   #.#   G   #.#   ",
		"####.# ##-## #.####",
		"    .  #GGG#  .    ",
		"####.# ##### #.####",
		"   #.#       #.#   ",
		"####.# ##### #.####",
		"#........#........#",
		"#.##.###.#.###.##.#",
		"#o.#.....P.....#.o#",
		"##.#.#.#####.#.#.##",
		"#....#...#...#....#",
		"#.######.#.######.#",
		"#.................#",
		"###################",
	};

	public static int Width => Layout[0].Length;
	public static int Height => Layout.Length;

	static readonly Vector2Int Up = new( 0, -1 );
	static readonly Vector2Int Down = new( 0, 1 );
	static readonly Vector2Int Left = new( -1, 0 );
	static readonly Vector2Int Right = new( 1, 0 );
	static readonly Vector2Int[] Directions = { Up, Left, Down, Right }; // tie-break order of the arcade game

	// Scatter, chase, scatter, chase... in seconds. Chase lasts forever after the last entry.
	static readonly float[] ModeTimes = { 7, 20, 7, 20, 5, 20, 5 };

	class Actor
	{
		public Vector2Int Tile;
		public Vector2Int Dir;
		public float Progress; // 0..1 towards Tile + Dir

		public Vector2 Position => new( Tile.x + Dir.x * Progress, Tile.y + Dir.y * Progress );
	}

	enum GhostMode { InHouse, Leaving, Active, Eaten }

	sealed class Ghost : Actor
	{
		public string Name;
		public Vector2Int Corner;
		public Vector2Int Home;
		public GhostMode Mode;
		public float ReleaseTimer;
		public bool Frightened;
	}

	/// <summary>
	/// The app's panel: input is read through it.
	/// </summary>
	readonly Panel app;

	readonly bool[,] dots = new bool[Width, Height];
	readonly bool[,] pellets = new bool[Width, Height];
	readonly Actor pac = new();
	readonly List<Ghost> ghosts = new();

	PacManGameState state = PacManGameState.Ready;
	int score;
	int lives = 3;
	int level = 1;
	bool paused = true;

	Vector2Int pacStart;
	Vector2Int doorAbove; // tile just outside the ghost house door
	int dotsLeft;
	Vector2Int wanted;
	bool restartPressed;

	float stateTimer;
	float modeTimer;
	int modeIndex;
	float frightTimer;
	int ghostsEatenThisFright;
	float clock;

	public PacManViewModel( Panel app )
	{
		this.app = app ?? throw new System.ArgumentNullException( nameof(app) );

		FindStarts();
		NewGame();
	}

	/// <summary>
	/// What the board draws. Replaced whole on every frame the game runs.
	/// </summary>
	public PacManUiState UiState { get; private set; } = new();

	/// <summary>
	/// Called every frame by the panel. <paramref name="running"/> is false while the window doesn't have the player's
	/// controls or can't be seen, which pauses the game.
	/// </summary>
	public void Tick( bool running )
	{
		if ( !running )
		{
			if ( !paused )
			{
				paused = true;
				Publish();
			}

			return;
		}

		paused = false;

		var dt = System.MathF.Min( Time.Delta, 0.05f );
		clock += dt;

		ReadInput();

		switch ( state )
		{
			case PacManGameState.Ready:
				if ( (stateTimer -= dt) <= 0 ) state = PacManGameState.Playing;
				break;

			case PacManGameState.Playing:
				Step( dt );
				break;

			case PacManGameState.Dying:
				if ( (stateTimer -= dt) <= 0 )
				{
					if ( --lives <= 0 ) state = PacManGameState.GameOver;
					else ResetActors();
				}
				break;

			case PacManGameState.LevelClear:
				if ( (stateTimer -= dt) <= 0 )
				{
					level++;
					ResetDots();
					ResetActors();
				}
				break;

			case PacManGameState.GameOver:
				if ( restartPressed )
					NewGame();
				break;
		}

		Publish();
	}

	void FindStarts()
	{
		var homes = new List<Vector2Int>();

		for ( var y = 0; y < Height; y++ )
		{
			for ( var x = 0; x < Width; x++ )
			{
				var c = Layout[y][x];
				if ( c == '-' ) doorAbove = new Vector2Int( x, y - 1 );
				else if ( c == 'P' ) pacStart = new Vector2Int( x, y );
				else if ( c == 'G' ) homes.Add( new Vector2Int( x, y ) );
			}
		}

		var names = new[] { "blinky", "pinky", "inky", "clyde" };
		var corners = new[] { new Vector2Int( Width - 2, 0 ), new Vector2Int( 1, 0 ), new Vector2Int( Width - 2, Height - 1 ), new Vector2Int( 1, Height - 1 ) };

		// The first G (outside the house) is Blinky's.
		homes = homes.OrderBy( h => h.y ).ThenBy( h => System.Math.Abs( h.x - doorAbove.x ) ).ToList();

		for ( var i = 0; i < names.Length && i < homes.Count; i++ )
			ghosts.Add( new Ghost { Name = names[i], Corner = corners[i], Home = homes[i] } );
	}

	/// <summary>
	/// Can an actor stand on this tile? Off the left/right edge wraps (the tunnel). The door is only open to ghosts going in or out.
	/// </summary>
	static bool IsOpen( Vector2Int tile, bool throughDoor = false )
	{
		if ( tile.y < 0 || tile.y >= Height ) return false;
		var c = Layout[tile.y][Wrap( tile.x )];
		return c != '#' && (c != '-' || throughDoor);
	}

	static int Wrap( int x ) => ((x % Width) + Width) % Width;
	static Vector2Int Wrap( Vector2Int tile ) => new( Wrap( tile.x ), tile.y );
	static float DistanceSquared( Vector2Int a, Vector2Int b ) => (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y);

	void NewGame()
	{
		score = 0;
		lives = 3;
		level = 1;
		ResetDots();
		ResetActors();
	}

	void ResetDots()
	{
		dotsLeft = 0;

		for ( var y = 0; y < Height; y++ )
		{
			for ( var x = 0; x < Width; x++ )
			{
				var c = Layout[y][x];
				dots[x, y] = c is '.' or 'o';
				pellets[x, y] = c == 'o';
				if ( dots[x, y] ) dotsLeft++;
			}
		}
	}

	void ResetActors()
	{
		pac.Tile = pacStart;
		pac.Dir = Left;
		pac.Progress = 0;
		wanted = Left;

		for ( var i = 0; i < ghosts.Count; i++ )
		{
			var g = ghosts[i];
			g.Tile = g.Home;
			g.Progress = 0;
			g.Frightened = false;
			g.Mode = i == 0 ? GhostMode.Active : GhostMode.InHouse;
			g.Dir = i == 0 ? Left : Up;
			g.ReleaseTimer = i * 3f;
		}

		modeTimer = 0;
		modeIndex = 0;
		frightTimer = 0;
		state = PacManGameState.Ready;
		stateTimer = 2f;
		Publish();
	}

	void ReadInput()
	{
		if ( Navi.InputDown( app, "Forward" ) ) wanted = Up;
		else if ( Navi.InputDown( app, "Backward" ) ) wanted = Down;
		else if ( Navi.InputDown( app, "Left" ) ) wanted = Left;
		else if ( Navi.InputDown( app, "Right" ) ) wanted = Right;

		// A fresh press, so a key still held from the last game doesn't restart straight away.
		restartPressed = new[] { "Jump", "Forward", "Backward", "Left", "Right" }.Any( x => Navi.InputPressed( app, x ) );
	}

	void Step( float dt )
	{
		UpdateGhostMode( dt );
		MovePac( dt );

		foreach ( var g in ghosts )
			MoveGhost( g, dt );

		CheckCollisions();

		if ( dotsLeft == 0 )
		{
			state = PacManGameState.LevelClear;
			stateTimer = 2f;
		}
	}

	float PacSpeed => 6f + level * 0.2f;
	float GhostSpeed => 5f + level * 0.25f;
	float FrightTime => System.MathF.Max( 2f, 7f - level );

	void MovePac( float dt )
	{
		// Turning around is allowed mid-tile.
		if ( pac.Dir != Vector2Int.Zero && wanted == -pac.Dir )
		{
			pac.Tile = Wrap( pac.Tile + pac.Dir );
			pac.Dir = wanted;
			pac.Progress = 1 - pac.Progress;
		}

		if ( pac.Dir == Vector2Int.Zero )
		{
			if ( IsOpen( pac.Tile + wanted ) ) pac.Dir = wanted;
			else return;
		}

		pac.Progress += PacSpeed * dt;

		while ( pac.Progress >= 1 )
		{
			pac.Progress -= 1;
			pac.Tile = Wrap( pac.Tile + pac.Dir );
			Eat( pac.Tile );

			if ( IsOpen( pac.Tile + wanted ) )
			{
				pac.Dir = wanted;
			}
			else if ( !IsOpen( pac.Tile + pac.Dir ) )
			{
				pac.Dir = Vector2Int.Zero;
				pac.Progress = 0;
				break;
			}
		}
	}

	void Eat( Vector2Int tile )
	{
		if ( !dots[tile.x, tile.y] )
			return;

		dots[tile.x, tile.y] = false;
		dotsLeft--;

		if ( pellets[tile.x, tile.y] )
		{
			score += 50;
			frightTimer = FrightTime;
			ghostsEatenThisFright = 0;

			foreach ( var g in ghosts.Where( g => g.Mode == GhostMode.Active ) )
			{
				g.Frightened = true;
				Reverse( g );
			}
		}
		else
		{
			score += 10;
		}
	}

	void UpdateGhostMode( float dt )
	{
		if ( frightTimer > 0 )
		{
			frightTimer -= dt;

			if ( frightTimer <= 0 )
				foreach ( var g in ghosts ) g.Frightened = false;

			return; // the scatter/chase clock pauses while ghosts are frightened
		}

		if ( modeIndex >= ModeTimes.Length )
			return;

		modeTimer += dt;
		if ( modeTimer < ModeTimes[modeIndex] )
			return;

		modeTimer = 0;
		modeIndex++;

		foreach ( var g in ghosts.Where( g => g.Mode == GhostMode.Active ) )
			Reverse( g );
	}

	bool Chasing => modeIndex % 2 == 1 || modeIndex >= ModeTimes.Length;

	static void Reverse( Actor a )
	{
		if ( a.Dir == Vector2Int.Zero ) return;
		a.Tile = Wrap( a.Tile + a.Dir );
		a.Dir = -a.Dir;
		a.Progress = 1 - a.Progress;
	}

	void MoveGhost( Ghost g, float dt )
	{
		if ( g.Mode == GhostMode.InHouse )
		{
			if ( (g.ReleaseTimer -= dt) <= 0 ) g.Mode = GhostMode.Leaving;
			return;
		}

		var speed = g.Mode switch
		{
			GhostMode.Eaten => GhostSpeed * 2f,
			GhostMode.Leaving => GhostSpeed * 0.6f,
			_ when g.Frightened => GhostSpeed * 0.6f,
			_ => GhostSpeed,
		};

		if ( g.Dir == Vector2Int.Zero )
			g.Dir = ChooseDirection( g );

		g.Progress += speed * dt;

		while ( g.Progress >= 1 )
		{
			g.Progress -= 1;
			g.Tile = Wrap( g.Tile + g.Dir );

			if ( g.Mode == GhostMode.Leaving && g.Tile == doorAbove )
			{
				g.Mode = GhostMode.Active;
				g.Frightened = false;
			}
			else if ( g.Mode == GhostMode.Eaten && g.Tile == g.Home )
			{
				g.Mode = GhostMode.Leaving;
				g.Frightened = false;
			}

			g.Dir = ChooseDirection( g );
		}
	}

	Vector2Int ChooseDirection( Ghost g )
	{
		var throughDoor = g.Mode is GhostMode.Leaving or GhostMode.Eaten;
		var options = Directions
			.Where( d => IsOpen( g.Tile + d, throughDoor ) )
			.ToList();

		// Ghosts never turn back, unless it's the only way.
		var forward = options.Where( d => d != -g.Dir ).ToList();
		if ( forward.Count > 0 ) options = forward;
		if ( options.Count == 0 ) return Vector2Int.Zero;

		if ( g.Mode == GhostMode.Active && g.Frightened )
			return options[Game.Random.Int( 0, options.Count - 1 )];

		var target = Target( g );
		return options.OrderBy( d => DistanceSquared( Wrap( g.Tile + d ), target ) ).First();
	}

	Vector2Int Target( Ghost g )
	{
		if ( g.Mode == GhostMode.Leaving ) return doorAbove;
		if ( g.Mode == GhostMode.Eaten ) return g.Home;
		if ( !Chasing ) return g.Corner;

		var facing = pac.Dir == Vector2Int.Zero ? wanted : pac.Dir;

		switch ( g.Name )
		{
			case "pinky":
				return pac.Tile + facing * 4;

			case "inky":
				var blinky = ghosts[0].Tile;
				var ahead = pac.Tile + facing * 2;
				return ahead + (ahead - blinky);

			case "clyde":
				return DistanceSquared( g.Tile, pac.Tile ) > 64 ? pac.Tile : g.Corner;

			default:
				return pac.Tile;
		}
	}

	void CheckCollisions()
	{
		foreach ( var g in ghosts )
		{
			if ( g.Mode is GhostMode.InHouse or GhostMode.Eaten )
				continue;

			if ( (g.Position - pac.Position).Length > 0.6f )
				continue;

			if ( g.Frightened )
			{
				g.Mode = GhostMode.Eaten;
				g.Frightened = false;
				score += 200 << ghostsEatenThisFright;
				ghostsEatenThisFright = System.Math.Min( ghostsEatenThisFright + 1, 3 );
			}
			else
			{
				state = PacManGameState.Dying;
				stateTimer = 1.5f;
				return;
			}
		}
	}

	/// <summary>
	/// Turn the game as it is now into <see cref="UiState"/>.
	/// </summary>
	void Publish()
	{
		var facing = pac.Dir == Vector2Int.Zero ? wanted : pac.Dir;
		var facingName = facing == Up ? "up" : facing == Down ? "down" : facing == Right ? "right" : "left";
		var blinking = frightTimer > 0 && frightTimer < 2f && (int)(clock * 6) % 2 == 0;
		var hidden = state is PacManGameState.Dying or PacManGameState.LevelClear;

		var cells = new char[Width * Height];
		for ( var y = 0; y < Height; y++ )
			for ( var x = 0; x < Width; x++ )
				cells[y * Width + x] = !dots[x, y] ? ' ' : pellets[x, y] ? 'o' : '.';

		var pacPosition = Drawn( pac );

		UiState = UiState with
		{
			Revision = UiState.Revision + 1,
			State = state,
			Score = score,
			Lives = lives,
			Level = level,
			Paused = paused,
			Dots = new string( cells ),
			Flash = state == PacManGameState.LevelClear && (int)(stateTimer * 4) % 2 == 0,
			PelletsOn = (int)(clock * 3) % 2 == 0,
			Player = new PacManPlayer( pacPosition.x, pacPosition.y, facingName,
				state == PacManGameState.Playing && pac.Dir != Vector2Int.Zero && (int)(clock * 12) % 2 == 0,
				state == PacManGameState.Dying ),
			Ghosts = ghosts.Select( g =>
			{
				var position = Drawn( g );
				return new PacManGhost( g.Name, position.x, position.y, g.Frightened, g.Frightened && blinking,
					g.Mode == GhostMode.Eaten, hidden );
			} ).ToArray(),
		};
	}

	/// <summary>
	/// Where an actor is drawn, in tiles. Inside the tunnel, the half-step across the wrap would draw on the wrong side.
	/// </summary>
	static Vector2 Drawn( Actor a )
	{
		var pos = a.Position;
		if ( pos.x < -0.5f ) pos.x += Width;
		if ( pos.x > Width - 0.5f ) pos.x -= Width;
		return pos;
	}
}
