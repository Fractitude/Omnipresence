using System.Collections.Generic;

namespace NaviPacMan;

public enum PacManGameState { Ready, Playing, Dying, LevelClear, GameOver }

/// <summary>
/// Pac-Man as drawn: where he is, in tiles, and how he looks.
/// </summary>
public sealed record PacManPlayer( float X, float Y, string Facing, bool MouthClosed, bool Dying );

/// <summary>
/// A ghost as drawn. <see cref="Name"/> is also its class: blinky, pinky, inky or clyde.
/// </summary>
public sealed record PacManGhost( string Name, float X, float Y, bool Frightened, bool Blink, bool Eaten, bool Hidden );

/// <summary>
/// Everything the Pac-Man panel draws, one frame of the game. Replaced whole by <see cref="PacManViewModel"/> on every
/// frame the game runs. Small on purpose: in multiplayer the host sends it to every client.
/// </summary>
public sealed record PacManUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static PacManUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes.</summary>
	public int Revision { get; init; }

	public PacManGameState State { get; init; }
	public int Score { get; init; }
	public int Lives { get; init; } = 3;
	public int Level { get; init; } = 1;

	/// <summary>The game waits, because the window doesn't have the player's controls.</summary>
	public bool Paused { get; init; } = true;

	/// <summary>
	/// What is left to eat, row by row (<see cref="PacManViewModel.Width"/> per row): '.' a dot, 'o' a power pellet,
	/// anything else nothing. Null before the game starts.
	/// </summary>
	public string Dots { get; init; }

	/// <summary>The maze flashes, between levels.</summary>
	public bool Flash { get; init; }

	/// <summary>The power pellets are lit: they blink.</summary>
	public bool PelletsOn { get; init; }

	public PacManPlayer Player { get; init; }
	public IReadOnlyList<PacManGhost> Ghosts { get; init; } = [];
}
