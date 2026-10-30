// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class GeoChunkDouble : IGeoChunk {
        internal GeoChunkDouble(Transform transform) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, new GeoEditStore(), Vector3Int.zero);
        }

        internal List<(int LevelOfDetail, Vector3Int LocalPosition)> KnownEmptyAddresses { get; } = new();

        internal int NewLeafBroadcasts { get; private set; }

        public GeoChunkEngine GeoChunkEngine { get; }

        public void InitializeGeoChunk(Vector3Int coordinate, IGeoEditStore geoEditStore) { }

        public void HandleMeshReady() { }

        public bool IsKnownEmpty(int levelOfDetail, Vector3Int localPosition) {
            return KnownEmptyAddresses.Contains((levelOfDetail, localPosition));
        }

        public void MarkKnownEmpty(int levelOfDetail, Vector3Int localPosition) {
            KnownEmptyAddresses.Add((levelOfDetail, localPosition));
        }

        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int position, int index) {
            NewLeafBroadcasts++;
        }
    }
}
