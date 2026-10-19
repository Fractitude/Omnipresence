namespace OmnipresenceApps;

/// <summary>
/// Who has broken into the Hardware Store's staff panel, and who is locked out after failing to. Host only. In memory:
/// it is gone when the host restarts, which only ever lets a lockout end early.
/// </summary>
/// <remarks>
/// Not <c>readonly</c>: a hot reload would reset the dictionaries (see AGENTS.md, "Static state and hot reload").
/// </remarks>
public static class StoreAccess
{
	/// <summary>Seconds the staff panel stays open after a successful break-in.</summary>
	public const long UnlockSeconds = 600;

	/// <summary>Seconds a player can't try again after the terminal locked on them.</summary>
	public const long LockoutSeconds = 120;

	/// <summary>The share of the price a player pays while the staff panel is open, in percent.</summary>
	public const int DiscountPercent = 60;

	static Dictionary<ulong, long> unlockedUntil = new();
	static Dictionary<ulong, long> lockedUntil = new();

	static long Now => WorldClock.Now;

	public static void Unlock( ulong steamId )
	{
		unlockedUntil[steamId] = Now + UnlockSeconds;
		lockedUntil.Remove( steamId );
	}

	public static void Lock( ulong steamId ) => lockedUntil[steamId] = Now + LockoutSeconds;

	/// <summary>
	/// Seconds the staff panel stays open for this player, or 0 when it is closed.
	/// </summary>
	public static long UnlockedFor( ulong steamId ) =>
		unlockedUntil.TryGetValue( steamId, out var until ) ? System.Math.Max( until - Now, 0 ) : 0;

	/// <summary>
	/// Seconds before this player can try the terminal again, or 0 when they can.
	/// </summary>
	public static long LockedOutFor( ulong steamId ) =>
		lockedUntil.TryGetValue( steamId, out var until ) ? System.Math.Max( until - Now, 0 ) : 0;

	public static bool IsUnlocked( ulong steamId ) => UnlockedFor( steamId ) > 0;

	/// <summary>
	/// The price this player pays: the full price, or less while the staff panel is open. Never below 1.
	/// </summary>
	public static long PriceFor( ulong steamId, long price ) =>
		IsUnlocked( steamId ) ? System.Math.Max( price * DiscountPercent / 100, 1 ) : price;
}
