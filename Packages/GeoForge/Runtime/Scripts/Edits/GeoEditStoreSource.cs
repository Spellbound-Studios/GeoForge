// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public sealed class GeoEditStoreSource : IGeoEditStoreSource {
        public IGeoEditStore CreateFor(Vector3Int chunkCoordinate) {
            return new GeoEditStore();
        }
    }
}