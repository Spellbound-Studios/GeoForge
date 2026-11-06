// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class TransitionSeamHashTests {
        private const int ChunkSize = 32;
        private const int LevelsOfDetail = 2;
        private const int FinestLevel = 0;
        private const int CoarseLevel = 1;
        private const float PlaneHeight = 8f;
        private const float HillHeight = 3f;
        private const float HillWidth = 6f;
        private const string TransitionSeamHash = "ad2edae5b6716b4d";
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static readonly Vector3Int[] ChunkCoordinates = { Vector3Int.zero, Vector3Int.right };

        [Test]
        public void TwoNeighbouringChunksAtDifferentLevelsKeepTheirTransitionMeshBytes() {
            Assert.AreEqual(TransitionSeamHash, MarchSeamAndHash());
        }

        internal static string MarchSeamAndHash() {
            var created = new List<Object>();
            var chunks = new List<SeamGeoChunk>();
            var manager = Track(created, new GameObject("Seam Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager)
                .GetProperty(nameof(GeoForgeManager.McTablesBlob), BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, MarchTables.Build(Allocator.Persistent));
            manager.jobAndRenderProfile =
                Track(created, ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());
            var presenter = new RecordingChunkPresenter();
            manager.Presenter = presenter;
            manager.CollisionPresenter = new RecordingCollisionPresenter();

            var config = Track(created, ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = ChunkSize >> (LevelsOfDetail - 1);
            config.levelsOfDetail = LevelsOfDetail;
            config.maxChunkSize = ChunkSize;
            config.chunkSize = ChunkSize;
            config.sizeInChunks = new Vector3Int(ChunkCoordinates.Length, 1, 1);
            config.validatesPerFrame = ChunkCoordinates.Length + 1;

            var volume = Track(created, new GameObject("Seam Volume")).AddComponent<GeoVolumeDouble>();
            var engine = new GeoVolumeEngine(volume, volume, config, manager);
            volume.GeoVolumeEngine = engine;
            volume.ConfigBlob = engine.ConfigBlob;
            volume.ViewDistanceLodRanges = new[] { new Vector2(0f, float.MaxValue), new Vector2(0f, float.MaxValue) };

            try {
                engine.RegisterVolume();
                var source = new PlaneDensitySource(PlaneHeight, HillHeight, HillWidth);
                engine.UseDensitySource(source);
                ((IGeoVolume)volume).ChunkInterest = new SplitInterest(ChunkSize, FinestLevel, CoarseLevel);
                var dataWidth = ChunkLayout.DataWidth(ChunkSize);

                foreach (var coordinate in ChunkCoordinates) {
                    var chunkTransform = new GameObject($"Seam Chunk {coordinate}").transform;
                    chunkTransform.SetParent(volume.transform, false);
                    chunkTransform.localPosition = (Vector3)(coordinate * ChunkSize);
                    var chunk = new SeamGeoChunk(chunkTransform, coordinate);
                    chunks.Add(chunk);
                    engine.RegisterChunk(coordinate, chunk);

                    var origin = ChunkLayout.ChunkOrigin(new int3(coordinate.x, coordinate.y, coordinate.z),
                        engine.ConfigBlob.Value);
                    var request = new ChunkFillRequest(coordinate, new Vector3Int(origin.x, origin.y, origin.z),
                        engine.ConfigBlob.Value.Resolution, ChunkSize, dataWidth, 0u);
                    var voxels = new NativeArray<VoxelData>(request.DataVolume, Allocator.Persistent);
                    source.ScheduleFill(request, voxels, default).Complete();
                    chunk.GeoChunkEngine.SetVoxels(voxels);
                }

                ((IGeoVolume)volume).SetViewers(new[] { new ChunkViewer(Vector3.zero, ChunkSize, ChunkSize) });
                _ = engine.ValidateChunkLodsAsync();
                typeof(GeoForgeManager).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);

                return HashTransitionMeshes(presenter);
            }
            finally {
                foreach (var chunk in chunks)
                    chunk.GeoChunkEngine.Dispose();

                engine.Dispose();
                typeof(GeoForgeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);

                for (var index = created.Count - 1; index >= 0; index--) {
                    if (created[index] != null)
                        Object.DestroyImmediate(created[index]);
                }
            }
        }

        private static string HashTransitionMeshes(RecordingChunkPresenter presenter) {
            var heldLeaves = presenter.Acquired.Where(leaf => !presenter.Released.Contains(leaf)).ToList();
            var heldLevels = string.Join(",", heldLeaves.Select(leaf => leaf.MeshLevelOfDetail));
            Assert.IsTrue(heldLeaves.Any(leaf => leaf.MeshLevelOfDetail == FinestLevel), heldLevels);
            Assert.IsTrue(heldLeaves.Any(leaf => leaf.MeshLevelOfDetail == CoarseLevel), heldLevels);
            Assert.IsTrue(heldLeaves.Any(leaf => leaf.TransitionMesh.subMeshCount > 0 &&
                                                 leaf.TransitionMesh.GetIndexCount(0) > 0));

            var state = FnvOffsetBasis;

            foreach (var leaf in heldLeaves.Where(leaf => leaf.TransitionMesh.vertexCount > 0)) {
                using var meshData = Mesh.AcquireReadOnlyMeshData(leaf.TransitionMesh);
                state = Append(state, meshData[0].GetVertexData<byte>());
                state = Append(state, meshData[0].GetIndexData<int>().Reinterpret<byte>(sizeof(int)));
            }

            return state.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static ulong Append(ulong state, NativeArray<byte> bytes) {
            for (var index = 0; index < bytes.Length; index++) {
                state ^= bytes[index];
                state *= FnvPrime;
            }

            return state;
        }

        private static T Track<T>(List<Object> created, T instance) where T : Object {
            created.Add(instance);

            return instance;
        }
    }
}
