// Copyright (c) Spellbound Studios. All rights reserved.

namespace Spellbound.GeoForge.Tests {
    public sealed class RecordingLeafBehaviour : LeafBehaviour {
        public int MeshUpdates { get; private set; }

        public int LastLevelOfDetail { get; private set; } = -1;

        public override void OnMeshUpdated(int levelOfDetail) {
            MeshUpdates++;
            LastLevelOfDetail = levelOfDetail;
        }
    }
}
