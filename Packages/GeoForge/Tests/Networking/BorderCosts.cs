// Copyright (c) Spellbound Studios. All rights reserved.

namespace Spellbound.GeoForge.Networking.Tests {
    internal readonly struct BorderCosts {
        internal BorderCosts(BorderCost corner, BorderCost cornerInOneRegion, BorderCost interior) {
            Corner = corner;
            CornerInOneRegion = cornerInOneRegion;
            Interior = interior;
        }

        internal BorderCost Corner { get; }

        internal BorderCost CornerInOneRegion { get; }

        internal BorderCost Interior { get; }

        public override string ToString() {
            return "at the corner of eight chunks with a region a chunk: " + Corner +
                   "; at that corner with one region over the eight: " + CornerInOneRegion +
                   "; inside one chunk: " + Interior;
        }
    }
}