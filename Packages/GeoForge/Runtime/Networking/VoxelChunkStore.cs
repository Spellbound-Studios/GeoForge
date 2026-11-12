// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    internal sealed class VoxelChunkStore : IGeoEditStore {
        private readonly GeoEditStore _store;
        private readonly Action<Vector3Int, List<(int, VoxelData)>> _route;
        private readonly List<(int, VoxelData)> _cleared = new();

        internal VoxelChunkStore(Vector3Int coordinate, Action<Vector3Int, List<(int, VoxelData)>> route) {
            Coordinate = coordinate;
            Data = new GeoForgeChunkData();
            _store = new GeoEditStore(Data);
            _route = route;
            _store.OnGeoEditChanged += Changed;
        }

        internal Vector3Int Coordinate { get; }

        internal GeoForgeChunkData Data { get; }

        public GeoChunkEngine geoChunkEngine {
            get => _store.geoChunkEngine;
            set => _store.geoChunkEngine = value;
        }

        public event Action<List<(int, VoxelData)>> OnGeoEditChanged {
            add => _store.OnGeoEditChanged += value;
            remove => _store.OnGeoEditChanged -= value;
        }

        public bool HasEdits => Data.EditCount > 0;

        public bool TryRead(int idx, out VoxelData voxelData) {
            return _store.TryRead(idx, out voxelData);
        }

        public void Write(List<(int, VoxelData)> voxelDatas) {
            _store.Write(voxelDatas);
        }

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            _store.PassVoxelEditOperation(operation);
        }

        public int CopyEdits(List<(int, VoxelData)> edits) {
            return _store.CopyEdits(edits);
        }

        public IEnumerable<(int, VoxelData)> ReadAllEdits() {
            return _store.ReadAllEdits();
        }

        public void Clear() {
            if (Data.EditCount == 0)
                return;

            _cleared.Clear();

            foreach (var edit in Data.Edits)
                _cleared.Add((edit.Key, edit.Value));

            Data.ClearEdits();
            _route(Coordinate, _cleared);
        }

        internal int Remove(List<int> indices) {
            var removed = 0;

            for (var index = 0; index < indices.Count; index++) {
                if (Data.Edits.Remove(indices[index]))
                    removed++;
            }

            return removed;
        }

        internal void Detach() {
            _store.OnGeoEditChanged -= Changed;
        }

        private void Changed(List<(int, VoxelData)> changes) {
            _route(Coordinate, changes);
        }
    }
}