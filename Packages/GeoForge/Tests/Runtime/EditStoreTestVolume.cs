// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public sealed class EditStoreTestVolume : GeoVolume {
        public void Build(VoxelVolumeConfig config, GameObject chunkPrefab, BoundaryOverrides boundaryOverrides) {
            Config = config;
            BoundaryOverrides = boundaryOverrides;
            typeof(GeoVolume).GetProperty("ChunkPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, chunkPrefab);
            CreateGeoVolume(config);
        }

        public void InitializeEveryChunk() {
            var initialization = base.InitializeChunks();

            while (initialization.MoveNext()) { }
        }

        protected override IEnumerator InitializeChunks() {
            yield break;
        }
    }
}