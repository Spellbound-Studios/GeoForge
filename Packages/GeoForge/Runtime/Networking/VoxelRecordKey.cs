// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    internal static class VoxelRecordKey {
        internal const int IndexBits = 22;
        internal const int AxisBits = 14;
        internal const int LargestIndex = (1 << IndexBits) - 1;
        internal const int LargestAxis = (1 << (AxisBits - 1)) - 1;
        internal const int SmallestAxis = -(1 << (AxisBits - 1));

        private const ulong AxisMask = (1UL << AxisBits) - 1;
        private const ulong IndexMask = (1UL << IndexBits) - 1;
        private const int ZShift = IndexBits;
        private const int YShift = IndexBits + AxisBits;
        private const int XShift = IndexBits + 2 * AxisBits;

        internal static bool TryPack(Vector3Int chunk, int index, out ulong key) {
            if (index < 0 || index > LargestIndex || !Fits(chunk.x) || !Fits(chunk.y) || !Fits(chunk.z)) {
                key = 0;

                return false;
            }

            key = (Biased(chunk.x) << XShift) | (Biased(chunk.y) << YShift) | (Biased(chunk.z) << ZShift) |
                  (ulong)index;

            return true;
        }

        internal static Vector3Int ChunkOf(ulong key) {
            return new Vector3Int(Unbiased(key >> XShift), Unbiased(key >> YShift), Unbiased(key >> ZShift));
        }

        internal static int IndexOf(ulong key) {
            return (int)(key & IndexMask);
        }

        private static bool Fits(int axis) {
            return axis >= SmallestAxis && axis <= LargestAxis;
        }

        private static ulong Biased(int axis) {
            return (ulong)(axis - SmallestAxis) & AxisMask;
        }

        private static int Unbiased(ulong bits) {
            return (int)(bits & AxisMask) + SmallestAxis;
        }
    }
}