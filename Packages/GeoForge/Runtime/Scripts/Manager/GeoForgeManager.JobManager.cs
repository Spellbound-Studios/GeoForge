// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Logging;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge {
    public partial class GeoForgeManager : MonoBehaviour {
        [SerializeField] public MaterialRenderingProfile jobAndRenderProfile;

        private readonly Dictionary<OctreeNode, Vector3Int> _nodeToChunkCoord = new();
        private readonly Dictionary<OctreeNode, MarchJobData> _pendingMarchJobData = new();
        private readonly Dictionary<OctreeNode, TransitionMarchJobData> _pendingTransitionMarchJobData = new();

        private JobHandle _combinedJobHandle;
        private bool _refusedMissingRenderingProfile;

        internal bool TryGetRenderingProfile(out MaterialRenderingProfile profile) {
            profile = jobAndRenderProfile;

            if (profile != null)
                return true;

            if (_refusedMissingRenderingProfile)
                return false;

            _refusedMissingRenderingProfile = true;
            Log.Error($"{name}: {nameof(jobAndRenderProfile)} (Job And Render Profile in the Inspector) is not " +
                      "assigned, so nothing is marched; BarycentricProfile is the shipped default.", this);

            return false;
        }

        internal void RegisterMarchJob(
            OctreeNode node,
            JobHandle jobHandle,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeReference<Bounds> computedBounds,
            Vector3Int chunkCoord) {
            if (_pendingMarchJobData.ContainsKey(node))
                // Overwriting a pending entry would orphan the superseded job's NativeLists - that
                // job is still scheduled and actively writing into them via _combinedJobHandle, so
                // nothing can safely dispose them here without first isolating and completing that
                // one job's handle specifically, which this dictionary doesn't track (CombineDependencies
                // merges handles opaquely; there's no way to pull one back out). Fail loudly instead
                // of silently leaking - or, worse, inviting a future "helpful" naive dispose that
                // races the still-running job.
                throw new InvalidOperationException(
                    $"RegisterMarchJob: a march job is already pending for node {node} - " +
                    "the previous job must be completed and applied before registering another.");

            _combinedJobHandle = JobHandle.CombineDependencies(_combinedJobHandle, jobHandle);

            _pendingMarchJobData[node] = new MarchJobData {
                Vertices = vertices,
                Triangles = triangles,
                ComputedBounds = computedBounds
            };

            _nodeToChunkCoord[node] = chunkCoord;
        }

        internal void RegisterTransitionJob(
            OctreeNode node,
            JobHandle jobHandle,
            NativeList<MeshingVertexData> vertices,
            NativeList<int> triangles,
            NativeArray<int2> ranges,
            Vector3Int chunkCoord) {
            if (_pendingTransitionMarchJobData.ContainsKey(node))
                // Same leak/race shape as RegisterMarchJob above.
                throw new InvalidOperationException(
                    $"RegisterTransitionJob: a transition job is already pending for node {node} - " +
                    "the previous job must be completed and applied before registering another.");

            _combinedJobHandle = JobHandle.CombineDependencies(_combinedJobHandle, jobHandle);

            _pendingTransitionMarchJobData[node] = new TransitionMarchJobData {
                Vertices = vertices,
                Triangles = triangles,
                Ranges = ranges
            };

            _nodeToChunkCoord.TryAdd(node, chunkCoord);
        }

        internal void CompleteAndApplyMarchingCubesJobs() {
            if (_pendingMarchJobData.Count == 0 && _pendingTransitionMarchJobData.Count == 0) return;

            _combinedJobHandle.Complete();

            // Main leaf results MUST be applied before transition results: ApplyMarchResults is
            // what builds (or releases) each leaf's GameObject/Mesh/transition-Mesh in the first
            // place, and ApplyTransitionMarchResults needs those to already exist (or to already
            // know they don't) before it can safely write into them.
            foreach (var kvp in _pendingMarchJobData) {
                kvp.Key.ApplyMarchResults(kvp.Value.Vertices, kvp.Value.Triangles, kvp.Value.ComputedBounds.Value);
                ReturnMarchBuffers(kvp.Value);
            }

            foreach (var kvp in _pendingTransitionMarchJobData) {
                kvp.Key.ApplyTransitionMarchResults(kvp.Value.Vertices, kvp.Value.Triangles, kvp.Value.Ranges);
                ReturnTransitionBuffers(kvp.Value);
            }

            _pendingMarchJobData.Clear();
            _pendingTransitionMarchJobData.Clear();
            _nodeToChunkCoord.Clear();
            _combinedJobHandle = default;
        }

        // Returns each field to its pool instead of calling MarchJobData.Dispose()/
        // TransitionMarchJobData.Dispose(), which would actually free the underlying
        // NativeCollections - the whole point of the buffer pool (see GeoForgeManager.MarchBufferPool.cs)
        // is to avoid that allocate/dispose churn on the normal completion path. Those structs' own
        // Dispose() methods are left intact for any path that needs to genuinely drop a pending job
        // without applying it.
        private void ReturnMarchBuffers(MarchJobData data) {
            ReturnVertexBuffer(data.Vertices);
            ReturnTriangleBuffer(data.Triangles);
            ReturnBoundsBuffer(data.ComputedBounds);
        }

        private void ReturnTransitionBuffers(TransitionMarchJobData data) {
            ReturnVertexBuffer(data.Vertices);
            ReturnTriangleBuffer(data.Triangles);
            ReturnRangesBuffer(data.Ranges);
        }
    }
}