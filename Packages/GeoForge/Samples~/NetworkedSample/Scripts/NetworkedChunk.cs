// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Sample4 {
    public class NetworkedChunk : MonoBehaviour, IGeoChunk {
        public GeoChunkEngine GeoChunkEngine { get; private set; }

        public void InitializeGeoChunk(Vector3Int coord, IGeoEditStore geoEditStore) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, geoEditStore, coord);
            GeoChunkEngine.IGeoEditStore.geoChunkEngine = GeoChunkEngine;
        }

        public void HandleMeshReady() { }

        protected virtual void OnDestroy() {
            GeoChunkEngine?.Dispose();
        }
    }
}
