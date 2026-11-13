// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    internal sealed class PlaneWorld : IDisposable {
        internal const int ChunkSize = 16;

        private readonly List<GeoChunkDouble> _chunks = new();
        private readonly List<Object> _created = new();
        private readonly int _chunkSize;

        internal PlaneWorld(float planeHeight, params Vector3Int[] chunkCoordinates)
            : this(ChunkSize, 1, new PlaneDensitySource(planeHeight), chunkCoordinates) { }

        internal PlaneWorld(
            int chunkSize, int levelsOfDetail, IDensitySource source, params Vector3Int[] chunkCoordinates)
            : this(chunkSize, levelsOfDetail, source, true, chunkCoordinates) { }

        internal PlaneWorld(int chunkSize, int levelsOfDetail, IDensitySource source, bool skipFillByOccupancy,
            params Vector3Int[] chunkCoordinates) {
            _chunkSize = chunkSize;
            Manager = Track(new GameObject("Plane World Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager)
                .GetProperty(nameof(GeoForgeManager.McTablesBlob), BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Manager, MarchTables.Build(Allocator.Persistent));
            Manager.jobAndRenderProfile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = chunkSize >> (levelsOfDetail - 1);
            config.levelsOfDetail = levelsOfDetail;
            config.maxChunkSize = chunkSize;
            config.chunkSize = chunkSize;
            config.sizeInChunks = Vector3Int.one;
            config.validatesPerFrame = chunkCoordinates.Length + 1;
            config.skipFillByOccupancy = skipFillByOccupancy;

            Volume = Track(new GameObject("Plane World Volume")).AddComponent<GeoVolumeDouble>();
            Engine = new GeoVolumeEngine(Volume, Volume, config, Manager);
            Volume.GeoVolumeEngine = Engine;
            Volume.ConfigBlob = Engine.ConfigBlob;
            Volume.ViewDistanceLodRanges = new Vector2[levelsOfDetail];

            for (var level = 0; level < levelsOfDetail; level++)
                Volume.ViewDistanceLodRanges[level] = new Vector2(0f, float.MaxValue);

            Engine.RegisterVolume();
            Engine.UseDensitySource(source);

            var dataWidth = ChunkLayout.DataWidth(chunkSize);

            foreach (var coordinate in chunkCoordinates) {
                var chunk = AddChunk(coordinate);
                var origin = ChunkLayout.ChunkOrigin(new int3(coordinate.x, coordinate.y, coordinate.z),
                    Engine.ConfigBlob.Value);
                var request = new ChunkFillRequest(coordinate, new Vector3Int(origin.x, origin.y, origin.z),
                    Engine.ConfigBlob.Value.Resolution, chunkSize, dataWidth, 0u);
                var voxels = new NativeArray<VoxelData>(request.DataVolume, Allocator.Persistent);
                source.ScheduleFill(request, voxels, default).Complete();
                chunk.GeoChunkEngine.SetVoxels(voxels);
            }
        }

        internal GeoForgeManager Manager { get; }

        internal GeoVolumeDouble Volume { get; }

        internal GeoVolumeEngine Engine { get; }

        internal IReadOnlyList<GeoChunkDouble> Chunks => _chunks;

        public void Dispose() {
            foreach (var chunk in _chunks)
                chunk.GeoChunkEngine.Dispose();

            Engine.Dispose();
            typeof(GeoForgeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Manager, null);

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }
        }

        internal GeoChunkDouble AddChunk(Vector3Int coordinate) {
            return AddChunk(coordinate, new GeoEditStore());
        }

        internal GeoChunkDouble AddChunk(Vector3Int coordinate, IGeoEditStore editStore) {
            var chunkTransform = new GameObject($"Plane World Chunk {coordinate}").transform;
            chunkTransform.SetParent(Volume.transform, false);
            chunkTransform.localPosition = (Vector3)(coordinate * _chunkSize);
            var chunk = new GeoChunkDouble(chunkTransform, coordinate, editStore);
            _chunks.Add(chunk);
            Engine.RegisterChunk(coordinate, chunk);

            return chunk;
        }

        internal void ValidateEveryChunk(Vector3 viewerPosition) {
            ((IGeoVolume)Volume).SetViewers(new[] { new ChunkViewer(viewerPosition, _chunkSize, _chunkSize) });
            _ = Engine.ValidateChunkLodsAsync();
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
