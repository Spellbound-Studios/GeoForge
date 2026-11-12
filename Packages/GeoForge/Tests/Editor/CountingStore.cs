// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class CountingStore : IGeoEditStore {
        private readonly GeoEditStore _store = new();

        internal CountingStore(Vector3Int coordinate) {
            Coordinate = coordinate;
        }

        internal Vector3Int Coordinate { get; }

        internal int Operations { get; private set; }

        public GeoChunkEngine geoChunkEngine {
            get => _store.geoChunkEngine;
            set => _store.geoChunkEngine = value;
        }

        public event Action<List<(int, VoxelData)>> OnGeoEditChanged {
            add => _store.OnGeoEditChanged += value;
            remove => _store.OnGeoEditChanged -= value;
        }

        public bool TryRead(int idx, out VoxelData voxelData) {
            return _store.TryRead(idx, out voxelData);
        }

        public void Write(List<(int, VoxelData)> voxelDatas) {
            _store.Write(voxelDatas);
        }

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            Operations++;
            _store.PassVoxelEditOperation(operation);
        }

        public IEnumerable<(int, VoxelData)> ReadAllEdits() {
            return _store.ReadAllEdits();
        }

        public void Clear() {
            _store.Clear();
        }
    }
}