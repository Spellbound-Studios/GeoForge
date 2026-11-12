// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class EditStoreSourceTests {
        private const int ChunkSize = 16;
        private const float EditRadius = 4f;
        private const int EditStrength = 127;

        private static readonly Vector3Int AnyChunk = Vector3Int.zero;

        private readonly List<Object> _created = new();

        private GeoForgeManager _manager;
        private EditStoreTestVolume _volume;

        [TearDown]
        public void TearDown() {
            if (_volume != null && _volume.GeoVolumeEngine != null) {
                var chunks = new List<IGeoChunk>(_volume.GeoVolumeEngine.ChunkDict.Values);

                foreach (var chunk in chunks) {
                    var chunkObject = chunk.GeoChunkEngine.Transform.gameObject;
                    chunk.GeoChunkEngine.Dispose();
                    Object.DestroyImmediate(chunkObject);
                }

                _volume.GeoVolumeEngine.Dispose();
            }

            if (_manager != null)
                typeof(GeoForgeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_manager, null);

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
            _volume = null;
            _manager = null;
        }

        [Test]
        public void UseEditStores_ChunksTakeTheGivenStores() {
            var source = new HandedOutStoreSource();
            CreateVolume(source);

            Assert.AreEqual(2, source.Made.Count);

            foreach (var made in source.Made) {
                var chunk = _volume.GeoVolumeEngine.GetChunkByCoord(made.Key);
                Assert.IsNotNull(chunk, "chunk " + made.Key);
                Assert.AreSame(made.Value, chunk.GeoChunkEngine.IGeoEditStore, "chunk " + made.Key);
                Assert.AreSame(chunk.GeoChunkEngine, made.Value.geoChunkEngine, "chunk " + made.Key);
            }
        }

        [Test]
        public void GeoEditStoreSource_CustomImplementation() {
            Assert.IsInstanceOf<GeoEditStore>(new GeoEditStoreSource().CreateFor(AnyChunk));

            var source = new CountingStoreSource();
            CreateVolume(source);
            IGeoVolume volume = _volume;

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, Vector3.zero, EditRadius, EditStrength));

            Assert.AreEqual(2, source.Made.Count);
            var operations = 0;

            foreach (var store in source.Made) {
                Assert.AreSame(store, volume.GetChunkByCoord(store.Coordinate).GeoChunkEngine.IGeoEditStore);
                operations += store.Operations;
            }

            Assert.Greater(operations, 0);
            Assert.IsTrue(volume.TryQueryVoxel(Vector3.zero, out var voxel));
            Assert.IsFalse(VoxelEncoding.IsSolid(voxel.Density));
        }

        private void CreateVolume(IGeoEditStoreSource source) {
            _manager = Track(new GameObject("Edit Store Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager)
                .GetProperty(nameof(GeoForgeManager.McTablesBlob), BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_manager, MarchTables.Build(Allocator.Persistent));
            _manager.jobAndRenderProfile =
                Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = ChunkSize;
            config.levelsOfDetail = 1;
            config.maxChunkSize = ChunkSize;
            config.chunkSize = ChunkSize;
            config.sizeInChunks = new Vector3Int(2, 1, 1);
            config.validatesPerFrame = 3;

            var chunkPrefab = Track(new GameObject("Edit Store Chunk Prefab"));
            chunkPrefab.AddComponent<GeoChunk>();

            var volumeObject = Track(new GameObject("Edit Store Volume"));
            volumeObject.transform.SetParent(_manager.transform, false);
            _volume = volumeObject.AddComponent<EditStoreTestVolume>();
            _volume.UseDensitySource(new PlaneDensitySource(0f));
            _volume.UseEditStores(source);
            _volume.Build(config, chunkPrefab, Track(ScriptableObject.CreateInstance<BoundaryOverrides>()));
            _volume.InitializeEveryChunk();
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}