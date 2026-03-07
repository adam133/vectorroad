# Procedural Scripts

Mesh-generation systems that convert parsed OSM data into drivable geometry.

| File | Purpose |
|---|---|
| `SplineGenerator.cs` | Builds Catmull-Rom splines from lists of `Vector3` road nodes |
| `RoadMeshExtruder.cs` | Extrudes a UV-mapped road mesh along a spline |
| `BuildingGenerator.cs` | Extrudes building footprints into 3D meshes with randomised heights |
| `RegionTextures.cs` | Maps `RegionType` to region-appropriate texture asset IDs for roads and buildings |
| `BuildingMeshResult.cs` | Result type returned by `BuildingGenerator.Extrude` (meshes + texture IDs) |
| `RoadMeshResult.cs` | Result type returned by `RoadMeshExtruder.ExtrudeWithDetails` (meshes + texture IDs) |

## SplineGenerator

```csharp
var spline = SplineGenerator.BuildCatmullRom(nodes, samplesPerSegment: 20);
```

Returns a `List<Vector3>` of evenly-sampled world-space positions along the smoothed curve.

## RoadMeshExtruder

```csharp
// Simple road mesh (no texture selection):
var mesh = RoadMeshExtruder.Extrude(splinePoints, roadWidth: 7f);
meshFilter.sharedMesh = mesh;

// Detailed road mesh with region-appropriate texture identifiers:
RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, RoadType.Primary, region: region);
roadFilter.sharedMesh   = result.RoadMesh;   // UV0 = asphalt, UV1 = lane markings
kerbFilter.sharedMesh   = result.KerbMesh;
// Apply materials keyed by result.RoadTextureId and result.KerbTextureId
```

UV coordinates tile along the road length so an asphalt texture repeats at a natural scale.

## BuildingGenerator

```csharp
BuildingMeshResult result = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f, region: region);
wallFilter.sharedMesh = result.WallMesh;
roofFilter.sharedMesh = result.RoofMesh;
// Apply materials keyed by result.WallTextureId and result.RoofTextureId
```

Heights are randomised per-building using a seeded RNG derived from the OSM `WayId`, so the
same map always produces the same city skyline.

## RegionTextures

Provides the texture identifier look-up used by both `RoadMeshExtruder` and `BuildingGenerator`.
Can also be called directly to retrieve individual texture IDs:

```csharp
string roadTex = RegionTextures.GetRoadSurfaceTextureId(region, roadType);
string kerbTex = RegionTextures.GetKerbTextureId(region);
string wallTex = RegionTextures.GetWallTextureId(region);
string roofTex = RegionTextures.GetRoofTextureId(region);
```

### Region → texture mapping

| Region | Road surface | Kerb | Wall | Roof |
|---|---|---|---|---|
| Temperate | `road_asphalt_temperate` | `kerb_stone` | `building_wall_brick` | `building_roof_slate` |
| Desert | `road_asphalt_desert` | `kerb_concrete` | `building_wall_sandstone` | `building_roof_terracotta` |
| Tropical | `road_asphalt_tropical` | `kerb_concrete` | `building_wall_stucco` | `building_roof_terracotta` |
| Boreal | `road_asphalt_boreal` | `kerb_stone` | `building_wall_timber` | `building_roof_metal` |
| Arctic | `road_asphalt_arctic` | `kerb_concrete` | `building_wall_concrete` | `building_roof_metal` |
| Mediterranean | `road_asphalt_mediterranean` | `kerb_granite` | `building_wall_stucco` | `building_roof_terracotta` |
| Steppe | `road_asphalt_steppe` | `kerb_concrete` | `building_wall_concrete` | `building_roof_flat` |
| Unknown | `road_asphalt` | `kerb_stone` | `building_wall_brick` | `building_roof_slate` |

Unpaved roads (`RoadType.Dirt` and `RoadType.Path`) use a surface texture that varies by region:
`road_dirt` (default), `road_sand` (Desert), `road_mud` (Tropical), `road_gravel_boreal` (Boreal),
`road_gravel_arctic` (Arctic).
