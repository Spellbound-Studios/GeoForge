// Copyright (c) Spellbound Studios. All rights reserved.

namespace Spellbound.GeoForge {
    public readonly struct SolidRun {
        public SolidRun(int startIndex, bool solid) {
            StartIndex = startIndex;
            Solid = solid;
        }

        public int StartIndex { get; }

        public bool Solid { get; }
    }
}
