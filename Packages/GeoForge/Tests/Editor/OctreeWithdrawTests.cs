// Copyright (c) Spellbound Studios. All rights reserved.

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class OctreeWithdrawTests {
        private const int ChunkSize = 64;
        private const int Levels = 3;
        private const int CoarseLevel = 1;
        private const int CoarseChildAlongX = 1;
        private const float SplitX = ChunkSize / 2f;
        private const float PlaneAboveChunkOrigin = 20f;

        private CountingGeoChunk _chunk;
        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _chunk?.GeoChunkEngine?.Dispose();

            if (_chunk != null)
                Object.DestroyImmediate(_chunk.gameObject);

            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void WithdrawingFinerNodesClearsTheirCoarserNeighboursTransitionFace() {
            var chunkOriginHeight = -(1 + ChunkSize / 2);
            var plane = new PlaneDensitySource(chunkOriginHeight + PlaneAboveChunkOrigin);
            _world = new PlaneWorld(ChunkSize, Levels, plane);
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = null;
            var chunkObject = new GameObject("Withdrawn Chunk");
            chunkObject.transform.SetParent(_world.Volume.transform, false);
            _chunk = chunkObject.AddComponent<CountingGeoChunk>();
            _chunk.InitializeGeoChunk(Vector3Int.zero, new GeoEditStore());
            _world.Engine.RegisterChunk(Vector3Int.zero, _chunk);
            IGeoChunk chunk = _chunk;
            chunk.SetVoxels(_world.Engine.CreateFillRequest(Vector3Int.zero), plane);
            var interest = new SplitInterest(SplitX, 0, CoarseLevel);
            ((IGeoVolume)_world.Volume).ChunkInterest = interest;
            var faceTowardTheSplit = (int)GfStaticHelper.TransitionFaceMask.XMin;

            _world.ValidateEveryChunk(Vector3.zero);

            var coarseNeighbour = Children(_chunk.GeoChunkEngine.RootNode)[CoarseChildAlongX];
            Assert.AreEqual(CoarseLevel, coarseNeighbour.LevelOfDetail);
            Assert.IsNull(Children(coarseNeighbour));
            Assert.AreNotEqual(0, TransitionMask(coarseNeighbour) & faceTowardTheSplit);

            interest.LevelBelowSplit = ChunkInterest.NotWanted;
            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreSame(coarseNeighbour, Children(_chunk.GeoChunkEngine.RootNode)[CoarseChildAlongX]);
            Assert.AreEqual(0, TransitionMask(coarseNeighbour) & faceTowardTheSplit);
        }

        private static OctreeNode[] Children(OctreeNode node) {
            return (OctreeNode[])typeof(OctreeNode)
                .GetField("_children", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(node);
        }

        private static int TransitionMask(OctreeNode node) {
            return (int)typeof(OctreeNode).GetField("_transitionMask", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(node);
        }
    }
}
