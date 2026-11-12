// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    public interface IGeoEditStoreSource {
        IGeoEditStore CreateFor(Vector3Int chunkCoordinate);

        void ClearStores() { }
    }
}