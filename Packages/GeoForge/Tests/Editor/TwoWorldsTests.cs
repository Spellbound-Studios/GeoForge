// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Unity.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spellbound.GeoForge.Tests {
    public class TwoWorldsTests {
        private const float GroundHeight = 0f;
        private const float BowlWidth = 4f;
        private const int BowlStrength = 127;
        private const float FixedStep = 0.02f;
        private const float RayHeight = 50f;
        private const float OneVoxel = 1f;

        private static readonly Vector3 EditCentre = new(0f, GroundHeight, 0f);
        private static readonly Vector3 RayOrigin = new(0f, RayHeight, 0f);

        private Scene _firstScene;
        private PlaneWorld _first;
        private Scene _secondScene;
        private PlaneWorld _second;

        [SetUp]
        public void SetUp() {
            ChunkPresenterDefaults.Factory = null;
            ChunkPresenterDefaults.CollisionFactory = null;
            _firstScene = EditorSceneManager.NewPreviewScene();
            _secondScene = EditorSceneManager.NewPreviewScene();
            _first = CreateWorld(_firstScene);
            _second = CreateWorld(_secondScene);
        }

        [TearDown]
        public void TearDown() {
            _first.Dispose();
            _second.Dispose();
            EditorSceneManager.ClosePreviewScene(_firstScene);
            EditorSceneManager.ClosePreviewScene(_secondScene);
            ChunkPresenterDefaults.Reset();
        }

        [Test]
        public void AnEditInOneWorldChangesNothingOfAnotherAtTheSameCoordinates() {
            var firstVolume = (IGeoVolume)_first.Volume;
            var secondVolume = (IGeoVolume)_second.Volume;
            using var firstRunsBefore = new NativeList<SparseVoxelData>(Allocator.Temp);
            using var firstRunsAfter = new NativeList<SparseVoxelData>(Allocator.Temp);
            using var secondRunsBefore = new NativeList<SparseVoxelData>(Allocator.Temp);
            using var secondRunsAfter = new NativeList<SparseVoxelData>(Allocator.Temp);

            Assert.IsTrue(firstVolume.TryQueryVoxel(EditCentre, out var firstVoxelBefore));
            Assert.IsTrue(secondVolume.TryQueryVoxel(EditCentre, out var secondVoxelBefore));
            Assert.IsTrue(firstVolume.TryCopyRunList(Vector3Int.zero, firstRunsBefore, out var firstVersionBefore));
            Assert.IsTrue(secondVolume.TryCopyRunList(Vector3Int.zero, secondRunsBefore, out var secondVersionBefore));
            Assert.AreEqual(GroundHeight, SurfaceHeight(_firstScene), OneVoxel);
            Assert.AreEqual(GroundHeight, SurfaceHeight(_secondScene), OneVoxel);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(firstVolume, EditCentre, BowlWidth, BowlStrength));

            Assert.IsTrue(firstVolume.TryQueryVoxel(EditCentre, out var firstVoxelAfter));
            Assert.IsTrue(secondVolume.TryQueryVoxel(EditCentre, out var secondVoxelAfter));
            Assert.IsTrue(firstVolume.TryCopyRunList(Vector3Int.zero, firstRunsAfter, out var firstVersionAfter));
            Assert.IsTrue(secondVolume.TryCopyRunList(Vector3Int.zero, secondRunsAfter, out var secondVersionAfter));

            Assert.AreNotEqual(firstVoxelBefore.Density, firstVoxelAfter.Density);
            Assert.IsFalse(RunsEqual(firstRunsBefore, firstRunsAfter));
            Assert.Greater(firstVersionAfter.CompareTo(firstVersionBefore), 0);
            Assert.Less(SurfaceHeight(_firstScene), GroundHeight - OneVoxel);

            Assert.AreEqual(secondVoxelBefore, secondVoxelAfter);
            Assert.IsTrue(RunsEqual(secondRunsBefore, secondRunsAfter));
            Assert.AreEqual(secondVersionBefore, secondVersionAfter);
            Assert.AreEqual(GroundHeight, SurfaceHeight(_secondScene), OneVoxel);
        }

        private static PlaneWorld CreateWorld(Scene collisionScene) {
            var world = new PlaneWorld(GroundHeight, Vector3Int.zero);
            world.Manager.CollisionScene = collisionScene;
            world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            world.ValidateEveryChunk(Vector3.zero);

            return world;
        }

        private static float SurfaceHeight(Scene collisionScene) {
            var physicsScene = collisionScene.GetPhysicsScene();
            physicsScene.Simulate(FixedStep);
            Assert.IsTrue(physicsScene.Raycast(RayOrigin, Vector3.down, out var hit, 2f * RayHeight));
            Assert.AreEqual(collisionScene, hit.collider.gameObject.scene);

            return hit.point.y;
        }

        private static bool RunsEqual(NativeList<SparseVoxelData> left, NativeList<SparseVoxelData> right) {
            if (left.Length != right.Length)
                return false;

            for (var index = 0; index < left.Length; index++) {
                if (left[index].StartIndex != right[index].StartIndex || left[index].Voxel != right[index].Voxel)
                    return false;
            }

            return true;
        }
    }
}
