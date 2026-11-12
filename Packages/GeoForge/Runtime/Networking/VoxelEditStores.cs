// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Logging;
using Spellbound.Core.Observations;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    public sealed class VoxelEditStores : IGeoEditStoreSource, IDisposable {
        private readonly Dictionary<Vector3Int, VoxelChunkStore> _chunks = new();
        private readonly Dictionary<RegionKey, VoxelEditSource> _sources = new();
        private readonly List<Vector3Int> _regionChunks = new();
        private readonly List<Vector3Int> _loadedChunks = new();
        private readonly List<VoxelChunkStore> _unloadedChunks = new();
        private readonly List<Vector3Int> _refills = new();
        private readonly List<(int, VoxelData)> _accepted = new();
        private readonly VoxelEditSourceSettings _settings;
        private readonly Action<Vector3Int, List<(int, VoxelData)>> _route;

        private int _applying;
        private int _dataVolume;
        private bool _disposed;
        private bool _reportedOutOfRange;
        private bool _reportedOutsideChunk;

        internal VoxelEditStores(IGeoVolume volume, in VoxelEditSourceSettings settings,
            ObservationContext observations) {
            Volume = volume;
            _settings = settings;
            ChunkRegions = settings.ChunkRegions ?? new GridChunkRegionMap(volume, settings.RegionCellSizeMetres);
            Observations = observations;
            _route = Route;
        }

        public IGeoVolume Volume { get; }

        public IChunkRegionMap ChunkRegions { get; }

        public ObservationContext Observations { get; }

        public int LargestEditExtent => _settings.LargestEditExtent;

        public int SourceCount => _sources.Count;

        public int StoreCount => _chunks.Count;

        public IGeoEditStore CreateFor(Vector3Int chunkCoordinate) {
            RequireLive();

            return StoreFor(chunkCoordinate);
        }

        public void ClearStores() {
            foreach (var store in _chunks.Values)
                store.Clear();
        }

        public VoxelEditSource SourceFor(in RegionKey key) {
            RequireLive();

            if (_sources.TryGetValue(key, out var source))
                return source;

            source = new VoxelEditSource(this, key, _settings.SourceId, _settings.BaseFingerprint);
            _sources.Add(key, source);

            return source;
        }

        public bool TryGetSource(in RegionKey key, out VoxelEditSource source) {
            return _sources.TryGetValue(key, out source);
        }

        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var store in _chunks.Values)
                store.Detach();

            if (Volume is GeoVolume volume)
                volume.ForgetEditStores(this);

            _chunks.Clear();
            _sources.Clear();
        }

        internal bool TryReadEdit(ulong key, out VoxelData voxel) {
            if (_chunks.TryGetValue(VoxelRecordKey.ChunkOf(key), out var store))
                return store.Data.TryReadEdit(VoxelRecordKey.IndexOf(key), out voxel);

            voxel = default;

            return false;
        }

        internal int CollectKeys(in RegionKey key, List<ulong> keys) {
            ChunkRegions.ChunksOf(key, _regionChunks);

            for (var index = 0; index < _regionChunks.Count; index++) {
                var chunk = _regionChunks[index];

                if (!_chunks.TryGetValue(chunk, out var store))
                    continue;

                foreach (var edit in store.Data.Edits) {
                    if (VoxelRecordKey.TryPack(chunk, edit.Key, out var recordKey))
                        keys.Add(recordKey);
                }
            }

            return keys.Count;
        }

        internal int Write(Vector3Int chunk, List<(int, VoxelData)> changes) {
            var store = StoreFor(chunk);
            var largestIndex = LargestIndex();
            _accepted.Clear();

            for (var index = 0; index < changes.Count; index++) {
                if (changes[index].Item1 <= largestIndex) {
                    _accepted.Add(changes[index]);

                    continue;
                }

                ReportOutsideChunk(chunk, changes[index].Item1, largestIndex);
            }

            if (_accepted.Count == 0)
                return 0;

            _applying++;

            try {
                store.Write(_accepted);
            }
            finally {
                _applying--;
            }

            VoxelEditObservations.RecordsApplied.Add(Observations, _accepted.Count);

            return _accepted.Count;
        }

        internal int Remove(Vector3Int chunk, List<int> indices) {
            if (!_chunks.TryGetValue(chunk, out var store))
                return 0;

            var removed = store.Remove(indices);

            if (removed > 0 && IsLoaded(chunk) && !_refills.Contains(chunk))
                _refills.Add(chunk);

            VoxelEditObservations.RecordsApplied.Add(Observations, indices.Count);

            return removed;
        }

        internal void RefillRemoved() {
            if (_refills.Count == 0)
                return;

            _applying++;

            try {
                Volume.GeoVolumeEngine?.RefillChunks(_refills);
            }
            finally {
                _applying--;
                _refills.Clear();
            }
        }

        internal void ResetRegion(in RegionKey key) {
            ChunkRegions.ChunksOf(key, _regionChunks);
            _loadedChunks.Clear();
            _unloadedChunks.Clear();

            for (var index = 0; index < _regionChunks.Count; index++) {
                var chunk = _regionChunks[index];

                if (!_chunks.TryGetValue(chunk, out var store))
                    continue;

                if (IsLoaded(chunk))
                    _loadedChunks.Add(chunk);
                else
                    _unloadedChunks.Add(store);
            }

            _applying++;

            try {
                if (_loadedChunks.Count > 0 && !Volume.GeoVolumeEngine.ResetChunksToProcedural(_loadedChunks))
                    return;

                for (var index = 0; index < _unloadedChunks.Count; index++)
                    _unloadedChunks[index].Clear();
            }
            finally {
                _applying--;
            }
        }

        internal bool IsFilled(in RegionKey key) {
            ChunkRegions.ChunksOf(key, _regionChunks);
            var found = false;

            for (var index = 0; index < _regionChunks.Count; index++) {
                var chunk = Volume.GetChunkByCoord(_regionChunks[index]);

                if (chunk == null)
                    continue;

                if (!chunk.VoxelsReady)
                    return false;

                found = true;
            }

            return found;
        }

        internal void ReportOutOfRange(Vector3Int chunk, int index) {
            VoxelEditObservations.KeyOutOfRange.Add(Observations);

            if (_reportedOutOfRange)
                return;

            _reportedOutOfRange = true;
            Log.Error($"Voxel {index} of chunk {chunk} lies outside what a voxel edit record carries (chunk " +
                      $"coordinates {VoxelRecordKey.SmallestAxis} to {VoxelRecordKey.LargestAxis} on each axis, " +
                      $"voxel indices up to {VoxelRecordKey.LargestIndex}), so its edit stays local and is not " +
                      "replicated. This is logged once.");
        }

        private VoxelChunkStore StoreFor(Vector3Int chunk) {
            if (_chunks.TryGetValue(chunk, out var store))
                return store;

            store = new VoxelChunkStore(chunk, _route);
            _chunks.Add(chunk, store);

            return store;
        }

        private void Route(Vector3Int chunk, List<(int, VoxelData)> changes) {
            if (_applying > 0 || _disposed)
                return;

            SourceFor(ChunkRegions.RegionOf(chunk)).MarkChanged(chunk, changes);
        }

        private bool IsLoaded(Vector3Int chunk) {
            var engine = Volume.GeoVolumeEngine;

            return engine != null && engine.ChunkDict.ContainsKey(chunk);
        }

        private void ReportOutsideChunk(Vector3Int chunk, int index, int largestIndex) {
            VoxelEditObservations.RecordsMalformed.Add(Observations);

            if (_reportedOutsideChunk)
                return;

            _reportedOutsideChunk = true;
            Log.Error($"Voxel edit records for chunk {chunk} name voxel {index}, past the chunk's last voxel " +
                      $"{largestIndex}; those records are dropped. This is logged once.");
        }

        private int LargestIndex() {
            if (_dataVolume > 0)
                return _dataVolume - 1;

            var engine = Volume.GeoVolumeEngine;

            if (engine == null)
                return VoxelRecordKey.LargestIndex;

            foreach (var chunk in engine.ChunkDict.Values) {
                var width = ChunkLayout.DataWidth(chunk.GeoChunkEngine.Bounds.size.x);
                _dataVolume = width * width * width;

                return _dataVolume - 1;
            }

            return VoxelRecordKey.LargestIndex;
        }

        private void RequireLive() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(VoxelEditStores));
        }
    }
}