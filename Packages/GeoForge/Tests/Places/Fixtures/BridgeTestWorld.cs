// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using Spellbound.GeoForge.Tests;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places.Tests {
    public sealed class BridgeTestWorld : IDisposable {
        public const int ChunkSize = 64;

        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<BridgeChunkDouble> _chunks = new();
        private readonly List<Object> _created = new();
        private readonly IDensitySource _source;

        public BridgeTestWorld(float height, params Vector3Int[] chunkCoordinates) {
            _source = new HeightDensitySource(height);
            Manager = Track(new GameObject("Bridge Test World Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager).GetProperty("McTablesBlob", InstanceMembers)
                .SetValue(Manager, MarchTables.Build(Allocator.Persistent));
            Manager.jobAndRenderProfile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            SetConfigField(config, "cubesPerMarch", ChunkSize);
            SetConfigField(config, "levelsOfDetail", 1);
            SetConfigField(config, "maxChunkSize", ChunkSize);
            SetConfigField(config, "chunkSize", ChunkSize);
            SetConfigField(config, "sizeInChunks", Vector3Int.one);
            SetConfigField(config, "validatesPerFrame", chunkCoordinates.Length + 1);
            SetConfigField(config, "skipFillByOccupancy", false);

            Volume = Track(new GameObject("Bridge Test World Volume")).AddComponent<GeoVolumeDouble>();
            Engine = new GeoVolumeEngine(Volume, Volume, config, Manager);
            Volume.GeoVolumeEngine = Engine;
            Volume.ConfigBlob = Engine.ConfigBlob;
            Volume.ViewDistanceLodRanges = new[] { new Vector2(0f, float.MaxValue) };

            Engine.RegisterVolume();
            Engine.UseDensitySource(_source);

            foreach (var coordinate in chunkCoordinates) {
                AddChunk(coordinate);
            }
        }

        public GeoForgeManager Manager { get; }

        public GeoVolumeDouble Volume { get; }

        public GeoVolumeEngine Engine { get; }

        public IReadOnlyList<BridgeChunkDouble> Chunks => _chunks;

        public int DataVolume => Engine.ConfigBlob.Value.ChunkDataVolumeSize;

        public void Dispose() {
            foreach (var chunk in _chunks) {
                chunk.GeoChunkEngine.Dispose();
            }

            Engine.Dispose();
            typeof(GeoForgeManager).GetMethod("OnDestroy", InstanceMembers).Invoke(Manager, null);

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null) {
                    Object.DestroyImmediate(_created[index]);
                }
            }
        }

        public List<SparseVoxelData> RunsOf(Vector3Int coordinate) {
            var runs = Engine.GetChunkByCoord(coordinate).GeoChunkEngine.GetsSparseVoxelData();
            var copy = new List<SparseVoxelData>(runs.Length);

            for (var index = 0; index < runs.Length; index++) {
                copy.Add(runs[index]);
            }

            return copy;
        }

        private BridgeChunkDouble AddChunk(Vector3Int coordinate) {
            var chunkObject = Track(new GameObject("Bridge Test World Chunk " + coordinate));
            chunkObject.transform.SetParent(Volume.transform, false);
            chunkObject.transform.localPosition = coordinate * ChunkSize;
            var chunk = new BridgeChunkDouble(chunkObject.transform, coordinate, new GeoEditStore());
            _chunks.Add(chunk);
            Engine.RegisterChunk(coordinate, chunk);

            ref var configBlob = ref Engine.ConfigBlob.Value;
            var origin = ChunkLayout.ChunkOrigin(new int3(coordinate.x, coordinate.y, coordinate.z), configBlob);
            var request = new ChunkFillRequest(coordinate, new Vector3Int(origin.x, origin.y, origin.z),
                configBlob.Resolution, ChunkSize, ChunkLayout.DataWidth(ChunkSize), 0u);
            var voxels = new NativeArray<VoxelData>(request.DataVolume, Allocator.Persistent);
            _source.ScheduleFill(request, voxels, default).Complete();
            chunk.GeoChunkEngine.SetVoxels(voxels);

            return chunk;
        }

        private static void SetConfigField(VoxelVolumeConfig config, string fieldName, object value) {
            typeof(VoxelVolumeConfig).GetField(fieldName, InstanceMembers).SetValue(config, value);
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
