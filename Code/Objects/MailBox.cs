using Sandbox;

/// <summary>
/// A mailbox whose flag shows whether it has mail. Using it (<see cref="Interactable"/>) puts the flag up or down. Set the
/// Interactable to run on the host, since <see cref="HasMail"/> is set there and synced to everyone else.
/// </summary>
public sealed class MailBox : Component, Component.ExecuteInEditor, IInteractionHandler
{
	/// <summary>Whether the mailbox has any mail in it. Raises the flag when true.</summary>
	[Property, Sync] public bool HasMail { get; set; }

	GameObject _flag;

	InteractionInfo? IInteractionHandler.Describe( InteractionEvent e ) =>
		new( HasMail ? "Put flag down" : "Put flag up", "flag" );

	void IInteractionHandler.OnInteract( InteractionEvent e )
	{
		HasMail = !HasMail;
	}

	protected override void OnUpdate()
	{
		if ( !_flag.IsValid() )
			_flag = GameObject.GetAllObjects( true ).FirstOrDefault( o => o != GameObject && o.Name == "Flag" );

		if ( !_flag.IsValid() )
			return;

		_flag.LocalRotation = Rotation.FromPitch( HasMail ? -90f : 0f );
	}
}
