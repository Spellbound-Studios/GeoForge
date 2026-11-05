// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RecordingRenderingProfile : MaterialRenderingProfile {
        internal int Marches { get; private set; }

        internal int TransitionMarches { get; private set; }

        public override JobHandle ScheduleMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeReference<Bounds> computedBounds,
            int lod,
            int3 start,
            JobHandle dependency = default) {
            Marches++;

            var normal = new float3(0f, 0f, -1f);
            var white = new Color32(255, 255, 255, 255);
            vertices.Add(new MeshingVertexData(new float3(0f, 0f, 0f), normal, white, white));
            vertices.Add(new MeshingVertexData(new float3(0f, 1f, 0f), normal, white, white));
            vertices.Add(new MeshingVertexData(new float3(1f, 0f, 0f), normal, white, white));
            triangles.Add(0);
            triangles.Add(1);
            triangles.Add(2);
            computedBounds.Value = new Bounds(new Vector3(0.5f, 0.5f, 0f), Vector3.one);

            return dependency;
        }

        public override JobHandle ScheduleTransitionMarchingCubes(
            BlobAssetReference<McTablesBlobAsset> tablesBlob,
            BlobAssetReference<VolumeConfigBlobAsset> configBlob,
            NativeArray<VoxelData> voxelArray,
            NativeList<MeshingVertexData> transitionMeshingVertexData,
            NativeList<int> transitionTriangles,
            NativeArray<int2> transitionRanges,
            int lod,
            int3 start,
            JobHandle dependency = default) {
            TransitionMarches++;

            return dependency;
        }
    }
}
