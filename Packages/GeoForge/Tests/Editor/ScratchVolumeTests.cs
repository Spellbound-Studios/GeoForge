// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Spellbound.GeoForge.EditMode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class ScratchVolumeTests {
        private const int ChunkSize = 32;
        private const float VoxelSize = 1f;
        private const float StrokeSize = 8f;
        private const int StrokeDelta = 255;
        private const int CornerInset = 2;
        private const byte RockMaterial = 0;

        private ScratchVolume _scratch;

        [TearDown]
        public void TearDown() {
            _scratch?.Dispose();
            _scratch = null;
        }

        [Test]
        public void StrokeAtCentreOfSolidChunkCarvesAndMeshes() {
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false);
            var engine = _scratch.Volume.GeoVolumeEngine;
            var chunk = engine.GetChunkByCoord(Vector3Int.zero);
            Assert.That(chunk, Is.Not.Null);

            FillSolid(chunk, engine.ConfigBlob.Value.ChunkDataVolumeSize);
            var centre = WorldOfVoxel(engine, Vector3Int.one * (ChunkSize / 2));
            var corner = WorldOfVoxel(engine, Vector3Int.one * CornerInset);

            Assert.That(GeoForgeCommands.RemoveSphere(_scratch.Volume, centre, StrokeSize, StrokeDelta), Is.True);

            _scratch.Complete();

            Assert.That(_scratch.Volume.TryQuerySolid(centre, out var centreSolid), Is.True);
            Assert.That(centreSolid, Is.False);
            Assert.That(_scratch.Volume.TryQuerySolid(corner, out var cornerSolid), Is.True);
            Assert.That(cornerSolid, Is.True);
            Assert.That(LargestLeafVertexCount(chunk), Is.GreaterThan(0));
        }

        [Test]
        public void VolumeOverCallerConfigBuildsEveryChunkOfItsExtent() {
            var extent = new Vector3Int(2, 1, 1);
            var config = VoxelVolumeConfig.Create(ChunkSize, VoxelSize, Vector3Int.one);
            _scratch = ScratchVolume.Create(config, extent, false);

            Assert.That(_scratch.Volume.GeoVolumeEngine.ChunkDict.Count, Is.EqualTo(extent.x * extent.y * extent.z));
            Assert.That(_scratch.Volume.ConfigBlob.Value.SizeInChunks, Is.EqualTo(extent));
            Assert.That(_scratch.Manager.Presenter, Is.Not.Null);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void CreateSetsTheDefaultPresenters() {
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false);

            Assert.That(_scratch.Manager.Presenter, Is.Not.Null);
            Assert.That(_scratch.Manager.CollisionPresenter, Is.Not.Null);
        }

        [Test]
        public void DisposeClosesTheSceneAndLeavesNoManager() {
            var managersBefore = Resources.FindObjectsOfTypeAll<GeoForgeManager>().Length;
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false);
            var scene = _scratch.Scene;
            var sceneName = scene.name;
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(Resources.FindObjectsOfTypeAll<GeoForgeManager>().Length, Is.EqualTo(managersBefore + 1));

            _scratch.Dispose();
            _scratch = null;

            Assert.That(scene.IsValid(), Is.False);

            for (var index = 0; index < SceneManager.sceneCount; index++)
                Assert.That(SceneManager.GetSceneAt(index).name, Is.Not.EqualTo(sceneName));

            Assert.That(Object.FindObjectsByType<GeoForgeManager>(), Is.Empty);
            Assert.That(Resources.FindObjectsOfTypeAll<GeoForgeManager>().Length, Is.EqualTo(managersBefore));
        }

        [Test]
        public void BuiltVolumeIsNotBuiltTwice() {
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false);
            var volume = (GeoVolume)_scratch.Volume;
            var engine = volume.GeoVolumeEngine;

            Assert.That(volume.Build(), Is.True);
            Assert.That(volume.GeoVolumeEngine, Is.SameAs(engine));
        }

        private static Vector3 WorldOfVoxel(GeoVolumeEngine engine, Vector3Int voxel) {
            var offset = engine.ConfigBlob.Value.Offset;

            return engine.Transform.TransformPoint((Vector3)(voxel + offset) * engine.VoxelSize);
        }

        private static void FillSolid(IGeoChunk chunk, int dataVolume) {
            var voxels = new NativeArray<VoxelData>(dataVolume, Allocator.Persistent);

            for (var index = 0; index < dataVolume; index++)
                voxels[index] = VoxelData.CreateImmature(sbyte.MaxValue, RockMaterial);

            chunk.GeoChunkEngine.SetVoxels(voxels);
        }

        private static int LargestLeafVertexCount(IGeoChunk chunk) {
            var largest = 0;

            foreach (var filter in chunk.GeoChunkEngine.Transform.GetComponentsInChildren<MeshFilter>()) {
                if (filter.sharedMesh != null && filter.sharedMesh.vertexCount > largest)
                    largest = filter.sharedMesh.vertexCount;
            }

            return largest;
        }
    }
}
