namespace Sandbox;

/// <summary>
/// Helpers for components that answer to an <see cref="Interactable"/> and need it to have certain options, so an object
/// placed in the editor works without setting its options by hand.
/// </summary>
public static class InteractionSetup
{
	/// <summary>
	/// Make sure the <see cref="Interactable"/> on or under this handler has an option with this id, running on the host.
	/// An Interactable still on its untouched default option has that replaced; otherwise the option is added. Run it
	/// the same way on every machine (e.g. from <c>OnStart</c>): the host takes an option as its place in the list. Does
	/// nothing in the editor, so the scene is not changed by it.
	/// </summary>
	public static InteractionOption EnsureOption(
		Component handler, string id, string label, string icon, string description = "" )
	{
		if ( !Game.IsPlaying )
			return null;

		if ( handler.Components.Get<Interactable>( FindMode.EnabledInSelfAndDescendants ) is not { } interactable )
			return null;

		if ( interactable.Options.FirstOrDefault( x => x?.Id == id ) is { } existing )
			return existing;

		var option = new InteractionOption
		{
			Id = id,
			Label = label,
			Icon = icon,
			Description = description,
			RunOn = InteractionSide.Host,
		};

		var untouched = interactable.Options is [{ Id: "", Label: "Use", OnInteract: null }];

		interactable.Options = untouched ? [option] : [.. interactable.Options, option];
		return option;
	}

	/// <summary>
	/// A countdown as the player reads it, like "2:05".
	/// </summary>
	public static string Countdown( long seconds )
	{
		seconds = System.Math.Max( seconds, 0 );
		return $"{seconds / 60}:{seconds % 60:00}";
	}
}
