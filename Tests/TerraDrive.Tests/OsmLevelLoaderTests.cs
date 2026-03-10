using System.IO;
using NUnit.Framework;
using TerraDrive.Core;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OsmLevelLoader"/> — the pure-C# helper that validates
    /// the file-path settings used by the
    /// <b>TerraDrive → Load OSM File / Generate Level</b> editor menu item.
    /// </summary>
    [TestFixture]
    public class OsmLevelLoaderTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static string WriteTempFile(string content, string extension)
        {
            string path = Path.GetTempFileName() + extension;
            File.WriteAllText(path, content);
            return path;
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        // ── IsValid / Validate — empty paths ─────────────────────────────────

        [Test]
        public void IsValid_BothPathsEmpty_ReturnsFalse()
        {
            var loader = new OsmLevelLoader();

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void Validate_BothPathsEmpty_ReturnsTwoErrors()
        {
            var loader = new OsmLevelLoader();

            var errors = loader.Validate();

            Assert.That(errors.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validate_OsmPathEmpty_ContainsOsmError()
        {
            string csvPath = WriteTempFile("1,2,3,4,2,2\n1,1\n1,1\n", ".elevation.csv");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = string.Empty,
                    ElevationCsvPath = csvPath,
                };

                var errors = loader.Validate();

                Assert.That(errors, Has.Some.Contains("OSM file path must not be empty"));
            }
            finally { DeleteFile(csvPath); }
        }

        [Test]
        public void Validate_CsvPathEmpty_ContainsCsvError()
        {
            string osmPath = WriteTempFile("<osm/>", ".osm");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = osmPath,
                    ElevationCsvPath = string.Empty,
                };

                var errors = loader.Validate();

                Assert.That(errors, Has.Some.Contains("Elevation CSV path must not be empty"));
            }
            finally { DeleteFile(osmPath); }
        }

        // ── IsValid / Validate — missing files ────────────────────────────────

        [Test]
        public void Validate_OsmFileMissing_ContainsMissingFileError()
        {
            string csvPath = WriteTempFile("1,2,3,4,2,2\n1,1\n1,1\n", ".elevation.csv");
            string nonExistentOsm = Path.Combine(Path.GetTempPath(),
                Path.GetRandomFileName() + ".osm");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = nonExistentOsm,
                    ElevationCsvPath = csvPath,
                };

                var errors = loader.Validate();

                Assert.That(errors, Has.Some.Contains("OSM file not found"));
            }
            finally { DeleteFile(csvPath); }
        }

        [Test]
        public void Validate_CsvFileMissing_ContainsMissingFileError()
        {
            string osmPath = WriteTempFile("<osm/>", ".osm");
            string nonExistentCsv = Path.Combine(Path.GetTempPath(),
                Path.GetRandomFileName() + ".elevation.csv");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = osmPath,
                    ElevationCsvPath = nonExistentCsv,
                };

                var errors = loader.Validate();

                Assert.That(errors, Has.Some.Contains("Elevation CSV file not found"));
            }
            finally { DeleteFile(osmPath); }
        }

        [Test]
        public void IsValid_OsmFileMissing_ReturnsFalse()
        {
            string csvPath = WriteTempFile("1,2,3,4,2,2\n1,1\n1,1\n", ".elevation.csv");
            string nonExistentOsm = Path.Combine(Path.GetTempPath(),
                Path.GetRandomFileName() + ".osm");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = nonExistentOsm,
                    ElevationCsvPath = csvPath,
                };

                Assert.That(loader.IsValid(), Is.False);
            }
            finally { DeleteFile(csvPath); }
        }

        // ── IsValid / Validate — valid paths ─────────────────────────────────

        [Test]
        public void IsValid_BothFilesExist_ReturnsTrue()
        {
            string osmPath = WriteTempFile("<osm/>", ".osm");
            string csvPath = WriteTempFile("1,2,3,4,2,2\n1,1\n1,1\n", ".elevation.csv");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = osmPath,
                    ElevationCsvPath = csvPath,
                };

                Assert.That(loader.IsValid(), Is.True);
            }
            finally { DeleteFile(osmPath); DeleteFile(csvPath); }
        }

        [Test]
        public void Validate_BothFilesExist_ReturnsEmptyList()
        {
            string osmPath = WriteTempFile("<osm/>", ".osm");
            string csvPath = WriteTempFile("1,2,3,4,2,2\n1,1\n1,1\n", ".elevation.csv");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = osmPath,
                    ElevationCsvPath = csvPath,
                };

                Assert.That(loader.Validate(), Is.Empty);
            }
            finally { DeleteFile(osmPath); DeleteFile(csvPath); }
        }

        // ── Property assignment ───────────────────────────────────────────────

        [Test]
        public void OsmFilePath_DefaultValue_IsEmpty()
        {
            var loader = new OsmLevelLoader();
            Assert.That(loader.OsmFilePath, Is.Empty);
        }

        [Test]
        public void ElevationCsvPath_DefaultValue_IsEmpty()
        {
            var loader = new OsmLevelLoader();
            Assert.That(loader.ElevationCsvPath, Is.Empty);
        }

        [Test]
        public void OsmFilePath_SetValue_RoundTrips()
        {
            var loader = new OsmLevelLoader { OsmFilePath = "/some/path/map.osm" };
            Assert.That(loader.OsmFilePath, Is.EqualTo("/some/path/map.osm"));
        }

        [Test]
        public void ElevationCsvPath_SetValue_RoundTrips()
        {
            var loader = new OsmLevelLoader { ElevationCsvPath = "/some/path/map.elevation.csv" };
            Assert.That(loader.ElevationCsvPath, Is.EqualTo("/some/path/map.elevation.csv"));
        }

        // ── Whitespace paths treated as empty ─────────────────────────────────

        [Test]
        public void Validate_WhiteSpaceOsmPath_ReportsEmptyError()
        {
            string csvPath = WriteTempFile("1,2,3,4,2,2\n1,1\n1,1\n", ".elevation.csv");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = "   ",
                    ElevationCsvPath = csvPath,
                };

                var errors = loader.Validate();

                Assert.That(errors, Has.Some.Contains("OSM file path must not be empty"));
            }
            finally { DeleteFile(csvPath); }
        }

        [Test]
        public void Validate_WhiteSpaceCsvPath_ReportsEmptyError()
        {
            string osmPath = WriteTempFile("<osm/>", ".osm");
            try
            {
                var loader = new OsmLevelLoader
                {
                    OsmFilePath      = osmPath,
                    ElevationCsvPath = "   ",
                };

                var errors = loader.Validate();

                Assert.That(errors, Has.Some.Contains("Elevation CSV path must not be empty"));
            }
            finally { DeleteFile(osmPath); }
        }
    }
}
