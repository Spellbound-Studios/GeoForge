// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core.Ground;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class OccupancySkipTests {
        private const int ChunkSize = 16;
        private const sbyte AirFill = -100;
        private const sbyte RockFill = 40;
        private const sbyte OtherRockFill = 30;
        private const byte PlaneMaterial = 1;
        private const sbyte EditedDensity = -60;
        private const int WarmUps = 3;
        private const float BoxTolerance = 1e-4f;
        private const float HalfVoxel = 0.5f;

        private static readonly Vector3Int Chunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3Int OffsetChunk = new(1, -1, 2);
        private static readonly Vector3Int UnalignedOrigin = new(5, 7, -3);
        private static readonly Vector3Int AirChunk = new(0, 2, 0);
        private static readonly Vector3Int RockChunk = new(0, -2, 0);
        private static readonly Vector3 InTheAirChunk = new(0f, 30f, 0f);
        private static readonly Vector3 InTheRockChunk = new(0f, -30f, 0f);
        private static readonly Vector3 InTheChunk = Vector3.zero;
        private static readonly Vector3 BelowTheSurface = new(4f, -1.4f, 4f);
        private static readonly Vector3Int EditedVoxel = new(4, 5, 6);
        private static readonly Vector3Int UneditedVoxel = new(9, 9, 9);

        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void OccupancySkip_EmptyChunkIsNotFilled() {
            var source = new RecordingOccupancySource(Occupancy.Empty, AirFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            var residencies = new List<ChunkResidency>();
            _world.Engine.ResidencyChanged += residencies.Add;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(0, source.Fills);
            Assert.AreEqual(1, source.Classifications);
            Assert.IsTrue(engine.VoxelsReady);
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(Uniform(sbyte.MinValue), ReadWithoutFilling(engine, UneditedVoxel));
            Assert.AreEqual(1, residencies.Count);
            Assert.AreEqual(Chunk, residencies[0].ChunkCoordinate);
            Assert.AreEqual(ChunkResidencyState.Resident, residencies[0].State);
        }

        [Test]
        public void OccupancySkip_SolidChunkIsNotFilled() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(0, source.Fills);
            Assert.AreEqual(1, source.Classifications);
            Assert.IsTrue(engine.VoxelsReady);
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(Uniform(sbyte.MaxValue), ReadWithoutFilling(engine, UneditedVoxel));
        }

        [Test]
        public void OccupancySkip_MixedChunkIsFilled() {
            var source = new RecordingOccupancySource(Occupancy.Mixed, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(1, source.Classifications);
            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_ChunkWithStoredEditIsFilled() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            var edit = VoxelData.CreateImmature(EditedDensity, VoxelData.NullSentinelValue);
            engine.IGeoEditStore.Write(new List<(int, VoxelData)> { (IndexOf(EditedVoxel), edit) });

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(1, source.Fills);
            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(edit, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_ChunkWithOverridesIsFilled() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            var overrides = new VoxelOverrides();
            var overridden = VoxelData.CreateImmature(EditedDensity, VoxelData.NullSentinelValue);
            overrides.AddPointOverride(EditedVoxel, overridden);
            engine.SetOverrides(overrides);

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(1, source.Fills);
            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(overridden, engine.GetVoxelData(IndexOf(EditedVoxel)));
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_SourceWithoutOccupancyIsNeverAsked() {
            var recording = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            var source = new FillOnlyDensitySource(recording);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.AreEqual(0, recording.Classifications);
            Assert.AreEqual(1, recording.Fills);
            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(recording.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_VolumeWithoutRefillSourceFillsTheChunk() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            _world.Engine.UseDensitySource(null);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.IsTrue(_world.Engine.SkipFillByOccupancy);
            Assert.IsNull(_world.Engine.RefillSource);
            Assert.IsFalse(engine.IsUniform);
            Assert.AreEqual(1, source.Fills);
            Assert.AreEqual(source.FillVoxel, engine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_SourceThatIsNotTheVolumesFillsTheChunk() {
            var volumeSource = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            var otherSource = new RecordingOccupancySource(Occupancy.Solid, OtherRockFill);
            _world = new PlaneWorld(ChunkSize, 1, volumeSource);
            IGeoChunk chunk = _world.AddChunk(Chunk);

            chunk.SetVoxels(_world.Engine.CreateFillRequest(Chunk), otherSource);

            Assert.IsTrue(_world.Engine.SkipFillByOccupancy);
            Assert.AreEqual(1, otherSource.Fills);
            Assert.AreEqual(0, volumeSource.Fills);
            Assert.IsFalse(chunk.GeoChunkEngine.IsUniform);
            Assert.AreEqual(otherSource.FillVoxel, chunk.GeoChunkEngine.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_SettingOffFillsEveryChunk() {
            var source = new RecordingOccupancySource(Occupancy.Empty, AirFill);
            _world = new PlaneWorld(ChunkSize, 1, source, false);
            var first = _world.AddChunk(Chunk).GeoChunkEngine;
            var second = _world.AddChunk(SecondChunk).GeoChunkEngine;

            first.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            second.SetVoxels(_world.Engine.CreateFillRequest(SecondChunk), source);

            Assert.IsFalse(_world.Engine.SkipFillByOccupancy);
            Assert.AreEqual(2, source.Fills);
            Assert.AreEqual(0, source.Classifications);
            Assert.IsFalse(first.IsUniform);
            Assert.IsFalse(second.IsUniform);
            Assert.AreEqual(source.FillVoxel, second.GetVoxelData(IndexOf(UneditedVoxel)));
        }

        [Test]
        public void OccupancySkip_OwnVolumeImplementationIsSkipped() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.IsInstanceOf<IGeoVolume>(_world.Volume);
            Assert.IsNotInstanceOf<GeoVolume>(_world.Volume);
            Assert.IsTrue(_world.Engine.SkipFillByOccupancy);
            Assert.AreEqual(0, source.Fills);
            Assert.IsTrue(engine.IsUniform);
        }

        [Test]
        public void OccupancySkip_NoAllocation() {
            var source = new RecordingOccupancySource(Occupancy.Empty, AirFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            var request = _world.Engine.CreateFillRequest(Chunk);

            GcAllocations.AssertNone(() => engine.SetVoxels(request, source), WarmUps);

            Assert.AreEqual(0, source.Fills);
            Assert.IsTrue(engine.IsUniform);
        }

        [Test]
        public void OccupancySkip_CustomImplementation() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(OffsetChunk).GeoChunkEngine;
            ref var config = ref _world.Engine.ConfigBlob.Value;
            var minimum = ((Vector3)(OffsetChunk * config.ChunkSize) + config.Offset) * config.Resolution;
            var maximum = minimum + Vector3.one * ((config.ChunkSize + 2) * config.Resolution);

            engine.SetVoxels(_world.Engine.CreateFillRequest(OffsetChunk), source);

            Assert.AreEqual(1, source.Classifications);
            AssertNear(minimum, source.ClassifiedBounds.min);
            AssertNear(maximum, source.ClassifiedBounds.max);
            Assert.AreEqual(0, source.Fills);
            Assert.AreEqual(Uniform(sbyte.MaxValue), ReadWithoutFilling(engine, UneditedVoxel));
        }

        [Test]
        public void OccupancySkip_ClassifiesTheBoxTheRequestFills() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            var dataWidth = ChunkLayout.DataWidth(ChunkSize);
            var request = new ChunkFillRequest(Chunk, UnalignedOrigin, HalfVoxel, ChunkSize, dataWidth, 0u);

            engine.SetVoxels(request, source);

            Assert.AreEqual(1, source.Classifications);
            AssertNear((Vector3)UnalignedOrigin * HalfVoxel, source.ClassifiedBounds.min);
            AssertNear((Vector3)(UnalignedOrigin + Vector3Int.one * (dataWidth - 1)) * HalfVoxel,
                source.ClassifiedBounds.max);
            Assert.AreEqual(0, source.Fills);
        }

        [Test]
        public void OccupancySkip_UniformChunkAnswersPointQueries() {
            _world = new PlaneWorld(ChunkSize, 1, new PlaneDensitySource(0f));
            var air = _world.AddChunk(AirChunk).GeoChunkEngine;
            var rock = _world.AddChunk(RockChunk).GeoChunkEngine;
            IGeoVolume volume = _world.Volume;

            air.SetVoxels(_world.Engine.CreateFillRequest(AirChunk), _world.Engine.DensitySource);
            rock.SetVoxels(_world.Engine.CreateFillRequest(RockChunk), _world.Engine.DensitySource);

            Assert.IsTrue(air.IsUniform);
            Assert.IsTrue(rock.IsUniform);
            Assert.IsTrue(volume.TryQuerySolid(InTheAirChunk, out var airSolid));
            Assert.IsFalse(airSolid);
            Assert.IsTrue(volume.TryQuerySolid(InTheRockChunk, out var rockSolid));
            Assert.IsTrue(rockSolid);
        }

        [Test]
        public void OccupancySkip_UniformRockAnswersNoSurfaceMaterial() {
            _world = new PlaneWorld(ChunkSize, 1, new PlaneDensitySource(0f));
            var rock = _world.AddChunk(RockChunk).GeoChunkEngine;
            IGeoVolume volume = _world.Volume;

            rock.SetVoxels(_world.Engine.CreateFillRequest(RockChunk), _world.Engine.DensitySource);

            Assert.IsTrue(rock.IsUniform);
            Assert.IsTrue(volume.TryQuerySolid(InTheRockChunk, out var rockSolid));
            Assert.IsTrue(rockSolid);
            Assert.IsFalse(volume.TryQuerySurfaceMaterial(InTheRockChunk, out var material));
            Assert.AreEqual(VoxelData.NullSentinelValue, material);
            Assert.IsTrue(rock.IsUniform);
        }

        [Test]
        public void OccupancySkip_FilledChunkAnswersItsSurfaceMaterial() {
            _world = new PlaneWorld(ChunkSize, 1, new PlaneDensitySource(0f));
            var surface = _world.AddChunk(Chunk).GeoChunkEngine;
            IGeoVolume volume = _world.Volume;

            surface.SetVoxels(_world.Engine.CreateFillRequest(Chunk), _world.Engine.DensitySource);

            Assert.IsFalse(surface.IsUniform);
            Assert.IsTrue(volume.TryQuerySurfaceMaterial(BelowTheSurface, out var material));
            Assert.AreEqual(PlaneMaterial, material);
        }

        [Test]
        public void OccupancySkip_PointQueryLeavesTheChunkUniform() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;
            IGeoVolume volume = _world.Volume;
            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);

            Assert.IsTrue(volume.TryQueryVoxel(InTheChunk, out var voxel));

            Assert.AreEqual(Uniform(sbyte.MaxValue), voxel);
            Assert.IsTrue(engine.IsUniform);
            Assert.AreEqual(0, source.Fills);
        }

        [Test]
        public void OccupancySkip_UniformChunkMarchesNothing() {
            var source = new RecordingOccupancySource(Occupancy.Solid, RockFill);
            _world = new PlaneWorld(ChunkSize, 1, source);
            var engine = _world.AddChunk(Chunk).GeoChunkEngine;

            engine.SetVoxels(_world.Engine.CreateFillRequest(Chunk), source);
            _world.ValidateEveryChunk(Vector3.zero);
            Assert.IsTrue(_world.Engine.RefillChunks(new[] { Chunk }));

            Assert.IsTrue(engine.IsUniform);
            Assert.IsTrue(engine.DensityRange.IsSkippable());
            Assert.IsTrue(engine.MeshReady);
            Assert.IsNull(engine.RootNode);
            Assert.AreEqual(ChunkResidency.NoBuiltLevel, engine.FinestBuiltLevel);
            Assert.AreEqual(0, source.Fills);
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

        private static void AssertNear(Vector3 expected, Vector3 actual) {
            Assert.AreEqual(expected.x, actual.x, BoxTolerance);
            Assert.AreEqual(expected.y, actual.y, BoxTolerance);
            Assert.AreEqual(expected.z, actual.z, BoxTolerance);
        }
    }
}
