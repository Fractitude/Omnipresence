# Effects: smoke

`Code/Effects/` holds the small effects the game makes itself, rather than borrowing a particle file. The first is smoke.

## `SmokeEmitter`

A component you put on anything that should smoke — a broken camera, a chimney, a wreck. It knows nothing about
cameras, and a plume at the default rate costs about two dozen billboards.

| Property | What it is |
|---|---|
| `Emitting` | On or off. Turning it off lets the puffs already in the air finish their life instead of vanishing with the plume. |
| `Rate` | Puffs a second. |
| `Lifetime` | How long a puff lives, in seconds. |
| `Rise` | How fast a puff climbs, in units a second. |
| `Wind` | How far it may wander sideways, in units a second. |
| `Spread` | How far from the emitter a puff may start, in units. |
| `StartSize`, `EndSize` | How big a puff is when it appears and when it goes, in units across. |
| `Tint` | The smoke's colour, alpha included: lower alpha is thinner smoke. |
| `Sprite` | What a puff is drawn with. Empty uses the puff the emitter makes itself: a soft ragged disc, 128 px, so the sizes above mean what they say. |

Each puff is a `SmokePuff`: a billboard that grows, drifts, fades and destroys itself, on a GameObject marked
`NotSaved` and `NetworkMode.Never`. **Nothing about the smoke is saved or sent** — every machine makes the smoke it
sees, the way each client draws its own camera feed. `Emitting` is not synced either: whatever turns the smoke on
decides on each client, from something that is synced.

Two things about the drawing, both found the hard way:

- `SpriteRenderer.AlphaCutoff` has to be 0. At its default of 0.5 the soft rim of a puff is clipped, and a plume reads
  as a string of speckled bubbles instead of smoke.
- `SpriteRenderer.Lighting` and `Shadows` off: smoke is its own light, and a lit billboard goes dark in a corner it
  should not be dark in.

## The security camera's smoke

`SecurityCamera.EnsureSmoke` finds the emitter a camera already has, or makes one at the top of its body
(`Vector3.Up * 9f`) for a camera put together by hand, and sets its `Emitting` to `Broken` every frame. A camera smokes
from the moment it breaks, on every client, and stops if it is ever fixed again.

## Trying it

Add a `Smoke Emitter` to any object and press Play: it smokes with the defaults. Or `spawn_security_camera`, shoot the
camera, and watch it start.
