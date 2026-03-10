using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TerraDrive.Core;

namespace TerraDrive.Editor
{
    /// <summary>
    /// Adds a <b>TerraDrive → Load OSM File / Generate Level</b> menu item to the Unity
    /// Editor menu bar.
    ///
    /// <para>
    /// When invoked the editor:
    /// <list type="number">
    ///   <item>Opens a file-picker for the <c>.osm</c> (or <c>.osm.xml</c>) data file.</item>
    ///   <item>Opens a second file-picker for the companion <c>.elevation.csv</c> file.</item>
    ///   <item>
    ///     Validates both paths via <see cref="OsmLevelLoader"/>.  If validation fails
    ///     an error dialog is shown and the operation is aborted.
    ///   </item>
    ///   <item>
    ///     Finds the first <see cref="MapSceneBuilder"/> in the active scene, or creates
    ///     a new <c>MapSceneBuilder</c> GameObject if none exists.
    ///   </item>
    ///   <item>
    ///     Assigns the chosen paths to
    ///     <see cref="MapSceneBuilder.OsmFilePath"/> /
    ///     <see cref="MapSceneBuilder.ElevationCsvPath"/> and marks the scene dirty.
    ///   </item>
    ///   <item>
    ///     Offers to enter Play mode so the <see cref="MapSceneBuilder"/> coroutine runs
    ///     immediately and builds the level geometry.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public static class LoadOsmMenuEditor
    {
        [MenuItem("TerraDrive/Load OSM File / Generate Level")]
        public static void LoadOsmFileAndGenerateLevel()
        {
            // 1. Pick the .osm file.
            string osmPath = EditorUtility.OpenFilePanel(
                "Select OSM File", "Assets/Data", "xml,osm");

            if (string.IsNullOrEmpty(osmPath))
                return;

            // 2. Pick the companion .elevation.csv file.
            string csvPath = EditorUtility.OpenFilePanel(
                "Select Elevation CSV File", "Assets/Data", "csv");

            if (string.IsNullOrEmpty(csvPath))
                return;

            // 3. Validate both paths with the pure-C# helper.
            var loader = new OsmLevelLoader
            {
                OsmFilePath      = osmPath,
                ElevationCsvPath = csvPath,
            };

            var errors = loader.Validate();
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "TerraDrive — Invalid Files",
                    string.Join("\n", errors),
                    "OK");
                return;
            }

            // 4. Find or create a MapSceneBuilder in the active scene.
#pragma warning disable CS0618 // FindObjectOfType is fine for editor-only code
            var builder = UnityEngine.Object.FindObjectOfType<MapSceneBuilder>();
#pragma warning restore CS0618
            if (builder == null)
            {
                var go = new GameObject("MapSceneBuilder");
                builder = go.AddComponent<MapSceneBuilder>();
                Debug.Log("[LoadOsmMenu] Created a new MapSceneBuilder GameObject.");
            }

            // 5. Configure paths and mark the scene dirty.
            builder.OsmFilePath      = osmPath;
            builder.ElevationCsvPath = csvPath;
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

            Debug.Log(
                $"[LoadOsmMenu] MapSceneBuilder configured.\n" +
                $"  OSM: {osmPath}\n" +
                $"  CSV: {csvPath}");

            // 6. Offer to enter Play mode.
            bool enterPlay = EditorUtility.DisplayDialog(
                "TerraDrive — Generate Level",
                $"Files configured:\n\n• OSM:  {osmPath}\n• CSV:  {csvPath}\n\n" +
                "Enter Play mode now to generate the level?",
                "Enter Play Mode",
                "Later");

            if (enterPlay)
                EditorApplication.isPlaying = true;
        }
    }
}
