using System.Collections.Generic;
using Sandbox;
using Sandbox.Npcs;

namespace Hl2Manhack;

/// <summary>
/// The Combine's one radio net across packages. The soldier, the elite, the prison guard and the
/// manhack each come in a package of their own and can't see each other's types, so they talk over
/// the sandbox's own stimulus bus, which every package shares: a contact report is a stimulus whose
/// Source is the enemy. Reports go to a spot far below the world, so no ordinary NPC ever hears them
/// as a noise. The same file lives in every Combine package - keep the copies alike.
/// </summary>
public static class CombineNet
{
	public const string ContactKind = "combine.net.contact";

	/// <summary>How far from the enemy a report is taken up, as on Overwatch's own net.</summary>
	public const float Range = 3000f;

	private static readonly Vector3 Channel = new( 0f, 0f, -1000000f );
	// Which package a report came from (the variant script renames it for the elite and the guard)
	private const string Own = ContactKind + "|Hl2Manhack";

	/// <summary>Someone in this package has eyes on <paramref name="enemy"/> right now.</summary>
	public static void ReportContact( Scene scene, GameObject enemy )
	{
		if ( !enemy.IsValid() ) return;
		scene?.GetSystem<NpcStimulusSystem>()?.Broadcast( Own, Channel, enemy, 1f, 1f );
	}

	/// <summary>Enemies the Combine in the other packages (or this one too) have seen in the last second.</summary>
	public static List<GameObject> Contacts( Scene scene, bool includeOwn )
	{
		var list = new List<GameObject>();
		if ( scene?.GetSystem<NpcStimulusSystem>() is not { } system ) return list;

		foreach ( var stimulus in system.Near( Channel ) )
		{
			if ( !stimulus.Kind.StartsWith( ContactKind + "|" ) || !stimulus.Source.IsValid() ) continue;
			if ( !includeOwn && stimulus.Kind == Own ) continue;
			if ( !list.Contains( stimulus.Source ) ) list.Add( stimulus.Source );
		}

		return list;
	}
}
