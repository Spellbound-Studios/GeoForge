// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Simulation;
using Spellbound.Networking.Replication;

namespace Spellbound.GeoForge.Networking.Tests {
    internal static class VoxelBatches {
        internal const int FlushBytes = 8192;

        internal static void Apply(byte[] batch, int length, Tick previousTick, ParticipantId receiver,
            PendingEchoMask pendingEchoes, IReplicatedSource replica) {
            var reader = new BatchReader(new ReadOnlySpan<byte>(batch, 0, length));

            if (!reader.TryReadHeader(previousTick, receiver, out _, out _, out var acknowledgedUpload,
                    out var sectionCount))
                throw new InvalidOperationException("The batch header did not read.");

            if (acknowledgedUpload != 0)
                pendingEchoes?.Acknowledge(acknowledgedUpload);

            for (var section = 0; section < sectionCount; section++) {
                if (!reader.TryReadSection(out _, out _, out var count, out var body))
                    throw new InvalidOperationException("Section " + section + " did not read.");

                replica.ApplyChanges(ref body, count);
            }

            if (!reader.IsComplete)
                throw new InvalidOperationException("The batch has bytes past its last section.");
        }
    }
}