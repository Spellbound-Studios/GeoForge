// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class SeamGeoChunk : IGeoChunk {
        internal SeamGeoChunk(Transform transform, Vector3Int coordinate) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, new GeoEditStore(), coordinate);
            GeoChunkEngine.IGeoEditStore.geoChunkEngine = GeoChunkEngine;
        }

        public GeoChunkEngine GeoChunkEngine { get; }

        public void InitializeGeoChunk(Vector3Int coordinate, IGeoEditStore geoEditStore) { }

        public void HandleMeshReady() { }
    }
}
