// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge {
    /// <summary>
    ///     Recursively Subdividing OctreeNode to subdivide a GeoChunk at varying LODs.
    ///     Either it has 8 children, or it has an Octree leaf (representing actual terrain).
    /// </summary>
    public sealed class OctreeNode : IDisposable {
        private readonly IGeoChunk _geoChunk;
        private readonly GeoForgeManager _gfManager;
        private readonly Vector3Int _localPosition;
        private readonly int _lod;
        private readonly Vector3 _centreVoxelSpace;
        private readonly IGeoVolume _parentGeoVolume;

        private NativeList<int> _allTransitionTriangles;
        private BoundsInt _boundsVoxel;
        private bool _broadcastsNewLeaf;
        private Vector3Int[] _cachedNeighborPositions;
        private OctreeNode[] _children;
        private IChunkCollisionLeaf _collisionLeaf;
        private IChunkCollisionPresenter _collisionLeafPresenter;
        private NativeList<int> _filteredTransitionTriangles;
        private IChunkLeaf _leaf;
        private bool _leafInitialized;
        private bool _meshPresented;
        private bool _awaitsPresenter;
        private IChunkPresenter _leafPresenter;
        private IChunkCollisionPresenter _refusingCollisionPresenter;
        private IChunkPresenter _refusingPresenter;
        private Mesh _mesh;
        private bool _transitionDirtyFlag;
        private int _transitionMask;
        private Mesh _transitionMesh;
        private NativeArray<int2> _transitionRanges;

        internal OctreeNode(Vector3Int localPosition, int lod, IGeoChunk geoChunk, IGeoVolume parentGeoVolume) {
            _parentGeoVolume = parentGeoVolume;
            _localPosition = localPosition;
            _lod = lod;
            _geoChunk = geoChunk;

            _gfManager = _parentGeoVolume.GeoVolumeEngine.Manager;
            var octreeSizeVoxels = 3 + (_parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << _lod);
            _boundsVoxel = new BoundsInt(_localPosition, Vector3Int.one * octreeSizeVoxels);
            _centreVoxelSpace = Center + geoChunk.ChunkCoord * _parentGeoVolume.ConfigBlob.Value.ChunkSize;
        }

        private Vector3 Center => (_boundsVoxel.min + _boundsVoxel.max - Vector3.one) * 0.5f;

        private bool IsLeaf => _children == null;

        private bool HasAnyLeaf => _leaf != null || _collisionLeaf != null;

        internal Mesh CollisionMesh => _collisionLeaf != null ? _mesh : null;

        internal int LevelOfDetail => _lod;

        public void Dispose() {
            if (_children != null) {
                for (var i = 0; i < 8; i++) {
                    _children[i].Dispose();
                    _children[i] = null;
                }

                _children = null;
            }

            ReleaseLeafObjects();

            if (_allTransitionTriangles.IsCreated)
                _allTransitionTriangles.Dispose();

            if (_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles.Dispose();

            if (_transitionRanges.IsCreated)
                _transitionRanges.Dispose();

            _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
        }

        private void Subdivide() {
            if (_children != null)
                return;

            ReleaseLeafObjects();
            _leafInitialized = false;

            _children = new OctreeNode[8];
            var childLod = _lod - 1;
            var childSize = _parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << childLod;

            for (var i = 0; i < 8; i++) {
                var offset = new Vector3Int(
                    (i & 1) == 0 ? 0 : childSize,
                    (i & 2) == 0 ? 0 : childSize,
                    (i & 4) == 0 ? 0 : childSize
                );

                _children[i] = new OctreeNode(_localPosition + offset, childLod, _geoChunk, _parentGeoVolume);
            }
        }

        // Whether all 8 finer-LOD addresses this node would subdivide into (see Subdivide's own
        // offset math, mirrored exactly here) are already known-empty in the cache. Doesn't
        // require those child OctreeNode objects to actually exist - it's a pure address lookup,
        // so it works whether this subtree was ever subdivided or not.
        //
        // Sound in the bottom-up direction only: if every finer address genuinely has no
        // geometry, this coarser address can't have any either, since the finer addresses
        // partition the exact same voxel region this node covers. This is never used to justify
        // skipping something FINER than what was actually evaluated (that direction is unsound -
        // see the earlier fix to ValidateOctreeLods, which never lets a cache hit skip the
        // Subdivide()/recursion decision). Only meaningful when _lod > 0; lod 0 has no finer
        // children to check, so it always returns false there and falls through to a real march.
        private bool AreAllChildAddressesKnownEmpty() {
            if (_lod == 0) return false;

            var childLod = _lod - 1;
            var childSize = _parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << childLod;

            for (var i = 0; i < 8; i++) {
                var offset = new Vector3Int(
                    (i & 1) == 0 ? 0 : childSize,
                    (i & 2) == 0 ? 0 : childSize,
                    (i & 4) == 0 ? 0 : childSize
                );

                if (!_geoChunk.IsKnownEmpty(childLod, _localPosition + offset))
                    return false;
            }

            return true;
        }

        private void ReleaseLeafObjects() {
            if (!HasAnyLeaf)
                return;

            var wasPresented = _meshPresented;

            try {
                _leafPresenter?.Release(_leaf);
            }
            finally {
                try {
                    _collisionLeafPresenter?.Release(_collisionLeaf);
                }
                finally {
                    _leaf = null;
                    _leafPresenter = null;
                    _collisionLeaf = null;
                    _collisionLeafPresenter = null;
                    _broadcastsNewLeaf = false;
                    _meshPresented = false;
                    DestroyMesh(ref _transitionMesh);
                    DestroyMesh(ref _mesh);
                    _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
                    _transitionDirtyFlag = false;

                    if (wasPresented)
                        _geoChunk.GeoChunkEngine.HandleLeafReleased(_lod);
                }
            }
        }

        private void Withdraw() {
            if (_children != null) {
                for (var i = 0; i < 8; i++) {
                    _children[i].Withdraw();
                    _children[i].Dispose();
                }

                _children = null;
            }

            var hadDrawnLeaf = _leaf != null;
            ReleaseLeafObjects();
            _leafInitialized = false;
            _awaitsPresenter = false;

            if (hadDrawnLeaf)
                BroadcastReleasedLeaf();
        }

        private static void DestroyMesh(ref Mesh mesh) {
            if (mesh != null) {
                if (Application.isPlaying)
                    Object.Destroy(mesh);
                else
                    Object.DestroyImmediate(mesh);
            }

            mesh = null;
        }

        internal void ValidateMaterial() {
            if (HasAnyLeaf) {
                SetMaterialOrigin();

                return;
            }

            if (_children == null) return;

            foreach (var child in _children) child.ValidateMaterial();
        }

        private bool HasPresenterToAsk() {
            if (_leaf == null) {
                var presenter = _gfManager.Presenter;

                if (presenter != null && presenter != _refusingPresenter)
                    return true;
            }

            if (_collisionLeaf != null)
                return false;

            var collisionPresenter = _gfManager.CollisionPresenter;

            return collisionPresenter != null && collisionPresenter != _refusingCollisionPresenter;
        }

        private void SetMaterialOrigin() {
            var worldToLocal = _parentGeoVolume.VolumeTransform.worldToLocalMatrix;
            _leaf?.SetVolumeMatrix(worldToLocal);
            _collisionLeaf?.SetVolumeMatrix(worldToLocal);
        }

        internal void ValidateOctreeLods(
            ReadOnlySpan<ChunkViewer> viewersVoxelSpace, IChunkInterest interest, NativeArray<VoxelData> voxelArray) {
            var levelOfDetailRanges = _parentGeoVolume.GeoVolumeEngine.ReadLevelOfDetailRanges();
            var chunkHasEdits = _geoChunk.GeoChunkEngine.HasEdits();
            ValidateOctreeLods(viewersVoxelSpace, interest, voxelArray, levelOfDetailRanges, chunkHasEdits);
        }

        private void ValidateOctreeLods(
            ReadOnlySpan<ChunkViewer> viewersVoxelSpace,
            IChunkInterest interest,
            NativeArray<VoxelData> voxelArray,
            NativeArray<Vector2>.ReadOnly levelOfDetailRanges,
            bool chunkHasEdits) {
            var request = new ChunkInterestRequest(new Bounds(_centreVoxelSpace, _boundsVoxel.size - Vector3Int.one),
                _parentGeoVolume.ConfigBlob.Value.Resolution, levelOfDetailRanges, chunkHasEdits);
            var targetLod = interest.DetailLevel(request, viewersVoxelSpace);

            if (_geoChunk.DensityRange.IsSkippable()) return;

            if (targetLod < 0) {
                Withdraw();

                return;
            }

            targetLod = Math.Min(targetLod, _parentGeoVolume.ConfigBlob.Value.LevelsOfDetail);

            if (_lod <= targetLod) {
                if (_geoChunk.IsKnownEmpty(_lod, _localPosition))
                    return;

                if (AreAllChildAddressesKnownEmpty()) {
                    _geoChunk.MarkKnownEmpty(_lod, _localPosition);
                    return;
                }

                if (!_leafInitialized)
                    MakeLeaf(voxelArray);
                else if (_awaitsPresenter && HasPresenterToAsk()) {
                    _awaitsPresenter = false;
                    MarchAndMesh(voxelArray);
                }

                return;
            }

            Subdivide();

            foreach (var child in _children)
                child.ValidateOctreeLods(viewersVoxelSpace, interest, voxelArray, levelOfDetailRanges, chunkHasEdits);
        }

        internal void ValidateOctreeEdits(BoundsInt boundsVoxel, NativeArray<VoxelData> voxelArray) {
            if (!BoundsIntersect(_boundsVoxel, boundsVoxel)) return;

            if (IsLeaf) {
                UpdateLeaf(voxelArray);

                return;
            }

            foreach (var child in _children)
                child.ValidateOctreeEdits(boundsVoxel, voxelArray);
        }

        private bool BoundsIntersect(BoundsInt a, BoundsInt b) {
            if (a.max.x < b.min.x || a.min.x > b.max.x) return false;
            if (a.max.y < b.min.y || a.min.y > b.max.y) return false;
            if (a.max.z < b.min.z || a.min.z > b.max.z) return false;

            return true;
        }

        internal void ValidateTransition(
            OctreeNode neighbor, Vector3Int voxelPos, GfStaticHelper.TransitionFaceMask faceMask) {
            if (!_boundsVoxel.Contains(voxelPos))
                return;

            if (!IsLeaf) {
                foreach (var child in _children)
                    child.ValidateTransition(neighbor, voxelPos, faceMask);

                return;
            }

            if (_lod > neighbor._lod) {
                UpdateTransitionMask(GetOppositeTransition(faceMask), true);
                neighbor.UpdateTransitionMask(faceMask, false);

                return;
            }

            if (_lod == neighbor._lod) {
                UpdateTransitionMask(GetOppositeTransition(faceMask), false);
                neighbor.UpdateTransitionMask(faceMask, false);

                return;
            }

            UpdateTransitionMask(GetOppositeTransition(faceMask), false);
            neighbor.UpdateTransitionMask(faceMask, true);
        }

        private void MarchAndMesh(NativeArray<VoxelData> voxelArray) {
            if (!_gfManager.TryGetRenderingProfile(out var profile))
                return;

            var start = new int3(_localPosition.x, _localPosition.y, _localPosition.z);

            // Rented from GeoForgeManager's buffer pool instead of freshly allocated - avoids the
            // per-march Allocator.Persistent churn (up to 6 fresh NativeCollections per call
            // previously). Returned to the pool (not disposed) once results are applied - see
            // GeoForgeManager.CompleteAndApplyMarchingCubesJobs / ReturnMarchBuffers.
            var vertices = _gfManager.RentVertexBuffer();
            var triangles = _gfManager.RentTriangleBuffer();
            var computedBounds = _gfManager.RentBoundsBuffer();

            var jobHandle = profile.ScheduleMarchingCubes(
                _gfManager.McTablesBlob,
                _parentGeoVolume.ConfigBlob,
                voxelArray,
                vertices,
                triangles,
                computedBounds,
                _lod,
                start);

            _gfManager.RegisterMarchJob(this, jobHandle, vertices, triangles, computedBounds, _geoChunk.ChunkCoord);

            if (_lod != 0) {
                var transitionMeshingVertexData = _gfManager.RentVertexBuffer();
                var transitionTriangles = _gfManager.RentTriangleBuffer();
                var transitionRanges = _gfManager.RentRangesBuffer();

                var transitionJobHandle = profile.ScheduleTransitionMarchingCubes(
                    _gfManager.McTablesBlob,
                    _parentGeoVolume.ConfigBlob,
                    voxelArray,
                    transitionMeshingVertexData,
                    transitionTriangles,
                    transitionRanges,
                    _lod,
                    start);

                _gfManager.RegisterTransitionJob(this,
                    transitionJobHandle,
                    transitionMeshingVertexData,
                    transitionTriangles,
                    transitionRanges,
                    _geoChunk.ChunkCoord);
            }
        }

        private void MakeLeaf(NativeArray<VoxelData> voxelArray) {
            if (!IsLeaf) {
                for (var i = 0; i < 8; i++) _children[i]?.Dispose();
                _children = null;
            }

            _leafInitialized = true;

            // GameObject/Mesh creation, material setup, and the new-leaf broadcast are all
            // deferred to ApplyMarchResults now - only once we actually know the march produced
            // triangles does any of that need to happen at all.
            MarchAndMesh(voxelArray);
        }

        private void UpdateLeaf(NativeArray<VoxelData> voxelArray) {
            if (!_leafInitialized)
                return;

            MarchAndMesh(voxelArray);
        }

        private void BuildTransitions() {
            DestroyMesh(ref _transitionMesh);

            _transitionMesh = new Mesh();
            _transitionMesh.MarkDynamic();
            _leaf.SetTransitionMesh(_transitionMesh);

            _transitionMask = 0;

            if (!_allTransitionTriangles.IsCreated)
                _allTransitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_filteredTransitionTriangles.IsCreated)
                _filteredTransitionTriangles = new NativeList<int>(Allocator.Persistent);

            if (!_transitionRanges.IsCreated)
                _transitionRanges = new NativeArray<int2>(6, Allocator.Persistent);
        }

        private bool TryBuildLeaf() {
            if (_leaf != null)
                return false;

            var presenter = _gfManager.Presenter;

            if (presenter == null || presenter == _refusingPresenter)
                return false;

            var leaf = presenter.Acquire(_geoChunk.GeoChunkEngine.Transform, _lod);

            if (leaf == null) {
                _refusingPresenter = presenter;

                return false;
            }

            _refusingPresenter = null;
            _leafPresenter = presenter;
            _leaf = leaf;

            return true;
        }

        private bool TryBuildCollisionLeaf() {
            if (_collisionLeaf != null)
                return false;

            var collisionPresenter = _gfManager.CollisionPresenter;

            if (collisionPresenter == null || collisionPresenter == _refusingCollisionPresenter)
                return false;

            var collisionLeaf = collisionPresenter.Acquire(_geoChunk.GeoChunkEngine.Transform, _lod);

            if (collisionLeaf == null) {
                _refusingCollisionPresenter = collisionPresenter;

                return false;
            }

            _refusingCollisionPresenter = null;
            _collisionLeafPresenter = collisionPresenter;
            _collisionLeaf = collisionLeaf;

            return true;
        }

        private void UpdateTransitionVertexBuffer(NativeList<MeshingVertexData> vertices) {
            if (!vertices.IsCreated)
                return;

            _transitionMesh.SetVertexBufferParams(vertices.Length, MeshingVertexData.VertexBufferMemoryLayout);

            _transitionMesh.SetVertexBufferData(
                vertices.AsArray(),
                0, 0, vertices.Length, 0,
                MeshUpdateFlags.DontValidateIndices
            );
        }

        private void UpdateTransitionMask(GfStaticHelper.TransitionFaceMask mask, bool isSetter) {
            var newTransitionMask = _transitionMask;

            if (isSetter)
                newTransitionMask |= (int)mask;
            else
                newTransitionMask &= ~(int)mask;

            if (_transitionMask == newTransitionMask)
                return;

            _transitionMask = newTransitionMask;

            if (_transitionDirtyFlag) return;

            _transitionDirtyFlag = true;
            _gfManager.OctreeBatchTransitionUpdate += HandleTransitionUpdate;
        }

        private void HandleTransitionUpdate() {
            if (_transitionDirtyFlag) {
                _gfManager.OctreeBatchTransitionUpdate -= HandleTransitionUpdate;
                _transitionDirtyFlag = false;
            }

            if (!_transitionRanges.IsCreated || _transitionMesh == null)
                return;

            var triangles =
                GetFilteredTransitionTriangles(_allTransitionTriangles, _transitionRanges, _transitionMask);

            _transitionMesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);

            _transitionMesh.SetIndexBufferData(
                triangles.AsArray(),
                0,
                0,
                triangles.Length,
                MeshUpdateFlags.DontValidateIndices
            );

            var subMesh = new SubMeshDescriptor(0, triangles.Length);
            _transitionMesh.subMeshCount = 1;

            _transitionMesh.SetSubMesh(0, subMesh);
            _leaf.SetTransitionMesh(_transitionMesh);
            // No RecalculateBounds() here - bounds are set once in ApplyTransitionMarchResults,
            // when the vertex buffer actually changes. This method also runs on mask-only changes
            // where the vertex buffer is untouched, so recomputing here would be redundant.
        }

        private NativeList<int> GetFilteredTransitionTriangles(
            NativeList<int> allTriangles, NativeArray<int2> triangleRanges,
            int transitionMask) {
            _filteredTransitionTriangles.Clear();

            for (var i = 0; i < 6; i++) {
                if ((transitionMask & (1 << i)) == 0) continue;

                var range = triangleRanges[i];

                if (range.x < 0 || range.y > allTriangles.Length || range.x > range.y) continue;

                for (var j = range.x; j < range.y; j++) _filteredTransitionTriangles.Add(allTriangles[j]);
            }

            return _filteredTransitionTriangles;
        }

        private void BroadcastNewLeaf() {
            var neighborVoxelPositions = GetNeighborPositions();

            for (var i = 0; i < 6; i++)
                _geoChunk.BroadcastNewLeafAcrossChunks(this, neighborVoxelPositions[i], i);
        }

        private void BroadcastReleasedLeaf() {
            var neighborVoxelPositions = GetNeighborPositions();

            for (var i = 0; i < 6; i++)
                _geoChunk.BroadcastReleasedLeafAcrossChunks(neighborVoxelPositions[i], i);
        }

        internal void ClearTransitionTowardReleasedNeighbor(
            Vector3Int voxelPos, GfStaticHelper.TransitionFaceMask faceMask) {
            if (!_boundsVoxel.Contains(voxelPos))
                return;

            if (!IsLeaf) {
                foreach (var child in _children)
                    child.ClearTransitionTowardReleasedNeighbor(voxelPos, faceMask);

                return;
            }

            UpdateTransitionMask(GetOppositeTransition(faceMask), false);
        }

        private Vector3Int[] GetNeighborPositions() {
            if (_cachedNeighborPositions == null) _cachedNeighborPositions = SetNeighborPositions(_boundsVoxel);

            return _cachedNeighborPositions;
        }

        private Vector3Int[] SetNeighborPositions(BoundsInt boundsVoxel) {
            var center = (boundsVoxel.min + boundsVoxel.max) / 2;

            return new[] {
                new Vector3Int(boundsVoxel.min.x - 1, center.y, center.z), // XMin face (outside left)
                new Vector3Int(center.x, boundsVoxel.min.y - 1, center.z), // YMin face (outside bottom)
                new Vector3Int(center.x, center.y, boundsVoxel.min.z - 1), // ZMin face (outside back)
                new Vector3Int(boundsVoxel.max.x + 1, center.y, center.z), // XMax face (at boundary right)
                new Vector3Int(center.x, boundsVoxel.max.y + 1, center.z), // YMax face (at boundary top)
                new Vector3Int(center.x, center.y, boundsVoxel.max.z + 1) // ZMax face (at boundary front)
            };
        }

        private GfStaticHelper.TransitionFaceMask GetOppositeTransition(
            GfStaticHelper.TransitionFaceMask transitionMask) {
            return transitionMask switch {
                GfStaticHelper.TransitionFaceMask.XMin => GfStaticHelper.TransitionFaceMask.XMax,
                GfStaticHelper.TransitionFaceMask.YMin => GfStaticHelper.TransitionFaceMask.YMax,
                GfStaticHelper.TransitionFaceMask.ZMin => GfStaticHelper.TransitionFaceMask.ZMax,
                GfStaticHelper.TransitionFaceMask.XMax => GfStaticHelper.TransitionFaceMask.XMin,
                GfStaticHelper.TransitionFaceMask.YMax => GfStaticHelper.TransitionFaceMask.YMin,
                GfStaticHelper.TransitionFaceMask.ZMax => GfStaticHelper.TransitionFaceMask.ZMin,
                _ => GfStaticHelper.TransitionFaceMask.XMin
            };
        }

        internal void ApplyMarchResults(
            NativeList<MeshingVertexData> vertices, NativeList<int> triangles, Bounds computedBounds) {
            if (PrepareMarchResults(vertices, triangles, computedBounds))
                PresentMarchResults();
        }

        internal bool PrepareMarchResults(
            NativeList<MeshingVertexData> vertices, NativeList<int> triangles, Bounds computedBounds) {
            if (triangles.Length < 3 || vertices.Length < 3) {
                ReleaseLeafObjects();

                // Record this exact address as known-empty, so a future ValidateOctreeLods pass at
                // the same (lod, localPosition) - e.g. the player walks away and back, or a
                // Subdivide/collapse cycle tears this node down and later rebuilds it to the
                // identical address - can skip re-marching it entirely. Only sound because
                // re-marching the same address off unchanged voxel data always reproduces the same
                // result; any edit invalidates the whole cache (see GeoChunk.ApplyVoxelEdits), so
                // this can never go stale silently.
                _geoChunk.MarkKnownEmpty(_lod, _localPosition);

                return false;
            }

            var builtLeaf = TryBuildLeaf();
            var builtCollisionLeaf = TryBuildCollisionLeaf();

            if (!HasAnyLeaf) {
                _awaitsPresenter = true;

                return false;
            }

            _awaitsPresenter = _leaf == null || _collisionLeaf == null;

            if (_mesh == null) {
                _mesh = new Mesh();
                _mesh.MarkDynamic();
            }

            var worldToLocal = _parentGeoVolume.VolumeTransform.worldToLocalMatrix;

            if (builtLeaf) {
                BuildTransitions();
                _leaf.SetVolumeMatrix(worldToLocal);
                _broadcastsNewLeaf = true;
            }

            if (builtCollisionLeaf)
                _collisionLeaf.SetVolumeMatrix(worldToLocal);

            GeoForgeKit.WriteMesh(_mesh, vertices.AsArray(), triangles.AsArray(), computedBounds);

            return true;
        }

        internal void PresentMarchResults() {
            _leaf?.SetMesh(_mesh, _lod);
            _collisionLeaf?.SetMesh(_mesh);
            if (!_meshPresented) {
                _meshPresented = true;
                _geoChunk.GeoChunkEngine.HandleLeafPresented(_lod);
            }

            if (!_broadcastsNewLeaf)
                return;

            _broadcastsNewLeaf = false;
            BroadcastNewLeaf();
        }

        internal bool IsRegionReady(
            Vector3Int regionMinimum, Vector3Int regionMaximum, ReadinessScope scope, bool requiresCollider) {
            if (_children == null)
                return IsLeafReady(scope, requiresCollider);

            var split = _localPosition + Vector3Int.one *
                (_parentGeoVolume.ConfigBlob.Value.CubesMarchedPerOctreeLeaf << (_lod - 1));

            for (var i = 0; i < 8; i++) {
                if (!HalfOverlaps((i & 1) != 0, regionMinimum.x, regionMaximum.x, split.x)
                    || !HalfOverlaps((i & 2) != 0, regionMinimum.y, regionMaximum.y, split.y)
                    || !HalfOverlaps((i & 4) != 0, regionMinimum.z, regionMaximum.z, split.z))
                    continue;

                if (!_children[i].IsRegionReady(regionMinimum, regionMaximum, scope, requiresCollider))
                    return false;
            }

            return true;
        }

        private static bool HalfOverlaps(bool upperHalf, int minimum, int maximum, int split) {
            return upperHalf ? maximum >= split : minimum < split;
        }

        private bool IsLeafReady(ReadinessScope scope, bool requiresCollider) {
            if (scope == ReadinessScope.FinestLevel && _lod != 0)
                return false;

            if (_geoChunk.IsKnownEmpty(_lod, _localPosition))
                return true;

            if (requiresCollider)
                return _collisionLeaf != null && _collisionLeaf.HasMesh;

            return _mesh != null && HasAnyLeaf;
        }

        internal void ApplyTransitionMarchResults(
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeArray<int2> triangleRanges) {
            // _transitionMesh (and _allTransitionTriangles/_transitionRanges alongside it) only
            // exist once this leaf actually has geometry - see ApplyMarchResults/BuildTransitions.
            // If this leaf turned out empty this pass, ApplyMarchResults already released
            // everything (possibly just now, in the same CompleteAndApplyMarchingCubesJobs call,
            // since that always applies march results before transition results) - there's nothing
            // to stitch a transition seam against, so just discard these results instead of writing
            // into collections that no longer exist.
            if (_transitionMesh == null)
                return;

            _allTransitionTriangles.CopyFrom(triangles);
            _transitionRanges.CopyFrom(triangleRanges);
            UpdateTransitionVertexBuffer(vertices);

            // Reuse the main mesh's bounds instead of computing separately in the transition job:
            // transition geometry re-samples the same boundary the main mesh already covers, just
            // at higher density to match a neighbor's finer LOD, so it should never extend
            // meaningfully past the main mesh's own extent. ApplyMarchResults always runs before
            // this method in the same CompleteAndApplyMarchingCubesJobs pass, so _mesh.bounds is
            // guaranteed current here. The margin guards against the T-junction correction pass
            // (bIsLowResFace) nudging a vertex a hair outside that extent.
            var expandedBounds = _mesh.bounds;
            expandedBounds.Expand(_parentGeoVolume.ConfigBlob.Value.Resolution);
            _transitionMesh.bounds = expandedBounds;

            HandleTransitionUpdate();
        }
    }
}