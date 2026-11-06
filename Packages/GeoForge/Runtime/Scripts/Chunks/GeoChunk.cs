// Copyright (c) Spellbound Studios. All rights reserved.

using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Simple implementation of IGeoChunk for samples, and small projects.
    /// </summary>
    public class GeoChunk : MonoBehaviour, IGeoChunk {
        private void OnDestroy() {
            if (GeoChunkEngine == null)
                return;

            GeoChunkEngine.Dispose();
        }

        public GeoChunkEngine GeoChunkEngine { get; private set; }

        public void InitializeGeoChunk(Vector3Int coord, IGeoEditStore geoEditStore) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, geoEditStore, coord);
            GeoChunkEngine.IGeoEditStore.geoChunkEngine = GeoChunkEngine;
        }

        void IGeoChunk.SetVoxels(in ChunkFillRequest request, IDensitySource source) {
            GeoChunkEngine.SetVoxels(request, source);

            OnVoxelsSet();
        }

        void IGeoChunk.HandleMeshReady() {
            OnMeshReady();
        }

        protected virtual void OnMeshReady() { }

        protected virtual void OnVoxelsSet() { }
    }
}