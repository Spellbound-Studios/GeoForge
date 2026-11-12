// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Packing;
using Spellbound.Networking.Replication;

namespace Spellbound.GeoForge.Networking.Tests {
    internal static class VoxelCommandEnvelopes {
        private const int ByteBits = 8;
        private const int ShortBits = 16;
        private const int FloatBits = 32;

        internal static byte[] Of(VoxelEditCommand command, CommandWriter writer) {
            var commandBuffer = new byte[64];
            var commandWriter = new BitWriter(commandBuffer);

            if (!command.TryWrite(ref commandWriter))
                throw new InvalidOperationException("The command did not write.");

            return Wrap(commandBuffer.AsSpan(0, commandWriter.BytesWritten), writer);
        }

        internal static byte[] Raw(VoxelEditShape shape, bool adds, float x, float y, float z, float radius,
            int halfExtent, short delta, byte material, CommandWriter writer) {
            var commandBuffer = new byte[64];
            var commandWriter = new BitWriter(commandBuffer);
            commandWriter.WriteBits((byte)shape, ByteBits);
            commandWriter.WriteBits(adds ? 1u : 0u, ByteBits);
            WriteFloat(ref commandWriter, x);
            WriteFloat(ref commandWriter, y);
            WriteFloat(ref commandWriter, z);

            if (shape == VoxelEditShape.Sphere)
                WriteFloat(ref commandWriter, radius);
            else
                commandWriter.WriteVarInt(halfExtent);

            commandWriter.WriteBits((ushort)delta, ShortBits);
            commandWriter.WriteBits(material, ByteBits);

            return Wrap(commandBuffer.AsSpan(0, commandWriter.BytesWritten), writer);
        }

        private static byte[] Wrap(ReadOnlySpan<byte> command, CommandWriter writer) {
            var envelope = new byte[256];

            if (!writer.TryWrite(0, command, envelope, out var written, out _))
                throw new InvalidOperationException("The envelope did not write.");

            return envelope.AsSpan(0, written).ToArray();
        }

        private static void WriteFloat(ref BitWriter writer, float value) {
            writer.WriteBits(unchecked((uint)BitConverter.SingleToInt32Bits(value)), FloatBits);
        }
    }
}