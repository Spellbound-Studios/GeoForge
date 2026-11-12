// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using Spellbound.Core.Observations;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Networking.Tests {
    internal sealed class VoxelWorld : IDisposable {
        internal const int ChunkSize = 16;
        internal const float GroundHeight = 20f;

        private readonly List<Object> _created = new();
        private readonly Dictionary<Vector3Int, GeoChunk> _chunks = new();
        private readonly List<Vector3Int> _coordinates = new();
        private readonly GameObject _chunkTemplate;
        private readonly IDensitySource _ground = new GroundDensitySource(GroundHeight);

        internal VoxelWorld(string name, in VoxelEditSourceSettings settings, ObservationContext observations,
            params Vector3Int[] chunkCoordinates) {
            Manager = Track(new GameObject(name + " Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager).GetProperty("McTablesBlob", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Manager, MarchTables.Build(Allocator.Persistent));
            Manager.jobAndRenderProfile =
                Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            var serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty("cubesPerMarch").intValue = ChunkSize;
            serializedConfig.FindProperty("levelsOfDetail").intValue = 1;
            serializedConfig.FindProperty("maxChunkSize").intValue = ChunkSize;
            serializedConfig.FindProperty("chunkSize").intValue = ChunkSize;
            serializedConfig.FindProperty("sizeInChunks").vector3IntValue = new Vector3Int(2, 2, 2);
            serializedConfig.FindProperty("validatesPerFrame").intValue = 9;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();

            Volume = Track(new GameObject(name + " Volume")).AddComponent<GeoVolume>();
            Engine = new GeoVolumeEngine(Volume, Volume, config, Manager);
            typeof(GeoVolume).GetProperty(nameof(GeoVolume.GeoVolumeEngine)).SetValue(Volume, Engine);
            typeof(GeoVolume).GetProperty(nameof(GeoVolume.ViewDistanceLodRanges))
                .SetValue(Volume, new[] { new Vector2(0f, float.MaxValue) });
            Engine.RegisterVolume();
            Engine.UseDensitySource(_ground);

            Stores = GeoForgeNetworkingKit.CreateEditStores(Volume, settings, observations);
            Volume.UseEditStores(Stores);
            _chunkTemplate = Track(new GameObject(name + " Chunk Template"));
            _chunkTemplate.AddComponent<GeoChunk>();

            foreach (var coordinate in chunkCoordinates)
                AddChunk(coordinate, true);
        }

        internal GeoForgeManager Manager { get; }

        internal GeoVolume Volume { get; }

        internal IGeoVolume GeoVolume => Volume;

        internal GeoVolumeEngine Engine { get; }

        internal VoxelEditStores Stores { get; }

        internal IReadOnlyList<Vector3Int> Coordinates => _coordinates;

        public void Dispose() {
            Stores.Dispose();

            foreach (var chunk in _chunks.Values) {
                var chunkObject = chunk.gameObject;
                chunk.GeoChunkEngine.Dispose();
                Object.DestroyImmediate(chunkObject);
            }

            _chunks.Clear();
            Engine.Dispose();
            typeof(GeoForgeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Manager, null);

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        internal GeoChunk AddChunk(Vector3Int coordinate, bool fill) {
            var chunk = Engine.CreateChunk<GeoChunk, IGeoEditStore>(coordinate, _chunkTemplate,
                Stores.CreateFor(coordinate));
            Engine.RegisterChunk(coordinate, chunk);
            _chunks.Add(coordinate, chunk);
            _coordinates.Add(coordinate);

            if (fill)
                Fill(coordinate);

            return chunk;
        }

        internal void Fill(Vector3Int coordinate) {
            var origin = ChunkLayout.ChunkOrigin(new int3(coordinate.x, coordinate.y, coordinate.z),
                Engine.ConfigBlob.Value);
            var request = new ChunkFillRequest(coordinate, new Vector3Int(origin.x, origin.y, origin.z),
                Engine.ConfigBlob.Value.Resolution, ChunkSize, ChunkLayout.DataWidth(ChunkSize), 0u);
            _chunks[coordinate].GeoChunkEngine.SetVoxels(request, _ground);
        }

        internal IGeoEditStore StoreOf(Vector3Int coordinate) {
            return _chunks[coordinate].GeoChunkEngine.IGeoEditStore;
        }

        internal VoxelData VoxelAt(Vector3 worldPosition) {
            if (!GeoVolume.TryQueryVoxel(worldPosition, out var voxel))
                throw new InvalidOperationException("No voxel is resident at " + worldPosition + ".");

            return voxel;
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}