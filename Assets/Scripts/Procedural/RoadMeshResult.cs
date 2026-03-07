using UnityEngine;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Holds the set of meshes and region-appropriate texture identifiers produced by
    /// <see cref="RoadMeshExtruder.ExtrudeWithDetails"/>.
    /// </summary>
    public readonly struct RoadMeshResult
    {
        /// <summary>
        /// Road surface mesh.
        /// UV channel 0 = asphalt texture tiling (U 0→1 across road, V tiles every
        /// <c>uvTileLength</c> metres).
        /// UV channel 1 = lane-marking tiling (same U, V tiles every
        /// <c>laneMarkingTileLength</c> metres, allowing a separate lane-marking
        /// texture or shader to render centre lines and edge markings).
        /// </summary>
        public readonly Mesh RoadMesh;

        /// <summary>
        /// Kerb (curb) strips on both sides of the carriageway, combined into one
        /// mesh.  The kerb quads sit just outside and slightly above the road surface
        /// so that a separate kerb material can be applied.
        /// UV channel 0 = kerb texture tiling (U 0→1 outer→inner, V tiles by
        /// distance along road).
        /// </summary>
        public readonly Mesh KerbMesh;

        /// <summary>
        /// Region-appropriate texture asset name for the road surface (e.g.
        /// <c>"road_asphalt_temperate"</c>).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string RoadTextureId;

        /// <summary>
        /// Region-appropriate texture asset name for the kerb surface (e.g.
        /// <c>"kerb_stone"</c>).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string KerbTextureId;

        /// <summary>
        /// Creates a new <see cref="RoadMeshResult"/>.
        /// </summary>
        /// <param name="roadMesh">The extruded road surface mesh.</param>
        /// <param name="kerbMesh">The combined kerb mesh.</param>
        /// <param name="roadTextureId">Texture asset name for the road surface.</param>
        /// <param name="kerbTextureId">Texture asset name for the kerb surface.</param>
        public RoadMeshResult(Mesh roadMesh, Mesh kerbMesh, string roadTextureId, string kerbTextureId)
        {
            RoadMesh      = roadMesh;
            KerbMesh      = kerbMesh;
            RoadTextureId = roadTextureId;
            KerbTextureId = kerbTextureId;
        }
    }
}
