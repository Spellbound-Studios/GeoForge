// Copyright (c) Spellbound Studios. All rights reserved.

using System.Globalization;

namespace Spellbound.GeoForge.Networking.Tests {
    internal readonly struct BorderCost {
        internal BorderCost(int voxels, int records, int bytes, int sections) {
            Voxels = voxels;
            Records = records;
            Bytes = bytes;
            Sections = sections;
        }

        internal int Voxels { get; }

        internal int Records { get; }

        internal int Bytes { get; }

        internal int Sections { get; }

        internal double BytesPerVoxel => Voxels == 0 ? 0d : (double)Bytes / Voxels;

        internal double RecordsPerVoxel => Voxels == 0 ? 0d : (double)Records / Voxels;

        public override string ToString() {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} voxels, {1} records ({2:0.00} a voxel), {3} bytes in {4} sections ({5:0.00} bytes a voxel)",
                Voxels, Records, RecordsPerVoxel, Bytes, Sections, BytesPerVoxel);
        }
    }
}