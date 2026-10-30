// Copyright (c) Spellbound Studios. All rights reserved.

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class SurfaceCases {
        public static SurfaceCase Compute(
            in NativeArray<VoxelData> voxels,
            int3 cellPosition,
            int chunkDataAreaSize,
            int chunkDataWidthSize,
            ref McTablesBlobAsset tables) {
            var corners = new NativeArray<VoxelData>(SurfaceCase.CornerCount, Allocator.Temp);

            GfMarchHelper.GatherRegularCornersAndComputeCaseCode(voxels, ref tables, cellPosition, 0, 1,
                chunkDataAreaSize, chunkDataWidthSize, ref corners);

            var surfaceCase = FromCorners(new Vector3Int(cellPosition.x, cellPosition.y, cellPosition.z), corners);
            corners.Dispose();

            return surfaceCase;
        }

        public static SurfaceCase FromCorners(Vector3Int cellOrigin, in NativeArray<VoxelData> corners) {
            var caseCode = 0;

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++) {
                if (corners[corner].Density >= 0)
                    caseCode |= 1 << corner;
            }

            return new SurfaceCase(
                cellOrigin,
                (byte)caseCode,
                corners[0],
                corners[1],
                corners[2],
                corners[3],
                corners[4],
                corners[5],
                corners[6],
                corners[7]);
        }
    }
}
