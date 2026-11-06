// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Interface Contract for GeoForge Edits
    /// </summary>
    public interface IGeoEditStore {
        GeoChunkEngine geoChunkEngine { get; set; }
        event Action<List<(int, VoxelData)>> OnGeoEditChanged;

        bool TryRead(int idx, out VoxelData voxelData);

        void Write(List<(int, VoxelData)> voxelDatas);

        void PassVoxelEditOperation(VoxelEditOperation operation);

        IEnumerable<(int, VoxelData)> ReadAllEdits();

        bool HasEdits {
            get {
                using var edits = ReadAllEdits().GetEnumerator();

                return edits.MoveNext();
            }
        }

        void Clear();
    }
}