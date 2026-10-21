/// <summary>
/// Finds everything joined to an object: the objects on the other end of its physics joints, and the ones a physics
/// filter groups with it. A seat controls everything in its contraption this way.
/// </summary>
public class LinkedGameObjectBuilder
{
	public List<GameObject> Objects { get; } = new();
	private readonly HashSet<GameObject> _visited = new();

	/// <summary>
	/// Adds a GameObject. Won't find connections.
	/// </summary>
	public bool Add( GameObject obj )
	{
		if ( !obj.IsValid() ) return false;
		if ( obj.Tags.Contains( "world" ) ) return false;
		if ( _visited.Contains( obj ) ) return false;
		if ( obj.GetComponent<MapInstance>() is not null ) return false;

		_visited.Add( obj );
		Objects.Add( obj );
		return true;
	}

	/// <summary>
	/// Add a GameObject with all connected GameObjects.
	/// </summary>
	public void AddConnected( GameObject source )
	{
		if ( !source.IsValid() ) return;

		// we're only interested in root objects
		source = source.Root;

		// If we can't add this then don't add children, because we must have already added it, or it's the world.
		if ( !Add( source ) ) return;

		foreach ( var rb in source.GetComponentsInChildren<Rigidbody>() )
		{
			foreach ( var joint in rb.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		foreach ( var collider in source.GetComponentsInChildren<Collider>() )
		{
			foreach ( var joint in collider.Joints )
			{
				AddConnected( joint.Object1 );
				AddConnected( joint.Object2 );
			}
		}

		// If any children have a physics filter, also connect them.
		foreach ( var filter in source.GetComponentsInChildren<PhysicsFilter>( includeSelf: false ) )
		{
			AddConnected( filter.GameObject );
		}
	}
}
