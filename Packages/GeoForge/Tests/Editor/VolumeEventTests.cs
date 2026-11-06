// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spellbound.GeoForge.Tests {
    public class VolumeEventTests {
        private const int ChunkSize = PlaneWorld.ChunkSize;
        private const float SphereSize = 4f;
        private const short SphereDelta = 60;
        private const byte SphereMaterial = 1;
        private const int SearchExtent = 6;
        private const int LayeredChunkSize = 32;
        private const float NearRange = 20f;
        private const float FarAway = 1000f;
        private const float ApronOnlyGroundHeight = -(1 + ChunkSize / 2) + 0.5f;
        private const short SolidDelta = 120;

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3 AcrossTheBorder = new(8f, 0f, 0f);

        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void ASphereAcrossTwoChunksRaisesOneChangeForEachChunk() {
            _world = new PlaneWorld(0f, FirstChunk, SecondChunk);
            IGeoVolume volume = _world.Volume;
            var before = ReadAround(AcrossTheBorder);
            var versionBefore = _world.Engine.Version;
            var changes = new List<VolumeChange>();
            var readsInsideChange = new Dictionary<Vector3Int, sbyte>();

            volume.Changed += change => {
                changes.Add(change);
                ReadOwnedVoxels(change, readsInsideChange);
            };

            Assert.IsTrue(GeoForgeCommands.AddSphere(volume, AcrossTheBorder, SphereSize, SphereDelta, SphereMaterial));

            var after = ReadAround(AcrossTheBorder);
            var edited = new List<Vector3Int>();

            foreach (var entry in before) {
                if (after[entry.Key] != entry.Value)
                    edited.Add(entry.Key);
            }

            Assert.IsNotEmpty(edited);
            Assert.AreEqual(2, changes.Count);
            CollectionAssert.AreEquivalent(new[] { FirstChunk, SecondChunk },
                new[] { changes[0].ChunkCoordinate, changes[1].ChunkCoordinate });

            for (var index = 0; index < changes.Count; index++) {
                Assert.AreEqual(versionBefore.Value + index + 1, changes[index].Version.Value);
                AssertInsideDataArray(changes[index]);
            }

            Assert.AreEqual(changes[^1].Version, _world.Engine.Version);

            foreach (var voxel in edited) {
                Assert.IsTrue(changes.Exists(change => change.VoxelBounds.Contains(voxel)),
                    $"edited voxel {voxel} lies outside every raised change");
            }

            Assert.IsNotEmpty(readsInsideChange);

            foreach (var read in readsInsideChange) {
                if (after.TryGetValue(read.Key, out var final))
                    Assert.AreEqual(final, read.Value, $"read inside the change at {read.Key}");
            }
        }

        [Test]
        public void ASubscriberThatThrowsIsReportedAndTheNextStillRuns() {
            _world = new PlaneWorld(0f, FirstChunk);
            IGeoVolume volume = _world.Volume;
            var reached = 0;

            volume.Changed += _ => throw new InvalidOperationException("subscriber failure");
            volume.Changed += _ => reached++;
            LogAssert.Expect(LogType.Error, new Regex("subscriber failure"));

            Assert.IsTrue(GeoForgeCommands.AddSphere(volume, Vector3.zero, SphereSize, SphereDelta, SphereMaterial));

            Assert.AreEqual(1, reached);
        }

        [Test]
        public void AChunkReportsResidencyOnFillOnMeshAndOnRelease() {
            _world = new PlaneWorld(0f, FirstChunk);
            IGeoVolume volume = _world.Volume;
            var chunk = _world.Chunks[0];
            var residencies = new List<ChunkResidency>();
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = null;

            volume.ResidencyChanged += residencies.Add;

            ((IGeoChunk)chunk).SetVoxels(_world.Engine.CreateFillRequest(FirstChunk), new PlaneDensitySource(0f));

            Assert.AreEqual(1, residencies.Count);
            Assert.AreEqual(FirstChunk, residencies[0].ChunkCoordinate);
            Assert.AreEqual(ChunkResidencyState.Resident, residencies[0].State);
            Assert.AreEqual(ChunkResidency.NoBuiltLevel, residencies[0].FinestBuiltLevel);

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(2, residencies.Count);
            Assert.AreEqual(ChunkResidencyState.Resident, residencies[1].State);
            Assert.AreEqual(0, residencies[1].FinestBuiltLevel);

            chunk.GeoChunkEngine.Dispose();

            Assert.AreEqual(3, residencies.Count);
            Assert.AreEqual(ChunkResidencyState.Released, residencies[2].State);
            Assert.AreEqual(ChunkResidency.NoBuiltLevel, residencies[2].FinestBuiltLevel);
        }

        [Test]
        public void AnEditMadeByAResidencySubscriberLandsAfterTheBatchThatRaisedIt() {
            _world = new PlaneWorld(0f, FirstChunk);
            IGeoVolume volume = _world.Volume;
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = null;
            var densityBefore = DensityAt(Vector3.zero);
            var changes = new List<VolumeChange>();
            var edits = 0;

            volume.Changed += changes.Add;
            volume.ResidencyChanged += residency => {
                if (residency.FinestBuiltLevel == ChunkResidency.NoBuiltLevel || edits > 0)
                    return;

                edits++;
                Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, Vector3.zero, SphereSize, SphereDelta));
            };

            volume.SetViewers(new[] { new ChunkViewer(Vector3.zero, ChunkSize, 0f) });
            var validation = _world.Engine.ValidateChunkLodsAsync();

            Assert.DoesNotThrow(() => validation.GetAwaiter().GetResult());
            Assert.AreEqual(1, edits);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(FirstChunk, changes[0].ChunkCoordinate);
            Assert.Less(DensityAt(Vector3.zero), densityBefore);
            Assert.AreEqual(0, PendingCount("_pendingMarchJobData"));
            Assert.AreEqual(0, PendingCount("_pendingTransitionMarchJobData"));
        }

        [Test]
        public void AnEditMadeByAResidencySubscriberDuringAnEditLandsAfterTheChunkIsReleased() {
            _world = new PlaneWorld(ApronOnlyGroundHeight, FirstChunk);
            IGeoVolume volume = _world.Volume;
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = null;
            _world.ValidateEveryChunk(Vector3.zero);
            var chunkCentre = NodeCentreInWorld(ChunkSize / 2);
            var changes = new List<VolumeChange>();
            var edits = 0;

            volume.Changed += changes.Add;
            volume.ResidencyChanged += residency => {
                if (residency.FinestBuiltLevel == ChunkResidency.NoBuiltLevel || edits > 0)
                    return;

                edits++;
                Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, chunkCentre, SphereSize / 2f, SphereDelta));
            };

            Assert.IsTrue(GeoForgeCommands.AddSphere(volume, chunkCentre, SphereSize, SolidDelta, SphereMaterial));

            Assert.AreEqual(1, edits);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(FirstChunk, changes[0].ChunkCoordinate);
            Assert.AreEqual(FirstChunk, changes[1].ChunkCoordinate);
            Assert.Greater(changes[1].Version.Value, changes[0].Version.Value);
        }

        [Test]
        public void AViewerMovingAwayLowersTheReportedLevel() {
            var levels = TwoLevelChunkResidencies(out var volume);
            var nearViewer = NodeCentreInWorld(LayeredChunkSize / 2);

            volume.SetViewers(new[] { new ChunkViewer(nearViewer, LayeredChunkSize, 0f) });
            _ = _world.Engine.ValidateChunkLodsAsync();

            Assert.AreEqual(0, levels[^1]);

            volume.SetViewers(new[] { new ChunkViewer(nearViewer + Vector3.right * FarAway, LayeredChunkSize, 0f) });
            _ = _world.Engine.ValidateChunkLodsAsync();

            Assert.AreEqual(1, levels[^1]);
        }

        [Test]
        public void AChunkNoLongerWantedReportsNoBuiltLevel() {
            var levels = TwoLevelChunkResidencies(out var volume);

            volume.SetViewers(new[] { new ChunkViewer(NodeCentreInWorld(LayeredChunkSize / 2), LayeredChunkSize, 0f) });
            _ = _world.Engine.ValidateChunkLodsAsync();

            Assert.AreEqual(0, levels[^1]);

            volume.ChunkInterest = new FixedLevelInterest(ChunkInterest.NotWanted);
            _ = _world.Engine.ValidateChunkLodsAsync();

            Assert.AreEqual(ChunkResidency.NoBuiltLevel, levels[^1]);
        }

        private List<int> TwoLevelChunkResidencies(out IGeoVolume volume) {
            var chunkOriginHeight = -(1 + LayeredChunkSize / 2);
            var plane = new PlaneDensitySource(chunkOriginHeight + LayeredChunkSize / 4f);
            _world = new PlaneWorld(LayeredChunkSize, 2, plane, FirstChunk);
            _world.Volume.ViewDistanceLodRanges = new[] {
                new Vector2(0f, NearRange), new Vector2(NearRange, float.MaxValue)
            };
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = null;
            volume = _world.Volume;
            var levels = new List<int>();
            volume.ResidencyChanged += residency => levels.Add(residency.FinestBuiltLevel);

            return levels;
        }

        private Vector3 NodeCentreInWorld(int voxel) {
            ref var config = ref _world.Engine.ConfigBlob.Value;

            return ((Vector3)(Vector3Int.one * voxel) + config.Offset) * config.Resolution;
        }

        private sbyte DensityAt(Vector3 worldPosition) {
            Assert.IsTrue(_world.Engine.TryQueryVoxel(worldPosition, out var voxel));

            return voxel.Density;
        }

        private int PendingCount(string fieldName) {
            var pending = typeof(GeoForgeManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(pending, fieldName);

            return ((ICollection)pending.GetValue(_world.Manager)).Count;
        }

        private static void AssertInsideDataArray(in VolumeChange change) {
            var firstDataVoxel = change.ChunkCoordinate * ChunkSize;
            var lastDataVoxel = firstDataVoxel + Vector3Int.one * (ChunkSize + 2);
            var bounds = change.VoxelBounds;

            Assert.Greater(bounds.size.x * bounds.size.y * bounds.size.z, 0);
            Assert.IsTrue(Vector3Int.Min(bounds.min, firstDataVoxel) == firstDataVoxel, $"{bounds} starts outside");
            Assert.IsTrue(Vector3Int.Max(bounds.max - Vector3Int.one, lastDataVoxel) == lastDataVoxel,
                $"{bounds} ends outside");
        }

        private Dictionary<Vector3Int, sbyte> ReadAround(Vector3 centre) {
            var reads = new Dictionary<Vector3Int, sbyte>();

            for (var x = -SearchExtent; x <= SearchExtent; x++)
            for (var y = -SearchExtent; y <= SearchExtent; y++)
            for (var z = -SearchExtent; z <= SearchExtent; z++) {
                var position = centre + new Vector3(x, y, z);

                if (_world.Engine.TryQueryVoxel(position, out var voxel))
                    reads[_world.Engine.WorldToVoxelSpace(position)] = voxel.Density;
            }

            return reads;
        }

        private void ReadOwnedVoxels(in VolumeChange change, Dictionary<Vector3Int, sbyte> reads) {
            var engine = _world.Engine;
            var bounds = change.VoxelBounds;

            foreach (var voxelPosition in bounds.allPositionsWithin) {
                if (engine.GetCoordByVoxelPosition(voxelPosition) != change.ChunkCoordinate)
                    continue;

                var chunk = engine.GetChunkByCoord(change.ChunkCoordinate);
                reads[voxelPosition] = chunk.GetVoxelDataFromVoxelPosition(voxelPosition).Density;
            }
        }
    }
}
