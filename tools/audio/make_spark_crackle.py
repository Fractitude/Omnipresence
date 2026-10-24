"""Make the electric crackle the spark emitter plays.

Not part of the build: run it by hand to (re)generate `Assets/sounds/effects/sparks_*.wav`, then let the s&box editor
compile them (the `.sound` asset next to them picks them up). Edit the numbers here, not the wavs.

    python tools/audio/make_spark_crackle.py

A spark is an impulse: a burst of bright noise that dies in tens of milliseconds, a handful of smaller ticks scattered
through the first tenth of a second, and a little low body so it isn't only hiss. Three takes, pulled apart by their
decay and their ticks, so a clump of sparks doesn't play the same sample twice in a row.
"""

import math
import pathlib
import random
import struct
import wave

RATE = 48000
OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets" / "sounds" / "effects"

# (name, seed, main decay, ticks, main level, body level)
TAKES = [
    ( "sparks_1", 7, 62.0, 7, 0.95, 0.14 ),
    ( "sparks_2", 23, 48.0, 5, 0.85, 0.10 ),
    ( "sparks_3", 41, 78.0, 9, 1.00, 0.18 ),
]


def crackle( seed: int, decay: float, ticks: int, level: float, body: float ) -> tuple[list[float], int]:
    rng = random.Random( seed )
    length = 0.35
    count = int( RATE * length )
    samples = [0.0] * count

    # The burst: white noise through a one-pole high pass, gone in a few dozen milliseconds.
    previous_in = previous_out = 0.0
    for i in range( count ):
        t = i / RATE
        white = rng.uniform( -1.0, 1.0 )
        shaped = 0.94 * ( previous_out + white - previous_in )
        previous_in, previous_out = white, shaped
        samples[i] += shaped * math.exp( -t * decay ) * level

    # The ticks: a spark is never one sound, it is a handful of small ones.
    for _ in range( ticks ):
        start = rng.uniform( 0.0, 0.13 )
        span = rng.uniform( 0.003, 0.012 )
        amp = rng.uniform( 0.25, 0.75 )
        offset = int( start * RATE )
        for k in range( int( span * RATE ) ):
            if offset + k >= count:
                break
            samples[offset + k] += rng.uniform( -1.0, 1.0 ) * math.exp( -(k / RATE) * 1100.0 ) * amp

    # A little low end under it, so it reads as a spark and not as a hiss.
    for i in range( count ):
        t = i / RATE
        samples[i] += math.sin( 2.0 * math.pi * 72.0 * t ) * math.exp( -t * 45.0 ) * body

    # A clean tail, so no click at the end of the sample.
    fade = int( RATE * 0.06 )
    for i in range( fade ):
        samples[count - fade + i] *= 1.0 - (i / fade)

    peak = max( abs( value ) for value in samples ) or 1.0

    return [value / peak * 0.89 for value in samples], count


def brightness( samples: list[float] ) -> float:
    """Rough spectral centre, as the rate the signal crosses zero: high for a sizzle, low for a thud."""

    crossings = 0
    for i in range( 1, len( samples ) ):
        if ( samples[i - 1] < 0.0 ) != ( samples[i] < 0.0 ):
            crossings += 1

    return crossings / 2.0 / (len( samples ) / RATE)


def main() -> None:
    OUT.mkdir( parents=True, exist_ok=True )

    for name, seed, decay, ticks, level, body in TAKES:
        samples, count = crackle( seed, decay, ticks, level, body )
        path = OUT / f"{name}.wav"

        with wave.open( str( path ), "wb" ) as wav:
            wav.setnchannels( 1 )
            wav.setsampwidth( 2 )
            wav.setframerate( RATE )
            wav.writeframes( b"".join( struct.pack( "<h", int( value * 32767 ) ) for value in samples ) )

        peak = max( abs( value ) for value in samples )
        rms = math.sqrt( sum( value * value for value in samples ) / count )
        print( f"{path.name}: {count / RATE:.2f} s, peak {peak:.2f}, rms {rms:.3f}, about {brightness( samples ):,.0f} Hz" )


if __name__ == "__main__":
    main()
