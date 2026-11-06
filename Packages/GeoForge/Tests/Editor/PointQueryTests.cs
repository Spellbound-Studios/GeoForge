// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Spellbound.GeoForge.Tests {
    public class PointQueryTests {
        private const int ChunkSize = PlaneWorld.ChunkSize;

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3 OnTheSurface = new(0f, 0f, 0f);
        private static readonly Vector3 Underground = new(0f, -4f, 0f);
        private static readonly Vector3 InTheAir = new(0f, 4f, 0f);
        private static readonly Vector3 NoChunkHere = new(0f, 0f, 100f);
        private static readonly Bounds AcrossBothChunks = new(new Vector3(8f, 0f, 0f), new Vector3(8f, 2f, 2f));

        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void ASolidQueryAnswersFromTheNearestVoxel() {
            _world = new PlaneWorld(0f, FirstChunk);
            IGeoVolume volume = _world.Volume;

            Assert.IsTrue(volume.TryQuerySolid(Underground, out var undergroundSolid));
            Assert.IsTrue(undergroundSolid);
            Assert.IsTrue(volume.TryQuerySolid(InTheAir, out var airSolid));
            Assert.IsFalse(airSolid);
        }

        [Test]
        public void EveryQueryAnswersFalseWhereNoChunkIsLoaded() {
            _world = new PlaneWorld(0f, FirstChunk);
            IGeoVolume volume = _world.Volume;
            var outside = new Bounds(NoChunkHere, Vector3.one);

            Assert.IsFalse(volume.TryQuerySolid(NoChunkHere, out var solid));
            Assert.IsFalse(solid);
            Assert.IsFalse(volume.IsMeshReady(NoChunkHere, ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsColliderReady(NoChunkHere, ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsBoxReady(outside, ReadinessScope.AnyLevel, out _));
        }

        [Test]
        public void ALeafIsReadyOnlyOnceItsMeshIsApplied() {
            _world = new PlaneWorld(0f, FirstChunk);
            IGeoVolume volume = _world.Volume;
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = new RecordingCollisionPresenter();

            Assert.IsFalse(volume.IsMeshReady(OnTheSurface, ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsColliderReady(OnTheSurface, ReadinessScope.AnyLevel));

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.IsTrue(volume.IsMeshReady(OnTheSurface, ReadinessScope.AnyLevel));
            Assert.IsTrue(volume.IsMeshReady(OnTheSurface, ReadinessScope.FinestLevel));
            Assert.IsTrue(volume.IsColliderReady(OnTheSurface, ReadinessScope.AnyLevel));
        }

        [Test]
        public void TheFinestScopeWaitsForTheFinestLevel() {
            _world = new PlaneWorld(ChunkSize, 2, new PlaneDensitySource(0f), FirstChunk);
            IGeoVolume volume = _world.Volume;
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = new RecordingCollisionPresenter();

            _world.Engine.ChunkInterest = new FixedLevelInterest(1);
            _world.ValidateEveryChunk(Vector3.zero);

            Assert.IsTrue(volume.IsMeshReady(OnTheSurface, ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsMeshReady(OnTheSurface, ReadinessScope.FinestLevel));
            Assert.IsFalse(volume.IsColliderReady(OnTheSurface, ReadinessScope.FinestLevel));

            _world.Engine.ChunkInterest = new FixedLevelInterest(0);
            _world.ValidateEveryChunk(Vector3.zero);

            Assert.IsTrue(volume.IsMeshReady(OnTheSurface, ReadinessScope.FinestLevel));
            Assert.IsTrue(volume.IsColliderReady(OnTheSurface, ReadinessScope.FinestLevel));
        }

        [Test]
        public void ABoxAcrossTwoChunksIsReadyOnlyWhenBothAre() {
            _world = new PlaneWorld(0f, FirstChunk, SecondChunk);
            IGeoVolume volume = _world.Volume;
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = new RecordingCollisionPresenter();

            Validate(_world.Chunks[0]);

            Assert.IsTrue(volume.IsColliderReady(new Vector3(4f, 0f, 0f), ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsBoxReady(AcrossBothChunks, ReadinessScope.AnyLevel, out var notReadyVersion));
            Assert.AreEqual(_world.Engine.Version, notReadyVersion);

            Validate(_world.Chunks[1]);

            Assert.IsTrue(volume.IsBoxReady(AcrossBothChunks, ReadinessScope.AnyLevel, out var readyVersion));
            Assert.AreEqual(_world.Engine.Version, readyVersion);
        }

        [Test]
        public void PointQuery_NoAllocation() {
            _world = new PlaneWorld(0f, FirstChunk, SecondChunk);
            IGeoVolume volume = _world.Volume;
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = new RecordingCollisionPresenter();
            _world.ValidateEveryChunk(Vector3.zero);
            var bounds = AcrossBothChunks;

            void QueryEverything() {
                volume.TryQuerySolid(Underground, out _);
                volume.TryQuerySolid(NoChunkHere, out _);
                volume.IsMeshReady(OnTheSurface, ReadinessScope.AnyLevel);
                volume.IsColliderReady(OnTheSurface, ReadinessScope.FinestLevel);
                volume.IsBoxReady(bounds, ReadinessScope.AnyLevel, out _);
            }

            QueryEverything();

            Assert.That(() => QueryEverything(), Is.Not.AllocatingGCMemory());
        }

        private void Validate(GeoChunkDouble chunk) {
            using var viewers = new NativeArray<ChunkViewer>(1, Allocator.Temp);
            chunk.GeoChunkEngine.ScheduleOctreeLodValidation(viewers, new FixedLevelInterest(0));
            _world.Manager.CompleteAndApplyMarchingCubesJobs();
            chunk.GeoChunkEngine.ReleaseLodValidation();
        }
    }
}
