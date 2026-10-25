using System;

namespace CuratedVehicles;

public static class AutomaticPedals
{
	// Opposing input brakes at speed; both pedals start a burnout near a stop.
	public static (int Gear, float Throttle, float Brake) Resolve( int gear, float forwardSpeed, float forward, float backward, bool burnoutAllowed )
	{
		const float directionChangeSpeed = 30f;
		if ( gear == -1 )
		{
			if ( forward > 0 )
			{
				if ( backward > 0 || forwardSpeed < -directionChangeSpeed )
					return (gear, 0, forward);
				return (1, forward, 0);
			}
			return (gear, backward, 0);
		}

		if ( backward > 0 )
		{
			// At a stop, both pedals mean hold the car while spinning the driven wheels.
			if ( gear > 0 && forward > 0.45f && backward > 0.45f && burnoutAllowed )
				return (gear, forward, backward);
			if ( forward > 0 || forwardSpeed > directionChangeSpeed )
				return (gear, 0, backward);
			return (-1, backward, 0);
		}
		return (gear, forward, 0);
	}
}
