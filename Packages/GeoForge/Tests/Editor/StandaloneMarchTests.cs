// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spellbound.GeoForge.Tests {
    public class StandaloneMarchTests {
        private const int CubesPerMarch = 16;
        private const int LayeredChunkSize = 64;
        private const int LayeredLevels = 3;
        private const string OctreeLeafMeshHash = "1bd101fb3909f789";
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const float SurfaceTolerance = 1f;

        private readonly List<Object> _created = new();

        private VoxelVolumeConfig _config;
        private BlobAssetReference<VolumeConfigBlobAsset> _configBlob;

        [SetUp]
        public void SetUp() {
            _config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            _config.cubesPerMarch = CubesPerMarch;
            _config.levelsOfDetail = 1;
            _config.maxChunkSize = CubesPerMarch;
            _config.chunkSize = CubesPerMarch;
            _config.sizeInChunks = Vector3Int.one;
            _configBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(_config);
        }

        [TearDown]
        public void TearDown() {
            if (_configBlob.IsCreated)
                _configBlob.Dispose();

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void AnOctreeLeafMarchedFromPerlinTerrainKeepsItsMeshBytes() {
            var tables = MarchTables.Build(Allocator.Persistent);
            var manager = Track(new GameObject("GeoForge Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager)
                .GetProperty(nameof(GeoForgeManager.McTablesBlob), BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, tables);
            var presenter = new RecordingChunkPresenter();
            manager.Presenter = presenter;
            manager.CollisionPresenter = null;
            manager.jobAndRenderProfile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());

            var volumeObject = Track(new GameObject("Volume"));
            var volume = volumeObject.AddComponent<GeoVolumeDouble>();
            volume.ConfigBlob = _configBlob;
            volume.ViewDistanceLodRanges = new[] { new Vector2(0f, float.MaxValue) };
            volume.GeoVolumeEngine = new GeoVolumeEngine(volume, volume, _config, manager);
            var chunkTransform = new GameObject("Chunk").transform;
            chunkTransform.SetParent(volumeObject.transform, false);
            var chunk = new GeoChunkDouble(chunkTransform);

            var factory = Track(ScriptableObject.CreateInstance<PerlinTerrainDataFactory>());
            var voxels = factory.GenerateProceduralVoxels(Vector3Int.zero, _configBlob);
            var node = new OctreeNode(Vector3Int.zero, 0, chunk, volume);
            string hash;

            try {
                node.ValidateOctreeLods(new[] { new ChunkViewer(Vector3.zero, 0f, 0f) }, new DistanceChunkInterest(),
                    voxels);
                manager.CompleteAndApplyMarchingCubesJobs();

                Assert.AreEqual(1, presenter.Acquired.Count);
                var mesh = presenter.Acquired[0].Mesh;
                Assert.Greater(mesh.GetIndexCount(0), 0);
                hash = HashMesh(mesh);
            }
            finally {
                node.Dispose();
                voxels.Dispose();
                manager.RentVertexBuffer().Dispose();
                manager.RentTriangleBuffer().Dispose();
                manager.RentBoundsBuffer().Dispose();
                volume.GeoVolumeEngine.Dispose();
                tables.Dispose();
            }

            Assert.AreEqual(OctreeLeafMeshHash, hash);
        }

        [Test]
        public void AnUnderwaterCaveMarchesWithoutAManagerOntoItsOwnSurface() {
            var cave = new UnderwaterCaveDensitySource(3f, new Vector3(0f, -2f, 0f), 4f, 16f, 1);
            var profile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());
            ref var config = ref _configBlob.Value;
            var request = ChunkFillRequest.ForChunk(default, config, 0u);
            using var voxels = GeoForgeKit.Generate(cave, request, Allocator.Temp);

            using var output = GeoForgeKit.March(voxels, new MarchRequest(profile, _configBlob, 0), Allocator.Temp);

            Assert.GreaterOrEqual(output.Indices.Length, 3);
            Assert.AreEqual(0, output.Indices.Length % 3);

            var resolution = config.Resolution;

            foreach (var vertex in output.Vertices) {
                var position = (Vector3)vertex.Position / resolution;

                Assert.LessOrEqual(Mathf.Abs(cave.SignedDistance(position)), SurfaceTolerance, $"vertex {position}");
            }

            var mesh = Track(GeoForgeKit.BuildMesh(output));

            Assert.AreEqual(output.Vertices.Length, mesh.vertexCount);
            Assert.AreEqual((uint)output.Indices.Length, mesh.GetIndexCount(0));
            Assert.AreEqual(output.Bounds, mesh.bounds);

            GeoForgeKit.Bake(mesh);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AKitMarchCoversTheWholeChunkAtEveryLevel() {
            var profile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());
            var configBlob = LayeredConfigBlob();

            try {
                var request = ChunkFillRequest.ForChunk(default, configBlob.Value, 0u);
                var plane = new PlaneDensitySource(request.OriginVoxel.y + LayeredChunkSize / 2f);
                using var voxels = GeoForgeKit.Generate(plane, request, Allocator.Temp);

                using var finest = GeoForgeKit.March(voxels, new MarchRequest(profile, configBlob, 0), Allocator.Temp);
                using var middle = GeoForgeKit.March(voxels, new MarchRequest(profile, configBlob, 1), Allocator.Temp);
                using var coarsest =
                    GeoForgeKit.March(voxels, new MarchRequest(profile, configBlob, 2), Allocator.Temp);

                var chunkWidth = (LayeredChunkSize - 1) * configBlob.Value.Resolution;
                Assert.GreaterOrEqual(finest.Bounds.size.x, chunkWidth);
                Assert.GreaterOrEqual(finest.Bounds.size.z, chunkWidth);
                Assert.Greater(finest.Vertices.Length, middle.Vertices.Length);
                Assert.Greater(middle.Vertices.Length, coarsest.Vertices.Length);
                Assert.AreEqual(0, finest.Indices.Length % 3);

                foreach (var index in finest.Indices)
                    Assert.Less(index, finest.Vertices.Length);
            }
            finally {
                configBlob.Dispose();
            }
        }

        [Test]
        public void ACallersTablesSurviveTheMarch() {
            var profile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());
            var tables = MarchTables.Build(Allocator.Persistent);

            try {
                var request = ChunkFillRequest.ForChunk(default, _configBlob.Value, 0u);
                var plane = new PlaneDensitySource(request.OriginVoxel.y + CubesPerMarch / 2f);
                using var voxels = GeoForgeKit.Generate(plane, request, Allocator.Temp);
                var marchRequest = new MarchRequest(profile, _configBlob, 0, tables);

                using var first = GeoForgeKit.March(voxels, marchRequest, Allocator.Temp);
                using var second = GeoForgeKit.March(voxels, marchRequest, Allocator.Temp);

                Assert.Greater(first.Indices.Length, 0);
                Assert.AreEqual(first.Indices.Length, second.Indices.Length);
                Assert.Greater(tables.Value.RegularCornerOffset.Length, 0);
            }
            finally {
                tables.Dispose();
            }
        }

        private BlobAssetReference<VolumeConfigBlobAsset> LayeredConfigBlob() {
            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = CubesPerMarch;
            config.levelsOfDetail = LayeredLevels;
            config.maxChunkSize = LayeredChunkSize;
            config.chunkSize = LayeredChunkSize;
            config.sizeInChunks = Vector3Int.one;

            return VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);
        }

        private static string HashMesh(Mesh mesh) {
            using var meshData = Mesh.AcquireReadOnlyMeshData(mesh);
            var state = FnvOffsetBasis;
            state = Append(state, meshData[0].GetVertexData<byte>());
            state = Append(state, meshData[0].GetIndexData<int>().Reinterpret<byte>(sizeof(int)));

            return state.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static ulong Append(ulong state, NativeArray<byte> bytes) {
            for (var index = 0; index < bytes.Length; index++) {
                state ^= bytes[index];
                state *= FnvPrime;
            }

            return state;
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
