using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// A sent that was integrated into this project (<c>sent_integrate</c>, the Integrate entry of the Workshop Entities
/// window): its prefab is a file of ours in <c>Assets/sents/&lt;ident&gt;/</c> and its code, if it has any, is in
/// <c>Code/Sents/Imported/&lt;ident&gt;/</c>, so spawning it needs neither the workshop's prefab nor its code, and an
/// update by the author changes nothing here. <c>source.json</c> beside the prefab says where it came from.
/// <para>
/// What stays on the workshop are the sent's <b>assets</b> (models, materials, sounds): a package ships them compiled,
/// without sources, so they cannot be kept as files of ours. They are mounted from the package when the sent is spawned
/// (<see cref="Mount"/>), from the copy the engine has downloaded. See docs/sents.md.
/// </para>
/// </summary>
public static class SentLocal
{
	/// <summary>
	/// Where the prefab of an integrated sent lives, relative to <c>Assets/</c>.
	/// </summary>
	public static string PrefabPath( string ident ) => $"sents/{ident}/sent.prefab";

	/// <summary>
	/// The integrated prefab of <paramref name="ident"/>, or null if it was not integrated (or the editor has not
	/// compiled the file yet).
	/// </summary>
	public static PrefabFile Find( string ident ) =>
		Sents.IsIdent( ident ) && ResourceLibrary.TryGet<PrefabFile>( PrefabPath( ident ), out var prefab ) ? prefab : null;

	public static bool IsIntegrated( string ident ) => Find( ident ) is not null;

	/// <summary>
	/// Has the engine mount the package's assets, without its code (which is ours now). A failure is logged, not thrown:
	/// the sent still spawns, with error models where the assets are missing.
	/// </summary>
	public static async Task Mount( string ident )
	{
		try
		{
			var package = await Package.FetchAsync( ident, false );

			if ( package is not null )
				await package.MountAsync( false );
		}
		catch ( Exception e )
		{
			Log.Warning( $"spawn_sent: could not mount the assets of {ident}: {e.Message}" );
		}
	}
}
