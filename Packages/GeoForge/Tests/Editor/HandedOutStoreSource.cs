// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class HandedOutStoreSource : IGeoEditStoreSource {
        internal Dictionary<Vector3Int, GeoEditStore> Made { get; } = new();

        public IGeoEditStore CreateFor(Vector3Int chunkCoordinate) {
            var store = new GeoEditStore();
            Made.Add(chunkCoordinate, store);

            return store;
        }
    }
}