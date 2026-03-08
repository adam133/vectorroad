using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TerraDrive.Tools;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OsmDownloader"/>.
    ///
    /// Covers <c>BuildQuery</c>, <c>DownloadOsmAsync</c> (HTTP mocked), retry behaviour
    /// on 429 responses, and <c>SaveOsm</c> file I/O — mirroring the coverage of the
    /// original Python test suite.
    /// </summary>
    [TestFixture]
    public class OsmDownloaderTests
    {
        // ── Minimal Overpass XML used as a stub API response ──────────────────

        private const string SampleOverpassXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <osm version="0.6" generator="Overpass API 0.7.62.1">
              <note>The data included in this document is from www.openstreetmap.org.</note>
              <meta osm_base="2024-01-01T00:00:00Z"/>
              <node id="1" lat="51.5000" lon="-0.1000">
                <tag k="name" v="Node A"/>
              </node>
              <node id="2" lat="51.5010" lon="-0.1010"/>
              <node id="3" lat="51.5020" lon="-0.1020"/>
              <node id="4" lat="51.5000" lon="-0.1020"/>
              <way id="100">
                <nd ref="1"/>
                <nd ref="2"/>
                <tag k="highway" v="primary"/>
                <tag k="name" v="Test Road"/>
              </way>
              <way id="200">
                <nd ref="3"/>
                <nd ref="4"/>
                <nd ref="3"/>
                <tag k="building" v="yes"/>
                <tag k="building:levels" v="3"/>
              </way>
            </osm>
            """;

        // ── BuildQuery ────────────────────────────────────────────────────────

        [Test]
        public void BuildQuery_ContainsLatLonRadius()
        {
            string q = OsmDownloader.BuildQuery(51.5, -0.1, 1000);

            Assert.That(q, Does.Contain("51.5"));
            Assert.That(q, Does.Contain("-0.1"));
            Assert.That(q, Does.Contain("1000"));
        }

        [Test]
        public void BuildQuery_ContainsHighwayFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"highway\"]"));
        }

        [Test]
        public void BuildQuery_ContainsBuildingFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"building\"]"));
        }

        [Test]
        public void BuildQuery_ContainsRecurseDown()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("._;>;"),
                "(._;>;) must be present so referenced nodes are included.");
        }

        [Test]
        public void BuildQuery_ContainsOutXmlDirective()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("[out:xml]"));
        }

        [Test]
        public void BuildQuery_DifferentRadiiProduceDifferentQueries()
        {
            string q1 = OsmDownloader.BuildQuery(0.0, 0.0, 500);
            string q2 = OsmDownloader.BuildQuery(0.0, 0.0, 2000);

            Assert.That(q1, Is.Not.EqualTo(q2));
        }

        [Test]
        public void BuildQuery_UsesInvariantCultureDecimalSeparator()
        {
            string q = OsmDownloader.BuildQuery(1.5, 2.5, 100);

            Assert.That(q, Does.Contain("1.5"));
            Assert.That(q, Does.Contain("2.5"));
            Assert.That(q, Does.Not.Contain("1,5"));
        }

        // ── DownloadOsmAsync ──────────────────────────────────────────────────

        [Test]
        public async Task DownloadOsmAsync_SuccessResponse_ReturnsXml()
        {
            var handler = MakeHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                });

            var downloader = new OsmDownloader(new HttpClient(handler));

            string result = await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(result, Is.EqualTo(SampleOverpassXml));
        }

        [Test]
        public async Task DownloadOsmAsync_PostsToOverpassUrl()
        {
            HttpRequestMessage? captured = null;
            var handler = MakeHandler(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new OsmDownloader(new HttpClient(handler), "https://example.com/overpass");

            await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.RequestUri!.ToString(), Is.EqualTo("https://example.com/overpass"));
        }

        [Test]
        public async Task DownloadOsmAsync_RequestBodyContainsQuery()
        {
            string? bodyContent = null;
            var handler = new CapturingBodyHandler(content =>
            {
                bodyContent = content;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new OsmDownloader(new HttpClient(handler));

            await downloader.DownloadOsmAsync(51.5, -0.1, 500);

            Assert.That(bodyContent, Does.Contain("data="),
                "The POST body should include a 'data' form field.");
            Assert.That(bodyContent, Does.Contain("51.5"),
                "The POST body should contain the latitude.");
        }

        [Test]
        public void DownloadOsmAsync_HttpError_ThrowsHttpRequestException()
        {
            var handler = MakeHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var downloader = new OsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));
        }

        // ── Retry on 429 ─────────────────────────────────────────────────────

        [Test]
        public async Task DownloadOsmAsync_429ThenSuccess_RetriesAndReturnsXml()
        {
            int callCount = 0;
            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage((HttpStatusCode)429);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            // Use a subclass that overrides delay to make tests instant
            var downloader = new InstantRetryOsmDownloader(new HttpClient(handler));

            string result = await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(result, Is.EqualTo(SampleOverpassXml));
            Assert.That(callCount, Is.EqualTo(2));
        }

        [Test]
        public void DownloadOsmAsync_AllRetriesExhausted_ThrowsHttpRequestException()
        {
            var handler = MakeHandler(_ => new HttpResponseMessage((HttpStatusCode)429));

            var downloader = new InstantRetryOsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));
        }

        [Test]
        public async Task DownloadOsmAsync_RetryAfterHeader_IsRespected()
        {
            int callCount = 0;
            double? observedDelay = null;

            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var r429 = new HttpResponseMessage((HttpStatusCode)429);
                    r429.Headers.Add("Retry-After", "30");
                    return r429;
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new CapturingRetryOsmDownloader(
                new HttpClient(handler), d => observedDelay = d);

            await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(observedDelay, Is.EqualTo(30.0));
        }

        [Test]
        public async Task DownloadOsmAsync_ExponentialBackoff_DelayDoublesEachAttempt()
        {
            int callCount = 0;
            var delays = new List<double>();

            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount <= 2)
                    return new HttpResponseMessage((HttpStatusCode)429);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new CapturingRetryOsmDownloader(
                new HttpClient(handler), d => delays.Add(d));

            await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(delays.Count, Is.EqualTo(2));
            Assert.That(delays[0], Is.EqualTo(OsmDownloader.BackoffBase * Math.Pow(2, 0)));
            Assert.That(delays[1], Is.EqualTo(OsmDownloader.BackoffBase * Math.Pow(2, 1)));
        }

        [Test]
        public void DownloadOsmAsync_Non429Error_RaisesImmediately()
        {
            int callCount = 0;
            var handler = MakeHandler(_ =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

            var downloader = new OsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));

            Assert.That(callCount, Is.EqualTo(1), "Non-429 errors must not be retried.");
        }

        [Test]
        public void DownloadOsmAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = MakeHandler(_ => throw new TaskCanceledException());
            var downloader = new OsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<TaskCanceledException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000, cts.Token));
        }

        // ── SaveOsm ───────────────────────────────────────────────────────────

        [Test]
        public void SaveOsm_WritesFileToPath()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "out.osm");

            OsmDownloader.SaveOsm(SampleOverpassXml, path);

            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void SaveOsm_FileContentMatchesInput()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "out.osm");

            OsmDownloader.SaveOsm(SampleOverpassXml, path);

            string content = File.ReadAllText(path, Encoding.UTF8);
            Assert.That(content, Is.EqualTo(SampleOverpassXml));
        }

        [Test]
        public void SaveOsm_CreatesParentDirectories()
        {
            using var tmp = new TempDirectory();
            string nested = Path.Combine(tmp.Path, "a", "b", "c", "out.osm");

            OsmDownloader.SaveOsm(SampleOverpassXml, nested);

            Assert.That(File.Exists(nested), Is.True);
        }

        [Test]
        public void SaveOsm_PreservesUtf8Characters()
        {
            const string germanStreet = "Stra\u00dfe"; // "Straße"
            string content =
                "<?xml version=\"1.0\"?>" +
                "<osm>" +
                "<node id=\"1\" lat=\"0\" lon=\"0\">" +
                $"<tag k=\"name\" v=\"{germanStreet}\"/>" +
                "</node>" +
                "</osm>";

            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "utf8.osm");

            OsmDownloader.SaveOsm(content, path);

            string result = File.ReadAllText(path, Encoding.UTF8);
            Assert.That(result, Does.Contain(germanStreet));
        }

        // ── Constructor ───────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullHttpClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OsmDownloader(null!, OsmDownloader.DefaultOverpassUrl));
        }

        [Test]
        public void Constructor_NullOverpassUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OsmDownloader(new HttpClient(), null!));
        }

        [Test]
        public void DefaultOverpassUrl_IsOverpassApiEndpoint()
        {
            Assert.That(OsmDownloader.DefaultOverpassUrl,
                Does.StartWith("https://overpass-api.de"),
                "Default endpoint should point to overpass-api.de.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static StubHttpMessageHandler MakeHandler(
            Func<HttpRequestMessage, HttpResponseMessage> fn) =>
            new(fn);

        // Stub HTTP handler -──────────────────────────────────────────────────

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
                => _handler = handler;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_handler(request));
            }
        }

        // HTTP handler that reads the request body before delegating ──────────

        private sealed class CapturingBodyHandler : HttpMessageHandler
        {
            private readonly Func<string, HttpResponseMessage> _handler;

            public CapturingBodyHandler(Func<string, HttpResponseMessage> handler)
                => _handler = handler;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content != null
                    ? await request.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;
                return _handler(body);
            }
        }

        // OsmDownloader subclass that skips actual Task.Delay (tests run instantly) ──

        private sealed class InstantRetryOsmDownloader : OsmDownloader
        {
            public InstantRetryOsmDownloader(HttpClient client)
                : base(client, DefaultOverpassUrl) { }

            protected override Task WaitAsync(double seconds, CancellationToken ct) =>
                Task.CompletedTask;
        }

        // OsmDownloader subclass that captures the computed delay value ──────────────

        private sealed class CapturingRetryOsmDownloader : OsmDownloader
        {
            private readonly Action<double> _onDelay;

            public CapturingRetryOsmDownloader(HttpClient client, Action<double> onDelay)
                : base(client, DefaultOverpassUrl) => _onDelay = onDelay;

            protected override Task WaitAsync(double seconds, CancellationToken ct)
            {
                _onDelay(seconds);
                return Task.CompletedTask;
            }
        }

        // Temp-directory helper ────────────────────────────────────────────────

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                System.IO.Path.GetRandomFileName());

            public TempDirectory() => Directory.CreateDirectory(Path);

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
        }
    }
}
