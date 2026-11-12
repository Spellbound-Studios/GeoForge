// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Networking.Replication;

namespace Spellbound.GeoForge.Networking.Tests {
    internal sealed class CountingDirtyNotifier : IDirtyNotifier {
        internal int Marks { get; private set; }

        public void MarkDirty() {
            Marks++;
        }
    }
}