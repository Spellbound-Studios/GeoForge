// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class GeoChunkEngineSurfaceCaseTests {
        private const int ChunkSize = 16;

        private readonly List<Object> _created = new();

        private GeoChunkDouble _chunk;
        private BlobAssetReference<VolumeConfigBlobAsset> _configBlob;
        private BlobAssetReference<McTablesBlobAsset> _tables;
        private GeoVolumeDouble _volume;
        private NativeArray<VoxelData> _voxels;

        [SetUp]
        public void SetUp() {
            var manager = Track(new GameObject("GeoForge Manager")).AddComponent<GeoForgeManager>();

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = ChunkSize;
            config.levelsOfDetail = 1;
            config.maxChunkSize = ChunkSize;
            config.chunkSize = ChunkSize;
            config.sizeInChunks = Vector3Int.one;
            _configBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);
            _tables = MarchTables.Build(Allocator.Persistent);

            var volumeObject = Track(new GameObject("Volume"));
            _volume = volumeObject.AddComponent<GeoVolumeDouble>();
            _volume.ConfigBlob = _configBlob;
            _volume.GeoVolumeEngine = new GeoVolumeEngine(_volume, _volume, config, manager);
            var chunkTransform = new GameObject("Chunk").transform;
            chunkTransform.SetParent(volumeObject.transform, false);
            _chunk = new GeoChunkDouble(chunkTransform);

            _voxels = new NativeArray<VoxelData>(_configBlob.Value.ChunkDataVolumeSize, Allocator.Persistent);

            for (var index = 0; index < _voxels.Length; index++) {
                var density = index % 3 == 0 ? (sbyte)40 : (sbyte)-40;
                _voxels[index] = VoxelData.CreateImmature(density, (byte)(index % VoxelData.NullSentinelValue));
            }

            _chunk.GeoChunkEngine.SetVoxels(new NativeArray<VoxelData>(_voxels, Allocator.Persistent));
        }

        [TearDown]
        public void TearDown() {
            _chunk.GeoChunkEngine.RootNode?.Dispose();
            var sparseVoxels = _chunk.GeoChunkEngine.GetsSparseVoxelData();

            if (sparseVoxels.IsCreated)
                sparseVoxels.Dispose();

            if (_voxels.IsCreated)
                _voxels.Dispose();

            if (_tables.IsCreated)
                _tables.Dispose();

            _volume.GeoVolumeEngine.Dispose();

            if (_configBlob.IsCreated)
                _configBlob.Dispose();

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [TestCase(1, 1, 1)]
        [TestCase(2, 3, 4)]
        [TestCase(7, 0, 12)]
        public void TheChunkGathersTheEightCornersInTheMarchCornerOrder(int x, int y, int z) {
            var cellOrigin = new Vector3Int(x, y, z);
            var marchCorners = new NativeArray<VoxelData>(SurfaceCase.CornerCount, Allocator.Temp);
            var marchCaseCode = GfMarchHelper.GatherRegularCornersAndComputeCaseCode(_voxels, ref _tables.Value,
                new int3(x, y, z), 0, 1, _configBlob.Value.ChunkDataAreaSize, _configBlob.Value.ChunkDataWidthSize,
                ref marchCorners);

            var surfaceCase = _chunk.GeoChunkEngine.ComputeSurfaceCase(cellOrigin, ref _tables.Value);

            Assert.AreEqual(cellOrigin, surfaceCase.CellOrigin);
            Assert.AreEqual(marchCaseCode, surfaceCase.CaseCode);

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++)
                Assert.AreEqual(marchCorners[corner], surfaceCase.Corner(corner), $"Corner {corner}");

            marchCorners.Dispose();
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
