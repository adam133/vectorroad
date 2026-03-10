using System.Collections.Generic;
using System.IO;

namespace TerraDrive.Core
{
    /// <summary>
    /// Holds the file-path settings for a
    /// <b>TerraDrive → Load OSM File / Generate Level</b> operation and validates them
    /// before the Editor wires them into a <see cref="MapSceneBuilder"/> component.
    ///
    /// <para>
    /// This class is pure C# (no Unity Engine dependency) so it can be exercised by the
    /// .NET unit-test suite as well as by the Unity Editor script.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   var loader = new OsmLevelLoader
    ///   {
    ///       OsmFilePath       = "/path/to/map.osm",
    ///       ElevationCsvPath  = "/path/to/map.elevation.csv",
    ///   };
    ///
    ///   IReadOnlyList&lt;string&gt; errors = loader.Validate();
    ///   if (errors.Count == 0)
    ///   {
    ///       // Apply to MapSceneBuilder and enter Play mode.
    ///   }
    /// </code>
    /// </summary>
    public sealed class OsmLevelLoader
    {
        // ── Settings ───────────────────────────────────────────────────────────

        /// <summary>
        /// Absolute or project-root-relative path to the <c>.osm</c> (or
        /// <c>.osm.xml</c>) file.
        /// </summary>
        public string OsmFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Absolute or project-root-relative path to the companion
        /// <c>.elevation.csv</c> file produced by the OSM downloader CLI.
        /// </summary>
        public string ElevationCsvPath { get; set; } = string.Empty;

        // ── Validation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the current settings and returns a list of human-readable error
        /// messages.  An empty list means the settings are valid.
        /// </summary>
        /// <returns>
        /// A read-only list of error strings.  Empty when both paths are non-empty and
        /// both files exist on disk.
        /// </returns>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(OsmFilePath))
                errors.Add("OSM file path must not be empty.");
            else if (!File.Exists(OsmFilePath))
                errors.Add($"OSM file not found: {OsmFilePath}");

            if (string.IsNullOrWhiteSpace(ElevationCsvPath))
                errors.Add("Elevation CSV path must not be empty.");
            else if (!File.Exists(ElevationCsvPath))
                errors.Add($"Elevation CSV file not found: {ElevationCsvPath}");

            return errors;
        }

        /// <summary>
        /// Returns <c>true</c> when <see cref="Validate"/> produces no errors.
        /// </summary>
        public bool IsValid() => Validate().Count == 0;
    }
}
