// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public sealed class EditStoreTestVolume : GeoVolume {
        public void Build(VoxelVolumeConfig config, GameObject chunkPrefab, BoundaryOverrides boundaryOverrides) {
            Config = config;
            BoundaryOverrides = boundaryOverrides;
            ChunkPrefab = chunkPrefab;
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