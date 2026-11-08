namespace Sandbox;

/// <summary>
/// Marks a Navi computer as the one in the Hardware Store, and makes it a kiosk: the Hardware Store app is the only app it
/// has, it opens by itself and can't be closed, and the taskbar is hidden. The computer gets the <see cref="StoreTag"/> tag, which the app's manifest lists, so the app can't be installed on
/// any other computer. The app also refuses to work on a computer without this component. Put it on the computer's
/// GameObject, beside its <c>NaviComputer</c>.
/// </summary>
[Title( "Store Terminal" )]
[Category( "Omnipresence" )]
public sealed class StoreTerminal : Component
{
	public const string StoreAppId = "navi/shop/hardwarestore.napp";

	/// <summary>
	/// The computer tag the Hardware Store's manifest lists in <c>ComputerTags</c>.
	/// </summary>
	public const string StoreTag = "hardware-store";

	/// <summary>
	/// Is this computer the store's?
	/// </summary>
	public static bool IsStore( NaviComputer computer ) =>
		computer is not null && computer.GetComponent<StoreTerminal>() is not null;

	protected override void OnAwake()
	{
		// Set here as well as in the scene, so no copy of the computer can be left with the other apps or without the tag.
		if ( GetComponent<NaviComputer>() is not { } computer )
			return;

		computer.KioskMode = true;
		computer.HideTaskbar = true;
		computer.OnlyApps = [StoreAppId];
		computer.OpenApp = StoreAppId;

		if ( !computer.ComputerTags.Contains( StoreTag ) )
			computer.ComputerTags = [.. computer.ComputerTags, StoreTag];
	}
}
