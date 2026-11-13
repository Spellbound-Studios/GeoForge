// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class DeltaResolvingEditStore : IGeoEditStore {
        private readonly Dictionary<int, VoxelData> _edits = new();

        internal List<sbyte> ReadDensities { get; } = new();

        public GeoChunkEngine geoChunkEngine { get; set; }

        public event Action<List<(int, VoxelData)>> OnGeoEditChanged;

        public bool TryRead(int idx, out VoxelData voxelData) {
            if (_edits.TryGetValue(idx, out voxelData))
                return true;

            voxelData = geoChunkEngine.GetVoxelData(idx);

            return false;
        }

        public void Write(List<(int, VoxelData)> voxelDatas) {
            foreach (var (index, voxel) in voxelDatas)
                _edits[index] = voxel;

            OnGeoEditChanged?.Invoke(voxelDatas);
        }

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            var changes = new List<(int, VoxelData)>(operation.Deltas.Length);

            foreach (var delta in operation.Deltas) {
                var resolved = Resolve(delta);
                _edits[delta.Index] = resolved;
                changes.Add((delta.Index, resolved));
            }

            if (changes.Count > 0)
                OnGeoEditChanged?.Invoke(changes);
        }

        internal void ResolveDelta(VoxelDensityDelta delta) {
            Write(new List<(int, VoxelData)> { (delta.Index, Resolve(delta)) });
        }

        private VoxelData Resolve(VoxelDensityDelta delta) {
            TryRead(delta.Index, out var current);
            ReadDensities.Add(current.Density);
            var density = (sbyte)Mathf.Clamp(current.Density + delta.DensityDelta, sbyte.MinValue, sbyte.MaxValue);
            var material = VoxelEncoding.IsSolid(density) ? current.GetPlainMatIndex() : VoxelData.NullSentinelValue;

            return VoxelData.CreateImmature(density, material);
        }

        public IEnumerable<(int, VoxelData)> ReadAllEdits() {
            foreach (var edit in _edits)
                yield return (edit.Key, edit.Value);
        }

        public void Clear() {
            _edits.Clear();
        }
    }
}
