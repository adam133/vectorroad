using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TerraDrive.Tools
{
    /// <summary>
    /// Downloads OpenStreetMap road and building data via the Overpass API for a given
    /// GPS coordinate and radius, then saves the result as a standard <c>.osm</c> file.
    ///
    /// <para><strong>Usage (programmatic)</strong></para>
    /// <code>
    ///   var downloader = new OsmDownloader();
    ///   string xml = await downloader.DownloadOsmAsync(lat: 51.5074, lon: -0.1278, radius: 5000);
    ///   OsmDownloader.SaveOsm(xml, "Assets/Data/london.osm");
    /// </code>
    /// </summary>
    public class OsmDownloader
    {
        /// <summary>Default Overpass API endpoint.</summary>
        public const string DefaultOverpassUrl = "https://overpass-api.de/api/interpreter";

        /// <summary>Maximum number of retry attempts on HTTP 429 responses.</summary>
        internal const int MaxRetries = 5;

        /// <summary>Base delay in seconds for exponential backoff: delay = BackoffBase * 2^attempt.</summary>
        internal const double BackoffBase = 2.0;

        private static readonly string QueryTemplate = string.Join("\n",
            "[out:xml][timeout:90];",
            "(",
            "  way[\"highway\"](around:{radius},{lat},{lon});",
            "  way[\"building\"](around:{radius},{lat},{lon});",
            ");",
            "(._;>;);",
            "out body;");

        private readonly HttpClient _httpClient;
        private readonly string _overpassUrl;

        /// <summary>
        /// Initialises a new instance using the supplied <paramref name="httpClient"/>
        /// and an optional custom endpoint URL.
        /// </summary>
        /// <param name="httpClient">
        /// The HTTP client used to send requests. Pass a pre-configured instance or a
        /// mock for testing.
        /// </param>
        /// <param name="overpassUrl">
        /// Override the default Overpass API endpoint. Defaults to
        /// <see cref="DefaultOverpassUrl"/>.
        /// </param>
        public OsmDownloader(HttpClient httpClient, string overpassUrl = DefaultOverpassUrl)
        {
            _httpClient  = httpClient   ?? throw new ArgumentNullException(nameof(httpClient));
            _overpassUrl = overpassUrl  ?? throw new ArgumentNullException(nameof(overpassUrl));
        }

        /// <summary>
        /// Initialises a new instance with a fresh <see cref="HttpClient"/> and the
        /// default Overpass API endpoint.
        /// </summary>
        public OsmDownloader() : this(new HttpClient()) { }

        /// <summary>
        /// Builds an Overpass QL query string that fetches all <c>highway</c> and
        /// <c>building</c> ways within <paramref name="radius"/> metres of the given
        /// coordinate, including all referenced nodes.
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres.</param>
        /// <returns>Overpass QL query string.</returns>
        public static string BuildQuery(double lat, double lon, int radius)
        {
            return QueryTemplate
                .Replace("{lat}",    lat.ToString(CultureInfo.InvariantCulture))
                .Replace("{lon}",    lon.ToString(CultureInfo.InvariantCulture))
                .Replace("{radius}", radius.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Queries the Overpass API and returns the raw OSM XML response.
        ///
        /// <para>
        /// Retries up to <see cref="MaxRetries"/> times on HTTP 429 (Too Many Requests),
        /// honouring the <c>Retry-After</c> response header when present and falling back
        /// to exponential backoff (<c>BackoffBase * 2^attempt</c> seconds).
        /// </para>
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Raw OSM XML string.</returns>
        /// <exception cref="HttpRequestException">
        /// Thrown if the Overpass API returns a non-2xx status code after all retries are
        /// exhausted (or immediately for non-429 errors).
        /// </exception>
        public async Task<string> DownloadOsmAsync(
            double lat,
            double lon,
            int radius,
            CancellationToken cancellationToken = default)
        {
            string query = BuildQuery(lat, lon, radius);
            Console.WriteLine($"Querying Overpass API (lat={lat}, lon={lon}, radius={radius}m)...");

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                var formData = new FormUrlEncodedContent(
                    new[] { new KeyValuePair<string, string>("data", query) });

                HttpResponseMessage response =
                    await _httpClient.PostAsync(_overpassUrl, formData, cancellationToken)
                                     .ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    if (attempt == MaxRetries)
                        response.EnsureSuccessStatusCode(); // always throws for 4xx/5xx

                    double delay = BackoffBase * Math.Pow(2, attempt);

                    if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
                    {
                        string raw = string.Join(",", values);
                        if (double.TryParse(raw, NumberStyles.Any,
                                            CultureInfo.InvariantCulture, out double parsed))
                            delay = parsed;
                    }

                    Console.WriteLine(
                        $"Rate limited (429). Retrying in {delay:F0}s " +
                        $"(attempt {attempt + 1}/{MaxRetries})...");

                    await WaitAsync(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                string content =
                    await response.Content.ReadAsStringAsync(cancellationToken)
                                  .ConfigureAwait(false);

                Console.WriteLine($"Received {content.Length:N0} bytes from Overpass API.");
                return content;
            }

            // Unreachable: the loop always returns or throws before exhausting retries.
            throw new InvalidOperationException("Retry loop exhausted without returning or throwing.");
        }

        /// <summary>
        /// Delays for the given number of seconds before the next retry attempt.
        /// Override in tests to skip actual waiting.
        /// </summary>
        protected virtual Task WaitAsync(double seconds, CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);

        /// <summary>
        /// Writes <paramref name="content"/> to <paramref name="outputPath"/>, creating
        /// any missing parent directories automatically.
        /// </summary>
        /// <param name="content">OSM XML string to write.</param>
        /// <param name="outputPath">Destination file path.</param>
        public static void SaveOsm(string content, string outputPath)
        {
            string? parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (parent != null)
                Directory.CreateDirectory(parent);

            File.WriteAllText(outputPath, content, Encoding.UTF8);
            Console.WriteLine($"Saved OSM data to: {outputPath}");
        }
    }
}
