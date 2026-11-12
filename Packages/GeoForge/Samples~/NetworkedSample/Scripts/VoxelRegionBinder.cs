// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.GeoForge.Networking;
using Spellbound.Networking;
using Spellbound.Networking.Replication;

namespace Spellbound.GeoForge.Sample4 {
    public sealed class VoxelRegionBinder : IRegionSourceBinder {
        private readonly VoxelEditStores _editStores;
        private readonly HashSet<RegionKey> _bound = new();

        public VoxelRegionBinder(VoxelEditStores editStores) {
            _editStores = editStores ?? throw new ArgumentNullException(nameof(editStores));
        }

        public int BoundCount => _bound.Count;

        public bool IsBound(in RegionKey key) => _bound.Contains(key);

        public void Bind(in RegionKey key, in ReplicationLayer layer, RegionBinder binder) {
            if (binder == null)
                throw new ArgumentNullException(nameof(binder));

            binder.Bind(_editStores.SourceFor(key), layer.WritePolicy);
            _bound.Add(key);
        }

        public void Unbind(in RegionKey key, in ReplicationLayer layer) {
            if (!_bound.Remove(key))
                return;

            if (_editStores.TryGetSource(key, out var source))
                source.Attach(null);
        }
    }
}
