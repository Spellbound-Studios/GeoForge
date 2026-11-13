// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class SelfResolvingGeoChunk : IGeoChunk {
        internal SelfResolvingGeoChunk(Transform transform, Vector3Int coordinate) {
            GeoChunkEngine = new GeoChunkEngine(this, transform, new GeoEditStore(), coordinate);
            GeoChunkEngine.IGeoEditStore.geoChunkEngine = GeoChunkEngine;
        }

        internal List<sbyte> ReadDensities { get; } = new();

        public GeoChunkEngine GeoChunkEngine { get; }

        public void InitializeGeoChunk(Vector3Int coordinate, IGeoEditStore geoEditStore) { }

        public void HandleMeshReady() { }

        public void PassVoxelEditOperation(VoxelEditOperation operation) {
            var resolved = new List<(int, VoxelData)>();

            foreach (var delta in operation.Deltas) {
                var voxel = GeoChunkEngine.GetVoxelData(delta.Index);
                ReadDensities.Add(voxel.Density);
                var density = (sbyte)Mathf.Clamp(voxel.Density + delta.DensityDelta, sbyte.MinValue, sbyte.MaxValue);
                resolved.Add((delta.Index, VoxelData.CreateImmature(density, VoxelData.NullSentinelValue)));
            }

            if (GeoChunkEngine.ApplyVoxelEdits(resolved, out var editBounds))
                GeoChunkEngine.ScheduleOctreeEditValidation(editBounds);
        }
    }
}
