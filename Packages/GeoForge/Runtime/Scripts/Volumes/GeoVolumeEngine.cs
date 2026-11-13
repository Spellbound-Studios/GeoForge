// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Spellbound.Core.Logging;
using Spellbound.Core.Packing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    public class GeoVolumeEngine : IDisposable {
        private const int SaveFormatVersion = 1;
        private readonly Bounds _bounds;
        private readonly List<DeferredVolumeEvent> _deferredEvents = new();
        private readonly HashSet<IGeoChunk> _meshedChunks = new();

        private readonly MonoBehaviour _owner;
        private readonly IGeoVolume _ownerAsIGeoVolume;

        private IChunkInterest _chunkInterest = new DistanceChunkInterest();
        private IDensitySource _densitySource;
        private IDensitySource _generatorDensitySource;
        private GeoForgeDataGenerator _wrappedGenerator;
        private int _eventHolds;
        private bool _flushingEvents;
        private bool _validationHoldsEvents;
        private bool _isDisposed;
        private bool _reportedMissingViewers;
        private int _viewerCount;
        private ChunkViewer[] _viewers = Array.Empty<ChunkViewer>();
        private NativeArray<ChunkViewer> _viewersVoxelSpace;
        private NativeArray<Vector2> _levelOfDetailRanges;

        public GeoVolumeEngine(
            MonoBehaviour owner, IGeoVolume ownerAsIGeoVolume, VoxelVolumeConfig config, GeoForgeManager manager) {
            _owner = owner;
            _ownerAsIGeoVolume = ownerAsIGeoVolume;
            Manager = manager;
            ConfigBlob = VolumeConfigBlobCreator.CreateVolumeConfigBlobAsset(config);
            SkipFillByOccupancy = config.skipFillByOccupancy;
            _bounds = CalculateVolumeBounds();
        }

        public BlobAssetReference<VolumeConfigBlobAsset> ConfigBlob { get; }

        public bool SkipFillByOccupancy { get; }

        public GeoForgeManager Manager { get; }

        public Transform Transform => _owner.transform;
        public Dictionary<Vector3Int, IGeoChunk> ChunkDict { get; } = new();

        public IChunkInterest ChunkInterest {
            get => _chunkInterest;
            set => _chunkInterest = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IDensitySource DensitySource => _densitySource;

        internal IDensitySource RefillSource => _densitySource ?? _generatorDensitySource;

        public VolumeVersion Version { get; private set; }

        public event Action<VolumeChange> Changed;

        public event Action<ChunkResidency> ResidencyChanged;

        public void Dispose() {
            _isDisposed = true;

            var chunkList = new List<IGeoChunk>(ChunkDict.Values);
            foreach (var chunk in chunkList) {
                chunk.GeoChunkEngine.Dispose();

                // Destroy (deferred to end-of-frame) instead of DestroyImmediate: DestroyImmediate
                // synchronously re-enters SimpleGeoChunk.OnDestroy() -> _geoChunk.Dispose() right
                // here in the middle of this loop, which is the risky ordering this exit criterion
                // calls out. GeoChunk.Dispose() is now idempotent by design (see its own guard),
                // so whichever order the deferred OnDestroy fires in, it's safe either way.
                Object.Destroy(chunk.GeoChunkEngine.Transform.gameObject);
            }

            if (Manager != null)
                Manager.UnRegisterVoxelVolume(_ownerAsIGeoVolume);

            if (_viewersVoxelSpace.IsCreated)
                _viewersVoxelSpace.Dispose();

            if (_levelOfDetailRanges.IsCreated)
                _levelOfDetailRanges.Dispose();

            if (ConfigBlob.IsCreated)
                ConfigBlob.Dispose();
        }

        public void UseDensitySource(IDensitySource source) {
            _densitySource = source;
        }

        internal IDensitySource DensitySourceFor(GeoForgeDataGenerator generator) {
            if (_densitySource != null)
                return _densitySource;

            if (_generatorDensitySource == null || _wrappedGenerator != generator) {
                _generatorDensitySource = GeoForgeKit.CreateDensitySource(generator, ConfigBlob);
                _wrappedGenerator = generator;
            }

            return _generatorDensitySource;
        }

        internal ChunkFillRequest CreateFillRequest(Vector3Int chunkCoordinate) {
            return ChunkFillRequest.ForChunk(chunkCoordinate, ConfigBlob.Value, uint.MinValue);
        }

        internal void RaiseChanged(Vector3Int chunkCoordinate, BoundsInt voxelBounds) {
            Version = Version.Next();
            var change = new VolumeChange(chunkCoordinate, voxelBounds, Version);

            if (HoldsEvents) {
                _deferredEvents.Add(DeferredVolumeEvent.ForChange(change));
                return;
            }

            Raise(Changed, change);
        }

        internal void RaiseResidencyChanged(in ChunkResidency residency) {
            if (HoldsEvents) {
                _deferredEvents.Add(DeferredVolumeEvent.ForResidency(residency));
                return;
            }

            Raise(ResidencyChanged, residency);
        }

        private bool HoldsEvents =>
            _flushingEvents || _eventHolds > 0 || (Manager != null && Manager.IsApplyingMarchResults);

        internal void HoldEvents() {
            _eventHolds++;
        }

        internal void ReleaseHeldEvents() {
            if (_eventHolds > 0)
                _eventHolds--;

            FlushDeferredEvents();
        }

        internal void FlushDeferredEvents() {
            if (_deferredEvents.Count == 0 || HoldsEvents)
                return;

            _flushingEvents = true;

            try {
                for (var index = 0; index < _deferredEvents.Count; index++) {
                    var deferredEvent = _deferredEvents[index];

                    if (deferredEvent.IsResidency)
                        Raise(ResidencyChanged, deferredEvent.Residency);
                    else
                        Raise(Changed, deferredEvent.Change);
                }
            }
            finally {
                _deferredEvents.Clear();
                _flushingEvents = false;
            }
        }

        private void Raise<T>(Action<T> handlers, T argument) {
            if (handlers == null)
                return;

            foreach (var handler in handlers.GetInvocationList()) {
                try {
                    ((Action<T>)handler)(argument);
                }
                catch (Exception exception) {
                    Log.Error($"{_owner.name}: a subscriber to {typeof(T).Name} threw: {exception}", _owner);
                }
            }
        }

        internal NativeArray<Vector2>.ReadOnly ReadLevelOfDetailRanges() {
            var ranges = _ownerAsIGeoVolume.ViewDistanceLodRanges ?? Array.Empty<Vector2>();

            if (!_levelOfDetailRanges.IsCreated || _levelOfDetailRanges.Length != ranges.Length) {
                if (_levelOfDetailRanges.IsCreated)
                    _levelOfDetailRanges.Dispose();

                _levelOfDetailRanges = new NativeArray<Vector2>(ranges.Length, Allocator.Persistent);
            }

            _levelOfDetailRanges.CopyFrom(ranges);

            return _levelOfDetailRanges.AsReadOnly();
        }

        public void SetViewers(ReadOnlySpan<ChunkViewer> viewers) {
            if (_viewers.Length < viewers.Length)
                _viewers = new ChunkViewer[viewers.Length];

            viewers.CopyTo(_viewers);
            _viewerCount = viewers.Length;
        }

        private bool TryConvertViewers(out NativeArray<ChunkViewer> viewersVoxelSpace) {
            viewersVoxelSpace = default;

            if (_viewerCount == 0) {
                ReportMissingViewers();

                return false;
            }

            if (_viewersVoxelSpace.Length < _viewerCount) {
                if (_viewersVoxelSpace.IsCreated)
                    _viewersVoxelSpace.Dispose();

                _viewersVoxelSpace = new NativeArray<ChunkViewer>(_viewerCount, Allocator.Persistent);
            }

            var resolution = ConfigBlob.Value.Resolution;

            for (var index = 0; index < _viewerCount; index++) {
                var viewer = _viewers[index];
                _viewersVoxelSpace[index] = new ChunkViewer(WorldToVoxelSpace(viewer.Position),
                    viewer.CollisionRadius / resolution, viewer.DetailRadius / resolution);
            }

            viewersVoxelSpace = _viewersVoxelSpace.GetSubArray(0, _viewerCount);

            return true;
        }

        private void ReportMissingViewers() {
            if (_reportedMissingViewers)
                return;

            _reportedMissingViewers = true;
            Log.Emit(LogLevel.Warning,
                $"{_owner.name}: the volume has no viewers, so no chunk's level of detail is validated; give it a " +
                $"{nameof(TransformChunkViewer)} or call {nameof(IGeoVolume)}.{nameof(SetViewers)}.", _owner);
        }

        public void HandleChunkMeshReady(IGeoChunk chunk) {
            if (!_meshedChunks.Add(chunk))
                return;

            var isAllMeshed = _meshedChunks.Count == ConfigBlob.Value.TotalChunks
                              || (!ConfigBlob.Value.IsFiniteSize && _meshedChunks.Count == ChunkDict.Count);

            if (isAllMeshed) _ownerAsIGeoVolume.HandleAllChunksMeshed();
        }

        public Vector3Int WorldToVoxelSpace(Vector3 worldPosition) {
            ref var config = ref ConfigBlob.Value;
            var localPos = Transform.InverseTransformPoint(worldPosition);

            return new Vector3Int(
                Mathf.RoundToInt(localPos.x / config.Resolution) - config.Offset.x,
                Mathf.RoundToInt(localPos.y / config.Resolution) - config.Offset.y,
                Mathf.RoundToInt(localPos.z / config.Resolution) - config.Offset.z
            );
        }

        public Vector3 WorldToVoxelSpaceContinuous(Vector3 worldPosition) {
            ref var config = ref ConfigBlob.Value;
            var localPos = Transform.InverseTransformPoint(worldPosition);

            return new Vector3(
                localPos.x / config.Resolution - config.Offset.x,
                localPos.y / config.Resolution - config.Offset.y,
                localPos.z / config.Resolution - config.Offset.z
            );
        }

        /// <summary>
        ///     Tries to resolve the voxel at a world position within this volume specifically - no
        ///     looping over other volumes, no IsPrimaryTerrain filtering, both of which are the caller's
        ///     concern (see GeoForgeManager.TryQueryVoxel). Returns false if this volume has no loaded
        ///     chunk with voxel data at this position.
        /// </summary>
        public bool TryQueryVoxel(Vector3 worldPosition, out VoxelData voxel) {
            voxel = default;

            var chunk = GetChunkByWorldPosition(worldPosition);

            if (chunk == null || !chunk.HasVoxelData())
                return false;

            var voxelPosition = WorldToVoxelSpace(worldPosition);
            voxel = chunk.GetVoxelDataFromVoxelPosition(voxelPosition);

            return true;
        }

        /// <summary>
        ///     Resolves the surface material at a world position by inspecting the 2x2x2 voxel cell
        ///     enclosing it - the same 8-corner cube marching cubes uses to generate a triangle there, so
        ///     this is meant to be called with a position that's actually near a generated surface (e.g. a
        ///     raycast hit against the mesh), not an arbitrary point in space.
        ///     Resolves all 8 corners from a SINGLE chunk - whichever chunk owns the voxel nearest to
        ///     worldPosition - rather than looking up a chunk per corner the way TryQueryVoxelCluster does.
        ///     This relies on each chunk's dense voxel array already including its halo of neighbor-owned
        ///     voxels (the same halo marching cubes itself needs to generate correct boundary triangles),
        ///     so the owning chunk's own array already has valid data for corners just outside its bounds.
        ///     Returns false if that chunk has no voxel data, none of the 8 corners are solid, or the
        ///     resolved corner holds the null material (a uniform chunk not yet filled).
        ///     Only solid ("full") corners are considered for material - an empty/air corner's
        ///     MaterialIndex isn't meaningful. If every solid corner shares one material, that's the
        ///     result; if they disagree, this returns the solid corner nearest worldPosition - a
        ///     placeholder tie-break for now, not a considered blend rule.
        /// </summary>
        public bool TryQuerySurfaceMaterial(Vector3 worldPosition, out byte materialIndex) {
            materialIndex = VoxelData.NullSentinelValue;

            var continuousVoxelPos = WorldToVoxelSpaceContinuous(worldPosition);

            var nearestVoxelPos = new Vector3Int(
                Mathf.RoundToInt(continuousVoxelPos.x),
                Mathf.RoundToInt(continuousVoxelPos.y),
                Mathf.RoundToInt(continuousVoxelPos.z));

            var chunk = GetChunkByVoxelPosition(nearestVoxelPos);

            if (chunk == null || !chunk.HasVoxelData())
                return false;

            // Enclosing cell, not "nearest voxel plus neighbors" - same convention TryQueryVoxelCluster
            // uses: floor to the corner below worldPosition on every axis, then the cell is that corner
            // plus the seven corners at +1 on each axis.
            var cellOrigin = new Vector3Int(
                Mathf.FloorToInt(continuousVoxelPos.x),
                Mathf.FloorToInt(continuousVoxelPos.y),
                Mathf.FloorToInt(continuousVoxelPos.z));

            var foundSolidCorner = false;
            var allSolidMaterialsMatch = true;
            byte firstSolidMaterial = 0;
            byte nearestSolidMaterial = 0;
            var nearestDistanceSqr = float.MaxValue;

            for (var dx = 0; dx <= 1; dx++)
            for (var dy = 0; dy <= 1; dy++)
            for (var dz = 0; dz <= 1; dz++) {
                var cornerVoxelPos = cellOrigin + new Vector3Int(dx, dy, dz);
                var cornerVoxel = chunk.GetVoxelDataFromVoxelPosition(cornerVoxelPos);

                if (!VoxelEncoding.IsSolid(cornerVoxel.Density))
                    continue;

                // Plain index, not the raw maturity-inclusive MaterialIndex byte
                // TryQueryVoxelCluster tallies - this result feeds VoxelMaterialDatabase /
                // MaterialSideTable lookups, which index 0..MaterialCount-1 and would silently
                // miss (or throw) on a maturity-flagged value.
                var cornerMaterial = cornerVoxel.GetPlainMatIndex();

                if (!foundSolidCorner) {
                    firstSolidMaterial = cornerMaterial;
                    foundSolidCorner = true;
                }
                else if (cornerMaterial != firstSolidMaterial) {
                    allSolidMaterialsMatch = false;
                }

                var distanceSqr = (continuousVoxelPos - cornerVoxelPos).sqrMagnitude;

                if (distanceSqr < nearestDistanceSqr) {
                    nearestDistanceSqr = distanceSqr;
                    nearestSolidMaterial = cornerMaterial;
                }
            }

            if (!foundSolidCorner)
                return false;

            materialIndex = allSolidMaterialsMatch ? firstSolidMaterial : nearestSolidMaterial;
            materialIndex = VoxelEncoding.MaterialIndex(materialIndex);

            return materialIndex != VoxelEncoding.NullMaterialIndex;
        }

        public bool TryQuerySurfaceCase(Vector3 worldPosition, out SurfaceCase surfaceCase) {
            surfaceCase = default;

            var continuousVoxelPosition = WorldToVoxelSpaceContinuous(worldPosition);

            var nearestVoxelPosition = new Vector3Int(
                Mathf.RoundToInt(continuousVoxelPosition.x),
                Mathf.RoundToInt(continuousVoxelPosition.y),
                Mathf.RoundToInt(continuousVoxelPosition.z));

            var chunk = GetChunkByVoxelPosition(nearestVoxelPosition);

            if (chunk == null || !chunk.HasVoxelData())
                return false;

            if (Manager == null || !Manager.McTablesBlob.IsCreated)
                return false;

            var cellOrigin = new Vector3Int(
                Mathf.FloorToInt(continuousVoxelPosition.x),
                Mathf.FloorToInt(continuousVoxelPosition.y),
                Mathf.FloorToInt(continuousVoxelPosition.z));

            surfaceCase = chunk.GeoChunkEngine.ComputeSurfaceCase(cellOrigin, ref Manager.McTablesBlob.Value);

            return true;
        }

        public bool TryQuerySolid(Vector3 worldPosition, out bool solid) {
            solid = false;

            if (!TryQueryVoxel(worldPosition, out var voxel))
                return false;

            solid = VoxelEncoding.IsSolid(voxel.Density);

            return true;
        }

        public bool IsMeshReady(Vector3 worldPosition, ReadinessScope scope) {
            var voxelPosition = WorldToVoxelSpace(worldPosition);

            return IsRegionReady(voxelPosition, voxelPosition, scope, false);
        }

        public bool IsColliderReady(Vector3 worldPosition, ReadinessScope scope) {
            var voxelPosition = WorldToVoxelSpace(worldPosition);

            return IsRegionReady(voxelPosition, voxelPosition, scope, true);
        }

        public bool IsBoxReady(in Bounds worldBounds, ReadinessScope scope, out VolumeVersion version) {
            version = Version;
            var minimum = Vector3Int.one * int.MaxValue;
            var maximum = Vector3Int.one * int.MinValue;
            var worldMinimum = worldBounds.min;
            var worldMaximum = worldBounds.max;

            for (var corner = 0; corner < 8; corner++) {
                var cornerPosition = new Vector3(
                    (corner & 1) == 0 ? worldMinimum.x : worldMaximum.x,
                    (corner & 2) == 0 ? worldMinimum.y : worldMaximum.y,
                    (corner & 4) == 0 ? worldMinimum.z : worldMaximum.z);
                var voxelPosition = WorldToVoxelSpace(cornerPosition);
                minimum = Vector3Int.Min(minimum, voxelPosition);
                maximum = Vector3Int.Max(maximum, voxelPosition);
            }

            return IsRegionReady(minimum, maximum, scope, true);
        }

        private bool IsRegionReady(
            Vector3Int voxelMinimum, Vector3Int voxelMaximum, ReadinessScope scope, bool requiresCollider) {
            var chunkSize = ConfigBlob.Value.ChunkSize;
            var firstChunk = GetCoordByVoxelPosition(voxelMinimum);
            var lastChunk = GetCoordByVoxelPosition(voxelMaximum);

            for (var x = firstChunk.x; x <= lastChunk.x; x++)
            for (var y = firstChunk.y; y <= lastChunk.y; y++)
            for (var z = firstChunk.z; z <= lastChunk.z; z++) {
                var coordinate = new Vector3Int(x, y, z);
                var chunk = GetChunkByCoord(coordinate);

                if (chunk == null || !chunk.HasVoxelData())
                    return false;

                if (chunk.DensityRange.IsSkippable())
                    continue;

                var rootNode = chunk.GeoChunkEngine.RootNode;

                if (rootNode == null)
                    return false;

                var chunkOrigin = coordinate * chunkSize;

                if (!rootNode.IsRegionReady(voxelMinimum - chunkOrigin, voxelMaximum - chunkOrigin, scope,
                        requiresCollider))
                    return false;
            }

            return true;
        }

        public bool TryCopyRunList(
            Vector3Int chunkCoordinate, NativeList<SparseVoxelData> destination, out VolumeVersion version) {
            version = Version;

            if (!destination.IsCreated)
                throw new ArgumentException("A run list copy needs a created destination list.", nameof(destination));

            if (!TryGetRunList(chunkCoordinate, out var runs))
                return false;

            destination.CopyFrom(runs);

            return true;
        }

        public bool TryCopySolidRuns(
            Vector3Int chunkCoordinate, NativeList<SolidRun> destination, out VolumeVersion version) {
            version = Version;

            if (!destination.IsCreated)
                throw new ArgumentException("A solid run copy needs a created destination list.", nameof(destination));

            if (!TryGetRunList(chunkCoordinate, out var runs))
                return false;

            destination.Clear();

            for (var index = 0; index < runs.Length; index++) {
                var run = runs[index];
                var solid = VoxelEncoding.IsSolid(run.Voxel.Density);

                if (destination.Length > 0 && destination[destination.Length - 1].Solid == solid)
                    continue;

                destination.Add(new SolidRun(run.StartIndex, solid));
            }

            return true;
        }

        private bool TryGetRunList(Vector3Int chunkCoordinate, out NativeList<SparseVoxelData> runs) {
            runs = default;
            var chunk = GetChunkByCoord(chunkCoordinate);

            if (chunk == null || !chunk.HasVoxelData())
                return false;

            runs = chunk.GeoChunkEngine.GetsSparseVoxelData();

            return true;
        }

        public void RegisterVolume() {
            if (Manager == null) {
                Log.Error($"{_owner.name}: the volume engine holds no {nameof(GeoForgeManager)}.", _owner);

                return;
            }

            Manager.RegisterVoxelVolume(_ownerAsIGeoVolume);
        }

        public IGeoChunk GetChunkByCoord(Vector3Int coord) {
            return ChunkDict.GetValueOrDefault(coord);
        }

        public IGeoChunk GetChunkByWorldPosition(Vector3 worldPos) {
            var voxelPos = WorldToVoxelSpace(worldPos);

            return GetChunkByVoxelPosition(voxelPos);
        }

        public IGeoChunk GetChunkByVoxelPosition(Vector3Int voxelPos) {
            var coord = GetCoordByVoxelPosition(voxelPos);

            return GetChunkByCoord(coord);
        }

        public Vector3Int GetCoordByVoxelPosition(Vector3Int voxelPos) {
            return ChunkLayout.CoordinateOfVoxel(voxelPos, ConfigBlob.Value.ChunkSize);
        }

        // Resets every chunk whose edit-store currently has recorded edits back to deterministic
        // procedural baseline - chunks that are already clean are skipped, since there's nothing to
        // wipe and regenerating them would just be wasted work reproducing what's already there.
        // Intended to run immediately before applying a loaded save (see TryLoadFromByteArray), so a
        // load fully replaces the current session's state rather than merging with it.
        //
        // Also seeds each reset chunk's brand-new octree root with one real ValidateOctreeLods
        // pass, batched in groups of ValidatesPerFrame through the Validation pool (same pattern
        // ValidateChunkLodsAsync uses). This is required, not optional: SetVoxels (inside
        // RegenerateFromProceduralData) disposes and replaces the octree root entirely, and every
        // node in a brand new root starts un-leaf-initialized (OctreeNode._leafInitialized is only
        // ever set by MakeLeaf, which only ever runs from ValidateOctreeLods). ValidateOctreeEdits's
        // own UpdateLeaf explicitly no-ops on a node that's never been leaf-initialized - by design,
        // edits assume the octree structure already reflects the correct LOD and only re-march an
        // existing leaf, they never construct one from nothing. Without this seeding pass, a
        // reset-then-immediately-loaded chunk's restored edits get correctly written into voxel
        // data but never actually meshed until the background ValidateAllVolumesLodsAsync loop
        // happens to reach that chunk on its own.
        public void ResetEditedChunksToProcedural(GeoForgeDataGenerator dataGenerator) {
            TryResetEditedChunksToProcedural(dataGenerator);
        }

        public bool ResetChunksToProcedural(IReadOnlyList<Vector3Int> chunkCoordinates) {
            if (chunkCoordinates == null)
                throw new ArgumentNullException(nameof(chunkCoordinates));

            var edited = new List<Vector3Int>();

            for (var index = 0; index < chunkCoordinates.Count; index++) {
                var coordinate = chunkCoordinates[index];

                if (ChunkDict.TryGetValue(coordinate, out var chunk) && HoldsStoredEdits(chunk))
                    edited.Add(coordinate);
            }

            if (edited.Count == 0)
                return true;

            if (!TryRefillSource(null, edited.Count, out _))
                return false;

            for (var index = 0; index < edited.Count; index++)
                ChunkDict[edited[index]].GeoChunkEngine.IGeoEditStore.Clear();

            return RefillChunks(edited);
        }

        public bool RefillChunks(IReadOnlyList<Vector3Int> chunkCoordinates) {
            if (chunkCoordinates == null)
                throw new ArgumentNullException(nameof(chunkCoordinates));

            if (chunkCoordinates.Count == 0)
                return true;

            if (!TryRefillSource(null, chunkCoordinates.Count, out var source))
                return false;

            var refilled = new List<IGeoChunk>();

            for (var index = 0; index < chunkCoordinates.Count; index++) {
                var coordinate = chunkCoordinates[index];

                if (!ChunkDict.TryGetValue(coordinate, out var chunk))
                    continue;

                chunk.SetVoxels(CreateFillRequest(coordinate), source);
                _meshedChunks.Remove(chunk);
                refilled.Add(chunk);
            }

            ValidateResetChunks(refilled);

            return true;
        }

        public Vector3 ChunkWorldOrigin(Vector3Int chunkCoordinate) {
            ref var config = ref ConfigBlob.Value;

            return Transform.TransformPoint((Vector3)chunkCoordinate * (config.ChunkSize * config.Resolution));
        }

        public float VoxelSize => ConfigBlob.Value.Resolution;

        internal bool TryResetEditedChunksToProcedural(GeoForgeDataGenerator dataGenerator) {
            var resetChunks = new List<IGeoChunk>();
            var coordinates = new List<Vector3Int>();

            foreach (var kvp in ChunkDict) {
                if (!HoldsStoredEdits(kvp.Value))
                    continue;

                resetChunks.Add(kvp.Value);
                coordinates.Add(kvp.Key);
            }

            if (resetChunks.Count == 0)
                return true;

            if (!TryRefillSource(dataGenerator, resetChunks.Count, out var source))
                return false;

            _meshedChunks.Clear();

            for (var index = 0; index < resetChunks.Count; index++)
                ResetToProcedural(coordinates[index], resetChunks[index], source);

            ValidateResetChunks(resetChunks);

            return true;
        }

        private bool TryRefillSource(GeoForgeDataGenerator dataGenerator, int chunks, out IDensitySource source) {
            source = dataGenerator != null ? DensitySourceFor(dataGenerator) : RefillSource;

            if (Manager != null && source != null)
                return true;

            var missing = Manager == null ? $"no {nameof(GeoForgeManager)}" : "no density source";
            Log.Error($"{_owner.name}: {chunks} edited chunks were to be refilled, but the volume has {missing} to " +
                      $"refill them with, so nothing was reset: their stores and voxels are kept. Give it one " +
                      $"with {nameof(UseDensitySource)} or a manager before resetting.", _owner);

            return false;
        }

        private static bool HoldsStoredEdits(IGeoChunk chunk) {
            using var enumerator = chunk.GeoChunkEngine.IGeoEditStore.ReadAllEdits().GetEnumerator();

            return enumerator.MoveNext();
        }

        private void ResetToProcedural(Vector3Int chunkCoordinate, IGeoChunk chunk, IDensitySource source) {
            chunk.GeoChunkEngine.IGeoEditStore.Clear();
            chunk.SetVoxels(CreateFillRequest(chunkCoordinate), source);
        }

        private void ValidateResetChunks(List<IGeoChunk> resetChunks) {
            if (resetChunks.Count == 0)
                return;

            var validatesPerFrame = ConfigBlob.Value.ValidatesPerFrame;

            for (var i = 0; i < resetChunks.Count; i += validatesPerFrame) {
                if (!TryConvertViewers(out var viewersVoxelSpace))
                    return;

                var batchSize = Mathf.Min(validatesPerFrame, resetChunks.Count - i);
                var batch = resetChunks.GetRange(i, batchSize);

                foreach (var chunk in batch)
                    ScheduleValidation(chunk, viewersVoxelSpace);

                CompleteValidationBatch(batch);
            }
        }

        private void ScheduleValidation(IGeoChunk chunk, NativeArray<ChunkViewer> viewersVoxelSpace) {
            if (!_validationHoldsEvents) {
                _validationHoldsEvents = true;
                HoldEvents();
            }

            try {
                chunk.ScheduleOctreeLodValidation(viewersVoxelSpace, _chunkInterest);
            }
            catch {
                EndValidationHold();

                throw;
            }
        }

        private void CompleteValidationBatch(List<IGeoChunk> batch) {
            try {
                Manager.CompleteAndApplyMarchingCubesJobs();

                foreach (var chunk in batch)
                    chunk.ReleaseLodValidation();
            }
            finally {
                EndValidationHold();
            }
        }

        private void EndValidationHold() {
            if (!_validationHoldsEvents)
                return;

            _validationHoldsEvents = false;
            ReleaseHeldEvents();
        }

        // Batches chunks into groups of ConfigBlob.Value.ValidatesPerFrame: schedule each chunk's
        // LOD validation (checkout + octree cascade, via GeoChunk.ScheduleOctreeLodValidation) but
        // don't complete or release any of them until the whole batch has been scheduled, so their
        // march jobs can actually run concurrently on worker threads via one shared Complete()
        // instead of one Complete() per chunk serializing everything.
        //
        // Fixed at exactly ValidatesPerFrame per batch, not "up to and including" - the old
        // (++count <= ValidatesPerFrame) check let one extra chunk through per batch, which was
        // harmless when each chunk released before the next was scheduled but would exhaust the
        // Validation pool now, since that pool is sized to exactly ValidatesPerFrame slots.
        public async Awaitable ValidateChunkLodsAsync() {
            var chunkList = ChunkDict.Keys.ToList();

            if (Manager == null)
                return;

            var validatesPerFrame = ConfigBlob.Value.ValidatesPerFrame;
            var scheduledChunks = new List<IGeoChunk>(validatesPerFrame);
            var viewersVoxelSpace = default(NativeArray<ChunkViewer>);

            foreach (var coord in chunkList) {
                if (!ChunkDict.TryGetValue(coord, out var chunk))
                    continue;

                if (!chunk.HasVoxelData())
                    continue;

                if (chunk.DensityRange.IsSkippable()) continue;

                if (scheduledChunks.Count == 0 && !TryConvertViewers(out viewersVoxelSpace))
                    return;

                ScheduleValidation(chunk, viewersVoxelSpace);
                scheduledChunks.Add(chunk);

                if (scheduledChunks.Count < validatesPerFrame)
                    continue;

                CompleteValidationBatch(scheduledChunks);
                scheduledChunks.Clear();

                await Awaitable.NextFrameAsync();

                // Re-check liveness after the yield, once per batch rather than once per chunk
                // (the old loop re-checked the manager singleton on every single chunk). Bails out
                // and abandons any remaining chunks in this call if the manager is gone.
                if (Manager == null || _isDisposed)
                    return;
            }

            // Flush a partial final batch that never reached validatesPerFrame.
            if (scheduledChunks.Count > 0)
                CompleteValidationBatch(scheduledChunks);
        }

        // Gathers every chunk's edits (via IGeoEditStore.ReadAllEdits, the interface's generic
        // walk-the-edits primitive - no assumption about which concrete IGeoEditStore each chunk
        // uses) into one packed byte[]. Chunks with zero edits are skipped entirely - most of a
        // large map is untouched, and there's nothing useful to persist for a chunk that's still
        // purely procedural. Payload layout: [SaveFormatVersion:int][ChunkSize:int][List<ChunkSaveEntry>].
        // ChunkSize is stamped so TryLoadFromByteArray can refuse a save whose stored voxel indices
        // wouldn't mean the same thing under the current chunk size, rather than silently
        // misinterpreting them.
        public byte[] SaveToByteArray() {
            var entries = new List<ChunkSaveEntry>();

            foreach (var kvp in ChunkDict) {
                var edits = new List<(int, VoxelData)>(kvp.Value.GeoChunkEngine.IGeoEditStore.ReadAllEdits());

                if (edits.Count == 0)
                    continue;

                entries.Add(new ChunkSaveEntry {
                    X = kvp.Key.x, Y = kvp.Key.y, Z = kvp.Key.z, Edits = edits
                });
            }

            ref var config = ref ConfigBlob.Value;
            var chunkSize = config.ChunkSize;

            return Packer.BuildPayload((ref Span<byte> buffer) => {
                Packer.WriteInt(ref buffer, SaveFormatVersion);
                Packer.WriteInt(ref buffer, chunkSize);
                Packer.PackList(ref buffer, entries);
            });
        }

        // Restores edits from a payload written by SaveToByteArray. Refuses to load (returns
        // false, logs) on a format-version or chunk-size mismatch, rather than risking silently
        // misinterpreted voxel indices.
        //
        // Applies edits in batches sized to the Edit pool's actual capacity (GetEditPoolCapacity),
        // NOT one giant batch across every saved chunk - a save file can easily touch more chunks
        // than a single realtime terraform action ever could (the Edit pool is sized around
        // terraform's apron fan-out, e.g. 8 for a fully 3D volume, not around "how many chunks a
        // save might contain"). Each restored chunk's IGeoEditStore.Write ultimately checks out an
        // Edit-pool slot (via ApplyVoxelEdits(isEdit: true)), so a single unbounded batch here would
        // hit the pool's exhaustion throw partway through a large load. Each batch is still wrapped
        // in GeoForgeManager.BeginEditBatch/EndEditBatch - same pattern DistributeVoxelEdits and the
        // job-based terraform commands use - so chunks within one batch still get their march jobs
        // scheduled together instead of completing one at a time.
        //
        // A saved coordinate with no loaded chunk (GetChunkByCoord returns null) logs a warning and
        // is skipped rather than failing the whole load - most likely means this was called before
        // every chunk finished spawning (see AllChunksReady()).
        public bool TryLoadFromByteArray(byte[] data) {
            if (data == null || data.Length == 0) {
                Debug.LogWarning("GeoVolume.TryLoadFromByteArray: empty payload.");

                return false;
            }

            _meshedChunks.Clear();

            ReadOnlySpan<byte> span = data;
            var version = Packer.ReadInt(ref span);

            if (version != SaveFormatVersion) {
                Debug.LogError(
                    $"GeoVolume.TryLoadFromByteArray: save format version mismatch ({version} vs " +
                    $"{SaveFormatVersion}) - refusing to load.");

                return false;
            }

            var savedChunkSize = Packer.ReadInt(ref span);
            ref var config = ref ConfigBlob.Value;

            if (savedChunkSize != config.ChunkSize) {
                Debug.LogError(
                    $"GeoVolume.TryLoadFromByteArray: chunk size mismatch (save={savedChunkSize}, " +
                    $"volume={config.ChunkSize}) - refusing to load, saved indices would be misinterpreted.");

                return false;
            }

            var entries = Packer.UnpackList<ChunkSaveEntry>(ref span);

            if (Manager == null) {
                Log.Error($"{_owner.name}: the volume engine holds no {nameof(GeoForgeManager)}, so nothing loads.",
                    _owner);

                return false;
            }

            var editPoolCapacity = Manager.GetEditPoolCapacity(config.ChunkSize);

            if (editPoolCapacity <= 0) {
                Debug.LogError(
                    "GeoVolume.TryLoadFromByteArray: Edit pool capacity is 0 for this chunk size - " +
                    "was RegisterVoxelVolume ever called for this volume? Refusing to load.");

                return false;
            }

            for (var i = 0; i < entries.Count; i += editPoolCapacity) {
                var batchSize = Mathf.Min(editPoolCapacity, entries.Count - i);

                Manager.BeginEditBatch();

                try {
                    for (var j = i; j < i + batchSize; j++) {
                        var entry = entries[j];
                        var coord = new Vector3Int(entry.X, entry.Y, entry.Z);
                        var chunk = GetChunkByCoord(coord);

                        if (chunk == null) {
                            Debug.LogWarning(
                                $"GeoVolume.TryLoadFromByteArray: no chunk loaded at {coord} - skipping " +
                                "its saved edits. Was this called before AllChunksReady()?");

                            continue;
                        }

                        chunk.GeoChunkEngine.IGeoEditStore.Write(entry.Edits);
                    }
                }
                finally {
                    Manager.EndEditBatch();
                }
            }

            return true;
        }

        public bool RegisterChunk(Vector3Int chunkCoord, IGeoChunk geoChunk) {
            if (geoChunk == null)
                return false;

            if (ChunkDict.TryAdd(chunkCoord, geoChunk)) return true;


            return false;
        }

        public T CreateChunk<T, TStore>(Vector3Int chunkCoord, GameObject chunkPrefab, TStore store)
            where T : class, IGeoChunk
            where TStore : IGeoEditStore {
            var worldChunkPos = ChunkWorldOrigin(chunkCoord);

            var chunkObj = Object.Instantiate(
                chunkPrefab,
                worldChunkPos,
                Transform.rotation,
                Transform
            );

            if (!chunkObj.TryGetComponent(out T chunk)) {
                Debug.LogError($"Chunk bakePrefab missing component of type {typeof(T).Name}");
                Object.Destroy(chunkObj); // Clean up failed instantiation

                return null;
            }

            chunk.InitializeGeoChunk(chunkCoord, store);

            return chunk;
        }

        public void UpdateVolumeOrigin() {
            foreach (var chunk in ChunkDict.Values)
                chunk.OnVolumeMovement();
        }

        public static Vector2[] ValidateLodRanges(Vector2[] lodRanges, VoxelVolumeConfig config) {
            // Ensure correct array length
            if (lodRanges == null || lodRanges.Length != config.levelsOfDetail)
                lodRanges = new Vector2[config.levelsOfDetail];

            var dist = 0f;

            for (var i = 0; i < lodRanges.Length; i++) {
                lodRanges[i].x = dist;

                lodRanges[i].y = Mathf.Max(lodRanges[i].y,
                    lodRanges[i].x + 2 * config.resolution * (config.cubesPerMarch << i));
                dist = lodRanges[i].y;
            }

            return lodRanges;
        }

        public bool IntersectsVolume(Bounds voxelBounds) {
            return _bounds.Intersects(voxelBounds);
        }

        private Bounds CalculateVolumeBounds() {
            ref var config = ref ConfigBlob.Value;

            var sizeInVoxels = new Vector3(
                config.SizeInChunks.x * config.ChunkSize,
                config.SizeInChunks.y * config.ChunkSize,
                config.SizeInChunks.z * config.ChunkSize
            );

            var center = Vector3.zero - config.Offset;

            return new Bounds(center, sizeInVoxels);
        }

        public (Vector3, Quaternion) SnapToGrid(Vector3 pos) {
            var localPos = Transform.InverseTransformPoint(pos);
            var resolution = ConfigBlob.Value.Resolution;

            var snappedLocal = resolution * new Vector3(
                Mathf.Round(localPos.x / resolution),
                Mathf.Round(localPos.y / resolution),
                Mathf.Round(localPos.z / resolution)
            );

            var snappedWorld = Transform.TransformPoint(snappedLocal);

            return (snappedWorld, Transform.rotation);
        }
    }
}
