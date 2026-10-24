# Security cameras and the Security System app

A camera that watches a place in the world, and the Navi app that shows what the cameras near a computer see.

## The camera

`SecurityCamera` (`Code/Security/SecurityCamera.cs`) sits on the prefab `Assets/security/camera.prefab`: a wall camera
made of boxes, with the root as the camera's aim (+X forward) and a `Lens Camera` child that holds the `CameraComponent`
the feed is drawn through. `spawn_security_camera` puts one where you stand, at eye height, looking where you look.

| Property | What it is |
|---|---|
| `Label` | The name the app lists. Empty uses the object's name. |
| `SignalRange` | How far from the camera a computer can be and still connect to it, in units (1 is an inch). 1500 is about 38 m, which covers the home scene from the computer at the desk. |
| `FieldOfView` | How wide the view is, in degrees. |

It draws only while something asks for its picture, and stops a second after the last ask, so a camera nobody watches
costs nothing:

- `RequestFeed()` returns one 640×360 render target, the same object on every ask, and starts the clock again.
- `OnPreRender` draws into it at 12 pictures a second while the clock is running. Twelve is enough to read as live and
  a twelfth of what the screen's own camera costs, which matters when nine of them are on one screen.
- The texture is kept for the life of the object (nothing re-creates it when the app closes again), so reopening the
  app is instant.

Nothing about a camera has to be synced to be watched. **The picture never crosses the network:** each client draws its
own, from its own copy of the world and its own copy of the camera. A camera placed in the scene or spawned by the host
exists on every client, so every client can draw it.

## The app

`Code/Apps/Security/`, described by `Assets/navi/system/security.napp`. A core app (`IsSystem` + prepackaged, so it is
installed on every computer with root), and **shared**: the host's copy decides, everyone near the screen sees it.

| File | Holds |
|---|---|
| `SecurityUiState.cs` | The rows and which one is enlarged, plus the wording ("4 cameras", "2 m away"). The computed `Selected`, `Others`, `IsEnlarged`, `CountText`, `Heading` live here so the markup does no arithmetic. |
| `SecurityViewModel.cs` | The app: connects to the cameras in range of the computer (`SecurityCamera.Near`), keeps that list current every half second, and turns a click into the enlarged camera. Runs in the host's copy only. |
| `SecurityFeeds.cs` | This client's own pictures: finds each camera the state names by its object id, and asks for its feed every frame. Never in the state, which goes to everyone. |
| `Security.razor` | The grid of every camera, and the enlarged one with the others listed beside it. No state of its own. |

**It connects to the cameras within `SignalRange` of the computer**, nearest first, up to nine. There is nothing to
register: the app scans the scene, so dropping the prefab in the world is all it takes to add a camera, and a camera
that is removed or switched off leaves the list within half a second.

**Clicking a camera enlarges it** and puts the other cameras in a scrolling panel on its left; clicking one of those
switches to it, and "All cameras" goes back to the grid. Clicks are actions (`Navi.Act`), so the host's copy of the app
decides and every client's window draws the same thing.

**The pictures are each client's own.** The state carries only the camera's object id, which is the same on every
machine; `SecurityFeeds` on each client looks that camera up in its own scene and asks it for its picture. The panel
asks for the pictures every frame in `Tick` — that is what keeps the feeds moving, and closing the window is what stops
them. `Feeds.Revision` bumps when a picture appears or goes away, so the panel redraws when the first frame of a feed
arrives rather than showing an empty cell until something else changes.

A viewer's copy of the app (a client that isn't the host) has no view model: it draws the host's state and its own
pictures, which is why the split between `UiState` and `SecurityFeeds` matters.

## Known limits

- Rendering is per client: with three clients near the same screen, each renders its own copy of the feeds. That is
  cheap for six cameras at 12 frames a second and would not be for sixty.
- A camera is a scene object. A player can walk past it, and shoot it, but nothing turns one off when it is hit: there
  is no damage handling on a camera yet.
