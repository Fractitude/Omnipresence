using System;

namespace Sandbox;

/// <summary>
/// Going between the real world (home.scene) and the Wired (wired.scene). The host changes the scene for everyone.
/// </summary>
public static class WiredTravel
{
	public const string HomeScene = "scenes/home.scene";
	public const string WiredScene = "scenes/wired.scene";

	/// <summary>
	/// Where the player is: home, the Wired, or neither (another scene, e.g. the playground).
	/// </summary>
	public static WiredPlace Current
	{
		get
		{
			var path = Game.ActiveScene?.Source?.ResourcePath;

			if ( string.Equals( path, HomeScene, StringComparison.OrdinalIgnoreCase ) )
				return WiredPlace.Home;

			if ( string.Equals( path, WiredScene, StringComparison.OrdinalIgnoreCase ) )
				return WiredPlace.Wired;

			return WiredPlace.Elsewhere;
		}
	}

	/// <summary>
	/// Go to the Wired from home, or home from the Wired, bringing every player. Only the host can; does nothing
	/// elsewhere.
	/// </summary>
	public static void Cross()
	{
		if ( !Networking.IsHost )
			return;

		var target = Current switch
		{
			WiredPlace.Home => WiredScene,
			WiredPlace.Wired => HomeScene,
			_ => null,
		};

		if ( target is null )
			return;

		var options = new SceneLoadOptions();

		if ( !options.SetScene( target ) )
		{
			Log.Warning( $"WiredTravel: can't find {target}" );
			return;
		}

		Game.ChangeScene( options );
	}
}

public enum WiredPlace
{
	Elsewhere,
	Home,
	Wired,
}
