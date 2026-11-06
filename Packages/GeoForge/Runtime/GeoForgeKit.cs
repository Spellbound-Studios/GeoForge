// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Spellbound.GeoForge {
    public static class GeoForgeKit {
        public static IDensitySource CreateDensitySource(
            GeoForgeDataGenerator generator, BlobAssetReference<VolumeConfigBlobAsset> configBlob) {
            if (generator == null)
                throw new ArgumentNullException(nameof(generator));

            if (!configBlob.IsCreated)
                throw new ArgumentException("A density source needs a volume configuration.", nameof(configBlob));

            return new DataGeneratorDensitySource(generator, configBlob);
        }

        public static NativeArray<VoxelData> Generate(
            IDensitySource source, in ChunkFillRequest request, Allocator allocator) {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var voxels = new NativeArray<VoxelData>(request.DataVolume, allocator);

            try {
                source.ScheduleFill(request, voxels, default).Complete();
            }
            catch {
                voxels.Dispose();

                throw;
            }

            return voxels;
        }

        public static MarchOutput March(NativeArray<VoxelData> voxels, in MarchRequest request, Allocator allocator) {
            if (request.Profile == null)
                throw new ArgumentException("A march needs a rendering profile.", nameof(request));

            if (!request.ConfigBlob.IsCreated)
                throw new ArgumentException("A march needs a volume configuration.", nameof(request));

            ref var config = ref request.ConfigBlob.Value;

            if (voxels.Length != config.ChunkDataVolumeSize)
                throw new ArgumentException($"{voxels.Length} voxels is not one chunk of {config.ChunkDataVolumeSize}.",
                    nameof(voxels));

            if (request.LevelOfDetail < 0 || request.LevelOfDetail > config.LevelsOfDetail)
                throw new ArgumentOutOfRangeException(nameof(request),
                    $"Level {request.LevelOfDetail} is outside the configuration's levels 0 to " +
                    $"{config.LevelsOfDetail}.");

            var ownsTables = !request.Tables.IsCreated;
            var tables = ownsTables ? MarchTables.Build(Allocator.Persistent) : request.Tables;
            var blockSize = config.CubesMarchedPerOctreeLeaf << request.LevelOfDetail;
            var blocksPerSide = Math.Max(1, config.ChunkSize / blockSize);
            var blockCount = blocksPerSide * blocksPerSide * blocksPerSide;
            var jobVoxels = new NativeArray<VoxelData>(voxels, Allocator.TempJob);
            var vertices = new NativeList<MeshingVertexData>[blockCount];
            var triangles = new NativeList<int>[blockCount];
            var bounds = new NativeReference<Bounds>[blockCount];
            var handles = new NativeArray<JobHandle>(blockCount, Allocator.Temp);

            try {
                for (var block = 0; block < blockCount; block++) {
                    vertices[block] = new NativeList<MeshingVertexData>(Allocator.TempJob);
                    triangles[block] = new NativeList<int>(Allocator.TempJob);
                    bounds[block] = new NativeReference<Bounds>(Allocator.TempJob);
                    var start = new int3(block % blocksPerSide, block / blocksPerSide % blocksPerSide,
                        block / (blocksPerSide * blocksPerSide)) * blockSize;
                    handles[block] = request.Profile.ScheduleMarchingCubes(tables, request.ConfigBlob, jobVoxels,
                        vertices[block], triangles[block], bounds[block], request.LevelOfDetail, start);
                }

                JobHandle.CombineDependencies(handles).Complete();

                return MergeBlocks(vertices, triangles, bounds, allocator);
            }
            finally {
                for (var block = 0; block < blockCount; block++) {
                    if (bounds[block].IsCreated)
                        bounds[block].Dispose();

                    if (triangles[block].IsCreated)
                        triangles[block].Dispose();

                    if (vertices[block].IsCreated)
                        vertices[block].Dispose();
                }

                handles.Dispose();
                jobVoxels.Dispose();

                if (ownsTables)
                    tables.Dispose();
            }
        }

        private static MarchOutput MergeBlocks(
            NativeList<MeshingVertexData>[] vertices,
            NativeList<int>[] triangles,
            NativeReference<Bounds>[] bounds,
            Allocator allocator) {
            var vertexCount = 0;
            var indexCount = 0;

            for (var block = 0; block < triangles.Length; block++) {
                if (triangles[block].Length == 0)
                    continue;

                vertexCount += vertices[block].Length;
                indexCount += triangles[block].Length;
            }

            var mergedVertices = new NativeArray<MeshingVertexData>(vertexCount, allocator);
            var mergedIndices = new NativeArray<int>(indexCount, allocator);
            var mergedBounds = new Bounds();
            var vertexOffset = 0;
            var indexOffset = 0;

            for (var block = 0; block < triangles.Length; block++) {
                var blockTriangles = triangles[block];

                if (blockTriangles.Length == 0)
                    continue;

                NativeArray<MeshingVertexData>.Copy(vertices[block].AsArray(), 0, mergedVertices, vertexOffset,
                    vertices[block].Length);

                for (var index = 0; index < blockTriangles.Length; index++)
                    mergedIndices[indexOffset + index] = blockTriangles[index] + vertexOffset;

                if (indexOffset == 0)
                    mergedBounds = bounds[block].Value;
                else
                    mergedBounds.Encapsulate(bounds[block].Value);

                vertexOffset += vertices[block].Length;
                indexOffset += blockTriangles.Length;
            }

            return new MarchOutput(mergedVertices, mergedIndices, mergedBounds);
        }

        public static Mesh BuildMesh(in MarchOutput output) {
            var mesh = new Mesh();
            WriteMesh(mesh, output.Vertices, output.Indices, output.Bounds);

            return mesh;
        }

        public static void Bake(Mesh mesh) {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            Physics.BakeMesh(mesh.GetEntityId(), false);
        }

        internal static void WriteMesh(
            Mesh mesh, NativeArray<MeshingVertexData> vertices, NativeArray<int> indices, Bounds bounds) {
            mesh.SetVertexBufferParams(vertices.Length, MeshingVertexData.VertexBufferMemoryLayout);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, MeshUpdateFlags.DontValidateIndices);
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.DontValidateIndices);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length));
            mesh.bounds = bounds;
        }
    }
}
