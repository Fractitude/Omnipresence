namespace OmnipresenceApps;

/// <summary>
/// What The Wired panel draws. Replaced whole by <see cref="TheWiredViewModel"/> on every change.
/// </summary>
public sealed record TheWiredUiState
{
	/// <summary>What a viewer draws until the host's state arrives.</summary>
	public static TheWiredUiState Empty { get; } = new();

	/// <summary>Goes up whenever anything else here changes. The panel's BuildHash is this.</summary>
	public int Revision { get; init; }

	public string Status { get; init; } = "";

	/// <summary>While the computer is offline: why. The window says so instead of the status.</summary>
	public string OfflineHint { get; init; }

	/// <summary>The button's label, or null for no button.</summary>
	public string ButtonText { get; init; }
}
