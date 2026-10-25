using System;

namespace CuratedVehicles;

/// <summary>The original high-RPM shift/lift detector. Speed is in km/h.</summary>
public sealed class BackfireTrigger
{
	private float cooldown, hot;
	private int gear;
	private bool throttle;
	private bool burnoutFollowup;
	private Random random;

	public BackfireTrigger( Random random = null ) => this.random = random ?? Random.Shared;
	private float Roll() => (random ??= Random.Shared).NextSingle();

	public void Reset() { cooldown = hot = 0; gear = 0; throttle = false; burnoutFollowup = false; }

	public bool Update( bool running, bool tuned, float speed, float rpm, float maxRpm, int currentGear, bool accelerating, float dt, bool burnout = false )
	{
		cooldown = Math.Max( 0, cooldown - dt );
		hot = Math.Max( 0, hot - dt );
		if ( !running || !tuned || speed <= 5 && !burnout || currentGear < 0 ) { Reset(); return false; }
		if ( !burnout ) burnoutFollowup = false;
		var changed = currentGear > 0 && gear > 0 && currentGear != gear;
		var lifted = throttle && !accelerating;
		var burnoutPop = burnout && currentGear > 0 && rpm > maxRpm * 0.55f;
		var fire = currentGear > 0 && cooldown == 0 && (burnoutPop || hot > 0 && (changed || lifted));
		if ( fire )
		{
			if ( burnoutPop )
			{
				if ( burnoutFollowup )
				{
					burnoutFollowup = false;
					cooldown = 0.65f + Roll() * 0.85f;
				}
				else
				{
					burnoutFollowup = Roll() < 0.22f;
					cooldown = burnoutFollowup
						? 0.26f + Roll() * 0.16f
						: 0.55f + Roll() * 0.95f;
				}
			}
			else cooldown = 0.5f;
		}
		if ( currentGear > 0 && rpm > maxRpm * 0.55f ) hot = 0.4f;
		// Neutral may be an intermediate sample of a manual shift.
		if ( currentGear > 0 ) gear = currentGear;
		throttle = accelerating;
		return fire;
	}
}
