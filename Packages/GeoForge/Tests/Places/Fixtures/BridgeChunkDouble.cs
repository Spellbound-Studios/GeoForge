// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public sealed class BridgeChunkDouble : IGeoChunk {
        public BridgeChunkDouble(Transform transform, Vector3Int coordinate, IGeoEditStore geoEditStore) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, geoEditStore, coordinate);
            GeoChunkEngine.IGeoEditStore.geoChunkEngine = GeoChunkEngine;
        }

        public GeoChunkEngine GeoChunkEngine { get; }

        public void InitializeGeoChunk(Vector3Int coordinate, IGeoEditStore geoEditStore) {
        }

        public void HandleMeshReady() {
        }
    }
}
