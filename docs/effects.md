# Effects: smoke and sparks

`Code/Effects/` holds the small effects the game makes itself rather than borrowing a particle file. Both are the same
shape: an emitter component you drop on anything, and one tiny billboard per particle that grows or falls, fades, and
destroys itself. Neither is saved and neither is networked — every machine makes the particles it sees, the way each
client draws its own camera feed.

## `SmokeEmitter`

Smoke: puffs that rise, drift, grow and fade. A broken chimney, a wreck, a fire someone put out.

| Property | What it is |
|---|---|
| `Emitting` | On or off. Turning it off lets the puffs already in the air finish their life instead of vanishing. |
| `Rate` | Puffs a second. |
| `Lifetime` | How long a puff lives, in seconds. |
| `Rise` | How fast a puff climbs, in units a second. |
| `Wind` | How far it may wander sideways, in units a second. |
| `Spread` | How far from the emitter a puff may start, in units. |
| `StartSize`, `EndSize` | How big a puff is when it appears and when it goes, in units across. |
| `Tint` | The smoke's colour, alpha included: lower alpha is thinner smoke. |
| `Sprite` | What a puff is drawn with. Empty uses the puff the emitter makes: a soft ragged disc, 128 px, so the sizes mean what they say. |

## `SparkEmitter`

Electric sparks: short-lived bright scraps thrown in clumps, the way a short circuit throws them, falling under gravity
and crackling out. A camera with a bad earth, a cut cable, a machine burning out.

| Property | What it is |
|---|---|
| `Emitting` | On or off. |
| `Interval`, `Count` | How often a clump is thrown, in seconds, and how many sparks are in it. |
| `Lifetime` | How long a spark lives, in seconds; each one differs a little from this. |
| `Speed`, `Gravity` | How fast it leaves, in units a second, and how hard it is pulled down. |
| `UpBias` | How much of its direction is up, 0 to 1: a little makes the sparks arc out, none sprays them every way. |
| `Spread` | How far from the emitter a spark may start, in units. |
| `StartSize`, `EndSize`, `Stretch` | How big a spark is at each end of its life, and how many times taller than wide it is drawn. |
| `Tint` | The sparks' colour. |
| `Additive` | Additive is the hotter look on a dark background, but it disappears against a bright sky. Off, the sparks keep their own colour anywhere — the default, because this game is played outdoors in daylight. |
| `Flash` | Whether each clump also flashes where it was thrown from, one big spark that lives a few frames. That flash is what reads as the arc itself, with the sparks as what it throws off. |
| `Light`, `LightRadius` | One point light on the emitter, flickering with the clumps, which is what makes sparks read at night. |
| `Sprite` | What a spark is drawn with. Empty uses a hard-edged hot dot the emitter makes, 32 px. |

## What both get wrong if you don't tune them

- **`SpriteRenderer.AlphaCutoff` has to be 0.** At its default of 0.5 the rim of a particle is clipped, and the effect
  reads as a string of speckled bubbles instead of smoke or sparks.
- **`Lighting` and `Shadows` off.** A particle is its own light; a lit billboard goes dark in a corner it should not be
  dark in, and casts a shadow nothing asked for.
- **Small and many beats big and few.** A smoke puff is 10 to 44 units across over its life; a spark is under two and
  gone in a fifth of a second. Much bigger, close to the camera, blooms into a glowing petal.
- **The sprite is made, not loaded.** The engine's smoke sheets are 2048 px with a small wisp inside them, so a sprite
  cut from one is mostly empty and the sizes stop meaning anything. Both emitters build a texture that fills its own
  square.

## The sparks' sound

A sparking camera is only half there without the noise, so the spark emitter came with a crackle of its own:
`Assets/sounds/effects/sparks.sound`, a `SoundEvent` holding three short takes. `SoundEvent` picks one at random
(`SelectionMode: Random`) and varies it (`Volume: 0.45 0.7`, `Pitch: 0.9 1.15`), so a camera that sparks for a minute
doesn't sound like a loop. Distance attenuation over 700 units, with occlusion on.

The takes are made, not found: `tools/audio/make_spark_crackle.py` writes `Assets/sounds/effects/sparks_{1,2,3}.wav` —
a burst of bright noise that dies in tens of milliseconds, a handful of small ticks across its first tenth of a second,
and a little low body so it reads as a spark rather than a hiss. Run it by hand and let the editor compile the wavs; it
is not part of the build, and the wavs are committed so the game doesn't depend on Python.

`SparkEmitter` plays it from `Clump`: `Crackle` (empty means the asset above), `CrackleVolume` on top of the sound's own
volume, and `CrackleGap` — the shortest time between two crackles, 0.15 s by default. A clump every 0.07 s would
machine-gun the sample; the gap keeps the sound to the clumps that matter and lets the rest be silent sparks.

## The broken camera's sparks

`SecurityCamera.EnsureSparks` finds the emitter a camera already has, or makes one at the top of its body
(`Vector3.Up * 6f`) for a camera put together by hand, and sets its `Emitting` to `Broken` every frame. A camera sparks
from the moment it breaks, on every client, and stops if it is ever fixed again.

The camera smoked before this (`SmokeEmitter` was on it). The smoke is still in the project and works the same, so
putting one back on the camera prefab is one component — but nothing drives a `SmokeEmitter` from `Broken` any more.

## Trying it

Add a `Smoke Emitter` or a `Spark Emitter` to any object and press Play: it runs with the defaults. Or
`spawn_security_camera`, shoot the camera, and watch.
