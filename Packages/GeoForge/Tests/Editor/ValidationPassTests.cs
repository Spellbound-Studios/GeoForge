// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class ValidationPassTests {
        private const int WatchedFrames = 30;

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void DestroyCreated() {
            foreach (var created in _created)
                if (created)
                    Object.DestroyImmediate(created);

            _created.Clear();
        }

        [Test]
        public void ValidationPass_SkipsAVolumeDestroyedDuringThePass() {
            var manager = Create("Validation Pass Manager").AddComponent<GeoForgeManager>();
            var first = Create("Validation Pass First Volume").AddComponent<ValidationPassVolume>();
            var second = Create("Validation Pass Second Volume").AddComponent<ValidationPassVolume>();
            first.DestroysOnFirstValidation = second;
            var volumes = VolumesOf(manager);
            volumes.Add(first);
            volumes.Add(second);

            manager.ValidateAllVolumesLodsAsync();

            Assert.AreEqual(1, first.Validations);
            Assert.IsTrue(second == null);
            Assert.AreEqual(0, second.ValidationsAfterDestroy, "a volume destroyed during the pass leaves it");
        }

        [UnityTest]
        public IEnumerator ValidationPass_EndsOnceTheManagerIsDestroyed() {
            var managerObject = Create("Validation Pass Destroyed Manager");
            var manager = managerObject.AddComponent<GeoForgeManager>();
            var volume = Create("Validation Pass Watched Volume").AddComponent<ValidationPassVolume>();
            VolumesOf(manager).Add(volume);

            manager.ValidateAllVolumesLodsAsync();
            Object.DestroyImmediate(managerObject);
            Assert.IsFalse(manager.IsActive());

            for (var frame = 0; frame < WatchedFrames; frame++)
                yield return null;

            Assert.AreEqual(1, volume.Validations, "no pass runs after the manager is gone");
        }

        private static HashSet<IGeoVolume> VolumesOf(GeoForgeManager manager) {
            return (HashSet<IGeoVolume>)typeof(GeoForgeManager)
                .GetField("_voxelVolumes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        }

        private GameObject Create(string name) {
            var created = new GameObject(name);
            _created.Add(created);

            return created;
        }
    }
}
