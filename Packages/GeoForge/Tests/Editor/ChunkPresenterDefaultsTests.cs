// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class ChunkPresenterDefaultsTests {
        private Func<GeoForgeManager, IChunkCollisionPresenter> _foundCollisionFactory;
        private Func<GeoForgeManager, IChunkPresenter> _foundFactory;
        private GameObject _managerObject;
        private Func<GeoForgeManager, IChunkCollisionPresenter> _registeredCollisionFactory;
        private Func<GeoForgeManager, IChunkPresenter> _registeredFactory;

        [SetUp]
        public void SetUp() {
            _foundFactory = ChunkPresenterDefaults.Factory;
            _foundCollisionFactory = ChunkPresenterDefaults.CollisionFactory;
            ChunkPresenterDefaults.Reset();
            _registeredFactory = ChunkPresenterDefaults.Factory;
            _registeredCollisionFactory = ChunkPresenterDefaults.CollisionFactory;
            ChunkPresenterDefaults.Factory = _foundFactory;
            ChunkPresenterDefaults.CollisionFactory = _foundCollisionFactory;
            _managerObject = new GameObject("GeoForge Manager");
        }

        [TearDown]
        public void TearDown() {
            ChunkPresenterDefaults.RegisterDefault(_registeredFactory);
            ChunkPresenterDefaults.RegisterDefaultCollision(_registeredCollisionFactory);
            ChunkPresenterDefaults.Factory = _foundFactory;
            ChunkPresenterDefaults.CollisionFactory = _foundCollisionFactory;
            Object.DestroyImmediate(_managerObject);
        }

        [Test]
        public void AfterTheResetBothRegisteredDefaultsStillAnswer() {
            var picture = new RecordingChunkPresenter();
            var collision = new RecordingCollisionPresenter();
            ChunkPresenterDefaults.RegisterDefault(manager => picture);
            ChunkPresenterDefaults.RegisterDefaultCollision(manager => collision);
            ChunkPresenterDefaults.Factory = manager => new RecordingChunkPresenter();
            ChunkPresenterDefaults.CollisionFactory = null;

            ChunkPresenterDefaults.Reset();

            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();
            Assert.AreSame(picture, geoForgeManager.Presenter);
            Assert.AreSame(collision, geoForgeManager.CollisionPresenter);
        }

        [Test]
        public void ACollisionComponentOnTheManagerWinsOverTheCollisionFactory() {
            ChunkPresenterDefaults.CollisionFactory = manager => new RecordingCollisionPresenter();
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();
            var component = _managerObject.AddComponent<MeshColliderChunkPresenter>();

            Assert.AreSame(component, geoForgeManager.CollisionPresenter);
        }

        [Test]
        public void AManagerWithoutAComponentTakesThePresenterTheFactoryMakes() {
            var made = new RecordingChunkPresenter();
            GeoForgeManager received = null;
            ChunkPresenterDefaults.Factory = manager => {
                received = manager;
                return made;
            };
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();

            Assert.AreSame(made, geoForgeManager.Presenter);
            Assert.AreSame(geoForgeManager, received);
        }

        [Test]
        public void AComponentOnTheManagerWinsOverTheFactory() {
            ChunkPresenterDefaults.Factory = manager => new RecordingChunkPresenter();
            var component = _managerObject.AddComponent<ComponentChunkPresenter>();
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();

            Assert.AreSame(component, geoForgeManager.Presenter);
        }

        [Test]
        public void APresenterSetByCodeWinsOverBoth() {
            ChunkPresenterDefaults.Factory = manager => new RecordingChunkPresenter();
            _managerObject.AddComponent<ComponentChunkPresenter>();
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();
            var set = new RecordingChunkPresenter();

            geoForgeManager.Presenter = set;

            Assert.AreSame(set, geoForgeManager.Presenter);
        }

        [Test]
        public void WithNothingInstalledThePresenterIsNullAndAFactoryRegisteredLaterIsFound() {
            ChunkPresenterDefaults.Factory = null;
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();

            Assert.IsNull(geoForgeManager.Presenter);

            var made = new RecordingChunkPresenter();
            ChunkPresenterDefaults.Factory = manager => made;

            Assert.AreSame(made, geoForgeManager.Presenter);
        }

        [Test]
        public void APresenterSetToNullByCodeStaysNullOverTheComponentAndTheFactory() {
            ChunkPresenterDefaults.Factory = manager => new RecordingChunkPresenter();
            _managerObject.AddComponent<ComponentChunkPresenter>();
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();

            geoForgeManager.Presenter = null;

            Assert.IsNull(geoForgeManager.Presenter);
            Assert.IsNull(geoForgeManager.Presenter);
        }

        [Test]
        public void ACollisionPresenterSetToNullByCodeStaysNullAndNoDefaultIsAdded() {
            ChunkPresenterDefaults.CollisionFactory =
                manager => manager.gameObject.AddComponent<MeshColliderChunkPresenter>();
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();

            geoForgeManager.CollisionPresenter = null;

            Assert.IsNull(geoForgeManager.CollisionPresenter);
            Assert.IsNull(geoForgeManager.CollisionPresenter);
            Assert.IsFalse(_managerObject.TryGetComponent<MeshColliderChunkPresenter>(out _));
        }
    }
}
