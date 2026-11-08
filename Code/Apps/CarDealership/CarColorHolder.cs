namespace OmnipresenceApps;

/// <summary>
/// A colour the engine's colour picker (<c>ColorPickerControl</c>) edits. The picker works on a serialized property, so the
/// panel keeps one of these, points the picker at its <see cref="Value"/> and reads it back to see what was picked.
/// </summary>
public sealed class CarColorHolder
{
	// No alpha and no brightness above 1: a body is opaque.
	[ColorUsage( false, false )]
	public Color Value { get; set; } = Color.White;
}
