using System;
using System.IO;
using NUnit.Framework;
using TerraDrive.Core;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Integration tests that render a perspective chase-cam screenshot from a
    /// static position on the sample map and save it so the CI workflow can upload
    /// it as a pull-request artifact.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class ChaseCamIntegrationTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Walks up from the test-binary directory until it finds
        /// <c>Assets/Data/map.osm.xml</c>, matching the repository layout regardless
        /// of the build configuration (Debug / Release) or working directory.
        /// </summary>
        private static string FindOsmMapFile()
        {
            string dir = Path.GetDirectoryName(
                typeof(ChaseCamIntegrationTests).Assembly.Location)
                ?? Directory.GetCurrentDirectory();

            for (int depth = 0; depth < 8; depth++)
            {
                string candidate = Path.Combine(dir, "Assets", "Data", "map.osm.xml");
                if (File.Exists(candidate))
                    return candidate;

                string? parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }

            throw new FileNotFoundException(
                "Could not locate Assets/Data/map.osm.xml relative to the test binary. " +
                "Ensure the repository is checked out in full.");
        }

        // ── tests ──────────────────────────────────────────────────────────────

        [Test]
        [Description("Parses the sample OSM file, renders a perspective chase-cam PNG from " +
                     "a static position at the map centre, and saves it for the CI artifact upload.")]
        public void Render_ChaseCamView_ProducesPngFile()
        {
            // ── Arrange ────────────────────────────────────────────────────────
            string osmPath = FindOsmMapFile();

            // Centre of the map bounds (minlat=41.8739, maxlat=41.9175,
            //                           minlon=-93.6240, maxlon=-93.5535)
            const double originLat =  41.8957;
            const double originLon = -93.5888;

            CoordinateConverter.ResetWorldOrigin();
            var (roads, buildings, _) = OSMParser.Parse(osmPath, originLat, originLon);

            // Static camera position: the car is at world origin (0, 0) — the
            // centre of the test map — facing forward (+Z direction).
            const float carX = 0f;
            const float carZ = 0f;

            // ── Act ────────────────────────────────────────────────────────────
            using var bitmap = ChaseCamRenderer.Render(roads, buildings, carX, carZ);

            string outputDir = Environment.GetEnvironmentVariable("CHASE_CAM_PREVIEW_DIR")
                ?? TestContext.CurrentContext.WorkDirectory;
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "chase-cam-preview.png");

            ChaseCamRenderer.SaveAsPng(bitmap, outputPath);

            // ── Assert ─────────────────────────────────────────────────────────
            Assert.That(File.Exists(outputPath), Is.True,
                $"Expected chase-cam-preview.png at: {outputPath}");

            long fileSize = new FileInfo(outputPath).Length;
            Assert.That(fileSize, Is.GreaterThan(10_000L),
                "Rendered PNG should be larger than 10 KB");

            Assert.That(bitmap.Width,  Is.EqualTo(1600));
            Assert.That(bitmap.Height, Is.EqualTo(900));

            TestContext.Out.WriteLine($"Chase-cam preview written to: {outputPath}");
            TestContext.Out.WriteLine($"  Roads parsed:     {roads.Count}");
            TestContext.Out.WriteLine($"  Buildings parsed: {buildings.Count}");
            TestContext.Out.WriteLine($"  PNG size:         {fileSize / 1024} KB");
        }
    }
}
