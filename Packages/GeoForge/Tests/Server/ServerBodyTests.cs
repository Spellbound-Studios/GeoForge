// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spellbound.GeoForge.Tests {
    public class ServerBodyTests {
        private const int ChunkSize = 32;
        private const float GroundHeight = 0f;
        private const float BowlWidth = 4f;
        private const int BowlStrength = 127;
        private const float BodyRadius = 0.5f;
        private const float BodyHeight = 2f;
        private const float DropClearance = 0.05f;
        private const float FixedStep = 0.02f;
        private const int Steps = 50;
        private const float RestingSpeed = 0.1f;
        private const float OneVoxel = 1f;

        private readonly List<Object> _created = new();

        private Scene _collisionScene;
        private GeoChunk _chunk;
        private GeoVolumeEngine _engine;
        private GeoForgeManager _manager;

        [SetUp]
        public void SetUp() {
            _collisionScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown() {
            _chunk?.GeoChunkEngine?.Dispose();
            _engine?.Dispose();

            if (_manager != null)
                typeof(GeoForgeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_manager, null);

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
            EditorSceneManager.ClosePreviewScene(_collisionScene);
        }

        [Test]
        public void ABodyDroppedIntoACarvedBowlRestsOnItsFloor() {
            var volume = CreateFlatWorld();
            var bowlCentre = new Vector3(0f, GroundHeight, 0f);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(volume, bowlCentre, BowlWidth, BowlStrength));

            var physicsScene = _collisionScene.GetPhysicsScene();
            physicsScene.Simulate(FixedStep);

            var rayOrigin = bowlCentre + Vector3.up * ChunkSize * 0.25f;
            Assert.IsTrue(physicsScene.Raycast(rayOrigin, Vector3.down, out var floor, ChunkSize));
            Assert.Less(floor.point.y, GroundHeight - OneVoxel);

            var body = DropBody(bowlCentre + Vector3.up * (BodyHeight * 0.5f + DropClearance));

            for (var step = 0; step < Steps; step++)
                physicsScene.Simulate(FixedStep);

            var bowlDepth = BowlWidth * 0.5f;
            Assert.Less(body.position.y, GroundHeight);
            Assert.Greater(body.position.y, GroundHeight - bowlDepth - 1f);
            Assert.Less(body.linearVelocity.magnitude, RestingSpeed);
        }

        private GeoVolume CreateFlatWorld() {
            _manager = Track(new GameObject("Server World")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager).GetProperty("McTablesBlob", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_manager, MarchTables.Build(Allocator.Persistent));
            _manager.jobAndRenderProfile =
                Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());
            _manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            _manager.Presenter = null;
            _manager.CollisionScene = _collisionScene;

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            var serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty("cubesPerMarch").intValue = ChunkSize;
            serializedConfig.FindProperty("levelsOfDetail").intValue = 1;
            serializedConfig.FindProperty("maxChunkSize").intValue = ChunkSize;
            serializedConfig.FindProperty("chunkSize").intValue = ChunkSize;
            serializedConfig.FindProperty("sizeInChunks").vector3IntValue = Vector3Int.one;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();

            var volume = Track(new GameObject("Server World Volume")).AddComponent<GeoVolume>();
            _engine = new GeoVolumeEngine(volume, volume, config, _manager);
            typeof(GeoVolume).GetProperty(nameof(GeoVolume.GeoVolumeEngine)).SetValue(volume, _engine);
            typeof(GeoVolume).GetProperty(nameof(GeoVolume.ViewDistanceLodRanges))
                .SetValue(volume, new[] { new Vector2(0f, float.MaxValue) });
            _engine.RegisterVolume();
            _engine.UseDensitySource(new FlatGroundDensitySource(GroundHeight));

            var chunkObject = Track(new GameObject("Server World Chunk"));
            chunkObject.transform.SetParent(volume.transform, false);
            _chunk = chunkObject.AddComponent<GeoChunk>();
            _chunk.InitializeGeoChunk(Vector3Int.zero, new GeoEditStore());
            _engine.RegisterChunk(Vector3Int.zero, _chunk);

            var origin = ChunkLayout.ChunkOrigin(int3.zero, _engine.ConfigBlob.Value);
            var request = new ChunkFillRequest(Vector3Int.zero, new Vector3Int(origin.x, origin.y, origin.z),
                _engine.ConfigBlob.Value.Resolution, ChunkSize, ChunkLayout.DataWidth(ChunkSize), 0u);
            var voxels = new NativeArray<VoxelData>(request.DataVolume, Allocator.Persistent);
            _engine.DensitySource.ScheduleFill(request, voxels, default).Complete();
            _chunk.GeoChunkEngine.SetVoxels(voxels);

            ((IGeoVolume)volume).SetViewers(new[] { new ChunkViewer(Vector3.zero, ChunkSize, ChunkSize) });
            _ = _engine.ValidateChunkLodsAsync();

            return volume;
        }

        private Rigidbody DropBody(Vector3 position) {
            var bodyObject = Track(new GameObject("Dropped Body"));
            SceneManager.MoveGameObjectToScene(bodyObject, _collisionScene);
            bodyObject.transform.position = position;

            var capsule = bodyObject.AddComponent<CapsuleCollider>();
            capsule.radius = BodyRadius;
            capsule.height = BodyHeight;

            return bodyObject.AddComponent<Rigidbody>();
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
