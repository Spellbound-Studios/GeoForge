// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class CountingStoreSource : IGeoEditStoreSource {
        internal List<CountingStore> Made { get; } = new();

        public IGeoEditStore CreateFor(Vector3Int chunkCoordinate) {
            var store = new CountingStore(chunkCoordinate);
            Made.Add(store);

            return store;
        }
    }
}