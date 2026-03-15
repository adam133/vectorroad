# HUD Scripts

Head-up display utilities that consume game state and render it for the player.

| File | Purpose |
|---|---|
| `MinimapRenderer.cs` | Converts road segments to normalised [0, 1] minimap line coordinates relative to the player's position |
| `MinimapHud.cs` | Unity `MonoBehaviour` that renders `MinimapRenderer` output onto a UI `RawImage` each frame |
| `CoordinateEntryHud.cs` | In-game IMGUI dialog (toggle with `Escape`) for entering new GPS coordinates and reloading the scene |
| `StartupMenuUi.cs` | Programmatic uGUI startup menu shown in `MainMenu` state; auto-created by `MapSceneBuilder` |

## MinimapRenderer

`MinimapRenderer` is a plain C# class (not a `MonoBehaviour`) that can be created and called
from any HUD script.  It projects nearby road segments into a normalised minimap coordinate
space where **(0.5, 0.5) is always the player**.

```csharp
var minimap = new MinimapRenderer { Radius = 200f };
List<MinimapLine> lines = minimap.BuildLines(mapData.Roads, carTransform.position, carYaw);

// Draw each line on a Unity UI RawImage or via GL/Canvas:
foreach (MinimapLine line in lines)
{
    // line.Start and line.End are in [0, 1] minimap space
    // line.RoadType can be used to pick a colour (e.g. motorway = yellow, residential = white)
}
```

### Properties

| Property | Default | Description |
|---|---|---|
| `Radius` | `150 m` | World-space radius of the visible minimap area.  Roads whose both endpoints are outside this radius are excluded. |

### MinimapLine

Each `MinimapLine` returned by `BuildLines` has:

| Property | Type | Description |
|---|---|---|
| `Start` | `Vector2` | Start point in normalised [0, 1] minimap space |
| `End` | `Vector2` | End point in normalised [0, 1] minimap space |
| `RoadType` | `RoadType` | Functional road type — use to select the line colour when rendering |

### Player-rotation support

Pass the player's current yaw angle (degrees, clockwise from north / +Z) as the third argument
to `BuildLines`.  When non-zero, the entire map is rotated so the player's forward direction
always points toward the top of the minimap display.

## MinimapHud

`MinimapHud` is a Unity `MonoBehaviour` that wires `MinimapRenderer` to a UI `RawImage` and
repaints it every frame using Bresenham's line algorithm.

Attach it to any persistent GameObject in the scene (e.g. the same one that holds
`MapSceneBuilder`).  Assign a `RawImage` from the HUD Canvas to `Target`, then call `Init`
once the map has loaded (or let `MapSceneBuilder` call it automatically):

```csharp
minimapHud.Init(carTransform, mapData.Roads);
```

### Inspector Parameters

| Field | Default | Description |
|---|---|---|
| `Target` | *(scene ref)* | `RawImage` on the HUD Canvas that will display the minimap |
| `Radius` | `150 m` | World-space radius of the visible minimap area |
| `Resolution` | `256` | Side length in pixels of the minimap texture (square) |

## CoordinateEntryHud

`CoordinateEntryHud` is a Unity `MonoBehaviour` that shows an IMGUI overlay dialog while in
Play mode, letting the player enter new GPS coordinates and reload the scene without leaving the
editor.

Attach it to any persistent GameObject (e.g. the same one that carries `GameManager`).
Press `ToggleKey` (default: `Escape`) to open or close the dialog at any time.

When the player confirms new coordinates the component:
1. Validates the input via `OsmLevelLoader`.
2. Downloads OSM and elevation data using `OsmDownloader`.
3. Stores the downloaded file paths in `GameManager.OsmFilePathOverride` and
   `GameManager.ElevationFilePathOverride` so `MapSceneBuilder` picks them up on reload.
4. Reloads the active scene, triggering a full level rebuild.

### Inspector Parameters

| Field | Default | Description |
|---|---|---|
| `ToggleKey` | `Escape` | Key that opens and closes the coordinate-entry dialog |
| `DefaultLatitude` | `51.5074` | Latitude pre-filled in the dialog |
| `DefaultLongitude` | `-0.1278` | Longitude pre-filled in the dialog |
| `DefaultRadius` | `500` | Search radius (metres) pre-filled in the dialog |

## StartupMenuUi

`StartupMenuUi` is a programmatic uGUI startup menu displayed when the game is in the
`GameState.MainMenu` state.  It gives the player two choices: load the bundled default map
immediately, or download a new location before building the level.

`MapSceneBuilder` creates this component automatically when `MainMenu` state is detected at
startup — no prefab or manual scene placement is required.  The component builds the full
Canvas hierarchy (splash panel, download panel, and loading panel) entirely in code using
TextMeshPro labels and standard Unity UI controls.
