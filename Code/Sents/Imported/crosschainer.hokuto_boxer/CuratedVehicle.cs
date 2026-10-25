using Sandbox;
using System;
using System.Linq;

namespace CuratedVehicles;

/// <summary>A host-simulated car. Each SENT supplies its own authored stock assembly and tuning.</summary>
[Title( "Vehicle by crosschainer" ), Category( "Vehicles" )]
public sealed partial class CuratedVehicle : Component, IVehicleGauges
{
	[Property] public string CarId { get; set; }
	[Property] public string DisplayName { get; set; }
	[Property] public float Mass { get; set; } = 1220;
	[Property] public float Torque { get; set; } = 420;
	[Property] public float MaxRpm { get; set; } = 7200;
	[Property] public float GearingSpeed { get; set; } = 230;
	[Property] public float WheelRadius { get; set; } = 12;
	[Property] public GameObject[] WheelObjects { get; set; }
	[Property] public Vector3[] WheelHubs { get; set; }
	[Property] public GameObject BodyVisual { get; set; }
	[Property] public GameObject PopupLights { get; set; }
	[Property] public float PopupAngle { get; set; }
	[Property] public Vector3[] HeadlightMounts { get; set; }
	[Property] public string EngineSound { get; set; }
	[Property] public Vector3[] ExhaustOutlets { get; set; }
	[Property] public Vector3[] ExhaustDirections { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public Color Paint { get; set; } = Color.Red;
	[Sync( SyncFlags.FromHost )] public float Rpm { get; set; } = 1000;
	[Sync( SyncFlags.FromHost )] public int Gear { get; set; } = 1;
	[Sync( SyncFlags.FromHost )] public int Mode { get; set; } = 1;
	[Sync( SyncFlags.FromHost )] public float Steering { get; set; }
	[Sync( SyncFlags.FromHost )] public float Slip { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Lights { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Braking { get; set; }
	[Sync( SyncFlags.FromHost )] public bool Burnout { get; set; }
	[Sync( SyncFlags.FromHost )] public Vector4 WheelTravel { get; set; }
	[Sync( SyncFlags.FromHost )] public int BackfireSequence { get; set; }
	public Rigidbody Body => GetComponent<Rigidbody>();
	public BaseChair Seat => GetComponent<BaseChair>();
	public PlayerController Driver => Seat?.GetOccupant();
	public bool LocalDriver => Driver.IsValid() && Driver.Network.IsOwner;
	public float Speed => Body.IsValid() ? Body.Velocity.Length * 0.09144f : 0;
	public bool Drift => Mode == 0;

	// What the speedometer (Code/Vehicles/Hud) reads. Added when this was imported; the rest is the package's.
	bool IVehicleGauges.GaugesVisible => LocalDriver;
	float IVehicleGauges.GaugeSpeed => Speed;
	float IVehicleGauges.GaugeRpm => Rpm;
	float IVehicleGauges.GaugeMaxRpm => MaxRpm;
	string IVehicleGauges.GaugeGear => Gear < 0 ? "R" : Gear == 0 ? "N" : Gear.ToString();
	string IVehicleGauges.GaugeMode => Drift ? "DRIFT MODE" : "GRIP MODE";

	IEnumerable<VehiclePrompt> IVehicleGauges.GaugePrompts =>
	[
		new( "B", "DRIVING MODE" ),
		new( "L", "LIGHTS" ),
	];
	private readonly Vector3[] hubs = new Vector3[4];
	private TimeSince sinceShift;
	private TimeSince sinceModeChange;
	private float rearSlide;
	private float rearSlideHold;
	private bool ready;
	internal const float InchesPerMeter = 39.3700787f;
	private static readonly float[] Gears = [2.65f, 1.9f, 1.42f, 1.1f, 0.88f, 0.72f];
	public float FrontWheelAngle => Steering * (Drift
		? 40 * Math.Clamp( 1 - Math.Max( 0, Speed / 1.609344f - 60 ) / 450, 0.8f, 1 )
		: 38 / (1 + Speed / 1.609344f / 25));

	protected override void OnStart()
	{
		ready = WheelObjects?.Length == 4 && WheelObjects.All( w => w.IsValid() ) && WheelHubs?.Length == 4
			&& Body.IsValid() && Seat.IsValid() && WheelRadius > 0 && Mass > 0 && MaxRpm > 1000 && GearingSpeed > 0;
		if ( !ready )
		{
			Log.Error( $"Vehicle {CarId}: invalid wheel assembly, seat or tuning." );
			Enabled = false;
			return;
		}
		for ( var i = 0; i < 4; i++ ) hubs[i] = WheelHubs[i];
		SetupPresentation();
	}

	protected override void OnUpdate()
	{
		if ( !ready ) return;
		// Undo queues destruction before every component has finished its last update.
		if ( GameObject.IsDestroyed || !Body.IsValid() || !Seat.IsValid() )
		{
			StopPresentation();
			return;
		}
		if ( LocalDriver && !Input.Suppressed && !Input.MouseCursorVisible )
		{
			if ( Input.Keyboard.Pressed( "b" ) ) RequestControl( "mode" );
			if ( Input.Keyboard.Pressed( "l" ) ) RequestControl( "lights" );
		}
		UpdatePresentation();
	}

	[Rpc.Host]
	public void RequestControl( string action )
	{
		if ( !Driver.IsValid() || Driver.Network.Owner != Rpc.Caller ) return;
		switch ( action )
		{
			case "mode" when sinceModeChange > 0.25f:
				Mode = Drift ? 1 : 0;
				Gear = 1;
				rearSlide = rearSlideHold = 0;
				sinceModeChange = 0;
				break;
			case "lights": Lights = !Lights; break;
		}
	}

	protected override void OnDisabled() => StopPresentation();
	protected override void OnDestroy() => StopPresentation();
}
