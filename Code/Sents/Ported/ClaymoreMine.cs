/// <summary>
/// The component of the sent <c>sftatlas.claymoremine</c>. The package's code was never published with it, only two
/// ActionGraphs that call <see cref="ActivateMine"/> (when a player or an NPC walks into the trigger, and when the mine
/// takes damage), so this is written here from what the prefab says: the explosion is a disabled child, and the mine
/// goes off by spawning it where it stands and removing itself. Without the type the graphs logged "Unable to resolve
/// type binding" every time something touched the mine. See docs/sents.md.
/// </summary>
public sealed class ClaymoreMine : Component
{
	/// <summary>
	/// The explosion, a disabled copy of the engine's explosion prefab under the mine.
	/// </summary>
	[Property] public GameObject Explosion { get; set; }

	// These two are lower case because the prefab saved them under those names.
	[Property] public ModelRenderer prop { get; set; }
	[Property] public ModelCollider prop1 { get; set; }

	private bool _exploded;

	/// <summary>
	/// Called by the prefab's graphs, on every machine. Only the host blows the mine up.
	/// </summary>
	public void ActivateMine()
	{
		if ( _exploded || !Networking.IsHost )
			return;

		_exploded = true;

		if ( Explosion.IsValid() )
		{
			var explosion = Explosion.Clone( Explosion.WorldTransform );
			explosion.Enabled = true;
			explosion.NetworkSpawn( true, null );
		}

		GameObject.Destroy();
	}
}
