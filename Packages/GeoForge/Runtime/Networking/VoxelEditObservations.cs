// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core;
using Spellbound.Core.Observations;

namespace Spellbound.GeoForge.Networking {
    public static class VoxelEditObservations {
        public static readonly ObservationReason CommandApplied =
            CoreKit.Observations.Reason("geoforge.voxel_edit.applied");

        public static readonly ObservationReason CommandMalformed =
            CoreKit.Observations.Reason("geoforge.voxel_edit.malformed");

        public static readonly ObservationReason CommandRefused =
            CoreKit.Observations.Reason("geoforge.voxel_edit.refused");

        public static readonly ObservationReason CommandTooLarge =
            CoreKit.Observations.Reason("geoforge.voxel_edit.command_too_large");

        public static readonly Counter CommandsTooLarge = CoreKit.Observations.Counter(
            "geoforge.voxel_edit.commands_too_large",
            "A voxel edit command reached further than the largest edit extent in voxels and was refused before " +
            "it touched the volume.");

        public static readonly Counter RecordsOutsideRegion = CoreKit.Observations.Counter(
            "geoforge.voxel_edit.record_outside_region",
            "A voxel edit record named a chunk outside the region of the source that took it; the record was " +
            "dropped.");

        public static readonly Counter KeyOutOfRange = CoreKit.Observations.Counter(
            "geoforge.voxel_edit.key_out_of_range",
            "An edited voxel's chunk coordinate lies outside what a voxel edit record carries, so the edit is " +
            "kept locally and not replicated.");

        public static readonly Counter RecordsMalformed = CoreKit.Observations.Counter(
            "geoforge.voxel_edit.records_malformed",
            "A section or snapshot page of voxel edit records did not read, or named a voxel outside its chunk; " +
            "the records from there on were dropped.");

        public static readonly Counter RecordsApplied = CoreKit.Observations.Counter(
            "geoforge.voxel_edit.records_applied",
            "Voxel edit records written into a chunk's edit store from a batch, an upload or a snapshot page.");
    }
}