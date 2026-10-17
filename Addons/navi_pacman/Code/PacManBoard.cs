using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System.Linq;

namespace NaviPacMan;

/// <summary>
/// Draws the Pac-Man maze and a <see cref="PacManUiState"/>. Builds its tiles once, then moves the actor panels and
/// flips classes every frame, so nothing is rebuilt while the game runs. It holds no game: the view model does.
/// </summary>
public sealed class PacManBoard : Panel
{
	public const int Cell = 12;

	static int Width => PacManViewModel.Width;
	static int Height => PacManViewModel.Height;
	static string[] Layout => PacManViewModel.Layout;

	/// <summary>
	/// How fast drawn actors catch up with where the state says they are, per second, when <see cref="Draw"/> smooths.
	/// </summary>
	const float SmoothRate = 30f;

	readonly Panel[,] dotPanels = new Panel[Width, Height];
	readonly Panel pacPanel;
	readonly Panel pacMouth;
	readonly Dictionary<string, Panel> ghostPanels = new();

	// Where each actor is drawn now, in tiles, by name ("" for Pac-Man).
	readonly Dictionary<string, Vector2> drawn = new();

	string drawnDots;

	public PacManBoard()
	{
		AddClass( "board" );
		Style.Width = Length.Pixels( Width * Cell );
		Style.Height = Length.Pixels( Height * Cell );

		for ( var y = 0; y < Height; y++ )
		{
			for ( var x = 0; x < Width; x++ )
			{
				var c = Layout[y][x];

				if ( c == '#' )
				{
					var wall = AddTile( "wall", x, y );
					if ( !IsWall( x, y - 1 ) ) wall.AddClass( "edge-top" );
					if ( !IsWall( x, y + 1 ) ) wall.AddClass( "edge-bottom" );
					if ( !IsWall( x - 1, y ) ) wall.AddClass( "edge-left" );
					if ( !IsWall( x + 1, y ) ) wall.AddClass( "edge-right" );
				}
				else if ( c == '-' )
				{
					AddTile( "door", x, y );
				}
				else if ( c is '.' or 'o' )
				{
					dotPanels[x, y] = AddTile( c == 'o' ? "pellet" : "dot", x, y );
				}
			}
		}

		pacPanel = Add.Panel( "pacman" );
		pacMouth = pacPanel.Add.Panel( "mouth" );

		foreach ( var name in new[] { "blinky", "pinky", "inky", "clyde" } )
		{
			var panel = Add.Panel( $"ghost {name}" );
			panel.Add.Panel( "eye left" );
			panel.Add.Panel( "eye right" );
			ghostPanels[name] = panel;
		}
	}

	Panel AddTile( string className, int x, int y )
	{
		var tile = Add.Panel( className );
		tile.Style.Left = Length.Pixels( x * Cell );
		tile.Style.Top = Length.Pixels( y * Cell );
		return tile;
	}

	static bool IsWall( int x, int y ) => x >= 0 && y >= 0 && x < Width && y < Height && Layout[y][x] == '#';

	/// <summary>
	/// Draw a frame of the game. With <paramref name="smooth"/>, actors glide to where the state puts them instead of
	/// jumping there: a viewer gets the host's frames less often than it draws.
	/// </summary>
	public void Draw( PacManUiState state, bool smooth )
	{
		SetClass( "flash", state.Flash );

		if ( state.Dots is { } dots && dots.Length == Width * Height && dots != drawnDots )
		{
			drawnDots = dots;

			for ( var y = 0; y < Height; y++ )
				for ( var x = 0; x < Width; x++ )
					dotPanels[x, y]?.SetClass( "eaten", dots[y * Width + x] is not ('.' or 'o') );
		}

		for ( var y = 0; y < Height; y++ )
			for ( var x = 0; x < Width; x++ )
				if ( Layout[y][x] == 'o' ) dotPanels[x, y]?.SetClass( "blink", state.PelletsOn );

		if ( state.Player is { } player )
		{
			Place( pacPanel, "", new Vector2( player.X, player.Y ), smooth );
			pacPanel.SetClass( "dying", player.Dying );

			foreach ( var facing in new[] { "up", "down", "left", "right" } )
				pacPanel.SetClass( $"face-{facing}", player.Facing == facing );

			pacMouth.SetClass( "closed", player.MouthClosed );
		}

		foreach ( var ghost in state.Ghosts ?? [] )
		{
			if ( ghost.Name is null || !ghostPanels.TryGetValue( ghost.Name, out var panel ) )
				continue;

			Place( panel, ghost.Name, new Vector2( ghost.X, ghost.Y ), smooth );
			panel.SetClass( "frightened", ghost.Frightened );
			panel.SetClass( "blink", ghost.Blink );
			panel.SetClass( "eaten", ghost.Eaten );
			panel.SetClass( "hidden", ghost.Hidden );
		}
	}

	void Place( Panel panel, string key, Vector2 target, bool smooth )
	{
		var position = target;

		// Far off means a jump, such as through the tunnel or back to the start: no gliding across the maze.
		if ( smooth && drawn.TryGetValue( key, out var current ) && (current - target).Length < 2f )
			position = Vector2.Lerp( current, target, System.MathF.Min( 1f, Time.Delta * SmoothRate ) );

		drawn[key] = position;
		panel.Style.Left = Length.Pixels( position.x * Cell );
		panel.Style.Top = Length.Pixels( position.y * Cell );
	}
}
