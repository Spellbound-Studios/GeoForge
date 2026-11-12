// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;

namespace Spellbound.GeoForge.Networking {
    internal static class VoxelKeyOrder {
        internal static void Sort(List<ulong> keys) {
            var count = keys.Count;

            for (var root = count / 2 - 1; root >= 0; root--)
                SiftDown(keys, root, count);

            for (var end = count - 1; end > 0; end--) {
                (keys[0], keys[end]) = (keys[end], keys[0]);
                SiftDown(keys, 0, end);
            }
        }

        private static void SiftDown(List<ulong> keys, int root, int count) {
            while (true) {
                var largest = root;
                var left = 2 * root + 1;
                var right = left + 1;

                if (left < count && keys[left] > keys[largest])
                    largest = left;

                if (right < count && keys[right] > keys[largest])
                    largest = right;

                if (largest == root)
                    return;

                (keys[root], keys[largest]) = (keys[largest], keys[root]);
                root = largest;
            }
        }
    }
}