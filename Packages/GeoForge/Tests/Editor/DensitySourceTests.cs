// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class DensitySourceTests {
        private const int ChunkSize = PlaneWorld.ChunkSize;
        private const float SeaFloorHeight = 3f;
        private const float CavityRadius = 3f;
        private const float Gradient = 16f;
        private const byte Material = 1;
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static readonly Vector3 CavityCentre = new(0f, -4f, 0f);

        private readonly List<GeoChunkDouble> _chunks = new();
        private readonly List<Object> _created = new();

        private PlaneWorld _world;

        [SetUp]
        public void SetUp() {
            _world = new PlaneWorld(0f);
        }

        [TearDown]
        public void TearDown() {
            foreach (var chunk in _chunks)
                chunk.GeoChunkEngine.Dispose();

            _world.Dispose();

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _chunks.Clear();
            _created.Clear();
        }

        [Test]
        public void DensitySource_CustomImplementation() {
            var cave = new UnderwaterCaveDensitySource(SeaFloorHeight, CavityCentre, CavityRadius, Gradient, Material);
            var coordinate = Vector3Int.zero;
            var dataWidth = ChunkSize + 3;
            var owningVolumeOffset = Vector3Int.one * -(1 + ChunkSize / 2);
            var firstDataVoxel = coordinate * ChunkSize + owningVolumeOffset;
            var request = new ChunkFillRequest(coordinate, firstDataVoxel, 1f, ChunkSize, dataWidth, 0u);
            var chunk = CreateChunk(coordinate);

            Assert.AreEqual(dataWidth * dataWidth * dataWidth, request.DataVolume);

            ((IGeoChunk)chunk).SetVoxels(request, cave);

            Assert.IsTrue(chunk.GeoChunkEngine.VoxelsReady);
            AssertSolid(new Vector3(5f, -6f, 5f), true);
            AssertSolid(CavityCentre, false);
            AssertSolid(new Vector3(0f, 0f, 0f), true);
            AssertSolid(new Vector3(0f, 6f, 0f), false);
        }

        [Test]
        public void AFillRequestStartsAtTheChunksFirstDataVoxel() {
            var request = _world.Engine.CreateFillRequest(new Vector3Int(1, -1, 2));
            var owningVolumeOffset = Vector3Int.one * -(1 + ChunkSize / 2);

            Assert.AreEqual(new Vector3Int(1, -1, 2) * ChunkSize + owningVolumeOffset, request.OriginVoxel);
            Assert.AreEqual(ChunkSize, request.ChunkSize);
            Assert.AreEqual(ChunkSize + 3, request.DataWidth);
            Assert.AreEqual(_world.Engine.ConfigBlob.Value.ChunkDataVolumeSize, request.DataVolume);
            Assert.AreEqual(1f, request.VoxelSize);
        }

        [TestCase(typeof(PerlinTerrainDataFactory), 0u, 0, 0, 0, "9013fc210a68c913")]
        [TestCase(typeof(PerlinTerrainDataFactory), 7u, 0, -1, 0, "b82da9fbea063312")]
        [TestCase(typeof(PerlinTerrainDataFactory), 2026u, 1, 0, -1, "74ff04d55e352bc8")]
        [TestCase(typeof(SimpleShapesDataFactory), 0u, 0, 0, 0, "ebc0cfa32a3236f2")]
        [TestCase(typeof(SimpleShapesDataFactory), 7u, 0, -1, 0, "21e6c77e7b480a75")]
        [TestCase(typeof(SimpleShapesDataFactory), 2026u, 1, 0, -1, "f94ccdb70fde0982")]
        public void AGeneratorFillsTheSameBytesThroughTheSeam(
            System.Type factoryType, uint seed, int x, int y, int z, string recordedHash) {
            var coordinate = new Vector3Int(x, y, z);
            var factory = (GeoForgeDataGenerator)Track(ScriptableObject.CreateInstance(factoryType));
            var configBlob = _world.Engine.ConfigBlob;
            var source = GeoForgeKit.CreateDensitySource(factory, configBlob);
            var request = ChunkFillRequest.ForChunk(coordinate, configBlob.Value, seed);
            var chunk = CreateChunk(coordinate);

            ((IGeoChunk)chunk).SetVoxels(request, source);

            Assert.AreEqual(recordedHash, HashRuns(chunk.GeoChunkEngine.GetsSparseVoxelData()));

            if (seed != 0u)
                return;

            ((IGeoChunk)chunk).SetVoxels(coordinate, configBlob, factory);

            Assert.AreEqual(recordedHash, HashRuns(chunk.GeoChunkEngine.GetsSparseVoxelData()));
        }

        [Test]
        public void TheKitGeneratesOneDataArrayInTheCallersAllocator() {
            var cave = new UnderwaterCaveDensitySource(SeaFloorHeight, CavityCentre, CavityRadius, Gradient, Material);
            var request = _world.Engine.CreateFillRequest(Vector3Int.zero);

            using var voxels = GeoForgeKit.Generate(cave, request, Allocator.Temp);

            Assert.AreEqual(request.DataVolume, voxels.Length);
            var centreIndex = ChunkLayout.Index(9, 5, 9, request.DataWidth);
            Assert.IsFalse(VoxelEncoding.IsSolid(voxels[centreIndex].Density));
        }

        [Test]
        public void AVolumeFillsThroughTheSourceItIsGiven() {
            var cave = new UnderwaterCaveDensitySource(SeaFloorHeight, CavityCentre, CavityRadius, Gradient, Material);
            IGeoVolume volume = _world.Volume;
            var generator = Track(ScriptableObject.CreateInstance<PerlinTerrainDataFactory>());

            volume.UseDensitySource(null);
            var wrappedGenerator = _world.Engine.DensitySourceFor(generator);

            Assert.IsNull(volume.DensitySource);
            Assert.IsNotNull(wrappedGenerator);
            Assert.AreSame(wrappedGenerator, _world.Engine.DensitySourceFor(generator));

            volume.UseDensitySource(cave);

            Assert.AreSame(cave, volume.DensitySource);
            Assert.AreSame(cave, _world.Engine.DensitySourceFor(generator));
        }

        private void AssertSolid(Vector3 worldPosition, bool expected) {
            Assert.IsTrue(_world.Engine.TryQueryVoxel(worldPosition, out var voxel), $"no voxel at {worldPosition}");
            Assert.AreEqual(expected, VoxelEncoding.IsSolid(voxel.Density), $"solid at {worldPosition}");
        }

        private GeoChunkDouble CreateChunk(Vector3Int coordinate) {
            var chunkTransform = new GameObject($"Density Source Chunk {coordinate}").transform;
            chunkTransform.SetParent(_world.Volume.transform, false);
            var chunk = new GeoChunkDouble(chunkTransform, coordinate);
            _chunks.Add(chunk);
            _world.Engine.RegisterChunk(coordinate, chunk);

            return chunk;
        }

        private static string HashRuns(NativeList<SparseVoxelData> runs) {
            var volume = (ChunkSize + 3) * (ChunkSize + 3) * (ChunkSize + 3);
            var hash = FnvOffsetBasis;

            for (var run = 0; run < runs.Length; run++) {
                var end = run + 1 < runs.Length ? runs[run + 1].StartIndex : volume;

                for (var index = runs[run].StartIndex; index < end; index++) {
                    hash = (hash ^ (byte)runs[run].Voxel.Density) * FnvPrime;
                    hash = (hash ^ runs[run].Voxel.MaterialIndex) * FnvPrime;
                }
            }

            return hash.ToString("x16");
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
