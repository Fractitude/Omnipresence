namespace Omnipresence.Editor;

/// <summary>
/// The "Omnipresence" menu in the editor: developer tools, like wiping a corrupted save.
/// </summary>
public static class OmnipresenceMenu
{
	[Menu( "Editor", "Omnipresence/Navi apps..." )]
	public static void OpenApps() => AppsWindow.Open();

	[Menu( "Editor", "Omnipresence/Saved State..." )]
	public static void OpenSavedState() => SavedStateWindow.Open();
}
