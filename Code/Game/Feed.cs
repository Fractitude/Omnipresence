namespace Sandbox;

/// <summary>
/// Where the sandbox game shows who killed whom. This game has no kill feed (see AGENTS.md, Health), so nothing in the
/// scene is a <see cref="Feed"/> and <c>Scene.RunEvent&lt;Feed&gt;</c> does nothing. The type is here, with upstream's
/// signature, so code written for the sandbox game (the sents) compiles.
/// </summary>
public sealed class Feed : Component
{
	public void NotifyKill( string victimName, string attackerName, long attackerSteamId, string damageTags,
		string attackerTags, string victimTags, Texture weaponIcon )
	{
	}
}

/// <summary>
/// A weapon that has a picture for the kill feed. Upstream's; nothing here reads it yet.
/// </summary>
public interface IKillIcon
{
	public Texture DisplayIcon { get; set; }
}
