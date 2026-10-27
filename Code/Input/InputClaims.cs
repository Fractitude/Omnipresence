namespace Sandbox;

/// <summary>
/// How much a component wants an input compared to the others that read it. Higher wins; equal priorities share.
/// </summary>
public static class InputPriority
{
	/// <summary>
	/// Always on, like the weapon switcher's wheel.
	/// </summary>
	public const int Default = 0;

	/// <summary>
	/// A weapon's trigger. Below everything the player is doing with their hands, so a click that a menu took
	/// never fires the gun.
	/// </summary>
	public const int Weapon = 50;

	/// <summary>
	/// Something the player is looking at, like an interaction menu.
	/// </summary>
	public const int Interaction = 100;

	/// <summary>
	/// A tool the player is working with right now, like the physgun holding an object.
	/// </summary>
	public const int Tool = 200;

	/// <summary>
	/// A menu the player is picking from, like the weapon switcher's. It takes the press that confirms a pick,
	/// so the weapon being put away does not fire on it.
	/// </summary>
	public const int Menu = 300;
}

/// <summary>
/// Gives an input to one component when several read it. A channel is an input action name (<c>"attack1"</c>) or
/// <see cref="MouseWheel"/>. A component that needs an input <see cref="Claim"/>s it every frame it needs it, with a
/// priority; any component that reads the same input asks with its own priority and gets nothing while a higher claim
/// is live. Claims run out a moment after the last one, so it does not matter which component updates first, and a
/// component that is destroyed or disabled gives its claim up by stopping.
/// </summary>
public static class InputClaims
{
	/// <summary>
	/// The channel of <c>Input.MouseWheel</c>, which is not an action.
	/// </summary>
	public const string MouseWheel = "MouseWheel";

	/// <summary>
	/// How long a claim lasts without being renewed. A few frames, so update order never matters.
	/// </summary>
	const float Lifetime = 0.1f;

	record struct Claimed( object Owner, int Priority, float Until );

	// Not readonly: a hot reload resets a static readonly collection of engine types.
	static Dictionary<string, List<Claimed>> claims = new();

	/// <summary>
	/// Holds a channel for <paramref name="owner"/> for the next moment. Call it every frame it is needed.
	/// </summary>
	public static void Claim( string channel, int priority, object owner )
	{
		if ( !claims.TryGetValue( channel, out var list ) )
			claims[channel] = list = new List<Claimed>();

		var until = RealTime.Now + Lifetime;
		var index = list.FindIndex( x => x.Owner == owner );
		if ( index >= 0 )
			list[index] = new Claimed( owner, priority, until );
		else
			list.Add( new Claimed( owner, priority, until ) );
	}

	/// <summary>
	/// Gives the channel up at once, instead of letting the claim run out.
	/// </summary>
	public static void Release( string channel, object owner )
	{
		if ( claims.TryGetValue( channel, out var list ) )
			list.RemoveAll( x => x.Owner == owner );
	}

	/// <summary>
	/// Whether another owner holds the channel at a higher priority. Pass <paramref name="owner"/> to ignore your own
	/// claim.
	/// </summary>
	public static bool IsBlocked( string channel, int priority, object owner = null )
	{
		if ( !claims.TryGetValue( channel, out var list ) )
			return false;

		var now = RealTime.Now;
		list.RemoveAll( x => x.Until < now || x.Owner is IValid { IsValid: false } );

		return list.Any( x => x.Owner != owner && x.Priority > priority );
	}

	/// <summary>
	/// <c>Input.MouseWheel</c>, or nothing while a higher priority holds the wheel.
	/// </summary>
	public static Vector2 Wheel( int priority, object owner = null ) =>
		IsBlocked( MouseWheel, priority, owner ) ? default : Input.MouseWheel;

	/// <summary>
	/// <c>Input.Pressed</c>, or false while a higher priority holds the action.
	/// </summary>
	public static bool Pressed( string action, int priority, object owner = null ) =>
		!IsBlocked( action, priority, owner ) && Input.Pressed( action );

	/// <summary>
	/// <c>Input.Down</c>, or false while a higher priority holds the action.
	/// </summary>
	public static bool Down( string action, int priority, object owner = null ) =>
		!IsBlocked( action, priority, owner ) && Input.Down( action );

	/// <summary>
	/// <c>Input.Released</c>, or false while a higher priority holds the action.
	/// </summary>
	public static bool Released( string action, int priority, object owner = null ) =>
		!IsBlocked( action, priority, owner ) && Input.Released( action );
}
