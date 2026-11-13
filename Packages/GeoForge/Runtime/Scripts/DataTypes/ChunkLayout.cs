// Copyright (c) Spellbound Studios. All rights reserved.

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class ChunkLayout {
        private const int ApronVoxels = 3;
        private const int FirstOwnedVoxel = 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DataWidth(int chunkSize) {
            return chunkSize + ApronVoxels;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index(int x, int y, int z, int width) {
            return GfStaticHelper.Coord3DToIndex(x, y, z, width * width, width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 ChunkOrigin(int3 chunkCoordinate, in VolumeConfigBlobAsset config) {
            return GfStaticHelper.GetChunkOrigin(chunkCoordinate, config);
        }

        public static Vector3Int CoordinateOfVoxel(Vector3Int voxelPosition, int chunkSize) {
            return new Vector3Int(
                Mathf.FloorToInt((voxelPosition.x - (float)FirstOwnedVoxel) / chunkSize),
                Mathf.FloorToInt((voxelPosition.y - (float)FirstOwnedVoxel) / chunkSize),
                Mathf.FloorToInt((voxelPosition.z - (float)FirstOwnedVoxel) / chunkSize));
        }

        internal static Bounds FillSpaceBounds(in Bounds boundsVoxelSpace, in VolumeConfigBlobAsset config) {
            return FillSpaceBounds(new Bounds(boundsVoxelSpace.center + config.Offset, boundsVoxelSpace.size),
                config.Resolution);
        }

        public static Bounds FillSpaceBounds(in ChunkFillRequest request) {
            var span = request.DataWidth - 1;
            var centre = (Vector3)request.OriginVoxel + Vector3.one * (span * 0.5f);

            return FillSpaceBounds(new Bounds(centre, Vector3.one * span), request.VoxelSize);
        }

        private static Bounds FillSpaceBounds(in Bounds fillVoxelBounds, float voxelSize) {
            return new Bounds(fillVoxelBounds.center * voxelSize, fillVoxelBounds.size * voxelSize);
        }

        public static NativeArray<float4> CornerWeights(int chunkSize, Allocator allocator) {
            var width = DataWidth(chunkSize);
            var weights = new NativeArray<float4>(width * width, allocator);

            for (var index = 0; index < weights.Length; index++) {
                GfStaticHelper.IndexToInt2(index, width, out var x, out var z);
                weights[index] = ColumnCornerWeight(x, z, chunkSize);
            }

            return weights;
        }

        private static float4 ColumnCornerWeight(int x, int z, int chunkSize) {
            var innerMaximum = chunkSize + FirstOwnedVoxel;
            var innerSize = innerMaximum - FirstOwnedVoxel;
            var weights = float4.zero;

            if (x == FirstOwnedVoxel && z == FirstOwnedVoxel) {
                weights.x = 1f;
            }
            else if (x == FirstOwnedVoxel && z == innerMaximum) {
                weights.y = 1f;
            }
            else if (x == innerMaximum && z == FirstOwnedVoxel) {
                weights.z = 1f;
            }
            else if (x == innerMaximum && z == innerMaximum) {
                weights.w = 1f;
            }
            else if (x == FirstOwnedVoxel) {
                var along = Smooth(z, innerSize);
                weights.x = 1f - along;
                weights.y = along;
            }
            else if (x == innerMaximum) {
                var along = Smooth(z, innerSize);
                weights.z = 1f - along;
                weights.w = along;
            }
            else if (z == FirstOwnedVoxel) {
                var across = Smooth(x, innerSize);
                weights.x = 1f - across;
                weights.z = across;
            }
            else if (z == innerMaximum) {
                var across = Smooth(x, innerSize);
                weights.y = 1f - across;
                weights.w = across;
            }
            else {
                var across = Smooth(x, innerSize);
                var along = Smooth(z, innerSize);
                weights = new float4((1f - across) * (1f - along), (1f - across) * along, across * (1f - along),
                    across * along);
            }

            return weights / math.csum(weights);
        }

        private static float Smooth(int coordinate, int innerSize) {
            return math.smoothstep(0f, 1f, (float)(coordinate - FirstOwnedVoxel) / innerSize);
        }
    }
}
