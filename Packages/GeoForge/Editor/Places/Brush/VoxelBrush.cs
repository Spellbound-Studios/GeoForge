// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Logging;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places {
    public sealed class VoxelBrush : IDisposable {
        private const int DefaultDensityDelta = 255;
        private const int DefaultCompletionTickLimit = 2000;
        private const int ApronVoxels = 3;

        private readonly VoxelBrushHistory _history;
        private List<VoxelBrushHistoryEntry> _shownEntries = new();
        private int _shownCursor;
        private bool _isDisposed;

        public VoxelBrush(IGeoVolume volume, VoxelRegion region) {
            Volume = volume ?? throw new ArgumentNullException(nameof(volume));
            Region = region;
            _history = VoxelBrushHistory.Create();
            UnityEditor.Undo.undoRedoPerformed += FollowUnityUndo;
        }

        public IGeoVolume Volume { get; }

        public VoxelRegion Region { get; }

        public int DensityDelta { get; set; } = DefaultDensityDelta;

        public int CompletionTickLimit { get; set; } = DefaultCompletionTickLimit;

        public bool CanUndo => _history.CanUndo;

        public bool CanRedo => _history.CanRedo;

        public IReadOnlyList<VoxelStroke> Strokes {
            get {
                var strokes = new List<VoxelStroke>(_history.Cursor);

                for (var index = 0; index < _history.Cursor; index++) {
                    strokes.Add(_history.Entries[index].Stroke);
                }

                return strokes;
            }
        }

        public bool Apply(in VoxelStroke stroke) {
            var touched = TouchedChunkOffsets(stroke);
            var before = Snapshot(touched);

            if (!Dispatch(stroke)) {
                return false;
            }

            Complete();
            _history.Record(new VoxelBrushHistoryEntry(stroke, before, Snapshot(touched)));
            RememberShownState();

            return true;
        }

        public bool Undo() {
            if (!_history.CanUndo) {
                return false;
            }

            _history.MoveCursor(_history.Cursor - 1);
            RestoreToCursor();

            return true;
        }

        public bool Redo() {
            if (!_history.CanRedo) {
                return false;
            }

            _history.MoveCursor(_history.Cursor + 1);
            RestoreToCursor();

            return true;
        }

        public bool TryFindSolid(Ray ray, float maximumDistance, out Vector3 point) {
            point = default;

            if (Volume.GeoVolumeEngine == null) {
                return false;
            }

            var step = Volume.ConfigBlob.Value.Resolution;
            var direction = ray.direction.normalized;

            for (var distance = 0f; distance <= maximumDistance; distance += step) {
                var sample = ray.origin + direction * distance;

                if (Volume.TryQuerySolid(sample, out var solid) && solid) {
                    point = sample;
                    return true;
                }
            }

            return false;
        }

        public void Dispose() {
            if (_isDisposed) {
                return;
            }

            _isDisposed = true;
            UnityEditor.Undo.undoRedoPerformed -= FollowUnityUndo;

            if (_history != null) {
                UnityEditor.Undo.ClearUndo(_history);
                Object.DestroyImmediate(_history);
            }
        }

        private bool Dispatch(in VoxelStroke stroke) {
            var centre = stroke.Centre;
            var size = stroke.Size;
            var material = stroke.Material;

            if (stroke.Shape == VoxelStrokeShape.Box) {
                var halfExtent = HalfExtentInVoxels(size);

                return stroke.Mode switch {
                    VoxelStrokeMode.Add =>
                        GeoForgeCommands.AddCube(Volume, centre, halfExtent, (short)DensityDelta, material),
                    VoxelStrokeMode.Remove =>
                        GeoForgeCommands.RemoveCube(Volume, centre, halfExtent, DensityDelta),
                    VoxelStrokeMode.Smooth =>
                        GeoForgeCommands.SmoothCube(Volume, centre, halfExtent),
                    VoxelStrokeMode.Paint =>
                        GeoForgeCommands.PaintCube(Volume, centre, halfExtent, material),
                    _ => false
                };
            }

            return stroke.Mode switch {
                VoxelStrokeMode.Add =>
                    GeoForgeCommands.AddSphere(Volume, centre, size, (short)DensityDelta, material),
                VoxelStrokeMode.Remove =>
                    GeoForgeCommands.RemoveSphere(Volume, centre, size, DensityDelta),
                VoxelStrokeMode.Smooth =>
                    GeoForgeCommands.SmoothSphere(Volume, centre, size),
                VoxelStrokeMode.Paint =>
                    GeoForgeCommands.PaintSphere(Volume, centre, size, material),
                _ => false
            };
        }

        private int HalfExtentInVoxels(float edgeInMetres) {
            if (Volume.GeoVolumeEngine == null) {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(edgeInMetres * 0.5f / Volume.ConfigBlob.Value.Resolution));
        }

        private List<Vector3Int> TouchedChunkOffsets(in VoxelStroke stroke) {
            var offsets = new List<Vector3Int>();

            if (Volume.GeoVolumeEngine == null) {
                return offsets;
            }

            var resolution = Volume.ConfigBlob.Value.Resolution;
            var reach = stroke.Shape == VoxelStrokeShape.Box
                ? HalfExtentInVoxels(stroke.Size)
                : Mathf.CeilToInt(stroke.Size * 0.5f / resolution);
            var span = Vector3Int.one * (reach + ApronVoxels);
            var centre = Volume.WorldToVoxelSpace(stroke.Centre);
            var first = Volume.GetCoordByVoxelPosition(centre - span);
            var last = Volume.GetCoordByVoxelPosition(centre + span);

            foreach (var coordinate in Region.Chunks()) {
                if (coordinate.x < first.x || coordinate.y < first.y || coordinate.z < first.z ||
                    coordinate.x > last.x || coordinate.y > last.y || coordinate.z > last.z) {
                    continue;
                }

                offsets.Add(Region.OffsetOf(coordinate));
            }

            return offsets;
        }

        private List<VoxelLayerChunk> Snapshot(IReadOnlyList<Vector3Int> offsets) {
            var snapshot = new List<VoxelLayerChunk>(offsets.Count);
            var runs = new NativeList<SparseVoxelData>(Allocator.Temp);

            try {
                foreach (var offset in offsets) {
                    if (!Volume.TryCopyRunList(Region.MinimumChunk + offset, runs, out _)) {
                        continue;
                    }

                    var managedRuns = new List<SparseVoxelData>(runs.Length);

                    for (var index = 0; index < runs.Length; index++) {
                        managedRuns.Add(runs[index]);
                    }

                    snapshot.Add(new VoxelLayerChunk(offset, managedRuns));
                }
            } finally {
                runs.Dispose();
            }

            return snapshot;
        }

        private void FollowUnityUndo() {
            if (_history == null) {
                return;
            }

            if (_history.Cursor == _shownCursor && _history.Entries.Count == _shownEntries.Count) {
                return;
            }

            RestoreToCursor();
        }

        private void RestoreToCursor() {
            var entries = _history.Entries;
            var cursor = _history.Cursor;
            var from = Math.Min(_shownCursor, cursor);
            var to = Math.Max(_shownCursor, cursor);
            var offsets = new HashSet<Vector3Int>();
            CollectOffsets(_shownEntries, from, to, offsets);
            CollectOffsets(entries, from, to, offsets);

            foreach (var offset in offsets) {
                var runs = RunsAtCursor(entries, cursor, offset) ?? RunsAtCursor(_shownEntries, cursor, offset);

                if (runs != null) {
                    RestoreChunk(Region.MinimumChunk + offset, runs);
                }
            }

            Complete();
            RememberShownState();
        }

        private static void CollectOffsets(IReadOnlyList<VoxelBrushHistoryEntry> entries, int from, int to,
            HashSet<Vector3Int> offsets) {
            for (var index = from; index < to && index < entries.Count; index++) {
                foreach (var chunk in entries[index].Before) {
                    offsets.Add(chunk.Offset);
                }
            }
        }

        private static IReadOnlyList<SparseVoxelData> RunsAtCursor(IReadOnlyList<VoxelBrushHistoryEntry> entries,
            int cursor, Vector3Int offset) {
            for (var index = cursor; index < entries.Count; index++) {
                var runs = RunsOf(entries[index].Before, offset);

                if (runs != null) {
                    return runs;
                }
            }

            for (var index = Math.Min(cursor, entries.Count) - 1; index >= 0; index--) {
                var runs = RunsOf(entries[index].After, offset);

                if (runs != null) {
                    return runs;
                }
            }

            return null;
        }

        private static IReadOnlyList<SparseVoxelData> RunsOf(IReadOnlyList<VoxelLayerChunk> chunks,
            Vector3Int offset) {
            foreach (var chunk in chunks) {
                if (chunk.Offset == offset) {
                    return chunk.Runs;
                }
            }

            return null;
        }

        private void RestoreChunk(Vector3Int coordinate, IReadOnlyList<SparseVoxelData> runs) {
            var restoredChunk = Volume.GetChunkByCoord(coordinate);

            if (restoredChunk == null) {
                return;
            }

            var restoredVoxels =
                new NativeArray<VoxelData>(Volume.ConfigBlob.Value.ChunkDataVolumeSize, Allocator.TempJob);
            var handedToChunk = false;

            try {
                VoxelRunDecoder.Decode(runs, restoredVoxels);
                restoredChunk.GeoChunkEngine.IGeoEditStore?.Clear();
                handedToChunk = true;
                restoredChunk.GeoChunkEngine.SetVoxels(restoredVoxels);
            } finally {
                if (!handedToChunk) {
                    restoredVoxels.Dispose();
                }
            }
        }

        private void Complete() {
            var manager = Volume.GeoVolumeEngine?.Manager;

            if (manager == null) {
                return;
            }

            for (var tick = 0; tick < CompletionTickLimit; tick++) {
                manager.Tick();

                if (!manager.HasPendingWork) {
                    return;
                }
            }

            Log.Emit(LogLevel.Warning,
                "Voxel brush: the volume still has pending work after " + CompletionTickLimit + " ticks.", manager);
        }

        private void RememberShownState() {
            _shownEntries = new List<VoxelBrushHistoryEntry>(_history.Entries);
            _shownCursor = _history.Cursor;
        }
    }
}
