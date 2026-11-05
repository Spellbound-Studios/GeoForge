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
            var corners = new FixedList32Bytes<VoxelData>();

            var caseCode = GfMarchHelper.GatherRegularCornersAndComputeCaseCode(voxels, ref tables, cellPosition, 0,
                1, chunkDataAreaSize, chunkDataWidthSize, ref corners);

            return FromCorners(new Vector3Int(cellPosition.x, cellPosition.y, cellPosition.z), caseCode, corners);
        }

        public static SurfaceCase FromCorners(Vector3Int cellOrigin, in NativeArray<VoxelData> corners) {
            var caseCode = 0;

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++)
                caseCode |= GfMarchHelper.CaseCodeBit(corners[corner], corner);

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

        internal static SurfaceCase FromCorners(
            Vector3Int cellOrigin, byte caseCode, FixedList32Bytes<VoxelData> corners) {
            return new SurfaceCase(
                cellOrigin,
                caseCode,
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
