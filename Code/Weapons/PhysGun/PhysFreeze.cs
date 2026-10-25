namespace Sandbox;

/// <summary>
/// How the Physgun freezes an object, in one place, and the record of it. Freezing is only <c>Rigidbody.MotionEnabled =
/// false</c> on the host (the object is then neither pulled by gravity nor pushed), and unfreezing sets it back; the
/// Physgun calls <see cref="Set"/> for both. When the object is one the game saves and makes again (a dropped
/// <see cref="WorldItem"/>, a bought <see cref="PlacedObject"/>), the new state and where the object stands are written
/// to its registry file, so a restart makes it again frozen, in the same place. A restore calls <see cref="Apply"/>, which
/// is the same assignment the Physgun makes, so a restored object behaves exactly like one frozen by hand and the Physgun
/// can unfreeze it later.
/// </summary>
/// <remarks>
/// Host only. Things the game does not save (the buggy, scene props) are frozen and unfrozen the same way but nothing is
/// recorded: they come back as their scene or prefab has them.
/// </remarks>
public static class PhysFreeze
{
	/// <summary>
	/// Freeze or unfreeze a body as the Physgun does, and record it for a saved object. Host only.
	/// </summary>
	public static void Set( Rigidbody body, bool frozen )
	{
		if ( !body.IsValid() )
			return;

		body.MotionEnabled = !frozen;

		if ( !Networking.IsActive || Networking.IsHost )
			Record( body.GameObject, frozen );
	}

	/// <summary>
	/// Put a freshly made object into a saved state: frozen or not, without recording anything. Host only.
	/// </summary>
	public static void Apply( GameObject go, bool frozen )
	{
		if ( !frozen || !go.IsValid() )
			return;

		if ( go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) is { } body )
			body.MotionEnabled = false;
	}

	/// <summary>
	/// Whether the object's body is frozen.
	/// </summary>
	public static bool IsFrozen( GameObject go ) =>
		go.IsValid() && go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants ) is { MotionEnabled: false };

	static void Record( GameObject go, bool frozen )
	{
		if ( !go.IsValid() )
			return;

		var at = go.WorldPosition;
		var angles = go.WorldRotation.Angles();

		// An item the map placed by hand is a normal saved item from the moment it is held or frozen.
		go.Components.Get<WorldItem>()?.Adopt();

		if ( go.Components.Get<PlacedObject>( FindMode.EverythingInSelfAndParent ) is { } placed
		     && !string.IsNullOrEmpty( placed.ObjectId ) )
		{
			PlacedObjectRegistry.SetFrozen( placed.ObjectId, frozen, at, angles );
			placed.Rest = at;
		}
		else if ( go.Components.Get<WorldItemKeeper>( FindMode.EverythingInSelfAndParent ) is { } keeper
		          && !string.IsNullOrEmpty( keeper.ItemId ) )
		{
			WorldItemRegistry.SetFrozen( keeper.ItemId, frozen, at, angles );
			keeper.Rest = at;
		}
	}
}
