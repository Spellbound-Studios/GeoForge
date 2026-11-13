// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spellbound.Core.Ground;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spellbound.GeoForge.Tests {
    public class UniformChunkTests {
        private const int ChunkSize = 16;
        private const sbyte RockFill = 40;
        private const sbyte EditedDensity = -60;
        private const sbyte UnsaturatedRock = 30;
        private const short DigDelta = -50;
        private const sbyte SubscriberDensity = -90;

        private static readonly Vector3Int Chunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3Int EditedVoxel = new(4, 5, 6);
        private static readonly Vector3Int UneditedVoxel = new(9, 9, 9);
        private static readonly Vector3Int DugVoxel = new(9, 9, 9);
        private static readonly Vector3 AtTheDugVoxel = Vector3.zero;

        private PlaneWorld _world;
        private PlaneWorld _fillingWorld;
        private SeamGeoChunk _defaultMemberChunk;
        private SelfResolvingGeoChunk _selfResolvingChunk;

        [TearDown]
        public void TearDown() {
            _defaultMemberChunk?.GeoChunkEngine.Dispose();
            _defaultMemberChunk = null;
            _selfResolvingChunk?.GeoChunkEngine.Dispose();
            _selfResolvingChunk = null;
            _world?.Dispose();
            _fillingWorld?.Dispose();
            _world = null;
            _fillingWorld = null;
        }

        [Test]
        public void UniformChunk_DeltaEditResolvesAgainstTheRealDensities() {
            var skippingSource = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            var fillingSource = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            _world = new PlaneWorld(ChunkSize, 1, skippingSource);
            _fillingWorld = new PlaneWorld(ChunkSize, 1, fillingSource, false);
            var skipping = _world.AddChunk(Chunk).GeoChunkEngine;
            var filling = _fillingWorld.AddChunk(Chunk).GeoChunkEngine;
            skipping.SetVoxels(_world.Engine.CreateFillRequest(Chunk), skippingSource);
            filling.SetVoxels(_fillingWorld.Engine.CreateFillRequest(Chunk), fillingSource);
            Assert.IsTrue(skipping.IsUniform);
            Assert.IsFalse(filling.IsUniform);

            Dig(_world);
            Dig(_fillingWorld);

            var expected = filling.GetVoxelData(IndexOf(DugVoxel));
            var actual = skipping.GetVoxelData(IndexOf(DugVoxel));
            Assert.AreEqual(UnsaturatedRock + DigDelta, expected.Density);
            Assert.AreEqual(expected.Density, actual.Density);
            Assert.AreEqual(expected.MaterialIndex, actual.MaterialIndex);
            Assert.AreEqual(1, skippingSource.Fills);
            Assert.IsFalse(skipping.IsUniform);
        }

        [Test]
        public void UniformChunk_CustomEditStoreSeesTheRealDensities() {
            var source = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var store = new DeltaResolvingEditStore();
            var engine = _world.AddChunk(Chunk, store).GeoChunkEngine;
            store.geoChunkEngine = engine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);

            Dig(_world);

            CollectionAssert.AreEqual(new[] { UnsaturatedRock }, store.ReadDensities);
            Assert.AreEqual(UnsaturatedRock + DigDelta, engine.GetVoxelData(IndexOf(DugVoxel)).Density);
            Assert.AreEqual(1, source.Fills);
        }

        [Test]
        public void UniformChunk_StoreReadOutsideTheEditResolvesAgainstTheFill() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var store = new DeltaResolvingEditStore();
            var engine = _world.AddChunk(Chunk, store).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);

            store.ResolveDelta(new VoxelDensityDelta(IndexOf(DugVoxel), DigDelta));

            CollectionAssert.AreEqual(new[] { RockFill }, store.ReadDensities);
            Assert.AreEqual(RockFill + DigDelta, engine.GetVoxelData(IndexOf(DugVoxel)).Density);
            Assert.IsFalse(VoxelEncoding.IsSolid(engine.GetVoxelData(IndexOf(DugVoxel)).Density));
            Assert.AreEqual(1, source.Fills);
        }

        [Test]
        public void UniformChunk_EditThroughAStoreKeepingNothingIsApplied() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var store = new RelayEditStore();
            var engine = _world.AddChunk(Chunk, store).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            var changedBounds = new List<BoundsInt>();
            _world.Engine.Changed += change => changedBounds.Add(change.VoxelBounds);
            var edit = Edit();

            store.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), edit) });

            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(edit, engine.GetVoxelData(IndexOf(EditedVoxel)));
            CollectionAssert.Contains(changedBounds, new BoundsInt(EditedVoxel, Vector3Int.one));
        }

        [Test]
        public void UniformChunk_FillUsesTheVolumesCurrentSource() {
            var classified = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            var current = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            _world = new PlaneWorld(ChunkSize, 1, classified);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), classified);
            Assert.IsTrue(engine.IsUniform);

            _world.Engine.UseDensitySource(current);
            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), Edit()) });

            Assert.AreEqual(1, current.Fills);
            Assert.AreEqual(0, classified.Fills);
            Assert.AreEqual(current.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void UniformChunk_EditWithoutASourceLeavesTheChunkUniform() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            _world.Engine.UseDensitySource(null);
            Assert.IsNull(_world.Engine.RefillSource);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(Chunk.ToString())));

            Assert.DoesNotThrow(() =>
                engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), Edit()) }));

            LogAssert.NoUnexpectedReceived();
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(0, source.Fills);
            Assert.AreEqual(Uniform(sbyte.MaxValue), ReadWithoutFilling(engine, EditedVoxel));
        }

        [Test]
        public void UniformChunk_EditCheckoutFillsTheChunkAndReadCheckoutDoesNot() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var edited = _world.AddChunk(Chunk).GeoChunkEngine;
            var read = _world.AddChunk(SecondChunk).GeoChunkEngine;
            edited.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            read.SetVoxels(_world.Engine.CreateFillRequest(SecondChunk), source);
            Assert.IsTrue(edited.IsUniform);
            Assert.IsTrue(read.IsUniform);

            var editVoxel = edited.GetVoxelDataArray(true)[IndexOf(UneditedVoxel)];
            _world.Manager.ReleaseVoxelArray(ChunkSize, edited, true);
            var readVoxel = read.GetVoxelDataArray(false)[IndexOf(UneditedVoxel)];
            _world.Manager.ReleaseVoxelArray(ChunkSize, read, false);

            Assert.AreEqual(source.FillVoxel, editVoxel);
            Assert.IsFalse(edited.IsUniform);
            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(Uniform(sbyte.MaxValue), readVoxel);
            Assert.IsTrue(read.IsUniform);
        }

        [Test]
        public void UniformChunk_EmptyEditListLeavesTheChunkUniform() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            var changes = 0;
            _world.Engine.Changed += _ => changes++;

            engine.HandleResolvedVoxelEdits(new List<(int, VoxelData)>());

            Assert.AreEqual(0, source.Fills);
            Assert.AreEqual(0, changes);
            Assert.IsTrue(engine.IsUniform);
        }

        [Test]
        public void UniformChunk_ChangedSubscriberEditingTheFilledVoxelKeepsItsEdit() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            var edit = Edit();
            var subscriberEdit = VoxelData.CreateImmature(SubscriberDensity, VoxelData.NullSentinelValue);
            var wholeChunk = new BoundsInt(Chunk * ChunkSize, Vector3Int.one * ChunkLayout.DataWidth(ChunkSize));
            var seenAtChanges = new List<VoxelData>();
            var subscriberEdited = false;
            _world.Engine.Changed += change => {
                seenAtChanges.Add(ReadWithoutFilling(engine, EditedVoxel));

                if (subscriberEdited || change.VoxelBounds != wholeChunk)
                    return;

                subscriberEdited = true;
                engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), subscriberEdit) });
            };

            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), edit) });

            Assert.IsTrue(subscriberEdited);
            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(edit, seenAtChanges[0]);
            Assert.AreEqual(subscriberEdit, seenAtChanges[seenAtChanges.Count - 1]);
            Assert.AreEqual(subscriberEdit, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.IsTrue(engine.IGeoEditStore.TryRead(IndexOf(EditedVoxel), out var stored));
            Assert.AreEqual(subscriberEdit, stored);
        }

        [Test]
        public void UniformChunk_CommandWithoutASourceStoresNothing() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            _world.Engine.UseDensitySource(null);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(Chunk.ToString())));

            GeoForgeCommands.RemoveSphere(_world.Volume, AtTheDugVoxel, 3f, -DigDelta);

            LogAssert.NoUnexpectedReceived();
            Assert.IsFalse(engine.IGeoEditStore.HasEdits);
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(0, source.Fills);
        }

        [Test]
        public void UniformChunk_DefaultEditOperationWithoutASourceStoresNothing() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var chunkTransform = new GameObject("Default Member Chunk").transform;
            chunkTransform.SetParent(_world.Volume.transform, false);
            _defaultMemberChunk = new SeamGeoChunk(chunkTransform, Chunk);
            _world.Engine.RegisterChunk(Chunk, _defaultMemberChunk);
            IGeoChunk chunk = _defaultMemberChunk;
            chunk.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(chunk.GeoChunkEngine.IsUniform);
            _world.Engine.UseDensitySource(null);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(Chunk.ToString())));

            var operation = new VoxelEditOperation(0,
                new List<VoxelDensityDelta> { new(IndexOf(DugVoxel), DigDelta), new(IndexOf(EditedVoxel), DigDelta) },
                AtTheDugVoxel);
            chunk.PassVoxelEditOperation(operation);
            chunk.PassVoxelEditOperation(operation);

            LogAssert.NoUnexpectedReceived();
            Assert.IsFalse(chunk.GeoChunkEngine.IGeoEditStore.HasEdits);
            Assert.IsTrue(chunk.GeoChunkEngine.IsUniform);
            Assert.AreEqual(0, source.Fills);
        }

        [Test]
        public void UniformChunk_ApplyingNoEditsLeavesTheChunkUniform() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            var changes = 0;
            _world.Engine.Changed += _ => changes++;

            Assert.IsFalse(engine.ApplyVoxelEdits(new List<(int, VoxelData)>(), out _));

            Assert.AreEqual(0, source.Fills);
            Assert.AreEqual(0, changes);
            Assert.IsTrue(engine.IsUniform);
        }

        [Test]
        public void UniformChunk_DirectPassVoxelEditOperationSeesTheRealDensities() {
            var source = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var chunkTransform = new GameObject("Default Member Chunk").transform;
            chunkTransform.SetParent(_world.Volume.transform, false);
            _defaultMemberChunk = new SeamGeoChunk(chunkTransform, Chunk);
            _world.Engine.RegisterChunk(Chunk, _defaultMemberChunk);
            IGeoChunk chunk = _defaultMemberChunk;
            chunk.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(chunk.GeoChunkEngine.IsUniform);

            chunk.PassVoxelEditOperation(new VoxelEditOperation(0,
                new List<VoxelDensityDelta> { new(IndexOf(DugVoxel), DigDelta) }, AtTheDugVoxel));

            Assert.AreEqual(UnsaturatedRock + DigDelta, chunk.GeoChunkEngine.GetVoxelData(IndexOf(DugVoxel)).Density);
            Assert.AreEqual(1, source.Fills);
            Assert.IsFalse(chunk.GeoChunkEngine.IsUniform);
        }

        [Test]
        public void UniformChunk_SelfResolvingChunkReadsTheRealDensities() {
            var source = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var chunkTransform = new GameObject("Self Resolving Chunk").transform;
            chunkTransform.SetParent(_world.Volume.transform, false);
            _selfResolvingChunk = new SelfResolvingGeoChunk(chunkTransform, Chunk);
            _world.Engine.RegisterChunk(Chunk, _selfResolvingChunk);
            IGeoChunk chunk = _selfResolvingChunk;
            chunk.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(chunk.GeoChunkEngine.IsUniform);

            chunk.PassVoxelEditOperation(new VoxelEditOperation(0,
                new List<VoxelDensityDelta> { new(IndexOf(DugVoxel), DigDelta) }, AtTheDugVoxel));

            CollectionAssert.AreEqual(new[] { UnsaturatedRock }, _selfResolvingChunk.ReadDensities);
            Assert.AreEqual(UnsaturatedRock + DigDelta, chunk.GeoChunkEngine.GetVoxelData(IndexOf(DugVoxel)).Density);
            Assert.AreEqual(1, source.Fills);
        }

        [Test]
        public void UniformChunk_ApplyVoxelEditsMaterialisesFirst() {
            var source = new RecordingOccupancySource(Occupancy.Solid, UnsaturatedRock);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var presenter = new RecordingChunkPresenter();
            _world.Manager.Presenter = presenter;
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            var direct = Edit();

            Assert.IsTrue(engine.ApplyVoxelEdits(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), direct) },
                out _));

            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(direct, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));

            _world.ValidateEveryChunk(Vector3.zero);
            Assert.Greater(presenter.Acquired.Count, 0);

            var stored = VoxelData.CreateImmature(EditedDensity, VoxelData.NullSentinelValue);
            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(DugVoxel), stored) });

            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(direct, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.AreEqual(stored, engine.GetVoxelData(IndexOf(DugVoxel)));
        }

        [Test]
        public void UniformChunk_FirstEditRunsTheFillAndAppliesTheEdit() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(Uniform(sbyte.MaxValue), ReadWithoutFilling(engine, UneditedVoxel));

            var edit = Edit();
            var seenAtResidency = new List<VoxelData>();
            _world.Engine.ResidencyChanged += _ => seenAtResidency.Add(engine.GetVoxelData(IndexOf(EditedVoxel)));
            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), edit) });

            Assert.AreEqual(1, source.Fills);
            Assert.IsFalse(engine.IsUniform);
            Assert.IsTrue(engine.VoxelsReady);
            Assert.AreEqual(edit, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
            Assert.AreEqual(1, seenAtResidency.Count);
            Assert.AreEqual(edit, seenAtResidency[0]);
        }

        [Test]
        public void UniformChunk_EditBeforeFillIsAppliedAtTheFill() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            var edit = Edit();

            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), edit) });
            Assert.IsFalse(engine.VoxelsReady);
            Assert.AreEqual(0, source.Fills);

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(1, source.Fills);
            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(edit, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void ResetEditedChunksToProcedural_ReturnsTheUniformState() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), Edit()) });
            Assert.IsFalse(engine.IsUniform);

            _world.Engine.ResetEditedChunksToProcedural(null);

            Assert.IsFalse(engine.IGeoEditStore.HasEdits);
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(2, source.Classifications);
            Assert.AreEqual(Uniform(sbyte.MaxValue), ReadWithoutFilling(engine, EditedVoxel));
            Assert.AreEqual(Uniform(sbyte.MaxValue), ReadWithoutFilling(engine, UneditedVoxel));
        }

        [Test]
        public void UniformChunk_RefillClassifiesAgain() {
            var source = new RecordingOccupancySource(Occupancy.Mixed, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            Assert.IsFalse(engine.IsUniform);

            source.Answer = Occupancy.Empty;
            Assert.IsTrue(_world.Engine.RefillChunks(new[] { Chunk }));

            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(2, source.Classifications);
            Assert.AreEqual(Uniform(sbyte.MinValue), ReadWithoutFilling(engine, UneditedVoxel));

            source.Answer = Occupancy.Mixed;
            Assert.IsTrue(_world.Engine.RefillChunks(new[] { Chunk }));

            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(2, source.Fills);
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        private static void Dig(PlaneWorld world) {
            Assert.IsTrue(GeoForgeCommands.RemoveCube(world.Volume, AtTheDugVoxel, 0, -DigDelta));
        }

        private static VoxelData Edit() {
            return VoxelData.CreateImmature(EditedDensity, VoxelData.NullSentinelValue);
        }

        private static VoxelData Uniform(sbyte density) {
            return VoxelData.CreateImmature(density, VoxelEncoding.NullMaterialIndex);
        }

        private static VoxelData ReadWithoutFilling(GeoChunkEngine engine, Vector3Int local) {
            return engine.GetVoxelDataFromVoxelPosition(engine.ChunkCoord * ChunkSize + local);
        }

        private static int IndexOf(Vector3Int local) {
            return ChunkLayout.Index(local.x, local.y, local.z, ChunkLayout.DataWidth(ChunkSize));
        }
    }
}
