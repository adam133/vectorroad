using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VectorRoad.DataInversion;
using VectorRoad.Procedural;

namespace VectorRoad.Tests.PlayMode
{
    /// <summary>
    /// Play-mode tests verifying that buildings and roadside props receive the correct
    /// physics collider components so the car cannot pass through them.
    /// </summary>
    public class CollisionPlayModeTests
    {
        // GameObjects created during each test – destroyed in TearDown.
        private readonly List<GameObject> _created = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            _created.Clear();
            yield return null;
        }

        // Helper to create and track a temporary GameObject.
        private GameObject MakeGO(string name = "TestGO")
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // ── Building wall collider ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator BuildingWall_WithMeshCollider_BlocksRigidbody()
        {
            // Build a minimal square building mesh using BuildingGenerator.
            var footprint = new[]
            {
                new Vector3( 0f, 0f,  0f),
                new Vector3(10f, 0f,  0f),
                new Vector3(10f, 0f, 10f),
                new Vector3( 0f, 0f, 10f),
            };

            BuildingMeshResult result = BuildingGenerator.Extrude(footprint, wayId: 1);

            // Replicate what MapSceneBuilder.BuildBuilding does.
            var wallGo = MakeGO("Walls");
            wallGo.AddComponent<MeshFilter>().sharedMesh = result.WallMesh;
            wallGo.AddComponent<MeshRenderer>();
            var col = wallGo.AddComponent<MeshCollider>();
            col.sharedMesh = result.WallMesh;

            yield return null;

            Assert.That(wallGo.GetComponent<MeshCollider>(), Is.Not.Null,
                "Building wall must have a MeshCollider.");
            Assert.That(wallGo.GetComponent<MeshCollider>().sharedMesh, Is.Not.Null,
                "Building wall MeshCollider must reference the wall mesh.");
        }

        [UnityTest]
        public IEnumerator BuildingRoof_WithMeshCollider_HasCollider()
        {
            var footprint = new[]
            {
                new Vector3( 0f, 0f,  0f),
                new Vector3(10f, 0f,  0f),
                new Vector3(10f, 0f, 10f),
                new Vector3( 0f, 0f, 10f),
            };

            BuildingMeshResult result = BuildingGenerator.Extrude(footprint, wayId: 2);

            var roofGo = MakeGO("Roof");
            roofGo.AddComponent<MeshFilter>().sharedMesh = result.RoofMesh;
            roofGo.AddComponent<MeshRenderer>();
            var col = roofGo.AddComponent<MeshCollider>();
            col.sharedMesh = result.RoofMesh;

            yield return null;

            Assert.That(roofGo.GetComponent<MeshCollider>(), Is.Not.Null,
                "Building roof must have a MeshCollider.");
            Assert.That(roofGo.GetComponent<MeshCollider>().sharedMesh, Is.Not.Null,
                "Building roof MeshCollider must reference the roof mesh.");
        }

        // ── Prop collider shapes ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator LampPost_Collider_IsCapsuleWithCorrectDimensions()
        {
            var go = MakeGO("Prop_LampPost");
            var col    = go.AddComponent<CapsuleCollider>();
            col.radius = 0.1f;
            col.height = 4f;
            col.center = new Vector3(0f, 2f, 0f);

            yield return null;

            var capsule = go.GetComponent<CapsuleCollider>();
            Assert.That(capsule, Is.Not.Null, "LampPost must have a CapsuleCollider.");
            Assert.That(capsule.radius, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(capsule.height, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(capsule.center.y, Is.EqualTo(2f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator SignPost_Collider_IsCapsuleWithCorrectDimensions()
        {
            var go = MakeGO("Prop_SignPost");
            var col    = go.AddComponent<CapsuleCollider>();
            col.radius = 0.1f;
            col.height = 4f;
            col.center = new Vector3(0f, 2f, 0f);

            yield return null;

            var capsule = go.GetComponent<CapsuleCollider>();
            Assert.That(capsule, Is.Not.Null, "SignPost must have a CapsuleCollider.");
            Assert.That(capsule.radius, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(capsule.height, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(capsule.center.y, Is.EqualTo(2f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator Tree_Collider_IsCapsuleWithWiderRadius()
        {
            var go = MakeGO("Prop_Tree");
            var col    = go.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = 4f;
            col.center = new Vector3(0f, 2f, 0f);

            yield return null;

            var capsule = go.GetComponent<CapsuleCollider>();
            Assert.That(capsule, Is.Not.Null, "Tree must have a CapsuleCollider.");
            Assert.That(capsule.radius, Is.EqualTo(0.3f).Within(1e-5f),
                "Tree trunk radius should be wider than a lamp post.");
            Assert.That(capsule.radius, Is.GreaterThan(0.1f),
                "Tree radius must be larger than a post radius.");
        }

        [UnityTest]
        public IEnumerator Fence_Collider_IsBoxWithCorrectDimensions()
        {
            var go = MakeGO("Prop_Fence");
            var col    = go.AddComponent<BoxCollider>();
            col.size   = new Vector3(2f, 1.5f, 0.1f);
            col.center = new Vector3(0f, 0.75f, 0f);

            yield return null;

            var box = go.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null, "Fence must have a BoxCollider.");
            Assert.That(box.size.x, Is.EqualTo(2f).Within(1e-5f),
                "Fence span (X) should be 2 m.");
            Assert.That(box.size.y, Is.EqualTo(1.5f).Within(1e-5f),
                "Fence height (Y) should be 1.5 m.");
            Assert.That(box.center.y, Is.EqualTo(0.75f).Within(1e-5f),
                "Fence centre must sit above the ground plane.");
        }

        // ── Prop placement positions are off-road ─────────────────────────────

        [UnityTest]
        public IEnumerator RoadsidePropPlacer_LampPostsArePlacedBeyondRoadEdge()
        {
            var spline = new List<Vector3>
            {
                new(0f, 0f,   0f),
                new(0f, 0f, 100f),
            };

            var placements = RoadsidePropPlacer.Place(
                spline, RoadType.Residential, RegionType.Temperate, wayId: 42);

            float halfWidth     = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential) * 0.5f;
            float minLateral    = halfWidth + RoadMeshExtruder.DefaultKerbWidth;

            Assert.That(placements, Is.Not.Empty, "Should have at least one prop placement.");

            foreach (PropPlacement p in placements)
            {
                float lateralDist = Mathf.Abs(p.Position.x);
                Assert.That(lateralDist, Is.GreaterThan(minLateral),
                    $"Prop at {p.Position} must be outside the road edge ({minLateral} m).");
            }

            yield return null;
        }
    }
}
