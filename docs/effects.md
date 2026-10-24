# Effects: smoke and sparks

`Code/Effects/` holds the small effects the game makes itself rather than borrowing a particle file. Both are the same
shape: an emitter component you drop on anything, which builds the engine's own particle system on its GameObject — a
`ParticleEffect` for the particles, a `ParticleSpriteRenderer` for their picture, a `ParticleSphereEmitter` for where
they come from — and maps a handful of readable properties onto it. Nothing about them is saved and nothing is
networked: every machine makes the particles it sees, the way each client draws its own camera feed.

The engine does the simulation and the drawing. There is no GameObject per particle, nothing to destroy, and no code
ticking particles forward: turn the emitter component's `Enabled` off and the particles already in the air finish their
life on their own.

## `SmokeEmitter`

Smoke: puffs that rise, drift, grow and fade. A broken chimney, a wreck, a fire someone put out.

| Property | What it is |
|---|---|
| `Emitting` | On or off. Turning it off lets the puffs already in the air finish their life instead of vanishing. |
| `Rate` | Puffs a second. |
| `Lifetime` | How long a puff lives, in seconds. |
| `Rise` | How hard a puff is pushed up, in units a second squared. |
| `Wind` | How hard it is pushed sideways, in units a second squared, along the emitter's right. |
| `Spread` | How far from the emitter a puff may start, in units (the sphere emitter's radius). |
| `StartSize`, `EndSize` | How big a puff is when it appears and when it goes, in units across. |
| `Thickness` | How thick the smoke is where it is thickest, 0 to 1. Lower is wispier. |
| `Tint` | The smoke's colour. Its alpha multiplies `Thickness`. |
| `Sprite` | What a puff is drawn with. Empty uses the puff the emitter makes: a soft ragged disc, 128 px, so the sizes mean what they say. |

## `SparkEmitter`

Electric sparks: short-lived bright scraps that fly out, fall under gravity and crackle out. A camera with a bad earth, a
cut cable, a machine burning out.

| Property | What it is |
|---|---|
| `Emitting` | On or off. |
| `Interval`, `Count` | How often a clump is thrown, in seconds, and how many sparks are in it. The engine emits at a rate, so `Count / Interval` sparks a second — 8 with the defaults, and about 20 in the air at once — which looks the same once they are flying. Sparse on purpose: a failing camera throws a scrap or two, not a shower. |
| `Lifetime` | How long a spark lives, in seconds; each one differs a little from this. |
| `Speed`, `Gravity` | How fast it leaves, in units a second, and how hard it is pulled down. |
| `Spread` | How far from the emitter a spark may start, in units. |
| `StartSize`, `EndSize`, `Stretch` | How big a spark is at each end of its life, and how many times taller than wide the engine draws it. Above 1 the renderer also trails it along its motion, which is what makes a fast spark a streak rather than a dot. |
| `Tint` | The sparks' colour. |
| `Additive` | Additive is the hotter look on a dark background, but it disappears against a bright sky. Off, the sparks keep their own colour anywhere — the default, because this game is played outdoors in daylight. |
| `Light`, `LightRadius` | One point light on the emitter, flickering while the sparks fly, which is what makes them read at night. |
| `Crackle`, `CrackleVolume`, `CrackleGap` | The sound, how loud, and the shortest gap between two of them: the first plays the moment the sparks start, then one every 5 seconds. See below. |
| `Sprite` | What a spark is drawn with. Empty uses a hard-edged hot dot the emitter makes, 32 px. |

There is no `UpBias` any more: the engine's emitters throw particles outward from the shape they are given, and a sphere
with a small radius and a speed range already sprays like a short. `Flash` went too — the sparks themselves are bright
scraps at the source, and the light does what the flash used to.

## What both get wrong if you don't tune them

- **`ForceScale` is in hundreds of units a second squared.** `Rise` and `Gravity` are in units a second squared, and the
  components divide by 100 before handing them over, which is the convention the engine's own smoke template uses.
- **Damping eats the force.** A `Damping` of 2 against a small rise leaves a puff with a terminal speed of a dozen units a
  second and a plume that looks like it is standing still. Smoke is damped at 0.8, and rises at 120.
- **`Duration` of 0 and looping means for ever.** A one-second loop looks fine for sparks and leaves a visible gap in a
  smoke plume every second, so both emitters set 0.
- **`Lighting` and `Shadows` off.** A particle is its own light; a lit one goes dark in a corner it should not be dark
  in, and casts a shadow nothing asked for.
- **Small and many beats big and few.** A smoke puff is 12 to 55 units across over its life; a spark is about one unit
  and gone in a fifth of a second. Much bigger, close to the camera, blooms into a glowing petal.
- **The sprite is made, not loaded.** The engine's smoke sheets are 2048 px with a small wisp inside them, so a sprite
  cut from one is mostly empty and the sizes stop meaning anything. Both emitters build a texture that fills its own
  square, and a puff that fades all the way through from its centre reads as a smudge — solid in the middle is smoke.
- **The components are found, not added twice.** `Build()` takes an existing `ParticleEffect`, renderer or emitter on the
  GameObject if there is one, so a prefab that already carries a tuned particle system keeps it and only has its numbers
  rewritten.

## The sparks' sound

A sparking camera is only half there without the noise, so the spark emitter came with a crackle of its own:
`Assets/sounds/effects/sparks.sound`, a `SoundEvent` holding three short takes. `SoundEvent` picks one at random
(`SelectionMode: Random`) and varies it (`Volume: 0.2 0.3`, `Pitch: 0.9 1.15`), so a camera that sparks for a minute
doesn't sound like a loop.

- **`UI` is the 2D switch, and its default is the reason a sound is heard across the map.** `SoundEvent.UI` is "is this
  sound 2D?": with it on there is no position and no distance attenuation, so every knob below does nothing. The crackle
  had it on, copied from another sound; it is off now.
- **It carries 220 units — about 5.5 m** (`Distance`), and its falloff is steep: half at 2% of the way out, gone by 30%.
  A spark is a noise for the player at the camera.
- **3D audio is switched on in code, not in the asset**, so each emitter can be given its own range: the handle gets
  `SpacialBlend` 1 (fully 3D), `OcclusionEnabled` (walls block it), `AirAbsorption` (distance dulls the top of it) and
  `ReverbEnabled` (it traces reflections, so it reaches you round a corner off a wall), with `Reverb` 0.35 so it doesn't
  fill the room. Reflections need surfaces with acoustic properties — an `AudioSurface` on the material — to bounce off;
  the map has none yet, so today it is occlusion. The emitter's `HearingRange` sets the distance and `CrackleVolume` the
  playback level.

The takes are made, not found: `tools/audio/make_spark_crackle.py` writes `Assets/sounds/effects/sparks_{1,2,3}.wav` —
a burst of bright noise that dies in tens of milliseconds, a handful of small ticks across its first tenth of a second,
and a little low body so it reads as a spark rather than a hiss. Run it by hand and let the editor compile the wavs; it
is not part of the build, and the wavs are committed so the game doesn't depend on Python.

`SparkEmitter` plays it when the sparks start — the moment a camera breaks — and then `CrackleGap` afterwards, 5 s by
default: `Crackle` (empty means the asset above) and `CrackleVolume` on top of the sound's own volume. The sound sits
at about 0.2, under everything else that is going on; a shorting camera should be a snap you notice, not a siren.

## The destroyed camera's sparks

`SecurityCamera.EnsureSparks` finds the emitter a camera already has, or makes one at the top of its body
(`Vector3.Up * 6f`) for a camera put together by hand, and sets its `Emitting` to `Destroyed` every frame. A camera sparks
from the moment it is shot to pieces, on every client, and stops when it is repaired. One that is merely unplugged is
off, not shorting: it shows the same static and throws no sparks.

The camera smoked before this (`SmokeEmitter` was on it). The smoke is still in the project and works the same, so
putting one back on the camera prefab is one component — but nothing drives a `SmokeEmitter` from the camera any more.

## Trying it

Add a `Smoke Emitter` or a `Spark Emitter` to any object and press Play: it runs with the defaults. Or
`spawn_security_camera`, shoot the camera, and watch.
