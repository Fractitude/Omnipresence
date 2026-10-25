/// <summary>
/// The component of the sent <c>tankmine</c>. Its code was never published with it, only one ActionGraph ("On Prop Take
/// Damage") that calls <see cref="ActivateMine"/>, so this is written here from what the prefab says: the explosion is a
/// disabled child, and the mine goes off by spawning it where it stands and removing itself. Without the type the graph
/// logged "Unable to resolve type binding" every time the mine was shot. The name is the prefab's, lower case and all.
/// See docs/sents.md.
/// </summary>
public sealed class Mineexplosion : Component, Component.ICollisionListener
{
	/// <summary>
	/// The explosion, a disabled copy of the engine's explosion prefab under the mine.
	/// </summary>
	[Property] public GameObject Explosion { get; set; }

	/// <summary>
	/// Played where the mine is when something with one of <see cref="TargetTags"/> lands on it.
	/// </summary>
	[Property] public SoundEvent FootstepSound { get; set; }

	[Property] public Prop MineProp { get; set; }

	// These two are lower case because the prefab saved them under those names.
	[Property] public Prop prop { get; set; }
	[Property] public ModelCollider prop1 { get; set; }

	/// <summary>
	/// What sets the mine off by touching it, separated by spaces.
	/// </summary>
	[Property] public string TargetTags { get; set; } = "player";

	private bool _exploded;

	/// <summary>
	/// Called by the prefab's graph, on every machine. Only the host blows the mine up.
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

	void Component.ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( _exploded || !Networking.IsHost || string.IsNullOrWhiteSpace( TargetTags ) )
			return;

		var other = collision.Other.GameObject;
		if ( !other.IsValid() || !TargetTags.Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Any( other.Tags.Has ) )
			return;

		if ( FootstepSound is not null )
			Sound.Play( FootstepSound, WorldPosition );

		ActivateMine();
	}
}
