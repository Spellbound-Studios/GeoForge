// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;

namespace Spellbound.GeoForge.Tests {
    public class CollisionSceneTests {
        private const float PlaneHeight = 4f;
        private const float FixedStep = 0.02f;
        private const float OneVoxel = 1f;
        private const float RayHeight = 50f;
        private const float VolumeLift = 10f;

        private static readonly Vector3 RayOrigin =
            new(PlaneWorld.ChunkSize * 0.5f, RayHeight, PlaneWorld.ChunkSize * 0.5f);

        private static readonly Vector3 MovedVolumePosition = new(-7f, 10f, 2f);
        private static readonly Quaternion MovedVolumeRotation = Quaternion.Euler(0f, 30f, 0f);

        private Scene _collisionScene;
        private Func<GeoForgeManager, IChunkCollisionPresenter> _foundCollisionFactory;
        private Func<GeoForgeManager, IChunkPresenter> _foundFactory;
        private PlaneWorld _world;

        [SetUp]
        public void SetUp() {
            _foundFactory = ChunkPresenterDefaults.Factory;
            _foundCollisionFactory = ChunkPresenterDefaults.CollisionFactory;
            ChunkPresenterDefaults.Factory = null;
            ChunkPresenterDefaults.CollisionFactory = null;
            _collisionScene = EditorSceneManager.NewPreviewScene();
            _world = new PlaneWorld(PlaneHeight, Vector3Int.zero);
        }

        [TearDown]
        public void TearDown() {
            _world.Dispose();
            _world = null;
            EditorSceneManager.ClosePreviewScene(_collisionScene);
            ChunkPresenterDefaults.Factory = _foundFactory;
            ChunkPresenterDefaults.CollisionFactory = _foundCollisionFactory;
        }

        [Test]
        public void TheTerrainCollidesInTheSceneTheManagerNamesAndNowhereElse() {
            _world.Manager.CollisionScene = _collisionScene;
            _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();

            _world.ValidateEveryChunk(Vector3.zero);

            var physicsScene = _collisionScene.GetPhysicsScene();
            physicsScene.Simulate(FixedStep);

            Assert.AreNotEqual(Physics.defaultPhysicsScene, physicsScene);
            Assert.IsTrue(physicsScene.Raycast(RayOrigin, Vector3.down, out var hit, 2f * RayHeight));
            Assert.AreEqual(PlaneHeight, hit.point.y, OneVoxel);
            Assert.AreEqual(_collisionScene, hit.collider.gameObject.scene);
            Assert.IsFalse(Physics.defaultPhysicsScene.Raycast(RayOrigin, Vector3.down, out _, 2f * RayHeight));

            _world.Volume.transform.position = Vector3.up * VolumeLift;
            _world.Engine.UpdateVolumeOrigin();
            physicsScene.Simulate(FixedStep);

            Assert.IsTrue(physicsScene.Raycast(RayOrigin, Vector3.down, out var movedHit, 2f * RayHeight));
            Assert.AreEqual(PlaneHeight + VolumeLift, movedHit.point.y, OneVoxel);
        }

        [Test]
        public void AManagerWithNoSceneSetReportsItsOwnScene() {
            Assert.IsTrue(_world.Manager.CollisionScene.IsValid());
            Assert.AreEqual(_world.Manager.gameObject.scene, _world.Manager.CollisionScene);
        }

        [Test]
        public void AColliderRootedInTheCollisionSceneTakesItsPoseFromTheVolumeMatrix() {
            _world.Manager.CollisionScene = _collisionScene;
            var collision = _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var leaf = collision.Acquire(_world.Chunks[0].GeoChunkEngine.Transform, 0);
            var colliderTransform = ((MeshColliderChunkLeaf)leaf).ColliderObject.transform;
            leaf.SetVolumeMatrix(_world.Volume.transform.worldToLocalMatrix);

            var movedVolumeToWorld = Matrix4x4.TRS(MovedVolumePosition, MovedVolumeRotation, Vector3.one);
            leaf.SetVolumeMatrix(movedVolumeToWorld.inverse);

            Assert.IsNull(colliderTransform.parent);
            Assert.AreEqual(_collisionScene, colliderTransform.gameObject.scene);
            Assert.That(colliderTransform.position,
                Is.EqualTo(MovedVolumePosition).Using(Vector3EqualityComparer.Instance));
            Assert.That(colliderTransform.rotation,
                Is.EqualTo(MovedVolumeRotation).Using(QuaternionEqualityComparer.Instance));

            collision.Release(leaf);
        }

        [Test]
        public void AColliderReleasedInTheCollisionSceneIsReusedByTheNextAcquire() {
            _world.Manager.CollisionScene = _collisionScene;
            var collision = _world.Manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var chunkTransform = _world.Chunks[0].GeoChunkEngine.Transform;
            var first = (MeshColliderChunkLeaf)collision.Acquire(chunkTransform, 0);
            var colliderObject = first.ColliderObject;

            collision.Release(first);

            Assert.IsFalse(colliderObject.activeSelf);
            Assert.AreEqual(_collisionScene, colliderObject.scene);

            var second = (MeshColliderChunkLeaf)collision.Acquire(chunkTransform, 0);

            Assert.AreSame(colliderObject, second.ColliderObject);
            Assert.IsTrue(colliderObject.activeSelf);
            Assert.AreEqual(_collisionScene, colliderObject.scene);

            collision.Release(second);
        }
    }
}
