// Copyright (c) Spellbound Studios. All rights reserved.

namespace Spellbound.GeoForge {
    internal readonly struct DeferredVolumeEvent {
        private DeferredVolumeEvent(bool isResidency, in VolumeChange change, in ChunkResidency residency) {
            IsResidency = isResidency;
            Change = change;
            Residency = residency;
        }

        public bool IsResidency { get; }

        public VolumeChange Change { get; }

        public ChunkResidency Residency { get; }

        public static DeferredVolumeEvent ForChange(in VolumeChange change) {
            return new DeferredVolumeEvent(false, change, default);
        }

        public static DeferredVolumeEvent ForResidency(in ChunkResidency residency) {
            return new DeferredVolumeEvent(true, default, residency);
        }
    }
}
