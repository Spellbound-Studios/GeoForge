// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections;

namespace Spellbound.GeoForge.Tests {
    public sealed class DeferredGeoVolume : GeoVolume {
        public void CreateEngine(VoxelVolumeConfig config) {
            CreateGeoVolume(config);
        }

        protected override IEnumerator InitializeChunks() {
            yield break;
        }
    }
}
