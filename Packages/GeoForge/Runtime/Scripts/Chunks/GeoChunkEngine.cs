// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Sealed GeoForge Chunk Logic.
    ///     This class should only be touched externally by an IGeoChunk implementer.
    /// </summary>
    public sealed class GeoChunkEngine : IDisposable {
        private readonly IGeoChunk _implementer;

        // Addresses (lod, localPosition) of octree nodes that produced zero triangles the last
        // time they were marched, and haven't been invalidated since. Presence in the set means
        // "known empty, safe to skip re-marching"; absence means "unknown" (never marched at this
        // address since the last edit, or it produced real geometry) - not "known non-empty" -
        // so the correct default on a miss is to march it, never to assume it has mesh.
        //
        // A HashSet rather than a Dictionary<_, bool> because there's never a reason to store a
        // false/non-empty entry - the "not present" state already means exactly that, for free.
        //
        // Keyed by exact (lod, localPosition) address, not by lod alone or by voxel region:
        // marching the same address again would always reproduce the same result off unchanged
        // data, which is what makes this safe to reuse - it says nothing about a *different*
        // address (a finer or coarser subdivision covering the same physical space), which needs
        // its own march and its own cache entry.
        //
        // Wiped wholesale on any edit to this chunk (see ApplyVoxelEdits) rather than only
        // invalidating addresses that overlap the edited bounds - simplest correct thing to do,
        // and a chunk that was just edited will have most of its cache invalidated by the edit's
        // meshing pass anyway, so partial invalidation would save little for real complexity.
        private readonly HashSet<(int Lod, Vector3Int LocalPosition)> _knownEmptyOctreeAddresses = new();

        private readonly GeoForgeManager _mcManager;
        private readonly int[] _presentedLeavesPerLevel;

        private BoundsInt _bounds;
        private DensityRange _densityRange;
        private bool _isDisposed;
        private NativeList<SparseVoxelData> _sparseVoxels;
        private VoxelOverrides _voxelOverrides;

        public GeoChunkEngine(
            IGeoChunk implementer, Transform transform, IGeoEditStore iGeoEditStore, Vector3Int chunkCoord) {
            _implementer = implementer;
            Transform = transform;
            IGeoEditStore = iGeoEditStore;
            IGeoEditStore.OnGeoEditChanged += HandleResolvedVoxelEdits;
            ChunkCoord = chunkCoord;
            ParentGeoVolume = Transform.GetComponentInParent<IGeoVolume>();
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            ChunkCoord = chunkCoord;
            var voxelMin = chunkCoord * config.ChunkSize;
            _bounds = new BoundsInt(voxelMin, config.ChunkSize * Vector3Int.one);
            Transform.gameObject.name = chunkCoord.ToString();
            _mcManager = ParentGeoVolume.GeoVolumeEngine.Manager;
            _voxelOverrides = new VoxelOverrides();
            _presentedLeavesPerLevel = new int[config.LevelsOfDetail + 1];
        }

        public IGeoEditStore IGeoEditStore { get; }

        public Vector3Int ChunkCoord { get; }

        public DensityRange DensityRange => _densityRange;
        public BoundsInt Bounds => _bounds;

        public Transform Transform { get; }

        public OctreeNode RootNode { get; private set; }

        public IGeoVolume ParentGeoVolume { get; }

        /// <summary>
        ///     Whether this chunk's voxel data has been set at least once (via SetVoxels).
        /// </summary>
        public bool VoxelsReady { get; private set; }

        /// <summary>
        ///     Whether this chunk's mesh is ready - see SetMeshReady for exactly when this flips.
        /// </summary>
        public bool MeshReady { get; private set; }

        internal int FinestBuiltLevel { get; private set; } = ChunkResidency.NoBuiltLevel;

        public void Dispose() {
            // Idempotent by design, not by accident: everything this touches downstream (event
            // -=, Dictionary.Remove, OctreeNode.Dispose's own IsCreated guards) happens to also
            // be safe to call twice today, but that's a chain of coincidences a future change
            // could break. GeoVolume.Dispose() calls this explicitly and then destroys the chunk
            // GameObject, which can re-enter here via SimpleGeoChunk.OnDestroy() - this guard is
            // what actually makes that safe, rather than relying on every downstream piece to
            // keep guarding itself correctly forever.
            if (_isDisposed)
                return;

            _isDisposed = true;

            IGeoEditStore.OnGeoEditChanged -= HandleResolvedVoxelEdits;
            ReleaseResidency();
            ParentGeoVolume.GeoVolumeEngine.ChunkDict.Remove(ChunkCoord);
            RootNode?.Dispose();

            if (_sparseVoxels.IsCreated)
                _sparseVoxels.Dispose();
        }

        public void SetOverrides(VoxelOverrides overrides) {
            _voxelOverrides = overrides;
        }

        public bool HasOverrides() {
            if (_voxelOverrides == null || !_voxelOverrides.HasAnyOverrides)
                return false;

            return true;
        }

        private bool ApplyOverrides(NativeArray<VoxelData> voxels) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            _voxelOverrides.CopyToNativeHashMaps(
                out var xOverrides,
                out var yOverrides,
                out var zOverrides,
                out var pointOverrides
            );

            var hasOverridesArray = new NativeArray<bool>(1, Allocator.TempJob);
            hasOverridesArray[0] = false;

            var job = new ApplyBoundaryOverridesJob {
                voxelArray = voxels,
                xOverrides = xOverrides,
                yOverrides = yOverrides,
                zOverrides = zOverrides,
                pointOverrides = pointOverrides,
                chunkDataAreaSize = config.ChunkDataAreaSize,
                chunkDataWidthSize = config.ChunkDataWidthSize,
                hasOverrides = hasOverridesArray
            };

            var jobHandle = job.Schedule(voxels.Length, 64);
            jobHandle.Complete();

            var hasOverriddenVoxels = hasOverridesArray[0];

            xOverrides.Dispose();
            yOverrides.Dispose();
            zOverrides.Dispose();
            pointOverrides.Dispose();
            hasOverridesArray.Dispose();

            return hasOverriddenVoxels;
        }

        // Applies the edit and schedules its octree/march validation. If GeoForgeManager is
        // currently batching (see BeginEditBatch/EndEditBatch), this chunk registers itself for a
        // shared Complete()+release later instead of completing/releasing right here - lets
        // DistributeVoxelEdits schedule every affected chunk's march jobs before any of them block,
        // so they can actually run concurrently on worker threads. Outside a batch (any other
        // caller of IGeoChunk.PassVoxelEdits), this stays fully synchronous, same as before.
        public void HandleResolvedVoxelEdits(List<(int, VoxelData)> newVoxelChanges) {
            if (!ApplyVoxelEdits(newVoxelChanges, out var editBounds))
                return;

            var volumeEngine = ParentGeoVolume.GeoVolumeEngine;
            volumeEngine.HoldEvents();

            try {
                ScheduleOctreeEditValidation(editBounds);

                _mcManager.CompleteAndApplyMarchingCubesJobs();
                _mcManager.ReleaseVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this, true);

                var chunkOrigin = ChunkCoord * ParentGeoVolume.ConfigBlob.Value.ChunkSize;
                volumeEngine.RaiseChanged(ChunkCoord,
                    new BoundsInt(editBounds.position + chunkOrigin, editBounds.size));
            }
            finally {
                volumeEngine.ReleaseHeldEvents();
            }
        }

        public void SetVoxels(in ChunkFillRequest request, IDensitySource source) {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var dataWidth = ParentGeoVolume.ConfigBlob.Value.ChunkDataWidthSize;

            if (request.DataWidth != dataWidth)
                throw new ArgumentException(
                    $"A fill of data width {request.DataWidth} does not fit a chunk of data width {dataWidth}.",
                    nameof(request));

            SetVoxels(GeoForgeKit.Generate(source, request, Allocator.Persistent));
        }

        public void SetVoxels(NativeArray<VoxelData> voxels) {
            if (!voxels.IsCreated) {
                Debug.LogError(
                    "_sparseVoxels being initialized with native array that has not been created for chunkCoord " +
                    $"{ChunkCoord}.");

                return;
            }

            VoxelsReady = false;
            MeshReady = false;
            FinestBuiltLevel = ChunkResidency.NoBuiltLevel;

            if (_sparseVoxels.IsCreated) _sparseVoxels.Dispose();
            _sparseVoxels = new NativeList<SparseVoxelData>(Allocator.Persistent);

            if (HasOverrides())
                ApplyOverrides(voxels);

            var densityRangeArray = new NativeArray<DensityRange>(1, Allocator.TempJob);

            new DenseToSparseVoxelDataJob {
                Voxels = voxels,
                SparseVoxels = _sparseVoxels,
                DensityRange = densityRangeArray
            }.Schedule().Complete();

            _densityRange = densityRangeArray[0];
            densityRangeArray.Dispose();
            if (voxels.IsCreated)
                voxels.Dispose();

            // SetVoxels rewrites _sparseVoxels directly from the caller-supplied array, bypassing the
            // GetOrUnpack -> mutate -> Pack path entirely - which means neither pool's cached residency
            // for this chunk gets refreshed the normal way (Pack's own SyncOtherPoolResidency step never
            // runs here). If this chunk happens to already be cached in either pool from before this call
            // (e.g. the background LOD validation loop touched it earlier), that cached array is now
            // stale relative to what was just written. Evict it from both pools unconditionally, so the
            // next checkout anywhere is forced to do a real unpack from the now-current _sparseVoxels
            // instead of silently handing back the old data via the "already resident" fast path.
            _mcManager.InvalidateChunkResidency(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this);

            if (RootNode != null) RootNode.Dispose();
            Array.Clear(_presentedLeavesPerLevel, 0, _presentedLeavesPerLevel.Length);

            RootNode = new OctreeNode(Vector3Int.zero, ParentGeoVolume.ConfigBlob.Value.LevelsOfDetail, _implementer,
                ParentGeoVolume);

            VoxelsReady = true;
            RaiseResidency(ChunkResidencyState.Resident);
            ParentGeoVolume.GeoVolumeEngine?.RaiseChanged(ChunkCoord,
                new BoundsInt(ChunkCoord * ParentGeoVolume.ConfigBlob.Value.ChunkSize,
                    Vector3Int.one * ParentGeoVolume.ConfigBlob.Value.ChunkDataWidthSize));

            // A skippable density range means this chunk will never produce a leaf/mesh at all -
            // OctreeNode.ValidateOctreeLods early-returns on DensityRange.IsSkippable() before it
            // ever marches anything, so ReleaseLodValidation's normal "a real march just happened"
            // signal would never fire for this chunk. Fire the mesh-ready signal here instead, so a
            // consumer waiting on MeshReady doesn't wait forever for a chunk that has nothing to mesh.
            if (_densityRange.IsSkippable())
                SetMeshReady();
        }

        public bool ApplyVoxelEdits(
            List<(int, VoxelData)> voxelChanges, out BoundsInt editBounds, BoundsInt existingEditBounds = default) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            var voxelArray = GetVoxelDataArray(true);

            var hasEdits = false;
            editBounds = existingEditBounds;

            foreach (var voxelChange in voxelChanges) {
                var index = voxelChange.Item1;

                GfStaticHelper.IndexToInt3(index, config.ChunkDataAreaSize, config.ChunkDataWidthSize, out var x,
                    out var y, out var z);
                var voxelPos = new Vector3Int(x, y, z);

                if (_voxelOverrides.HasOverride(voxelPos))
                    continue;

                voxelArray[index] =
                    VoxelData.CreateImmature(voxelChange.Item2.Density, voxelChange.Item2.MaterialIndex);

                if (!hasEdits) {
                    editBounds = new BoundsInt(voxelPos, Vector3Int.one);
                    hasEdits = true;
                }
                else {
                    var min = Vector3Int.Min(editBounds.min, voxelPos);
                    var max = Vector3Int.Max(editBounds.max, voxelPos + Vector3Int.one);
                    editBounds = new BoundsInt(min, max - min);
                }

                // DensityRange is a struct - the public DensityRange property only has a getter, so
                // calling Encapsulate through it (as this used to) mutates a throwaway copy and never
                // reaches _densityRange. Mutate the field directly since we're inside GeoChunk itself.
                _densityRange.Encapsulate(voxelChange.Item2.Density);
            }

            if (hasEdits) {
                _mcManager.PackVoxelArray(config.ChunkSize, this, true, editBounds);

                // Any cached "empty" result may no longer hold once the underlying voxel data has
                // changed - wipe the whole cache rather than figuring out which addresses actually
                // overlap editBounds. See the field's own comment for why whole-chunk invalidation
                // is the right tradeoff here.
                _knownEmptyOctreeAddresses.Clear();
            }

            _mcManager.ReleaseVoxelArray(config.ChunkSize, this, true);

            return hasEdits;
        }

        public void OnVolumeMovement() {
            RootNode?.ValidateMaterial();
        }

        // NOTE (David): public signature change - now requires isEdit. I've only verified the call
        // sites inside GeoChunk.cs/GeoForgeManager.*.cs; if anything outside this file calls
        // GetVoxelDataArray(), it'll need a grep-and-fix pass.
        public NativeArray<VoxelData> GetVoxelDataArray(bool isEdit) {
            return _mcManager.GetOrUnpackVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this,
                _sparseVoxels, isEdit);
        }

        public NativeList<SparseVoxelData> GetsSparseVoxelData() {
            return _sparseVoxels.IsCreated ? _sparseVoxels : new NativeList<SparseVoxelData>();
        }


        internal void UpdateVoxelData(NativeList<SparseVoxelData> voxels, DensityRange densityRange) {
            if (!_sparseVoxels.IsCreated)
                return;

            _sparseVoxels.Clear();
            _sparseVoxels.CopyFrom(voxels);
            _densityRange = densityRange;
        }

        public void BroadcastNewLeafAcrossChunks(OctreeNode newLeaf, Vector3Int pos, int index) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            var worldVoxelPos = pos + ChunkCoord * config.ChunkSize;

            if (_bounds.Contains(worldVoxelPos)) {
                RootNode?.ValidateTransition(newLeaf, pos, GfStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = GfStaticHelper.GetNeighborCoord(index, ChunkCoord);
            var neighborChunk = ParentGeoVolume.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            var neighborLocalPos = worldVoxelPos - neighborCoord * config.ChunkSize;
            neighborChunk.BroadcastNewLeafAcrossChunks(newLeaf, neighborLocalPos, index);
        }

        public void BroadcastReleasedLeafAcrossChunks(Vector3Int pos, int index) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            var worldVoxelPos = pos + ChunkCoord * config.ChunkSize;

            if (_bounds.Contains(worldVoxelPos)) {
                RootNode?.ClearTransitionTowardReleasedNeighbor(pos, GfStaticHelper.GetTransitionFaceMask(index));

                return;
            }

            var neighborCoord = GfStaticHelper.GetNeighborCoord(index, ChunkCoord);
            var neighborChunk = ParentGeoVolume.GetChunkByCoord(neighborCoord);

            if (neighborChunk == null)
                return;

            var neighborLocalPos = worldVoxelPos - neighborCoord * config.ChunkSize;
            neighborChunk.BroadcastReleasedLeafAcrossChunks(neighborLocalPos, index);
        }

        public VoxelData GetVoxelData(int index) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;

            if (_mcManager.TryGetResidentVoxelArray(config.ChunkSize, this, out var denseVoxels))
                return denseVoxels[index];

            return GetSparseVoxelData(index, config.ChunkDataVolumeSize);
        }

        internal SurfaceCase ComputeSurfaceCase(Vector3Int cellOrigin, ref McTablesBlobAsset tables) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            var chunkSpaceOrigin = cellOrigin - ChunkCoord * config.ChunkSize;
            var cellPosition = new int3(chunkSpaceOrigin.x, chunkSpaceOrigin.y, chunkSpaceOrigin.z);
            var corners = new FixedList32Bytes<VoxelData>();

            if (_mcManager.TryGetResidentVoxelArray(config.ChunkSize, this, out var denseVoxels)) {
                var denseCaseCode = GfMarchHelper.GatherRegularCornersAndComputeCaseCode(denseVoxels, ref tables,
                    cellPosition, 0, 1, config.ChunkDataAreaSize, config.ChunkDataWidthSize, ref corners);

                return SurfaceCases.FromCorners(cellOrigin, denseCaseCode, corners);
            }

            var caseCode = 0;

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++) {
                var index = GfMarchHelper.RegularCornerIndex(ref tables, cellPosition, 0, 1, config.ChunkDataAreaSize,
                    config.ChunkDataWidthSize, corner);
                corners.Add(GetSparseVoxelData(index, config.ChunkDataVolumeSize));
                caseCode |= GfMarchHelper.CaseCodeBit(corners[corner], corner);
            }

            return SurfaceCases.FromCorners(cellOrigin, (byte)caseCode, corners);
        }

        private VoxelData GetSparseVoxelData(int index, int chunkDataVolumeSize) {
            return _sparseVoxels[GfStaticHelper.BinarySearchVoxelData(index, chunkDataVolumeSize, _sparseVoxels)].Voxel;
        }

        public VoxelData GetVoxelDataFromVoxelPosition(Vector3Int position) {
            ref var config = ref ParentGeoVolume.ConfigBlob.Value;
            var chunkSpacePosition = position - ChunkCoord * config.ChunkSize;

            var index = GfStaticHelper.Coord3DToIndex(
                chunkSpacePosition.x,
                chunkSpacePosition.y,
                chunkSpacePosition.z,
                config.ChunkDataAreaSize,
                config.ChunkDataWidthSize
            );

            return GetVoxelData(index);
        }

        public bool HasVoxelData() {
            return _sparseVoxels.IsCreated;
        }

        // Whether the octree node at this exact address was found empty (zero triangles) the last
        // time it was marched, and hasn't been invalidated by an edit since. A miss (false) means
        // "unknown" - never marched at this address since the last edit, or it had real geometry -
        // never "known non-empty", so callers must treat a miss as "go ahead and march it", not as
        // proof of anything.
        public bool IsKnownEmpty(int lod, Vector3Int localPosition) {
            return _knownEmptyOctreeAddresses.Contains((lod, localPosition));
        }

        // Records that the octree node at this address marched to zero triangles. Overwriting an
        // existing entry is a no-op (HashSet.Add just returns false) - fine, since re-marching the
        // same address always reproduces the same result off unchanged data.
        public void MarkKnownEmpty(int lod, Vector3Int localPosition) {
            _knownEmptyOctreeAddresses.Add((lod, localPosition));
        }

        // Explicit clear for callers that need to invalidate outside of an edit (none exist yet -
        // ApplyVoxelEdits above handles the normal case directly). Kept public in case that
        // changes; safe to call even when the cache is already empty.
        public void ClearKnownEmptyOctreeAddresses() {
            _knownEmptyOctreeAddresses.Clear();
        }

        // Schedule-only half of octree edit validation: cascades the edit through the octree,
        // scheduling any resulting march/transition jobs via GeoForgeManager.RegisterMarchJob/
        // RegisterTransitionJob - but does NOT complete or release anything. See
        // HandleResolvedVoxelEdits for the caller and the batching/non-batching split.
        public void ScheduleOctreeEditValidation(BoundsInt bounds) {
            if (!_sparseVoxels.IsCreated)
                return;

            RootNode?.ValidateOctreeEdits(bounds, GetVoxelDataArray(true));
        }

        // Schedule-only half: checks out the Validation-pool slot and cascades the LOD check
        // through the octree, scheduling any resulting march/transition jobs - but does NOT
        // complete those jobs or release the checkout. Used by GeoVolume.ValidateChunkLodsAsync to
        // batch up to ValidatesPerFrame chunks' worth of scheduling before completing once, so
        // their march jobs can actually run concurrently instead of one Complete() serializing
        // each chunk before the next one is even scheduled.
        //
        // Caller MUST NOT call ReleaseLodValidation for this chunk until AFTER
        // CompleteAndApplyMarchingCubesJobs() has run for the whole batch - releasing any earlier
        // would let another chunk's LRU claim on the Validation pool overwrite this array while a
        // still-pending march job on a worker thread is reading it.
        public void ScheduleOctreeLodValidation(NativeArray<ChunkViewer> viewersVoxelSpace, IChunkInterest interest) {
            if (!_sparseVoxels.IsCreated)
                return;

            RootNode.ValidateOctreeLods(viewersVoxelSpace.AsReadOnlySpan(), interest, GetVoxelDataArray(false));
        }

        // Releases this chunk's Validation-pool checkout. Only valid to call after
        // CompleteAndApplyMarchingCubesJobs() has run for whatever batch this chunk's
        // ScheduleOctreeLodValidation call was part of.
        public void ReleaseLodValidation() {
            _mcManager.ReleaseVoxelArray(ParentGeoVolume.ConfigBlob.Value.ChunkSize, this, false);
            SetMeshReady();
        }

        internal void HandleLeafPresented(int levelOfDetail) {
            _presentedLeavesPerLevel[levelOfDetail]++;
            RefreshFinestBuiltLevel();
        }

        internal void HandleLeafReleased(int levelOfDetail) {
            if (_isDisposed || !VoxelsReady)
                return;

            _presentedLeavesPerLevel[levelOfDetail]--;
            RefreshFinestBuiltLevel();
        }

        private void RefreshFinestBuiltLevel() {
            var finestBuiltLevel = Array.FindIndex(_presentedLeavesPerLevel, count => count > 0);

            if (finestBuiltLevel == FinestBuiltLevel)
                return;

            FinestBuiltLevel = finestBuiltLevel;
            RaiseResidency(ChunkResidencyState.Resident);
        }

        internal bool HasEdits() {
            return IGeoEditStore.HasEdits;
        }

        private void ReleaseResidency() {
            if (!VoxelsReady)
                return;

            VoxelsReady = false;
            FinestBuiltLevel = ChunkResidency.NoBuiltLevel;
            RaiseResidency(ChunkResidencyState.Released);
        }

        private void RaiseResidency(ChunkResidencyState state) {
            ParentGeoVolume.GeoVolumeEngine?.RaiseResidencyChanged(new ChunkResidency(ChunkCoord, state,
                FinestBuiltLevel));
        }

        // Idempotent: mesh-ready should only ever notify once per SetVoxels call - both SetVoxels
        // itself (skippable-density shortcut) and ReleaseLodValidation (a real march completed)
        // can reach here, and only the first of whichever runs first should actually notify.
        private void SetMeshReady() {
            if (MeshReady)
                return;

            MeshReady = true;
            _implementer.HandleMeshReady();
            ParentGeoVolume.GeoVolumeEngine.HandleChunkMeshReady(_implementer);
        }
    }
}