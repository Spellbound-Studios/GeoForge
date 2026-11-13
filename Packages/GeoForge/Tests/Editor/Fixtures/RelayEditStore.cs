// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;

namespace Spellbound.GeoForge.Tests {
    internal sealed class RelayEditStore : IGeoEditStore {
        public GeoChunkEngine geoChunkEngine { get; set; }

        public event Action<List<(int, VoxelData)>> OnGeoEditChanged;

        public bool TryRead(int idx, out VoxelData voxelData) {
            voxelData = geoChunkEngine.GetVoxelData(idx);

            return false;
        }

        public void Write(List<(int, VoxelData)> voxelDatas) {
            OnGeoEditChanged?.Invoke(voxelDatas);
        }

        public void PassVoxelEditOperation(VoxelEditOperation operation) { }

        public IEnumerable<(int, VoxelData)> ReadAllEdits() {
            yield break;
        }

        public void Clear() { }
    }
}
