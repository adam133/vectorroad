using System;
using System.Net.Http;
using System.Threading.Tasks;
using TerraDrive.Tools;

/// <summary>
/// Entry point for the OsmDownloader command-line tool.
///
/// Usage:
///   OsmDownloader --lat &lt;latitude&gt; --lon &lt;longitude&gt; [--radius &lt;metres&gt;] [--output &lt;path&gt;]
///
/// Example:
///   OsmDownloader --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        double? lat    = null;
        double? lon    = null;
        int     radius = 5000;
        string  output = "output.osm";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--lat" when i + 1 < args.Length:
                    if (!double.TryParse(args[++i],
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double latVal))
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --lat: {args[i]}");
                        return 1;
                    }
                    lat = latVal;
                    break;

                case "--lon" when i + 1 < args.Length:
                    if (!double.TryParse(args[++i],
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double lonVal))
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --lon: {args[i]}");
                        return 1;
                    }
                    lon = lonVal;
                    break;

                case "--radius" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int radiusVal) || radiusVal <= 0)
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --radius: {args[i]}");
                        return 1;
                    }
                    radius = radiusVal;
                    break;

                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;

                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;

                default:
                    Console.Error.WriteLine($"ERROR: Unknown argument: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        if (lat is null || lon is null)
        {
            Console.Error.WriteLine("ERROR: --lat and --lon are required.");
            PrintUsage();
            return 1;
        }

        var downloader = new OsmDownloader();
        try
        {
            string content = await downloader.DownloadOsmAsync(lat.Value, lon.Value, radius);
            OsmDownloader.SaveOsm(content, output);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"ERROR: Overpass API request failed: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("ERROR: Request was cancelled.");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "Usage: OsmDownloader --lat <latitude> --lon <longitude> " +
            "[--radius <metres>] [--output <path>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --lat      Centre latitude in decimal degrees (WGS-84, required)");
        Console.WriteLine("  --lon      Centre longitude in decimal degrees (WGS-84, required)");
        Console.WriteLine("  --radius   Search radius in metres (default: 5000)");
        Console.WriteLine("  --output   Output .osm file path (default: output.osm)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  OsmDownloader --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm");
        Console.WriteLine("  OsmDownloader --lat 35.6595 --lon 139.7004 --radius 2000 --output ../Assets/Data/tokyo_shibuya.osm");
    }
}
