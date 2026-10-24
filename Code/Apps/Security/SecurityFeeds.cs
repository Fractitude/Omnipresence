using System;

namespace OmnipresenceApps;

/// <summary>
/// The pictures the Security System draws on this client: one live view for each camera its state lists, taken from
/// this client's own copy of that camera. A feed never crosses the network — every client draws the same world — so
/// what the host's copy of the app decides is only which cameras are connected and which one is enlarged.
/// </summary>
public sealed class SecurityFeeds
{
	/// <summary>How often the client looks for the cameras its state names, in seconds.</summary>
	const float RescanInterval = 1f;

	readonly Func<Scene> getScene;
	readonly Dictionary<string, SecurityCamera> cameras = [];

	RealTimeSince sinceScan = float.MaxValue;

	/// <summary>How many pictures the last <see cref="Keep"/> was without, so a picture arriving can be noticed.</summary>
	int lastMissing;

	public SecurityFeeds( Func<Scene> getScene )
	{
		this.getScene = getScene;
	}

	/// <summary>
	/// Goes up when a picture appears or goes away, so the panel draws again with it: a texture that arrives after the
	/// first render has nothing else to tell the panel about.
	/// </summary>
	public int Revision { get; private set; }

	/// <summary>
	/// Ask for the pictures the state shows, every frame. This is what keeps the feeds moving: a camera draws for a
	/// second after the last ask, so stopping the asks is what turns it off.
	/// </summary>
	public void Keep( SecurityUiState uiState )
	{
		var missing = 0;

		if ( uiState.Selected is { } selected )
		{
			missing = Picture( selected.Id ) is null ? 1 : 0;
		}
		else
		{
			foreach ( var row in uiState.Cameras )
			{
				if ( Picture( row.Id ) is null )
					missing++;
			}
		}

		if ( missing == lastMissing )
			return;

		lastMissing = missing;
		Revision++;
	}

	/// <summary>
	/// The live picture of the camera the state calls <paramref name="id"/>, or null while this client doesn't have that
	/// camera (yet).
	/// </summary>
	public Texture Picture( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return null;

		var camera = Find( id );

		return camera.IsValid() && camera.Enabled ? camera.RequestFeed() : null;
	}

	/// <summary>
	/// The camera here that the host's state names, looked for again when it isn't known or has gone: a client that
	/// joins late gets the cameras, and the state, a moment after the window appears.
	/// </summary>
	SecurityCamera Find( string id )
	{
		if ( cameras.TryGetValue( id, out var known ) && known.IsValid() && known.Enabled )
			return known;

		if ( sinceScan < RescanInterval )
			return null;

		sinceScan = 0;
		cameras.Clear();

		if ( getScene() is { } scene )
		{
			foreach ( var camera in scene.GetAllComponents<SecurityCamera>() )
			{
				if ( camera.IsValid() && camera.Enabled )
					cameras[camera.GameObject.Id.ToString()] = camera;
			}
		}

		return cameras.GetValueOrDefault( id );
	}
}
