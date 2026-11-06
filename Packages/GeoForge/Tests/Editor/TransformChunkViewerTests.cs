// Copyright (c) Spellbound Studios. All rights reserved.

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class TransformChunkViewerTests {
        private PlaneWorld _world;

        [TearDown]
        public void TearDown() {
            _world?.Dispose();
            _world = null;
        }

        [Test]
        public void TheRadiiComeFromAnEngineThatAppearsAfterStart() {
            _world = new PlaneWorld(0f, Vector3Int.zero);
            _world.Manager.Presenter = null;
            _world.Manager.CollisionPresenter = null;
            var engine = _world.Engine;
            var interest = new ViewerRecordingInterest();
            ((IGeoVolume)_world.Volume).ChunkInterest = interest;
            _world.Volume.GeoVolumeEngine = null;
            var viewer = _world.Volume.gameObject.AddComponent<TransformChunkViewer>();
            viewer.Viewpoint = _world.Volume.transform;

            Run(viewer, "Start");
            _world.Volume.GeoVolumeEngine = engine;
            Run(viewer, "Update");
            _ = engine.ValidateChunkLodsAsync();

            Assert.AreEqual(1, interest.ViewersSeen.Length);
            ref var config = ref engine.ConfigBlob.Value;
            Assert.AreEqual(config.UnitsPerChunk / config.Resolution, interest.ViewersSeen[0].CollisionRadius);
            Assert.AreEqual(0f, interest.ViewersSeen[0].DetailRadius);
        }

        private static void Run(TransformChunkViewer viewer, string message) {
            typeof(TransformChunkViewer).GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(viewer, null);
        }
    }
}
