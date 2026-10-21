/// <summary>
/// The local user's weapon and camera preferences.
/// </summary>
internal static class GamePreferences
{
	/// <summary>
	/// Enables automatic switching to better weapons on item pickup
	/// </summary>
	[ConVar( "omni.autoswitch", ConVarFlags.UserInfo | ConVarFlags.Saved )]
	public static bool AutoSwitch { get; set; } = true;

	/// <summary>
	/// Enables movement bob, weapon inertia and animation-driven camera motion.
	/// </summary>
	[ConVar( "omni.viewbob", ConVarFlags.Saved )]
	public static bool ViewBobbing { get; set; } = true;

	/// <summary>
	/// Intensity of camera shake, such as the vehicle camera's at speed.
	/// </summary>
	[ConVar( "omni.screenshake", ConVarFlags.Saved )]
	public static float Screenshake { get; set; } = 0.3f;
}
