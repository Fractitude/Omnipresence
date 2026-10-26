namespace Sandbox;

public static partial class Global
{
	/// <summary>
	/// Ownership events, posted to the whole scene on the host when a thing changes hands. Implement it on a component
	/// to react to a sale, a repossession or a gift without polling the registry.
	/// </summary>
	public interface IOwnershipEvents : ISceneEvent<IOwnershipEvents>
	{
		/// <summary>
		/// The thing has a new owner. <paramref name="thing"/> is the object in the world, or null for a thing that is
		/// not a GameObject (a business, a licence): its key is in <paramref name="change"/> either way.
		/// </summary>
		void OnOwnershipChanged( OwnershipChange change, OwnershipComponent thing ) { }
	}
}
