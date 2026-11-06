// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class GeoChunkTests {
        private static readonly Vector3Int ChunkCoordinate = Vector3Int.right;

        private CountingGeoChunk _chunk;
        private PerlinTerrainDataFactory _factory;
        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _chunk?.GeoChunkEngine?.Dispose();

            if (_chunk != null)
                Object.DestroyImmediate(_chunk.gameObject);

            if (_factory != null)
                Object.DestroyImmediate(_factory);

            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void EveryFillTellsTheChunkItsVoxelsWereSetOnce() {
            _world = new PlaneWorld(0f, Vector3Int.zero);
            var chunkObject = new GameObject("Counting Chunk");
            chunkObject.transform.SetParent(_world.Volume.transform, false);
            _chunk = chunkObject.AddComponent<CountingGeoChunk>();
            _chunk.InitializeGeoChunk(ChunkCoordinate, new GeoEditStore());
            IGeoChunk chunk = _chunk;

            chunk.SetVoxels(_world.Engine.CreateFillRequest(ChunkCoordinate), new PlaneDensitySource(0f));

            Assert.AreEqual(1, _chunk.VoxelsSet);

            _factory = ScriptableObject.CreateInstance<PerlinTerrainDataFactory>();
            chunk.SetVoxels(ChunkCoordinate, _world.Engine.ConfigBlob, _factory);

            Assert.AreEqual(2, _chunk.VoxelsSet);
            Assert.IsTrue(chunk.VoxelsReady);
        }
    }
}
