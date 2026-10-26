using Sandbox;

namespace CuratedVehicles;

public sealed partial class CuratedVehicle
{
	private void PreserveHandlingInertia()
	{
		var body = Body;
		if ( !body.PhysicsBody.IsValid() ) return;
		// Keep the tuned chassis's rotational response while collision follows the bodywork.
		// Recheck after physics-body recreation, rescaling, or a Sandbox mass-tool edit.
		var size = new Vector3( 164, 63, 30 ) * WorldScale;
		var squared = size * size;
		var inertia = new Vector3( squared.y + squared.z, squared.x + squared.z, squared.x + squared.y ) * (body.Mass / 12);
		if ( (body.InertiaTensor - inertia).Length > inertia.Length * 0.000001f
			|| body.InertiaTensorRotation != Rotation.Identity )
		{
			body.InertiaTensor = inertia;
			body.InertiaTensorRotation = Rotation.Identity;
		}
	}
}
