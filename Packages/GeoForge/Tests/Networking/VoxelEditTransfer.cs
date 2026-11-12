// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Packing;
using Spellbound.Networking.Replication;

namespace Spellbound.GeoForge.Networking.Tests {
    internal static class VoxelEditTransfer {
        internal const int SectionBytes = 65536;

        private const int LargestPages = 100000;

        internal static int CopyChanges(VoxelEditSource from, VoxelEditSource to) {
            var buffer = new byte[SectionBytes];
            var records = WriteChanges(from, buffer, out var bytes);
            from.ClearChanges();
            ApplyChanges(to, buffer, bytes, records);

            return bytes;
        }

        internal static int WriteChanges(VoxelEditSource from, byte[] buffer, out int bytes) {
            var writer = new BitWriter(buffer);
            var records = from.WriteChanges(ref writer);
            bytes = writer.BytesWritten;

            return records;
        }

        internal static void ApplyChanges(VoxelEditSource to, byte[] buffer, int bytes, int records) {
            var reader = new BitReader(new ReadOnlySpan<byte>(buffer, 0, bytes));
            to.ApplyChanges(ref reader, records);
        }

        internal static int CopySnapshot(VoxelEditSource from, VoxelEditSource to, int pageBytes) {
            to.ResetToEmpty();
            var buffer = new byte[pageBytes];
            var cursor = new SnapshotCursor(0UL, false);
            var pages = 0;
            var done = false;

            while (!done) {
                if (++pages > LargestPages)
                    throw new InvalidOperationException("The snapshot never finished paging.");

                var writer = new BitWriter(buffer);
                done = from.WriteSnapshotPage(ref cursor, ref writer);
                var reader = new BitReader(new ReadOnlySpan<byte>(buffer, 0, writer.BytesWritten));
                to.ApplySnapshotPage(ref reader);
            }

            return pages;
        }
    }
}