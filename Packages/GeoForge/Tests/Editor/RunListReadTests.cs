// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class RunListReadTests {
        private const float SphereSize = 4f;
        private const short SphereDelta = 60;
        private const byte SphereMaterial = 1;

        private static readonly Vector3Int LoadedChunk = Vector3Int.zero;
        private static readonly Vector3Int UnloadedChunk = new(4, 0, 0);

        private PerlinTerrainDataFactory _factory;
        private PlaneWorld _world;

        [SetUp]
        public void SetUp() {
            _world = new PlaneWorld(0f, LoadedChunk);
            _factory = ScriptableObject.CreateInstance<PerlinTerrainDataFactory>();
            var source = GeoForgeKit.CreateDensitySource(_factory, _world.Engine.ConfigBlob);
            ((IGeoChunk)_world.Chunks[0]).SetVoxels(_world.Engine.CreateFillRequest(LoadedChunk), source);
        }

        [TearDown]
        public void TearDown() {
            _world.Dispose();
            Object.DestroyImmediate(_factory);
        }

        [Test]
        public void TheCopiedRunListEqualsTheChunksRunList() {
            IGeoVolume volume = _world.Volume;
            using var copy = new NativeList<SparseVoxelData>(Allocator.Temp);
            copy.Add(default);

            Assert.IsTrue(volume.TryCopyRunList(LoadedChunk, copy, out _));

            var runs = _world.Chunks[0].GeoChunkEngine.GetsSparseVoxelData();
            Assert.AreEqual(runs.Length, copy.Length);

            for (var index = 0; index < runs.Length; index++) {
                Assert.AreEqual(runs[index].StartIndex, copy[index].StartIndex);
                Assert.AreEqual(runs[index].Voxel, copy[index].Voxel);
            }
        }

        [Test]
        public void TheSolidRunsCarryTheSignOfEveryVoxel() {
            IGeoVolume volume = _world.Volume;
            using var solidRuns = new NativeList<SolidRun>(Allocator.Temp);
            using var dense = _factory.GenerateProceduralVoxels(LoadedChunk, _world.Engine.ConfigBlob);

            Assert.IsTrue(volume.TryCopySolidRuns(LoadedChunk, solidRuns, out _));
            Assert.Greater(solidRuns.Length, 1);
            Assert.AreEqual(0, solidRuns[0].StartIndex);

            for (var run = 0; run < solidRuns.Length; run++) {
                var end = run + 1 < solidRuns.Length ? solidRuns[run + 1].StartIndex : dense.Length;

                if (run > 0)
                    Assert.AreNotEqual(solidRuns[run - 1].Solid, solidRuns[run].Solid);

                for (var index = solidRuns[run].StartIndex; index < end; index++) {
                    if (VoxelEncoding.IsSolid(dense[index].Density) != solidRuns[run].Solid)
                        Assert.Fail($"voxel {index} has the other sign from its run {run}");
                }
            }
        }

        [Test]
        public void AnUnloadedChunkIsRefused() {
            IGeoVolume volume = _world.Volume;
            using var copy = new NativeList<SparseVoxelData>(Allocator.Temp);
            using var solidRuns = new NativeList<SolidRun>(Allocator.Temp);

            Assert.IsFalse(volume.TryCopyRunList(UnloadedChunk, copy, out _));
            Assert.IsFalse(volume.TryCopySolidRuns(UnloadedChunk, solidRuns, out _));
            Assert.AreEqual(0, copy.Length);
            Assert.AreEqual(0, solidRuns.Length);
        }

        [Test]
        public void ACopyCarriesTheVolumeVersionItWasTakenAt() {
            IGeoVolume volume = _world.Volume;
            using var copy = new NativeList<SparseVoxelData>(Allocator.Temp);
            using var solidRuns = new NativeList<SolidRun>(Allocator.Temp);

            Assert.IsTrue(volume.TryCopyRunList(LoadedChunk, copy, out var versionBefore));
            Assert.AreEqual(_world.Engine.Version, versionBefore);

            Assert.IsTrue(GeoForgeCommands.AddSphere(volume, Vector3.zero, SphereSize, SphereDelta, SphereMaterial));

            Assert.IsTrue(volume.TryCopyRunList(LoadedChunk, copy, out var versionAfter));
            Assert.IsTrue(volume.TryCopySolidRuns(LoadedChunk, solidRuns, out var solidVersion));
            Assert.AreEqual(_world.Engine.Version, versionAfter);
            Assert.AreEqual(_world.Engine.Version, solidVersion);
            Assert.Greater(versionAfter.CompareTo(versionBefore), 0);
        }

        [Test]
        public void AResetToProceduralHandsOutANewVersion() {
            IGeoVolume volume = _world.Volume;
            using var copy = new NativeList<SparseVoxelData>(Allocator.Temp);

            Assert.IsTrue(GeoForgeCommands.AddSphere(volume, Vector3.zero, SphereSize, SphereDelta, SphereMaterial));
            Assert.IsTrue(volume.TryCopyRunList(LoadedChunk, copy, out var versionBefore));

            _world.Engine.ResetEditedChunksToProcedural(_factory);

            Assert.IsTrue(volume.TryCopyRunList(LoadedChunk, copy, out var versionAfter));
            Assert.Greater(versionAfter.CompareTo(versionBefore), 0);
        }

        [Test]
        public void AVolumeWithNoPresenterAndNoViewerAnswersTheCopy() {
            IGeoVolume volume = _world.Volume;
            using var copy = new NativeList<SparseVoxelData>(Allocator.Temp);

            Assert.IsFalse(volume.IsMeshReady(Vector3.zero, ReadinessScope.AnyLevel));
            Assert.IsTrue(volume.TryCopyRunList(LoadedChunk, copy, out _));
            Assert.Greater(copy.Length, 1);
        }
    }
}
