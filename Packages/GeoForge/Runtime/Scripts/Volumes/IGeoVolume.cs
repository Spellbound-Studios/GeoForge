// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Logging;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    public interface IGeoVolume {
        #region Abstract Methods and Properties

        /// <summary>
        ///     Getter Property for Base Volume, which holds the core functionality of a Volume.
        /// </summary>
        GeoVolumeEngine GeoVolumeEngine { get; }

        /// <summary>
        ///     Ranges for each Level of Detail and the view distance range where you see it.
        /// </summary>
        Vector2[] ViewDistanceLodRanges { get; }

        /// <summary>
        ///     Indication of if the Volume is moving or is capable of moving, or if not.
        /// </summary>
        bool IsMoving { get; set; }

        /// <summary>
        ///     Indication of if the Volume is the primary Terrain, making it the default Volume to Query.
        /// </summary>
        bool IsPrimaryTerrain { get; set; }

        #endregion

        #region Default Implmentations

        bool TryQueryVoxel(Vector3 worldPosition, out VoxelData voxel) {
            voxel = default;

            return GeoVolumeEngine != null && GeoVolumeEngine.TryQueryVoxel(worldPosition, out voxel);
        }

        bool TryQuerySurfaceMaterial(Vector3 worldPosition, out byte materialIndex) {
            materialIndex = default;

            return GeoVolumeEngine != null && GeoVolumeEngine.TryQuerySurfaceMaterial(worldPosition, out materialIndex);
        }

        bool TryQuerySurfaceCase(Vector3 worldPosition, out SurfaceCase surfaceCase) {
            surfaceCase = default;

            return GeoVolumeEngine != null && GeoVolumeEngine.TryQuerySurfaceCase(worldPosition, out surfaceCase);
        }

        IChunkInterest ChunkInterest {
            get => GeoVolumeEngine?.ChunkInterest;
            set {
                if (GeoVolumeEngine != null)
                    GeoVolumeEngine.ChunkInterest = value;
            }
        }

        void SetViewers(ReadOnlySpan<ChunkViewer> viewers) {
            GeoVolumeEngine?.SetViewers(viewers);
        }

        IDensitySource DensitySource => GeoVolumeEngine?.DensitySource;

        void UseDensitySource(IDensitySource source) {
            GeoVolumeEngine?.UseDensitySource(source);
        }

        event Action<VolumeChange> Changed {
            add {
                if (GeoVolumeEngine != null)
                    GeoVolumeEngine.Changed += value;
                else
                    RefuseSubscription(nameof(Changed));
            }
            remove {
                if (GeoVolumeEngine != null)
                    GeoVolumeEngine.Changed -= value;
            }
        }

        event Action<ChunkResidency> ResidencyChanged {
            add {
                if (GeoVolumeEngine != null)
                    GeoVolumeEngine.ResidencyChanged += value;
                else
                    RefuseSubscription(nameof(ResidencyChanged));
            }
            remove {
                if (GeoVolumeEngine != null)
                    GeoVolumeEngine.ResidencyChanged -= value;
            }
        }

        bool TryQuerySolid(Vector3 worldPosition, out bool solid) {
            solid = default;

            return GeoVolumeEngine != null && GeoVolumeEngine.TryQuerySolid(worldPosition, out solid);
        }

        bool IsMeshReady(Vector3 worldPosition, ReadinessScope scope) {
            return GeoVolumeEngine != null && GeoVolumeEngine.IsMeshReady(worldPosition, scope);
        }

        bool IsColliderReady(Vector3 worldPosition, ReadinessScope scope) {
            return GeoVolumeEngine != null && GeoVolumeEngine.IsColliderReady(worldPosition, scope);
        }

        bool IsBoxReady(in Bounds worldBounds, ReadinessScope scope, out VolumeVersion version) {
            version = default;

            return GeoVolumeEngine != null && GeoVolumeEngine.IsBoxReady(worldBounds, scope, out version);
        }

        bool TryCopyRunList(
            Vector3Int chunkCoordinate, NativeList<SparseVoxelData> destination, out VolumeVersion version) {
            version = default;

            return GeoVolumeEngine != null && GeoVolumeEngine.TryCopyRunList(chunkCoordinate, destination, out version);
        }

        bool TryCopySolidRuns(Vector3Int chunkCoordinate, NativeList<SolidRun> destination, out VolumeVersion version) {
            version = default;

            return GeoVolumeEngine != null &&
                   GeoVolumeEngine.TryCopySolidRuns(chunkCoordinate, destination, out version);
        }

        void OnTerraform(Vector3 worldPosition, int voxelCount) { }

        Transform VolumeTransform => GeoVolumeEngine?.Transform;

        (Vector3, Quaternion) SnapToGrid(Vector3 pos) {
            return GeoVolumeEngine?.SnapToGrid(pos) ?? (pos, Quaternion.identity);
        }

        BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob => GeoVolumeEngine?.ConfigBlob ?? default;

        bool IntersectsVolume(Bounds voxelBounds) {
            return GeoVolumeEngine != null && GeoVolumeEngine.IntersectsVolume(voxelBounds);
        }

        async Awaitable ValidateChunkLods() {
            if (GeoVolumeEngine == null)
                return;

            await GeoVolumeEngine.ValidateChunkLodsAsync();
        }

        Vector3Int WorldToVoxelSpace(Vector3 worldPosition) {
            return GeoVolumeEngine?.WorldToVoxelSpace(worldPosition) ?? default;
        }

        Vector3 WorldToVoxelSpaceContinuous(Vector3 worldPosition) {
            return GeoVolumeEngine?.WorldToVoxelSpaceContinuous(worldPosition) ?? default;
        }

        IGeoChunk GetChunkByCoord(Vector3Int coord) {
            return GeoVolumeEngine?.GetChunkByCoord(coord);
        }

        IGeoChunk GetChunkByWorldPosition(Vector3 worldPos) {
            return GeoVolumeEngine?.GetChunkByWorldPosition(worldPos);
        }

        IGeoChunk GetChunkByVoxelPosition(Vector3Int voxelPos) {
            return GeoVolumeEngine?.GetChunkByVoxelPosition(voxelPos);
        }

        Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) {
            return GeoVolumeEngine?.GetCoordByVoxelPosition(voxelPos) ?? default;
        }

        /// <summary>
        ///     Packs every chunk's edits in this volume into one byte[] - see
        ///     GeoVolumeEngine.SaveToByteArray for the wire format and what gets skipped/included.
        /// </summary>
        byte[] SaveToByteArray() {
            return GeoVolumeEngine?.SaveToByteArray();
        }

        /// <summary>
        ///     Restores edits from a payload written by SaveToByteArray. Returns false (and logs) if
        ///     the payload is malformed or doesn't match this volume's current chunk size - see
        ///     GeoVolumeEngine.TryLoadFromByteArray for the full contract, including its batching behavior
        ///     and how it handles a saved coordinate with no loaded chunk.
        /// </summary>
        bool TryLoadFromByteArray(byte[] data) {
            return GeoVolumeEngine != null && GeoVolumeEngine.TryLoadFromByteArray(data);
        }

        private void RefuseSubscription(string eventName) {
            Log.Emit(LogLevel.Warning,
                $"{eventName} was subscribed before the volume's {nameof(GeoVolumeEngine)} exists, so the " +
                "subscription is refused; subscribe once the engine is created.", this as Object);
        }

        void ResetEditedChunksToProcedural();

        void HandleAllChunksMeshed() { }

        #endregion
    }
}