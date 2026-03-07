using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SkiaSharp;
using TerraDrive.DataInversion;
using SysVec3 = System.Numerics.Vector3;
using UnityVec3 = UnityEngine.Vector3;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Renders a perspective (chase-cam) view of the parsed map data from a static
    /// camera position, simulating the in-game <c>ChaseCam</c> controller.
    ///
    /// The camera is placed 8 m behind and 3 m above a "car" sitting at a chosen
    /// world-space XZ position, looking 3 m ahead of the car — matching the
    /// default inspector values in <c>ChaseCam.cs</c>.
    /// </summary>
    public static class ChaseCamRenderer
    {
        // Camera defaults matching ChaseCam.cs inspector defaults.
        private const float FollowDistance    = 8f;
        private const float CameraHeight      = 3f;
        private const float LookAheadDistance = 3f;
        private const float FovDegrees        = 60f;
        private const float NearPlane         = 0.3f;
        private const float FarPlane          = 1500f;

        // Projection rendering constants
        /// <summary>
        /// NDC guard band: allows points slightly outside [-1,1] to avoid popping
        /// at the screen edges when a segment straddles the frustum boundary.
        /// </summary>
        private const float NdcGuardBand = 1.2f;

        /// <summary>
        /// Perspective scale factor: world depth (in metres) at which a road segment
        /// receives a 1:1 stroke-width multiplier.  Closer roads scale up, farther down.
        /// </summary>
        private const float PerspectiveScaleFactor = 30f;

        /// <summary>Minimum perspective scale multiplier so distant roads remain visible.</summary>
        private const float MinPerspectiveScale = 0.4f;

        /// <summary>Extra pixels added on each side to produce the casing (outline) of a road.</summary>
        private const float CasingWidthOffset = 1.5f;

        // ── Road colours (matching MapRenderer) ───────────────────────────────

        private static SKColor RoadColour(string highwayType) =>
            highwayType switch
            {
                "motorway" or "motorway_link"   => SKColor.Parse("#e892a2"),
                "trunk"    or "trunk_link"       => SKColor.Parse("#f9b29c"),
                "primary"  or "primary_link"     => SKColor.Parse("#fcd6a4"),
                "secondary" or "secondary_link"  => SKColor.Parse("#f7fabf"),
                "tertiary"  or "tertiary_link"   => new SKColor(255, 255, 255),
                "residential" or "living_street" => new SKColor(255, 255, 255),
                _                                => new SKColor(200, 200, 200),
            };

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Renders a perspective chase-cam view of the supplied map data.
        /// </summary>
        /// <param name="roads">Road segments from <see cref="OSMParser"/>.</param>
        /// <param name="buildings">Building footprints from <see cref="OSMParser"/>.</param>
        /// <param name="carX">World-space X of the static car position.</param>
        /// <param name="carZ">World-space Z of the static car position.</param>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <returns>A new <see cref="SKBitmap"/> owned by the caller.</returns>
        public static SKBitmap Render(
            List<RoadSegment>      roads,
            List<BuildingFootprint> buildings,
            float carX  = 0f,
            float carZ  = 0f,
            int   width = 1600,
            int   height = 900)
        {
            // Camera: FollowDistance behind the car, CameraHeight above it,
            // looking LookAheadDistance ahead of the car.
            var cameraPos  = new SysVec3(carX, CameraHeight, carZ - FollowDistance);
            var lookTarget = new SysVec3(carX, 0f,           carZ + LookAheadDistance);

            Matrix4x4 view = Matrix4x4.CreateLookAt(cameraPos, lookTarget, SysVec3.UnitY);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
                FovDegrees * MathF.PI / 180f,
                (float)width / height,
                NearPlane, FarPlane);
            Matrix4x4 vp = view * proj;

            var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            // 1. Sky + ground background
            float horizonY = ComputeHorizonY(vp, width, height);
            DrawBackground(canvas, width, height, horizonY);

            // 2. Building floor polygons (under roads)
            foreach (var b in buildings)
            {
                if (b.Footprint.Count < 3) continue;
                DrawBuildingFloor(canvas, b.Footprint, vp, width, height);
            }

            // 3. Roads
            foreach (var r in roads)
            {
                if (r.Nodes.Count < 2) continue;
                DrawRoadSegment(canvas, r, vp, width, height);
            }

            // 4. Car marker at the static position
            DrawCarMarker(canvas, carX, carZ, vp, width, height);

            // 5. Info overlay
            DrawOverlay(canvas, width, height, carX, carZ, roads.Count, buildings.Count);

            return bitmap;
        }

        /// <summary>Encodes <paramref name="bitmap"/> as a PNG file at <paramref name="outputPath"/>.</summary>
        public static void SaveAsPng(SKBitmap bitmap, string outputPath)
        {
            using var image  = SKImage.FromBitmap(bitmap);
            using var data   = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
        }

        // ── Projection ─────────────────────────────────────────────────────────

        /// <summary>
        /// Projects a world-space point to a screen-space pixel coordinate.
        /// Returns <c>false</c> if the point is behind the camera or outside
        /// a small NDC guard band.
        /// </summary>
        private static bool Project(
            float wx, float wy, float wz,
            Matrix4x4 vp,
            int w, int h,
            out SKPoint screen)
        {
            var clip = Vector4.Transform(new Vector4(wx, wy, wz, 1f), vp);

            if (clip.W <= 0f)                              // behind camera
            { screen = default; return false; }

            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;

            // Reject points well outside the visible frustum
            if (ndcX < -NdcGuardBand || ndcX > NdcGuardBand || ndcY < -NdcGuardBand || ndcY > NdcGuardBand)
            { screen = default; return false; }

            screen = new SKPoint(
                (ndcX + 1f) * 0.5f * w,
                (1f - ndcY) * 0.5f * h);
            return true;
        }

        private static float ComputeHorizonY(Matrix4x4 vp, int w, int h)
        {
            // Project a ground-level point far in front of the camera to find the horizon.
            if (Project(0f, 0f, 999f, vp, w, h, out var sp))
                return Math.Clamp(sp.Y, 0f, h);
            return h * 0.4f;
        }

        // ── Drawing helpers ────────────────────────────────────────────────────

        private static void DrawBackground(SKCanvas canvas, int w, int h, float horizonY)
        {
            // Sky gradient
            using var skyPaint  = new SKPaint { IsAntialias = false };
            using var skyShader = SKShader.CreateLinearGradient(
                new SKPoint(0f, 0f), new SKPoint(0f, horizonY),
                new[] { SKColor.Parse("#2c6fad"), SKColor.Parse("#87ceeb") },
                SKShaderTileMode.Clamp);
            skyPaint.Shader = skyShader;
            canvas.DrawRect(0f, 0f, w, horizonY, skyPaint);

            // Ground gradient
            using var groundPaint  = new SKPaint { IsAntialias = false };
            using var groundShader = SKShader.CreateLinearGradient(
                new SKPoint(0f, horizonY), new SKPoint(0f, h),
                new[] { SKColor.Parse("#6b8e5e"), SKColor.Parse("#3d5c2e") },
                SKShaderTileMode.Clamp);
            groundPaint.Shader = groundShader;
            canvas.DrawRect(0f, horizonY, w, h - horizonY, groundPaint);
        }

        private static void DrawRoadSegment(
            SKCanvas canvas, RoadSegment road,
            Matrix4x4 vp, int w, int h)
        {
            var colour = RoadColour(road.HighwayType);
            var nodes  = road.Nodes;

            for (int i = 0; i < nodes.Count - 1; i++)
            {
                var n0 = nodes[i];
                var n1 = nodes[i + 1];

                if (!Project(n0.x, n0.y, n0.z, vp, w, h, out var s0)) continue;
                if (!Project(n1.x, n1.y, n1.z, vp, w, h, out var s1)) continue;

                // Scale stroke width with perspective depth so near roads look wider
                var clip0 = Vector4.Transform(new Vector4(n0.x, n0.y, n0.z, 1f), vp);
                float perspScale = MathF.Max(MinPerspectiveScale, PerspectiveScaleFactor / MathF.Max(1f, clip0.W));
                float lineWidth  = MathF.Max(0.5f, 2.5f * perspScale);

                // Casing
                using var casingPaint = new SKPaint
                {
                    Color       = new SKColor(150, 140, 130),
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = lineWidth + CasingWidthOffset,
                    StrokeCap   = SKStrokeCap.Round,
                    IsAntialias = true,
                };
                canvas.DrawLine(s0, s1, casingPaint);

                // Fill
                using var fillPaint = new SKPaint
                {
                    Color       = colour,
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = lineWidth,
                    StrokeCap   = SKStrokeCap.Round,
                    IsAntialias = true,
                };
                canvas.DrawLine(s0, s1, fillPaint);
            }
        }

        private static void DrawBuildingFloor(
            SKCanvas canvas, List<UnityVec3> footprint,
            Matrix4x4 vp, int w, int h)
        {
            using var path = new SKPath();
            bool started = false;

            foreach (var v in footprint)
            {
                if (!Project(v.x, v.y, v.z, vp, w, h, out var sp))
                {
                    started = false;
                    continue;
                }
                if (!started) { path.MoveTo(sp); started = true; }
                else          { path.LineTo(sp); }
            }
            if (!started) return;
            path.Close();

            using var fillPaint = new SKPaint
            {
                Color       = SKColor.Parse("#c8b9a8"),
                Style       = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            canvas.DrawPath(path, fillPaint);

            using var strokePaint = new SKPaint
            {
                Color       = SKColor.Parse("#9e8e80"),
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                IsAntialias = true,
            };
            canvas.DrawPath(path, strokePaint);
        }

        /// <summary>Draws a simple red car rectangle at the car's ground position.</summary>
        private static void DrawCarMarker(
            SKCanvas canvas, float cx, float cz,
            Matrix4x4 vp, int w, int h)
        {
            // Car footprint: 4 m long × 2 m wide, centred at (cx, 0, cz)
            float hw = 1f, hl = 2f;
            var corners = new[]
            {
                new UnityVec3(cx - hw, 0f, cz - hl),
                new UnityVec3(cx + hw, 0f, cz - hl),
                new UnityVec3(cx + hw, 0f, cz + hl),
                new UnityVec3(cx - hw, 0f, cz + hl),
            };

            using var path = new SKPath();
            bool started = false;
            foreach (var c in corners)
            {
                if (!Project(c.x, c.y, c.z, vp, w, h, out var sp))
                { started = false; continue; }
                if (!started) { path.MoveTo(sp); started = true; }
                else          { path.LineTo(sp); }
            }
            if (!started) return;
            path.Close();

            using var fillPaint = new SKPaint
            {
                Color       = new SKColor(220, 50, 50),
                Style       = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            canvas.DrawPath(path, fillPaint);

            using var strokePaint = new SKPaint
            {
                Color       = SKColors.White,
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true,
            };
            canvas.DrawPath(path, strokePaint);
        }

        private static void DrawOverlay(
            SKCanvas canvas, int w, int h,
            float cx, float cz, int roadCount, int buildingCount)
        {
            string[] lines =
            {
                "Chase Cam View",
                $"Car position: ({cx:F0}, {cz:F0})",
                $"Roads: {roadCount}   Buildings: {buildingCount}",
                $"Camera: {FollowDistance} m behind, {CameraHeight} m above",
            };

            using var font = new SKFont { Size = 14f };
            using var textPaint = new SKPaint
            {
                Color       = new SKColor(240, 240, 240),
                IsAntialias = true,
            };
            using var bgPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 150),
            };

            const float margin = 10f;
            const float lineH  = 20f;
            float boxW = 310f;
            float boxH = lines.Length * lineH + margin * 2f;

            canvas.DrawRect(margin, margin, boxW, boxH, bgPaint);

            for (int i = 0; i < lines.Length; i++)
                canvas.DrawText(
                    lines[i],
                    margin * 2f,
                    margin + (i + 1) * lineH,
                    SKTextAlign.Left,
                    font,
                    textPaint);
        }
    }
}
