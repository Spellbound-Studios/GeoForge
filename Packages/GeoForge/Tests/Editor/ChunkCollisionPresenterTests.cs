// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class ChunkCollisionPresenterTests {
        private const float PlaneHeight = 4f;
        private const int LeafPrefabLayer = 9;
        private const int ExplicitLayer = 3;
        private const float EditRadius = 3f;
        private const int EditStrength = 127;

        private static readonly Vector3 EditCentre =
            new(PlaneWorld.ChunkSize * 0.5f, PlaneHeight, PlaneWorld.ChunkSize * 0.5f);

        private Func<GeoForgeManager, IChunkCollisionPresenter> _foundCollisionFactory;
        private Func<GeoForgeManager, IChunkPresenter> _foundFactory;
        private readonly List<Object> _created = new();

        private PlaneWorld _world;

        [SetUp]
        public void SetUp() {
            _foundFactory = ChunkPresenterDefaults.Factory;
            _foundCollisionFactory = ChunkPresenterDefaults.CollisionFactory;
            ChunkPresenterDefaults.Factory = null;
            ChunkPresenterDefaults.CollisionFactory = null;
            _world = new PlaneWorld(PlaneHeight, Vector3Int.zero);
        }

        [TearDown]
        public void TearDown() {
            _world.Dispose();
            _world = null;

            foreach (var created in _created)
                Object.DestroyImmediate(created);

            _created.Clear();

            ChunkPresenterDefaults.Factory = _foundFactory;
            ChunkPresenterDefaults.CollisionFactory = _foundCollisionFactory;
        }

        [Test]
        public void ChunkCollisionPresenter_CustomImplementation() {
            var picture = new RecordingChunkPresenter();
            var collision = new RecordingCollisionPresenter();
            _world.Manager.Presenter = picture;
            _world.Manager.CollisionPresenter = collision;
            var chunk = _world.Chunks[0];

            _world.ValidateEveryChunk(Vector3.zero);

            CollectionAssert.AreEqual(new[] { "Acquire", "SetVolumeMatrix", "SetMesh" }, collision.Calls);
            Assert.AreEqual(1, collision.Acquired.Count);
            var leaf = collision.Acquired[0];
            Assert.AreSame(chunk.GeoChunkEngine.Transform, leaf.Parent);
            Assert.AreEqual(0, leaf.AcquiredLevelOfDetail);
            Assert.IsTrue(leaf.HasMesh);
            Assert.AreSame(picture.Acquired[0].Mesh, leaf.Mesh);
            Assert.Greater(leaf.Mesh.vertexCount, 0);
            Assert.AreEqual(_world.Volume.transform.worldToLocalMatrix, leaf.VolumeMatrix);

            chunk.GeoChunkEngine.Dispose();

            Assert.AreEqual("Release", collision.Calls[collision.Calls.Count - 1]);
            CollectionAssert.AreEqual(new IChunkCollisionLeaf[] { leaf }, collision.Released);
            Assert.IsTrue(leaf.Mesh == null);
        }

        [Test]
        public void ACollisionPartThatBakesGetsItsMeshesBakedBeforeTheyAreHandedOver() {
            var collision = new RecordingCollisionPresenter { BakesMeshesBeforeApply = true };
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = collision;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(1, _world.Manager.MeshesBakedInLastApply);
            Assert.IsTrue(collision.Acquired[0].HasMesh);
        }

        [Test]
        public void ACollisionPartThatDoesNotBakeGetsItsMeshesWithNoBake() {
            var collision = new RecordingCollisionPresenter { BakesMeshesBeforeApply = false };
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.CollisionPresenter = collision;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreEqual(0, _world.Manager.MeshesBakedInLastApply);
            Assert.IsTrue(collision.Acquired[0].HasMesh);
        }

        [Test]
        public void TheDefaultCollisionLeafHoldsTheAppliedMeshOnItsMeshCollider() {
            var picture = new RecordingChunkPresenter();
            _world.Manager.Presenter = picture;
            var collision = _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var chunkTransform = _world.Chunks[0].GeoChunkEngine.Transform;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.AreSame(collision, _world.Manager.CollisionPresenter);
            Assert.IsTrue(collision.BakesMeshesBeforeApply);
            Assert.AreEqual(1, _world.Manager.MeshesBakedInLastApply);
            var colliders = chunkTransform.GetComponentsInChildren<MeshCollider>();
            Assert.AreEqual(1, colliders.Length);
            Assert.AreSame(picture.Acquired[0].Mesh, colliders[0].sharedMesh);
            Assert.AreSame(chunkTransform, colliders[0].transform.parent);
            Assert.AreEqual(_world.Manager.gameObject.scene, colliders[0].gameObject.scene);
            Assert.AreEqual("Collider 1 at level of detail 0", colliders[0].name);
            Assert.IsFalse(colliders[0].TryGetComponent<MeshRenderer>(out _));
        }

        [Test]
        public void ANodeWithACollisionPartAndNoPicturePartMarchesAndCollides() {
            _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var chunk = _world.Chunks[0];

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.IsNull(_world.Manager.Presenter);
            Assert.IsTrue(chunk.GeoChunkEngine.MeshReady);
            var colliders = chunk.GeoChunkEngine.Transform.GetComponentsInChildren<MeshCollider>();
            Assert.AreEqual(1, colliders.Length);
            Assert.IsNotNull(colliders[0].sharedMesh);
            Assert.Greater(colliders[0].sharedMesh.vertexCount, 0);
            Assert.IsFalse(chunk.GeoChunkEngine.Transform.GetComponentsInChildren<MeshFilter>().Any());
        }

        [Test]
        public void TheDefaultCollisionPartTakesTheLayerAndPhysicsMaterialOfTheLeafPrefab() {
            var material = UseLeafPrefabWithCollider();
            _world.Manager.Presenter = new RecordingChunkPresenter();
            _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();

            _world.ValidateEveryChunk(Vector3.zero);

            var collider = SingleChunkCollider();
            Assert.AreEqual(LeafPrefabLayer, collider.gameObject.layer);
            Assert.AreSame(material, collider.sharedMaterial);
        }

        [Test]
        public void ALayerAndPhysicsMaterialSetOnTheCollisionPartWinOverTheLeafPrefab() {
            UseLeafPrefabWithCollider();
            var explicitMaterial = Track(new PhysicsMaterial("Explicit Terrain"));
            _world.Manager.Presenter = new RecordingChunkPresenter();
            var collision = _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var serializedCollision = new SerializedObject(collision);
            serializedCollision.FindProperty("colliderLayer").intValue = ExplicitLayer;
            serializedCollision.FindProperty("physicsMaterial").objectReferenceValue = explicitMaterial;
            serializedCollision.ApplyModifiedPropertiesWithoutUndo();

            _world.ValidateEveryChunk(Vector3.zero);

            var collider = SingleChunkCollider();
            Assert.AreEqual(ExplicitLayer, collider.gameObject.layer);
            Assert.AreSame(explicitMaterial, collider.sharedMaterial);
        }

        [Test]
        public void AManagerWhoseCollisionPartIsSetToNullMarchesDrawsAndCreatesNoCollider() {
            ChunkPresenterDefaults.CollisionFactory =
                manager => manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var picture = new RecordingChunkPresenter();
            _world.Manager.Presenter = picture;
            _world.Manager.CollisionPresenter = null;

            _world.ValidateEveryChunk(Vector3.zero);

            Assert.IsNull(_world.Manager.CollisionPresenter);
            Assert.IsFalse(_world.Manager.TryGetComponent<MeshColliderChunkPresenter>(out _));
            Assert.AreEqual(1, picture.Acquired.Count);
            Assert.Greater(picture.Acquired[0].Mesh.vertexCount, 0);
            Assert.IsTrue(_world.Chunks[0].GeoChunkEngine.MeshReady);
            Assert.IsEmpty(_world.Volume.GetComponentsInChildren<MeshCollider>(true));
        }

        [Test]
        public void ADestroyedColliderHasNoMeshAndTheNextApplyDoesNotThrow() {
            _world.Manager.Presenter = new RecordingChunkPresenter();
            var collision = _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var looseLeaf = collision.Acquire(_world.Chunks[0].GeoChunkEngine.Transform, 0);
            looseLeaf.SetMesh(Track(new Mesh()));
            Object.DestroyImmediate(((MeshColliderChunkLeaf)looseLeaf).ColliderObject);

            Assert.IsFalse(looseLeaf.HasMesh);

            _world.ValidateEveryChunk(Vector3.zero);
            Object.DestroyImmediate(SingleChunkCollider().gameObject);
            LogAssert.Expect(LogType.Error, new Regex("destroyed"));

            Assert.DoesNotThrow(DigAtCentre);
            Assert.DoesNotThrow(DigAtCentre);
            Assert.DoesNotThrow(() => collision.Release(looseLeaf));
        }

        [Test]
        public void AThrowingCollisionLeafDoesNotLeaveTheApplyHalfDone() {
            var picture = new RecordingChunkPresenter();
            var collision = new RecordingCollisionPresenter { HandsOutThrowingLeaves = true };
            _world.Manager.Presenter = picture;
            _world.Manager.CollisionPresenter = collision;
            LogAssert.Expect(LogType.Error, new Regex(nameof(RecordingCollisionLeaf.ThrowsOnSetMesh)));

            Assert.DoesNotThrow(() => _world.ValidateEveryChunk(Vector3.zero));
            Assert.IsFalse(_world.Manager.IsApplyingMarchResults);

            collision.Acquired[0].ThrowsOnSetMesh = false;

            Assert.DoesNotThrow(DigAtCentre);
            Assert.IsTrue(collision.Acquired[0].HasMesh);
        }

        private PhysicsMaterial UseLeafPrefabWithCollider() {
            var leafPrefab = Track(new GameObject("Terrain Leaf Prefab") { layer = LeafPrefabLayer });
            var material = Track(new PhysicsMaterial("Leaf Prefab Terrain"));
            leafPrefab.AddComponent<MeshCollider>().sharedMaterial = material;
            _world.Manager.LeafPrefab = leafPrefab;

            return material;
        }

        private void DigAtCentre() {
            GeoForgeCommands.RemoveSphere(_world.Volume, EditCentre, EditRadius, EditStrength);
        }

        private MeshCollider SingleChunkCollider() {
            var colliders = _world.Chunks[0].GeoChunkEngine.Transform.GetComponentsInChildren<MeshCollider>();
            Assert.AreEqual(1, colliders.Length);

            return colliders[0];
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
