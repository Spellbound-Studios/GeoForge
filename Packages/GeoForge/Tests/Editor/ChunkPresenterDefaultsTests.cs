// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class ChunkPresenterDefaultsTests {
        private GameObject _managerObject;

        [SetUp]
        public void SetUp() {
            _managerObject = new GameObject("GeoForge Manager");
        }

        [TearDown]
        public void TearDown() {
            ChunkPresenterDefaults.Factory = null;
            Object.DestroyImmediate(_managerObject);
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
            var geoForgeManager = _managerObject.AddComponent<GeoForgeManager>();

            Assert.IsNull(geoForgeManager.Presenter);

            var made = new RecordingChunkPresenter();
            ChunkPresenterDefaults.Factory = manager => made;

            Assert.AreSame(made, geoForgeManager.Presenter);
        }
    }
}
