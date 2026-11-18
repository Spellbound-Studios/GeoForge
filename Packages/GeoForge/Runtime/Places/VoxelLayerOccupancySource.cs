// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Ground;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    public sealed class VoxelLayerOccupancySource : IOccupancySource, IDisposable {
        private readonly VoxelLayer _layer;
        private readonly Vector3 _worldOrigin;
        private readonly float _voxelSize;
        private readonly int _chunkSize;
        private readonly int _dataWidth;
        private readonly int _dataVolume;
        private readonly Dictionary<Vector3Int, int> _slotByOffset = new();
        private readonly bool[] _decoded;
        private NativeArray<VoxelData> _voxels;
        private bool _disposed;

        public VoxelLayerOccupancySource(VoxelLayer layer) : this(layer, Vector3.zero, 1f) { }

        public VoxelLayerOccupancySource(VoxelLayer layer, Vector3 worldOrigin, float voxelSize) {
            if (layer == null) {
                throw new ArgumentNullException(nameof(layer));
            }

            if (voxelSize <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(voxelSize), voxelSize, "A voxel has a positive size.");
            }

            _layer = layer;
            _worldOrigin = worldOrigin;
            _voxelSize = voxelSize;
            _chunkSize = layer.ChunkSize;
            _dataWidth = layer.DataWidth;
            _dataVolume = layer.ChunkDataVolume;

            var chunks = layer.Chunks;

            for (var index = 0; index < chunks.Count; index++) {
                if (chunks[index] != null) {
                    _slotByOffset.TryAdd(chunks[index].Offset, index);
                }
            }

            _decoded = new bool[chunks.Count];
            _voxels = new NativeArray<VoxelData>(Math.Max(1, chunks.Count) * _dataVolume, Allocator.Persistent);
        }

        public Occupancy Classify(in Bounds bounds) {
            if (_disposed) {
                throw new ObjectDisposedException(nameof(VoxelLayerOccupancySource));
            }

            var minimum = (bounds.min - _worldOrigin) / _voxelSize;
            var maximum = (bounds.max - _worldOrigin) / _voxelSize;
            var centre = (bounds.center - _worldOrigin) / _voxelSize;
            var first = new Vector3Int(FirstCovered(minimum.x, maximum.x, centre.x),
                FirstCovered(minimum.y, maximum.y, centre.y), FirstCovered(minimum.z, maximum.z, centre.z));
            var last = new Vector3Int(LastCovered(minimum.x, maximum.x, centre.x),
                LastCovered(minimum.y, maximum.y, centre.y), LastCovered(minimum.z, maximum.z, centre.z));
            var anySolid = false;
            var anyOpen = false;

            for (var y = first.y; y <= last.y; y++) {
                for (var z = first.z; z <= last.z; z++) {
                    for (var x = first.x; x <= last.x; x++) {
                        if (IsSolid(new Vector3Int(x, y, z))) {
                            anySolid = true;
                        } else {
                            anyOpen = true;
                        }

                        if (anySolid && anyOpen) {
                            return Occupancy.Mixed;
                        }
                    }
                }
            }

            return anySolid ? Occupancy.Solid : Occupancy.Empty;
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;

            if (_voxels.IsCreated) {
                _voxels.Dispose();
            }
        }

        private static int FirstCovered(float minimum, float maximum, float centre) {
            var first = Mathf.CeilToInt(minimum);
            return first <= Mathf.FloorToInt(maximum) ? first : Mathf.RoundToInt(centre);
        }

        private static int LastCovered(float minimum, float maximum, float centre) {
            var last = Mathf.FloorToInt(maximum);
            return Mathf.CeilToInt(minimum) <= last ? last : Mathf.RoundToInt(centre);
        }

        private bool IsSolid(Vector3Int voxel) {
            var extent = _layer.ChunkExtent;
            var chunk = new Vector3Int(
                Mathf.Clamp(OwningChunk(voxel.x), 0, extent.x - 1),
                Mathf.Clamp(OwningChunk(voxel.y), 0, extent.y - 1),
                Mathf.Clamp(OwningChunk(voxel.z), 0, extent.z - 1));
            var local = voxel - chunk * _chunkSize;

            if (local.x < 0 || local.y < 0 || local.z < 0
                || local.x >= _dataWidth || local.y >= _dataWidth || local.z >= _dataWidth) {
                return false;
            }

            if (!_slotByOffset.TryGetValue(chunk, out var slot)) {
                return false;
            }

            DecodeOnce(slot);

            var index = slot * _dataVolume + ChunkLayout.Index(local.x, local.y, local.z, _dataWidth);
            return VoxelEncoding.IsSolid(_voxels[index].Density);
        }

        private int OwningChunk(int voxel) {
            return ChunkLayout.CoordinateOfVoxel(new Vector3Int(voxel, 0, 0), _chunkSize).x;
        }

        private void DecodeOnce(int slot) {
            if (_decoded[slot]) {
                return;
            }

            VoxelRunDecoder.Decode(_layer.Chunks[slot].Runs, _voxels.GetSubArray(slot * _dataVolume, _dataVolume));
            _decoded[slot] = true;
        }
    }
}
