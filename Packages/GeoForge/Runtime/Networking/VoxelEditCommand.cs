// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Packing;
using UnityEngine;

namespace Spellbound.GeoForge.Networking {
    public readonly struct VoxelEditCommand : IEquatable<VoxelEditCommand> {
        private const int ByteBits = 8;
        private const int ShortBits = 16;
        private const int FloatBits = 32;

        private VoxelEditCommand(VoxelEditShape shape, bool adds, Vector3 worldPosition, float radius, int halfExtent,
            short delta, byte material) {
            Shape = shape;
            Adds = adds;
            WorldPosition = worldPosition;
            Radius = radius;
            HalfExtent = halfExtent;
            Delta = delta;
            Material = material;
        }

        public VoxelEditShape Shape { get; }

        public bool Adds { get; }

        public Vector3 WorldPosition { get; }

        public float Radius { get; }

        public int HalfExtent { get; }

        public short Delta { get; }

        public byte Material { get; }

        public static VoxelEditCommand AddSphere(Vector3 worldPosition, float radius, short delta, byte material) {
            return new VoxelEditCommand(VoxelEditShape.Sphere, true, worldPosition, radius, 0, delta, material);
        }

        public static VoxelEditCommand RemoveSphere(Vector3 worldPosition, float radius, short delta) {
            return new VoxelEditCommand(VoxelEditShape.Sphere, false, worldPosition, radius, 0, delta,
                VoxelData.NullSentinelValue);
        }

        public static VoxelEditCommand AddCube(Vector3 worldPosition, int halfExtent, short delta, byte material) {
            return new VoxelEditCommand(VoxelEditShape.Cube, true, worldPosition, 0f, halfExtent, delta, material);
        }

        public static VoxelEditCommand RemoveCube(Vector3 worldPosition, int halfExtent, short delta) {
            return new VoxelEditCommand(VoxelEditShape.Cube, false, worldPosition, 0f, halfExtent, delta, 0);
        }

        public bool TryWrite(ref BitWriter writer) {
            if (!writer.TryWriteBits((byte)Shape, ByteBits) || !writer.TryWriteBits(Adds ? 1u : 0u, ByteBits) ||
                !TryWriteFloat(ref writer, WorldPosition.x) || !TryWriteFloat(ref writer, WorldPosition.y) ||
                !TryWriteFloat(ref writer, WorldPosition.z))
                return false;

            var extentWritten = Shape == VoxelEditShape.Sphere
                ? TryWriteFloat(ref writer, Radius)
                : writer.TryWriteVarInt(HalfExtent);

            return extentWritten && writer.TryWriteBits((ushort)Delta, ShortBits) &&
                   writer.TryWriteBits(Material, ByteBits);
        }

        public static bool TryRead(ref BitReader reader, out VoxelEditCommand command) {
            command = default;

            if (!reader.TryReadBits(ByteBits, out var shapeBits) || !reader.TryReadBits(ByteBits, out var addsBits) ||
                !TryReadFloat(ref reader, out var x) || !TryReadFloat(ref reader, out var y) ||
                !TryReadFloat(ref reader, out var z))
                return false;

            var shape = (VoxelEditShape)shapeBits;

            if ((shape != VoxelEditShape.Sphere && shape != VoxelEditShape.Cube) || addsBits > 1u)
                return false;

            var radius = 0f;
            var halfExtent = 0;

            var extentRead = shape == VoxelEditShape.Sphere
                ? TryReadFloat(ref reader, out radius)
                : reader.TryReadVarInt(out halfExtent);

            if (!extentRead)
                return false;

            if (!reader.TryReadBits(ShortBits, out var deltaBits) || !reader.TryReadBits(ByteBits, out var material))
                return false;

            var read = new VoxelEditCommand(shape, addsBits == 1u, new Vector3(x, y, z), radius, halfExtent,
                (short)(ushort)deltaBits, (byte)material);

            if (!read.IsValid)
                return false;

            command = read;

            return true;
        }

        public bool IsValid => IsFinite(WorldPosition) && Delta >= 0 && HasValidExtent && HasValidMaterial;

        public int ExtentInVoxels(float voxelSize) {
            if (!(voxelSize > 0f) || float.IsInfinity(voxelSize))
                return int.MaxValue;

            if (Shape != VoxelEditShape.Sphere)
                return HalfExtent;

            var extent = Mathf.Ceil(Radius * 0.5f / voxelSize);

            return extent >= int.MaxValue ? int.MaxValue : (int)extent;
        }

        public bool TryApply(IGeoVolume volume) {
            if (volume == null)
                throw new ArgumentNullException(nameof(volume));

            if (!IsValid)
                return false;

            switch (Shape) {
                case VoxelEditShape.Sphere:
                    return Adds
                        ? GeoForgeCommands.AddSphere(volume, WorldPosition, Radius, Delta, Material)
                        : GeoForgeCommands.RemoveSphere(volume, WorldPosition, Radius, Delta);
                case VoxelEditShape.Cube:
                    return Adds
                        ? GeoForgeCommands.AddCube(volume, WorldPosition, HalfExtent, Delta, Material)
                        : GeoForgeCommands.RemoveCube(volume, WorldPosition, HalfExtent, Delta);
                default:
                    return false;
            }
        }

        public bool Equals(VoxelEditCommand other) {
            return Shape == other.Shape && Adds == other.Adds && WorldPosition.Equals(other.WorldPosition) &&
                   Radius.Equals(other.Radius) && HalfExtent == other.HalfExtent && Delta == other.Delta &&
                   Material == other.Material;
        }

        public override bool Equals(object obj) {
            return obj is VoxelEditCommand other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine((int)Shape, Adds, WorldPosition, Radius, HalfExtent, Delta, Material);
        }

        public override string ToString() {
            var verb = Adds ? "add" : "remove";
            var size = Shape == VoxelEditShape.Sphere ? "radius " + Radius : "half extent " + HalfExtent;

            return $"{verb} {Shape} at {WorldPosition}, {size}, delta {Delta}, material {Material}";
        }

        private static bool TryWriteFloat(ref BitWriter writer, float value) {
            return writer.TryWriteBits(unchecked((uint)BitConverter.SingleToInt32Bits(value)), FloatBits);
        }

        private static bool TryReadFloat(ref BitReader reader, out float value) {
            if (!reader.TryReadBits(FloatBits, out var bits)) {
                value = 0f;

                return false;
            }

            value = BitConverter.Int32BitsToSingle(unchecked((int)bits));

            return true;
        }

        private bool HasValidExtent => Shape == VoxelEditShape.Sphere
            ? Radius > 0f && !float.IsInfinity(Radius)
            : Shape == VoxelEditShape.Cube && HalfExtent >= 0;

        private bool HasValidMaterial => Adds
            ? Material < VoxelData.NullSentinelValue
            : Material == (Shape == VoxelEditShape.Sphere ? VoxelData.NullSentinelValue : (byte)0);

        private static bool IsFinite(Vector3 value) {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}