// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Spellbound.GeoForge.Places {
    public static class VoxelRunDecoder {
        public static void Decode(IReadOnlyList<SparseVoxelData> runs, NativeArray<VoxelData> voxels) {
            if (runs == null) {
                throw new ArgumentNullException(nameof(runs));
            }

            if (!voxels.IsCreated) {
                throw new ArgumentException("Runs decode into a created voxel array.", nameof(voxels));
            }

            for (var runIndex = 0; runIndex < runs.Count; runIndex++) {
                var start = runs[runIndex].StartIndex;

                if (start < 0 || start >= voxels.Length) {
                    throw new ArgumentOutOfRangeException(nameof(runs), start,
                        "Run " + runIndex + " starts outside the " + voxels.Length + " voxels it decodes into.");
                }

                var end = runIndex + 1 < runs.Count
                    ? Math.Min(runs[runIndex + 1].StartIndex, voxels.Length)
                    : voxels.Length;
                var voxel = runs[runIndex].Voxel;

                for (var index = start; index < end; index++) {
                    voxels[index] = voxel;
                }
            }
        }

        public static NativeArray<VoxelData> Decode(IReadOnlyList<SparseVoxelData> runs, int dataVolume,
            Allocator allocator) {
            if (runs == null) {
                throw new ArgumentNullException(nameof(runs));
            }

            if (dataVolume < 1) {
                throw new ArgumentOutOfRangeException(nameof(dataVolume), dataVolume,
                    "A chunk holds at least one voxel.");
            }

            var voxels = new NativeArray<VoxelData>(dataVolume, allocator);

            try {
                Decode(runs, voxels);
            } catch {
                voxels.Dispose();
                throw;
            }

            return voxels;
        }
    }
}
