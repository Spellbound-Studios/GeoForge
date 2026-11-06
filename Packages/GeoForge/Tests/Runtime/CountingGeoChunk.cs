// Copyright (c) Spellbound Studios. All rights reserved.

namespace Spellbound.GeoForge.Tests {
    public sealed class CountingGeoChunk : GeoChunk {
        public int VoxelsSet { get; private set; }

        protected override void OnVoxelsSet() {
            VoxelsSet++;
        }
    }
}
