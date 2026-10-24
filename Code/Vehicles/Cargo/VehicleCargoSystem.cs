namespace Sandbox;

/// <summary>
/// Gives every <see cref="FourWheelVehicle"/> a cargo: an <see cref="Interactable"/> and a <see cref="VehicleCargo"/>,
/// added on the host at run time and sent to the clients (<c>Network.Refresh</c>). The vehicle's prefab and any scene
/// that places one stay as they are, so <c>spawn_buggy</c> and a buggy standing in a scene both get it.
/// </summary>
/// <remarks>
/// Host only. Made in every scene by the engine; a <c>new Scene()</c> starts every system, so one that is not the active
/// scene does nothing.
/// </remarks>
public sealed class VehicleCargoSystem : GameObjectSystem<VehicleCargoSystem>
{
	/// <summary>The name of the cargo area's child object.</summary>
	public const string BedName = "cargo_bed";

	RealTimeSince sinceTick;

	public VehicleCargoSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 1, Tick, "VehicleCargoSystem.Tick" );
	}

	void Tick()
	{
		if ( Scene != Game.ActiveScene || !Game.IsPlaying || Scene.IsEditor || Networking.IsActive && !Networking.IsHost )
			return;

		if ( sinceTick < 1f )
			return;

		sinceTick = 0;
		Attach( Scene );
	}

	/// <summary>
	/// Add a cargo to every vehicle in the scene that has none. Returns how many were given one.
	/// </summary>
	public static int Attach( Scene scene )
	{
		var made = 0;

		foreach ( var vehicle in scene.GetAllComponents<FourWheelVehicle>().ToList() )
		{
			var go = vehicle.GameObject;

			if ( go.Components.Get<VehicleCargo>().IsValid() )
				continue;

			MakeBed( go );
			go.Components.Create<VehicleCargo>();

			if ( Networking.IsActive && go.Network.Active )
				go.Network.Refresh();

			made++;
		}

		return made;
	}

	/// <summary>
	/// The part of the vehicle you look at to manage its cargo: a trigger box over the rear third, with the
	/// <see cref="Interactable"/>. It is a child because the vehicle's own seat already answers to Use on the whole body
	/// (the engine presses the first pressable it finds on the object it looks at), and a trigger because it must not
	/// change how the vehicle drives. The child is a Snapshot object so clients get it with the vehicle.
	/// </summary>
	static void MakeBed( GameObject vehicle )
	{
		var bounds = vehicle.GetLocalBounds();
		var length = bounds.Size.x;
		var height = bounds.Size.z;

		var bed = new GameObject( true, BedName )
		{
			Parent = vehicle,
			LocalPosition = new Vector3( bounds.Mins.x + length * 0.15f, bounds.Center.y, bounds.Mins.z + height * 0.45f ),
			LocalRotation = Rotation.Identity,
			NetworkMode = NetworkMode.Snapshot,
		};

		var box = bed.Components.Create<BoxCollider>();
		box.Scale = new Vector3( length * 0.3f, bounds.Size.y * 0.8f, height * 0.6f );
		box.IsTrigger = true;

		bed.Components.Create<Interactable>().Range = 250f;
	}
}
