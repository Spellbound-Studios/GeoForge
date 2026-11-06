// Copyright (c) Spellbound Studios. All rights reserved.

using System;

namespace Spellbound.GeoForge {
    public readonly struct VolumeVersion : IComparable<VolumeVersion>, IEquatable<VolumeVersion> {
        public static readonly VolumeVersion Zero = default;

        private VolumeVersion(long value) {
            Value = value;
        }

        public long Value { get; }

        public VolumeVersion Next() {
            return new VolumeVersion(Value + 1);
        }

        public int CompareTo(VolumeVersion other) {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(VolumeVersion other) {
            return Value == other.Value;
        }

        public override bool Equals(object other) {
            return other is VolumeVersion version && Equals(version);
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public override string ToString() {
            return Value.ToString();
        }

        public static bool operator ==(VolumeVersion left, VolumeVersion right) {
            return left.Equals(right);
        }

        public static bool operator !=(VolumeVersion left, VolumeVersion right) {
            return !left.Equals(right);
        }
    }
}
