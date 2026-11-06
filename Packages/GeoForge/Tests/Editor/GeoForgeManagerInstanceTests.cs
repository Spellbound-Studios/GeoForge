// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class GeoForgeManagerInstanceTests {
        private const int ChunkSize = 16;
        private const uint Seed = 7;
        private const int SearchExtent = 4;
        private const float SphereSize = 2f;
        private const short SphereDelta = 40;
        private const byte SphereMaterial = 1;
        private const float PlaneHeight = 4f;

        private readonly List<GeoChunkDouble> _chunks = new();
        private readonly List<Object> _created = new();
        private readonly List<GeoForgeManager> _managers = new();
        private readonly List<GeoVolumeDouble> _volumes = new();

        [TearDown]
        public void TearDown() {
            foreach (var chunk in _chunks)
                chunk.GeoChunkEngine.Dispose();

            foreach (var volume in _volumes)
                volume.GeoVolumeEngine.Dispose();

            var endLifetime = typeof(GeoForgeManager).GetMethod("OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var manager in _managers)
                endLifetime.Invoke(manager, null);

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _chunks.Clear();
            _volumes.Clear();
            _managers.Clear();
            _created.Clear();
        }

        [Test]
        public void AnEditInOneWorldLeavesTheSameVoxelOfAnotherWorldUnchanged() {
            var first = CreateWorld("First World");
            var second = CreateWorld("Second World");
            var position = FindSurfacePosition(first.Manager);

            Assert.IsTrue(first.Manager.TryQueryVoxel(position, out var firstBefore, out var firstVolume));
            Assert.IsTrue(second.Manager.TryQueryVoxel(position, out var secondBefore, out var secondVolume));
            Assert.AreSame(first.Volume, firstVolume);
            Assert.AreSame(second.Volume, secondVolume);
            Assert.AreEqual(firstBefore.Density, secondBefore.Density);

            Assert.IsTrue(GeoForgeCommands.AddSphere(first.Volume, position, SphereSize, SphereDelta, SphereMaterial));

            first.Manager.TryQueryVoxel(position, out var firstAfter, out _);
            second.Manager.TryQueryVoxel(position, out var secondAfter, out _);
            Assert.AreNotEqual(firstBefore.Density, firstAfter.Density);
            Assert.AreEqual(secondBefore.Density, secondAfter.Density);
            Assert.AreEqual(secondBefore.MaterialIndex, secondAfter.MaterialIndex);
        }

        [Test]
        public void AnEditMarchesOnlyThroughTheManagerOfItsVolume() {
            var first = CreateWorld("First World");
            var second = CreateWorld("Second World");
            var position = FindSurfacePosition(first.Manager);
            var firstMeshesBefore = MeshesApplied(first.Presenter);
            var secondMeshesBefore = MeshesApplied(second.Presenter);

            Assert.Greater(firstMeshesBefore, 0);
            Assert.AreEqual(firstMeshesBefore, secondMeshesBefore);

            Assert.IsTrue(GeoForgeCommands.AddSphere(first.Volume, position, SphereSize, SphereDelta, SphereMaterial));

            Assert.Greater(MeshesApplied(first.Presenter), firstMeshesBefore);
            Assert.AreEqual(secondMeshesBefore, MeshesApplied(second.Presenter));
        }

        [Test]
        public void AddSphereReachesTheManagerOfTheVolumeItIsGiven() {
            var first = CreateWorld("First World");
            var second = CreateWorld("Second World");
            var position = FindSurfacePosition(first.Manager);
            var batchingManagers = new List<GeoForgeManager>();

            first.Chunk.EditOperationPassed = _ => {
                if (first.Manager.IsBatchingEdits)
                    batchingManagers.Add(first.Manager);

                if (second.Manager.IsBatchingEdits)
                    batchingManagers.Add(second.Manager);
            };

            second.Chunk.EditOperationPassed = _ => Assert.Fail("The edit reached the other world's chunk.");

            Assert.IsTrue(GeoForgeCommands.AddSphere(first.Volume, position, SphereSize, SphereDelta, SphereMaterial));

            Assert.IsNotEmpty(batchingManagers);
            Assert.IsTrue(batchingManagers.All(manager => manager == first.Manager));
            Assert.IsFalse(first.Manager.IsBatchingEdits);
        }

        [Test]
        public void AResidencySubscriberThatThrowsRunsAfterTheApplyHasFinished() {
            using var world = new PlaneWorld(PlaneHeight, Vector3Int.zero);
            world.Manager.Presenter = new RecordingChunkPresenter();
            world.Manager.CollisionPresenter = new RecordingCollisionPresenter();
            var applyingSeenBySubscriber = new List<bool>();
            ((IGeoVolume)world.Volume).ResidencyChanged += residency => {
                applyingSeenBySubscriber.Add(world.Manager.IsApplyingMarchResults);

                if (applyingSeenBySubscriber.Count == 1)
                    throw new InvalidOperationException(nameof(ChunkResidency));
            };
            LogAssert.Expect(LogType.Error, new Regex(nameof(ChunkResidency)));

            world.ValidateEveryChunk(Vector3.zero);

            Assert.IsFalse(world.Manager.IsApplyingMarchResults);
            CollectionAssert.IsNotEmpty(applyingSeenBySubscriber);
            CollectionAssert.DoesNotContain(applyingSeenBySubscriber, true);
        }

        private (GeoForgeManager Manager, GeoVolumeDouble Volume, GeoChunkDouble Chunk,
            RecordingChunkPresenter Presenter) CreateWorld(string worldName) {
            var manager = Track(new GameObject(worldName)).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager)
                .GetProperty(nameof(GeoForgeManager.McTablesBlob), BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, MarchTables.Build(Allocator.Persistent));
            var presenter = new RecordingChunkPresenter();
            manager.Presenter = presenter;
            manager.jobAndRenderProfile = Track(ScriptableObject.CreateInstance<BarycentricMaterialRenderingProfile>());
            _managers.Add(manager);

            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = ChunkSize;
            config.levelsOfDetail = 1;
            config.maxChunkSize = ChunkSize;
            config.chunkSize = ChunkSize;
            config.sizeInChunks = Vector3Int.one;

            var volume = Track(new GameObject($"{worldName} Volume")).AddComponent<GeoVolumeDouble>();
            var engine = new GeoVolumeEngine(volume, volume, config, manager);
            volume.GeoVolumeEngine = engine;
            volume.ConfigBlob = engine.ConfigBlob;
            volume.IsPrimaryTerrain = true;
            volume.ViewDistanceLodRanges = new[] { new Vector2(0f, float.MaxValue) };
            _volumes.Add(volume);
            engine.RegisterVolume();

            var chunkTransform = new GameObject($"{worldName} Chunk").transform;
            chunkTransform.SetParent(volume.transform, false);
            var chunk = new GeoChunkDouble(chunkTransform);
            _chunks.Add(chunk);
            engine.RegisterChunk(chunk.GeoChunkEngine.ChunkCoord, chunk);

            var factory = Track(ScriptableObject.CreateInstance<PerlinTerrainDataFactory>());
            var voxels = factory.GenerateProceduralVoxels(chunk.GeoChunkEngine.ChunkCoord, engine.ConfigBlob, Seed);
            chunk.GeoChunkEngine.SetVoxels(voxels);
            using var viewers = new NativeArray<ChunkViewer>(1, Allocator.Temp);
            chunk.GeoChunkEngine.ScheduleOctreeLodValidation(viewers, new DistanceChunkInterest());
            manager.CompleteAndApplyMarchingCubesJobs();
            chunk.GeoChunkEngine.ReleaseLodValidation();

            return (manager, volume, chunk, presenter);
        }

        private static Vector3 FindSurfacePosition(GeoForgeManager manager) {
            var nearest = Vector3.zero;
            var nearestDensity = int.MaxValue;

            for (var x = -SearchExtent; x < SearchExtent; x++)
            for (var y = -SearchExtent; y < SearchExtent; y++)
            for (var z = -SearchExtent; z < SearchExtent; z++) {
                var position = new Vector3(x, y, z);

                if (!manager.TryQueryVoxel(position, out var voxel, out _))
                    continue;

                var density = Mathf.Abs(voxel.Density);

                if (density >= nearestDensity)
                    continue;

                nearestDensity = density;
                nearest = position;
            }

            Assert.Less(nearestDensity, sbyte.MaxValue, "No voxel near the surface lies inside the search extent.");

            return nearest;
        }

        private static int MeshesApplied(RecordingChunkPresenter presenter) {
            return presenter.Calls.Count(call => call == nameof(IChunkLeaf.SetMesh));
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
