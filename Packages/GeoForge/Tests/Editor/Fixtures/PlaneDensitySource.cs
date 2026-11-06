// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core.Ground;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    internal sealed class PlaneDensitySource : IDensitySource, IOccupancySource {
        private const float Gradient = 16f;
        private const byte Material = 1;
        private const float UnitVoxelSize = 1f;

        private readonly float _height;
        private readonly float _hillHeight;
        private readonly float _hillWidth;

        internal PlaneDensitySource(float height) : this(height, 0f, 1f) { }

        internal PlaneDensitySource(float height, float hillHeight, float hillWidth) {
            _height = height;
            _hillHeight = hillHeight;
            _hillWidth = hillWidth;
        }

        internal int Classifications { get; private set; }

        public JobHandle ScheduleFill(
            in ChunkFillRequest request, NativeArray<VoxelData> voxels, JobHandle dependency) {
            dependency.Complete();
            Write(request.OriginVoxel, request.DataWidth, request.VoxelSize, voxels);

            return default;
        }

        public Occupancy Classify(in Bounds bounds) {
            Classifications++;

            var centre = bounds.center;
            var extents = bounds.extents;
            var surface = SurfaceHeight(centre.x, centre.z);
            var slack = _hillHeight / _hillWidth * (extents.x + extents.z);

            if (bounds.max.y < surface - slack)
                return Occupancy.Solid;

            return bounds.min.y > surface + slack + 1f / Gradient ? Occupancy.Empty : Occupancy.Mixed;
        }

        internal NativeArray<VoxelData> Fill(Vector3Int chunkOrigin, int dataWidth) {
            var voxels = new NativeArray<VoxelData>(dataWidth * dataWidth * dataWidth, Allocator.Persistent);
            Write(chunkOrigin, dataWidth, UnitVoxelSize, voxels);

            return voxels;
        }

        private void Write(Vector3Int origin, int dataWidth, float voxelSize, NativeArray<VoxelData> voxels) {
            var area = dataWidth * dataWidth;

            for (var index = 0; index < voxels.Length; index++) {
                GfStaticHelper.IndexToInt3(index, area, dataWidth, out var x, out var y, out var z);
                var surface = SurfaceHeight((origin.x + x) * voxelSize, (origin.z + z) * voxelSize);
                var signedDistance = (origin.y + y) * voxelSize - surface;
                var density = VoxelEncoding.DensityFromSignedDistance(signedDistance, Gradient);
                voxels[index] = VoxelData.CreateImmature(density, Material);
            }
        }

        private float SurfaceHeight(float x, float z) {
            return _height + _hillHeight * Mathf.Sin(x / _hillWidth) * Mathf.Cos(z / _hillWidth);
        }
    }
}
