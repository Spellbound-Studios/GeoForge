// Copyright (c) Spellbound Studios. All rights reserved.

using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Spellbound.GeoForge.Tests {
    public class ChunkInterestTests {
        private const float PlaneHeight = 4f;
        private const int LayeredChunkSize = 32;
        private const int LastLevel = 1;
        private const int AboveEveryLevel = 99;
        private const float SmallDetailRadius = 10f;
        private const int EdgeChunkSize = 32;
        private const float EdgeResolution = 0.5f;
        private const float EdgeOffsetVoxels = 157f;
        private const float EdgeDistance = EdgeOffsetVoxels * EdgeResolution;

        private static readonly Vector3Int FarChunk = new(37, -5, 12);

        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void ChunkInterest_CustomImplementation() {
            _world = new PlaneWorld(PlaneHeight, Vector3Int.zero, Vector3Int.right, 2 * Vector3Int.right);
            var presenter = new RecordingChunkPresenter();
            _world.Manager.Presenter = presenter;
            _world.Manager.CollisionPresenter = null;
            var wantedChunk = _world.Chunks[1];
            var wantedCentre = RootCentre(Vector3Int.right);
            var interest = new SingleNodeInterest(wantedCentre);
            ((IGeoVolume)_world.Volume).ChunkInterest = interest;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreSame(interest, ((IGeoVolume)_world.Volume).ChunkInterest);
            Assert.AreEqual(3, interest.Questions);
            Assert.AreEqual(1, interest.ViewersSeen);
            Assert.AreEqual(1, presenter.Acquired.Count);
            Assert.AreSame(wantedChunk.GeoChunkEngine.Transform, presenter.Acquired[0].Parent);
            Assert.Greater(presenter.Acquired[0].Mesh.vertexCount, 0);
        }

        [Test]
        public void TheDistanceInterestAnswersWhatTheOldLevelOfDetailRuleAnswered() {
            var interest = new DistanceChunkInterest();
            var viewers = new ChunkViewer[1];
            using var ranges = new NativeArray<Vector2>(DistanceLevelRecords.Ranges, Allocator.Temp);

            Assert.AreEqual(600, DistanceLevelRecords.Records.Length);

            foreach (var record in DistanceLevelRecords.Records) {
                viewers[0] = new ChunkViewer(record.Viewer, 0f, 0f);
                var request = new ChunkInterestRequest(new Bounds(record.NodeCentre, Vector3.zero), record.Resolution,
                    ranges.AsReadOnly(), false);

                Assert.AreEqual(record.Level, interest.DetailLevel(request, viewers),
                    $"viewer {record.Viewer}, node centre {record.NodeCentre}, resolution {record.Resolution}");
            }
        }

        [Test]
        public void TheServerInterestWantsTheFinestLevelInsideTheCollisionRadiusOnly() {
            _world = new PlaneWorld(PlaneHeight, Vector3Int.zero);
            var interest = new ServerChunkInterest(_world.Volume) { SkipByOccupancy = false };
            var chunkCentre = new Vector3(PlaneWorld.ChunkSize, PlaneWorld.ChunkSize, PlaneWorld.ChunkSize) * 0.5f;
            var viewers = new[] { new ChunkViewer(chunkCentre, PlaneWorld.ChunkSize, 0f) };
            var wanted = 0;
            var notWanted = 0;
            using var ranges = new NativeArray<Vector2>(DistanceLevelRecords.Ranges, Allocator.Temp);

            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
            for (var z = -2; z <= 2; z++) {
                var shell = Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z)));
                var centre = chunkCentre + new Vector3(x, y, z) * PlaneWorld.ChunkSize;
                var box = new Bounds(centre, Vector3.one * PlaneWorld.ChunkSize);
                var level = interest.DetailLevel(new ChunkInterestRequest(box, 1f, ranges.AsReadOnly(), false),
                    viewers);

                if (shell <= 1) {
                    Assert.AreEqual(0, level, $"chunk offset {x} {y} {z}");
                    wanted++;
                }
                else {
                    Assert.AreEqual(ChunkInterest.NotWanted, level, $"chunk offset {x} {y} {z}");
                    notWanted++;
                }
            }

            Assert.AreEqual(27, wanted);
            Assert.AreEqual(98, notWanted);
        }

        [Test]
        public void AVolumeWithNoViewersValidatesNothingAndSaysSoOnce() {
            _world = new PlaneWorld(PlaneHeight, Vector3Int.zero);
            var presenter = new RecordingChunkPresenter();
            _world.Manager.Presenter = presenter;
            _world.Manager.CollisionPresenter = null;
            var reports = 0;
            var pattern = new Regex(Regex.Escape(_world.Volume.name) + ".*no viewers");

            void Count(string message, string stackTrace, LogType type) {
                if (type == LogType.Warning && pattern.IsMatch(message))
                    reports++;
            }

            LogAssert.Expect(LogType.Warning, pattern);
            Application.logMessageReceived += Count;

            try {
                _ = _world.Engine.ValidateChunkLodsAsync();
                _ = _world.Engine.ValidateChunkLodsAsync();
            }
            finally {
                Application.logMessageReceived -= Count;
            }

            Assert.AreEqual(1, reports);
            CollectionAssert.IsEmpty(presenter.Calls);
            Assert.IsFalse(_world.Chunks[0].GeoChunkEngine.MeshReady);
        }

        [Test]
        public void AnAnswerAboveTheLastLevelIsClampedToIt() {
            var presenter = TwoLevelWorld(new FixedLevelInterest(AboveEveryLevel));

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(1, presenter.Acquired.Count);
            Assert.AreEqual(LastLevel, presenter.Acquired[0].MeshLevelOfDetail);
        }

        [Test]
        public void AnyNegativeAnswerIsNotWanted() {
            var presenter = TwoLevelWorld(new FixedLevelInterest(int.MinValue));

            _world.ValidateEveryChunk(Vector3.zero);

            CollectionAssert.IsEmpty(presenter.Acquired);
        }

        [Test]
        public void TheDistanceInterestDoesNotWantANodeBeyondEveryViewersDetailRadius() {
            var interest = new DistanceChunkInterest();
            using var ranges = new NativeArray<Vector2>(DistanceLevelRecords.Ranges, Allocator.Temp);
            var viewers = new[] { new ChunkViewer(Vector3.zero, 0f, SmallDetailRadius) };
            var inside = new ChunkInterestRequest(new Bounds(Vector3.right * (SmallDetailRadius - 1f), Vector3.one),
                1f, ranges.AsReadOnly(), false);
            var beyond = new ChunkInterestRequest(new Bounds(Vector3.right * (SmallDetailRadius + 1f), Vector3.one),
                1f, ranges.AsReadOnly(), false);

            Assert.AreEqual(0, interest.DetailLevel(inside, viewers));
            Assert.AreEqual(ChunkInterest.NotWanted, interest.DetailLevel(beyond, viewers));

            var secondViewer = new ChunkViewer(Vector3.right * SmallDetailRadius * 2f, 0f, SmallDetailRadius);
            viewers = new[] { viewers[0], secondViewer };

            Assert.AreEqual(0, interest.DetailLevel(beyond, viewers));
        }

        [Test]
        public void TheDistanceInterestMatchesTheChunkSpaceRuleAtAnExactRangeEdge() {
            var interest = new DistanceChunkInterest();
            var edgeRanges = new[] { new Vector2(0f, EdgeDistance), new Vector2(EdgeDistance, float.MaxValue) };
            using var ranges = new NativeArray<Vector2>(edgeRanges, Allocator.Temp);
            var chunkMinimum = FarChunk * EdgeChunkSize;
            var nodeCentreChunkSpace = Vector3.one * (EdgeChunkSize + 2) * 0.5f;
            var nodeCentreVoxelSpace = nodeCentreChunkSpace + chunkMinimum;
            var request = new ChunkInterestRequest(new Bounds(nodeCentreVoxelSpace, Vector3.one), EdgeResolution,
                ranges.AsReadOnly(), false);

            for (var step = -1; step <= 1; step++) {
                var viewerVoxelSpace = Vector3Int.RoundToInt(nodeCentreVoxelSpace + Vector3.right * EdgeOffsetVoxels) +
                                       Vector3Int.right * step;
                var viewerChunkSpace = (Vector3)(viewerVoxelSpace - chunkMinimum);
                var chunkSpaceDistance = Vector3.Distance(nodeCentreChunkSpace, viewerChunkSpace) * EdgeResolution;
                var expected = chunkSpaceDistance <= EdgeDistance ? 0 : 1;
                var viewers = new[] { new ChunkViewer(viewerVoxelSpace, 0f, 0f) };

                Assert.AreEqual(expected, interest.DetailLevel(request, viewers), $"step {step}");
            }

            var onTheEdge = Vector3Int.RoundToInt(nodeCentreVoxelSpace + Vector3.right * EdgeOffsetVoxels);
            Assert.AreEqual(EdgeDistance,
                Vector3.Distance(nodeCentreChunkSpace, onTheEdge - chunkMinimum) * EdgeResolution);
        }

        [Test]
        public void AskingWhetherAChunkHasEdits_NoAllocation() {
            _world = new PlaneWorld(PlaneHeight, Vector3Int.zero);
            var chunkEngine = _world.Chunks[0].GeoChunkEngine;

            chunkEngine.HasEdits();

            Assert.That(() => { chunkEngine.HasEdits(); }, Is.Not.AllocatingGCMemory());
            Assert.IsFalse(chunkEngine.HasEdits());

            GeoForgeCommands.AddSphere(_world.Volume, Vector3.zero, 3f, 60, 1);

            Assert.IsTrue(chunkEngine.HasEdits());
            Assert.That(() => { chunkEngine.HasEdits(); }, Is.Not.AllocatingGCMemory());
        }

        private RecordingChunkPresenter TwoLevelWorld(IChunkInterest interest) {
            var chunkOriginHeight = -(1 + LayeredChunkSize / 2);
            var plane = new PlaneDensitySource(chunkOriginHeight + LayeredChunkSize / 4f);
            _world = new PlaneWorld(LayeredChunkSize, LastLevel + 1, plane, Vector3Int.zero);
            var presenter = new RecordingChunkPresenter();
            _world.Manager.Presenter = presenter;
            _world.Manager.CollisionPresenter = null;
            ((IGeoVolume)_world.Volume).ChunkInterest = interest;

            return presenter;
        }

        private static Vector3 RootCentre(Vector3Int chunkCoordinate) {
            var halfNode = (PlaneWorld.ChunkSize + 2) * 0.5f;

            return new Vector3(halfNode, halfNode, halfNode) + chunkCoordinate * PlaneWorld.ChunkSize;
        }
    }
}
