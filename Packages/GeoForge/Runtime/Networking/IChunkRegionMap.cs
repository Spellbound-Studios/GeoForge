// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    public interface IChunkRegionMap {
        RegionKey RegionOf(Vector3Int chunkCoordinate);

        int ChunksOf(in RegionKey key, List<Vector3Int> chunks);
    }
}