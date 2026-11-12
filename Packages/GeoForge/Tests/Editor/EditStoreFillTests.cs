// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class EditStoreFillTests {
        private const float EditRadius = 4f;
        private const int EditStrength = 127;
        private const sbyte EmptyDensity = -100;
        private const int FillEdits = 300;
        private const int FillWarmUps = 3;

        private static readonly Vector3Int Chunk = Vector3Int.zero;
        private static readonly Vector3Int BelowTheGround = new(4, 2, 4);

        private GameObject _lateChunkObject;
        private GeoChunkDouble _lateChunk;
        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _lateChunk?.GeoChunkEngine.Dispose();

            if (_lateChunkObject != null)
                Object.DestroyImmediate(_lateChunkObject);

            _world?.Dispose();
            _lateChunk = null;
            _lateChunkObject = null;
            _world = null;
        }

        [Test]
        public void SetVoxels_ReappliesTheStoresEdits() {
            _world = new PlaneWorld(0f, Chunk);
            var engine = _world.Chunks[0].GeoChunkEngine;
            var index = IndexOf(BelowTheGround);
            var edited = VoxelData.CreateImmature(EmptyDensity, VoxelData.NullSentinelValue);
            Assert.IsTrue(VoxelEncoding.IsSolid(engine.GetVoxelData(index).Density));

            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (index, edited) });
            Assert.AreEqual(edited, engine.GetVoxelData(index));

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), _world.Engine.DensitySource);

            Assert.AreEqual(edited, engine.GetVoxelData(index));
        }

        [Test]
        public void ApplyBeforeFill_IsKeptAndAppliedAtTheFill() {
            _world = new PlaneWorld(0f);
            _lateChunkObject = new GameObject("Late Chunk");
            _lateChunkObject.transform.SetParent(_world.Volume.transform, false);
            _lateChunk = new GeoChunkDouble(_lateChunkObject.transform, Chunk);
            _world.Engine.RegisterChunk(Chunk, _lateChunk);
            var engine = _lateChunk.GeoChunkEngine;
            var index = IndexOf(BelowTheGround);
            var edited = VoxelData.CreateImmature(EmptyDensity, VoxelData.NullSentinelValue);

            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (index, edited) });

            Assert.IsFalse(engine.VoxelsReady);
            Assert.IsTrue(engine.IGeoEditStore.TryRead(index, out var kept));
            Assert.AreEqual(edited, kept);

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), _world.Engine.DensitySource);

            Assert.IsTrue(engine.VoxelsReady);
            Assert.AreEqual(edited, engine.GetVoxelData(index));
        }

        [Test]
        public void ResetEditedChunksToProcedural_ClearsTheStores() {
            _world = new PlaneWorld(0f, Chunk);
            IGeoVolume volume = _world.Volume;
            var store = _world.Chunks[0].GeoChunkEngine.IGeoEditStore;
            Assert.IsTrue(volume.TryQueryVoxel(Vector3.zero, out var procedural));

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, Vector3.zero, EditRadius, EditStrength));
            Assert.IsTrue(store.HasEdits);
            Assert.IsTrue(volume.TryQueryVoxel(Vector3.zero, out var dug));
            Assert.AreNotEqual(procedural, dug);

            _world.Engine.ResetEditedChunksToProcedural(null);

            Assert.IsFalse(store.HasEdits);
            Assert.IsTrue(volume.TryQueryVoxel(Vector3.zero, out var reset));
            Assert.AreEqual(procedural, reset);
        }

        [Test]
        public void Reset_WithoutADensitySource_RefusesAndChangesNothing() {
            _world = new PlaneWorld(0f, Chunk);
            IGeoVolume volume = _world.Volume;
            var store = _world.Chunks[0].GeoChunkEngine.IGeoEditStore;
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, Vector3.zero, EditRadius, EditStrength));
            Assert.IsTrue(volume.TryQueryVoxel(Vector3.zero, out var dug));
            var edits = new List<(int, VoxelData)>(store.ReadAllEdits());
            _world.Engine.UseDensitySource(null);

            LogAssert.Expect(LogType.Error, new Regex("nothing was reset"));
            _world.Engine.ResetEditedChunksToProcedural(null);
            LogAssert.Expect(LogType.Error, new Regex("nothing was reset"));
            Assert.IsFalse(_world.Engine.ResetChunksToProcedural(new[] { Chunk }));

            CollectionAssert.AreEquivalent(edits, store.ReadAllEdits());
            Assert.IsTrue(volume.TryQueryVoxel(Vector3.zero, out var kept));
            Assert.AreEqual(dug, kept);
        }

        [Test]
        public void Fill_OfAnEditedChunk_NoAllocation() {
            _world = new PlaneWorld(0f, Chunk);
            var engine = _world.Chunks[0].GeoChunkEngine;
            var edits = new List<(int, VoxelData)>();

            for (var index = 0; index < FillEdits; index++)
                edits.Add((index * 5, VoxelData.CreateImmature(EmptyDensity, VoxelData.NullSentinelValue)));

            var request = _world.Engine.CreateFillRequest(Chunk);
            var source = _world.Engine.DensitySource;

            for (var warmUp = 0; warmUp < FillWarmUps; warmUp++)
                engine.SetVoxels(request, source);

            var plain = GcAllocations.Count(() => engine.SetVoxels(request, source));
            engine.IGeoEditStore.Write(edits);

            for (var warmUp = 0; warmUp < FillWarmUps; warmUp++)
                engine.SetVoxels(request, source);

            var edited = GcAllocations.Count(() => engine.SetVoxels(request, source));

            Assert.AreEqual(edits[1].Item2, engine.GetVoxelData(edits[1].Item1));
            Assert.AreEqual(plain, edited);
        }

        private static int IndexOf(Vector3Int local) {
            return ChunkLayout.Index(local.x, local.y, local.z, ChunkLayout.DataWidth(PlaneWorld.ChunkSize));
        }
    }
}