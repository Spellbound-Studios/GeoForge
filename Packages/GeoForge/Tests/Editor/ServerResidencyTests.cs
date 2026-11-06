// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class ServerResidencyTests {
        private const int LayeredChunkSize = 32;
        private const int LayeredLevels = 2;
        private const int OctantsOfAChunk = 8;
        private const int OctantsBelowTheSurface = 4;
        private const float FlatHeight = 4f;
        private const float BoxHeightAboveGround = 40f;
        private const float WideRadius = 1000f;
        private const float TunnelRadius = 4f;
        private const int TunnelDelta = 255;

        private RecordingRenderingProfile _profile;
        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;

            if (_profile != null)
                Object.DestroyImmediate(_profile);
        }

        [Test]
        public void TheInterestNeverAsksOccupancyOfASourceThatDoesNotOfferIt() {
            var plane = LowerHalfPlane();
            _world = LayeredWorld(new FillOnlyDensitySource(plane));
            var interest = new ServerChunkInterest(_world.Volume) { SkipByOccupancy = true };
            ((IGeoVolume)_world.Volume).ChunkInterest = interest;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(0, plane.Classifications);
            Assert.AreEqual(OctantsOfAChunk, _profile.Marches);
        }

        [Test]
        public void TheInterestAsksNoOccupancyWithoutTheOption() {
            var plane = LowerHalfPlane();
            _world = LayeredWorld(plane);
            ((IGeoVolume)_world.Volume).ChunkInterest = new ServerChunkInterest(_world.Volume) {
                SkipByOccupancy = false
            };

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(0, plane.Classifications);
        }

        [Test]
        public void AnAllAirOrAllSolidBoxIsNotWantedUnderTheOptionAndWantedWithoutIt() {
            _world = new PlaneWorld(FlatHeight, Vector3Int.zero);
            var interest = new ServerChunkInterest(_world.Volume);
            var size = Vector3.one * PlaneWorld.ChunkSize;
            var airBox = new Bounds(Vector3.up * BoxHeightAboveGround, size);
            var solidBox = new Bounds(Vector3.down * BoxHeightAboveGround, size);
            var crossingBox = new Bounds(new Vector3(0f, FlatHeight - _world.Engine.ConfigBlob.Value.Offset.y, 0f),
                size);

            interest.SkipByOccupancy = true;

            Assert.AreEqual(ChunkInterest.NotWanted, DetailLevelBesideTheBox(interest, airBox));
            Assert.AreEqual(ChunkInterest.NotWanted, DetailLevelBesideTheBox(interest, solidBox));
            Assert.AreEqual(0, DetailLevelBesideTheBox(interest, crossingBox));

            interest.SkipByOccupancy = false;

            Assert.AreEqual(0, DetailLevelBesideTheBox(interest, airBox));
            Assert.AreEqual(0, DetailLevelBesideTheBox(interest, solidBox));
        }

        [Test]
        public void TheAirNodesOfACrossingChunkAreMarchedOnlyWithoutTheOption() {
            var plane = LowerHalfPlane();
            _world = LayeredWorld(plane);
            var interest = new ServerChunkInterest(_world.Volume) { SkipByOccupancy = true };
            ((IGeoVolume)_world.Volume).ChunkInterest = interest;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.Greater(plane.Classifications, 0);
            Assert.AreEqual(OctantsBelowTheSurface, _profile.Marches);

            _world.Dispose();
            Object.DestroyImmediate(_profile);
            _world = LayeredWorld(LowerHalfPlane());
            ((IGeoVolume)_world.Volume).ChunkInterest = new ServerChunkInterest(_world.Volume) {
                SkipByOccupancy = false
            };

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(OctantsOfAChunk, _profile.Marches);
        }

        [Test]
        public void TheServerInterestDoesNotSkipByOccupancyByDefault() {
            _world = new PlaneWorld(FlatHeight, Vector3Int.zero);

            Assert.IsFalse(new ServerChunkInterest(_world.Volume).SkipByOccupancy);
        }

        [Test]
        public void AnEditedChunkUnderSolidGroundIsMarchedAndCollidesUnderTheOption() {
            _world = new PlaneWorld(PlaneWorld.ChunkSize, 1, new PlaneDensitySource(BoxHeightAboveGround),
                Vector3Int.zero);
            IGeoVolume volume = _world.Volume;
            var collisionPresenter = new RecordingCollisionPresenter();
            _world.Manager.Presenter = null;
            _world.Manager.CollisionPresenter = collisionPresenter;
            volume.ChunkInterest = new ServerChunkInterest(volume) { SkipByOccupancy = true };
            ref var config = ref _world.Engine.ConfigBlob.Value;
            var chunkCentre = ((Vector3)(Vector3Int.one * (PlaneWorld.ChunkSize / 2)) + config.Offset) *
                              config.Resolution;

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, chunkCentre, TunnelRadius, TunnelDelta));

            _world.ValidateEveryChunk(chunkCentre);

            Assert.AreEqual(1, collisionPresenter.Acquired.Count);
            Assert.IsTrue(collisionPresenter.Acquired[0].HasMesh);
        }

        private static int DetailLevelBesideTheBox(ServerChunkInterest interest, Bounds box) {
            var viewers = new[] { new ChunkViewer(box.center, WideRadius, WideRadius) };
            using var ranges = new NativeArray<Vector2>(new[] { new Vector2(0f, float.MaxValue) }, Allocator.Temp);

            return interest.DetailLevel(new ChunkInterestRequest(box, 1f, ranges.AsReadOnly(), false), viewers);
        }

        private static PlaneDensitySource LowerHalfPlane() {
            var chunkOriginHeight = -(1 + LayeredChunkSize / 2);

            return new PlaneDensitySource(chunkOriginHeight + LayeredChunkSize / 4f);
        }

        private PlaneWorld LayeredWorld(IDensitySource source) {
            var world = new PlaneWorld(LayeredChunkSize, LayeredLevels, source, Vector3Int.zero);
            world.Manager.Presenter = null;
            world.Manager.CollisionPresenter = null;
            var origin = ChunkLayout.ChunkOrigin(int3.zero, world.Engine.ConfigBlob.Value);
            Assert.AreEqual(-(1 + LayeredChunkSize / 2), origin.y);

            _profile = ScriptableObject.CreateInstance<RecordingRenderingProfile>();
            world.Manager.jobAndRenderProfile = _profile;

            return world;
        }
    }
}
