// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.Intents;
using Spellbound.Core.Logging;
using Spellbound.Core.Packing;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    public sealed class VoxelEditSource : ICommandSource {
        private const int BitsPerByte = 8;

        private readonly VoxelEditStores _stores;
        private readonly HashSet<ulong> _dirtyKeys = new();
        private readonly List<ulong> _dirtyOrder = new();
        private readonly List<ulong> _pagingOrder = new();
        private readonly List<(int, VoxelData)> _received = new();
        private readonly List<int> _removed = new();

        private IDirtyNotifier _notifier;
        private Vector3Int _checkedChunk;
        private bool _checkedChunkInRegion;
        private bool _hasCheckedChunk;
        private int _written;
        private bool _reportedMalformed;

        internal VoxelEditSource(VoxelEditStores stores, in RegionKey key, uint sourceId, ulong baseFingerprint) {
            _stores = stores;
            Key = key;
            SourceId = sourceId;
            BaseFingerprint = baseFingerprint;
        }

        public RegionKey Key { get; }

        public uint SourceId { get; }

        public ulong BaseFingerprint { get; }

        public bool IsReady => _stores.IsFilled(Key);

        public int PendingChanges => _dirtyKeys.Count;

        public PendingEchoMask PendingEchoes { get; set; }

        public void Attach(IDirtyNotifier notifier) {
            _notifier = notifier;

            if (_dirtyKeys.Count > 0)
                _notifier?.MarkDirty();
        }

        public int WriteChanges(ref BitWriter writer) {
            VoxelKeyOrder.Sort(_dirtyOrder);
            var previous = 0UL;
            _written = 0;

            while (_written < _dirtyOrder.Count) {
                var key = _dirtyOrder[_written];
                var present = _stores.TryReadEdit(key, out var voxel);
                var probe = writer;

                if (!TryWriteRecord(ref probe, key - previous, present, voxel))
                    break;

                writer = probe;
                previous = key;
                _written++;
                PendingEchoes?.Mark(key, PendingEchoes.UploadBeingWritten);
            }

            return _written;
        }

        public void ClearChanges() {
            for (var index = 0; index < _written; index++)
                _dirtyKeys.Remove(_dirtyOrder[index]);

            _dirtyOrder.RemoveRange(0, _written);
            _written = 0;

            if (_dirtyOrder.Count > 0)
                _notifier?.MarkDirty();
        }

        public void ApplyChanges(ref BitReader reader, int recordCount) {
            var previous = 0UL;
            var chunk = default(Vector3Int);
            BeginReceiving();

            for (var record = 0; record < recordCount; record++) {
                if (!TryReadRecord(ref reader, out var delta, out var present, out var voxel)) {
                    ReportMalformed(record, recordCount);

                    break;
                }

                var key = previous + delta;
                previous = key;

                if (PendingEchoes != null && PendingEchoes.IsPending(key))
                    continue;

                if (PendingEchoes != null && _dirtyKeys.Contains(key))
                    continue;

                Receive(key, present, voxel, ref chunk);
            }

            EndReceiving(chunk);
        }

        public bool WriteSnapshotPage(ref SnapshotCursor cursor, ref BitWriter writer) {
            var position = (int)cursor.Position;

            if (position == 0) {
                _pagingOrder.Clear();
                _stores.CollectKeys(Key, _pagingOrder);
                VoxelKeyOrder.Sort(_pagingOrder);
            }

            var previous = 0UL;

            while (position < _pagingOrder.Count) {
                var key = _pagingOrder[position];

                if (!_stores.TryReadEdit(key, out var voxel)) {
                    position++;

                    continue;
                }

                var probe = writer;

                if (!TryWriteRecord(ref probe, key - previous, true, voxel))
                    break;

                writer = probe;
                previous = key;
                position++;
            }

            var done = position == _pagingOrder.Count;
            cursor = new SnapshotCursor((ulong)position, done);

            return done;
        }

        public void ApplySnapshotPage(ref BitReader reader) {
            var previous = 0UL;
            var chunk = default(Vector3Int);
            var record = 0;
            BeginReceiving();

            while (reader.BitsRemaining >= BitsPerByte) {
                if (!TryReadRecord(ref reader, out var delta, out var present, out var voxel)) {
                    ReportMalformed(record, record + 1);

                    break;
                }

                var key = previous + delta;
                previous = key;
                record++;
                Receive(key, present, voxel, ref chunk);
            }

            EndReceiving(chunk);
        }

        public void ResetToEmpty() {
            _dirtyKeys.Clear();
            _dirtyOrder.Clear();
            _written = 0;
            _stores.ResetRegion(Key);
        }

        public bool TryApplyCommand(ParticipantId sender, in IntentKey key, ref BitReader command,
            out IntentReceipt receipt) {
            if (!VoxelEditCommand.TryRead(ref command, out var edit) || command.BitsRemaining >= BitsPerByte) {
                receipt = new IntentReceipt(VoxelEditObservations.CommandMalformed, 0u);

                return false;
            }

            var engine = _stores.Volume.GeoVolumeEngine;

            if (engine == null) {
                receipt = new IntentReceipt(VoxelEditObservations.CommandRefused, 0u);

                return false;
            }

            if (edit.ExtentInVoxels(engine.VoxelSize) > _stores.LargestEditExtent) {
                VoxelEditObservations.CommandsTooLarge.Add(_stores.Observations);
                receipt = new IntentReceipt(VoxelEditObservations.CommandTooLarge, 0u);

                return false;
            }

            if (!edit.TryApply(_stores.Volume)) {
                receipt = new IntentReceipt(VoxelEditObservations.CommandRefused, 0u);

                return false;
            }

            receipt = new IntentReceipt(VoxelEditObservations.CommandApplied, 1u);

            return true;
        }

        internal void MarkChanged(Vector3Int chunk, List<(int, VoxelData)> changes) {
            var marked = false;

            for (var index = 0; index < changes.Count; index++) {
                if (!VoxelRecordKey.TryPack(chunk, changes[index].Item1, out var key)) {
                    _stores.ReportOutOfRange(chunk, changes[index].Item1);

                    continue;
                }

                if (!_dirtyKeys.Add(key))
                    continue;

                _dirtyOrder.Add(key);
                marked = true;
            }

            if (marked)
                _notifier?.MarkDirty();
        }

        private void BeginReceiving() {
            _received.Clear();
            _removed.Clear();
            _hasCheckedChunk = false;
        }

        private void EndReceiving(Vector3Int chunk) {
            Flush(chunk);
            _stores.RefillRemoved();
        }

        private void Receive(ulong key, bool present, VoxelData voxel, ref Vector3Int chunk) {
            var keyChunk = VoxelRecordKey.ChunkOf(key);

            if (!IsInRegion(keyChunk)) {
                VoxelEditObservations.RecordsOutsideRegion.Add(_stores.Observations);

                return;
            }

            if ((_received.Count > 0 || _removed.Count > 0) && keyChunk != chunk)
                Flush(chunk);

            chunk = keyChunk;

            if (present)
                _received.Add((VoxelRecordKey.IndexOf(key), voxel));
            else
                _removed.Add(VoxelRecordKey.IndexOf(key));
        }

        private bool IsInRegion(Vector3Int chunk) {
            if (_hasCheckedChunk && chunk == _checkedChunk)
                return _checkedChunkInRegion;

            _checkedChunk = chunk;
            _checkedChunkInRegion = _stores.ChunkRegions.RegionOf(chunk) == Key;
            _hasCheckedChunk = true;

            return _checkedChunkInRegion;
        }

        private void Flush(Vector3Int chunk) {
            if (_received.Count > 0)
                _stores.Write(chunk, _received);

            if (_removed.Count > 0)
                _stores.Remove(chunk, _removed);

            _received.Clear();
            _removed.Clear();
        }

        private void ReportMalformed(int record, int recordCount) {
            VoxelEditObservations.RecordsMalformed.Add(_stores.Observations);

            if (_reportedMalformed)
                return;

            _reportedMalformed = true;
            Log.Error($"Voxel edit records of region {Key} stopped reading at record {record} of {recordCount}; " +
                      "the records from there on are dropped. This is logged once.");
        }

        private static bool TryWriteRecord(ref BitWriter writer, ulong delta, bool present, VoxelData voxel) {
            if (!writer.TryWriteVarULong(delta) || !writer.TryWriteBool(present))
                return false;

            return !present || (writer.TryWriteBits((byte)voxel.Density, BitsPerByte) &&
                                writer.TryWriteBits(voxel.MaterialIndex, BitsPerByte));
        }

        private static bool TryReadRecord(ref BitReader reader, out ulong delta, out bool present,
            out VoxelData voxel) {
            voxel = default;
            present = false;

            if (!reader.TryReadVarULong(out delta) || !reader.TryReadBool(out present))
                return false;

            if (!present)
                return true;

            if (!reader.TryReadBits(BitsPerByte, out var density) || !reader.TryReadBits(BitsPerByte, out var material))
                return false;

            var encoded = (byte)material;
            voxel = VoxelEncoding.IsMature(encoded)
                ? VoxelData.CreateMature((sbyte)(byte)density, encoded)
                : VoxelData.CreateImmature((sbyte)(byte)density, encoded);

            return true;
        }
    }
}