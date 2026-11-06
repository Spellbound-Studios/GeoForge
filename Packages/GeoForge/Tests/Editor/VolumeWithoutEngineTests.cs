// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Spellbound.GeoForge.Tests {
    public class VolumeWithoutEngineTests {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown() {
            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void AVolumeWithoutAnEngineAnswersFalseAndDoesNothing() {
            IGeoVolume volume = Track(new GameObject("Volume Without Engine")).AddComponent<GeoVolumeDouble>();
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            using var runs = new NativeList<SparseVoxelData>(Allocator.Temp);
            using var solidRuns = new NativeList<SolidRun>(Allocator.Temp);

            Assert.IsFalse(volume.TryQueryVoxel(Vector3.zero, out _));
            Assert.IsFalse(volume.TryQuerySurfaceMaterial(Vector3.zero, out _));
            Assert.IsFalse(volume.TryQuerySurfaceCase(Vector3.zero, out _));
            Assert.IsFalse(volume.TryQuerySolid(Vector3.zero, out _));
            Assert.IsFalse(volume.IsMeshReady(Vector3.zero, ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsColliderReady(Vector3.zero, ReadinessScope.AnyLevel));
            Assert.IsFalse(volume.IsBoxReady(bounds, ReadinessScope.AnyLevel, out _));
            Assert.IsFalse(volume.TryCopyRunList(Vector3Int.zero, runs, out _));
            Assert.IsFalse(volume.TryCopySolidRuns(Vector3Int.zero, solidRuns, out _));
            Assert.IsFalse(volume.IntersectsVolume(bounds));
            Assert.IsFalse(volume.TryLoadFromByteArray(new byte[] { 0 }));
            Assert.IsNull(volume.GetChunkByCoord(Vector3Int.zero));
            Assert.IsNull(volume.DensitySource);
            Assert.IsNull(volume.ChunkInterest);

            Assert.DoesNotThrow(() => {
                volume.SetViewers(new[] { new ChunkViewer(Vector3.zero, 1f, 0f) });
                volume.UseDensitySource(new PlaneDensitySource(0f));
                volume.ChunkInterest = new DistanceChunkInterest();
            });
        }

        [Test]
        public void SubscribingBeforeTheEngineExistsIsRefusedWithAWarningNamingTheEvent() {
            IGeoVolume volume = Track(new GameObject("Volume Without Engine")).AddComponent<GeoVolumeDouble>();

            LogAssert.Expect(LogType.Warning, new Regex(nameof(IGeoVolume.Changed)));
            volume.Changed += _ => { };

            LogAssert.Expect(LogType.Warning, new Regex(nameof(IGeoVolume.ResidencyChanged)));
            volume.ResidencyChanged += _ => { };

            Assert.DoesNotThrow(() => volume.Changed -= _ => { });
        }

        [Test]
        public void ADensitySourceGivenBeforeTheEngineExistsIsTheEnginesOnceItDoes() {
            var manager = Track(new GameObject("Manager")).AddComponent<GeoForgeManager>();
            typeof(GeoForgeManager)
                .GetProperty(nameof(GeoForgeManager.McTablesBlob), BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, MarchTables.Build(Allocator.Persistent));
            var volumeObject = Track(new GameObject("Deferred Volume"));
            volumeObject.transform.SetParent(manager.transform, false);
            var volume = volumeObject.AddComponent<DeferredGeoVolume>();
            var config = Track(ScriptableObject.CreateInstance<VoxelVolumeConfig>());
            config.cubesPerMarch = PlaneWorld.ChunkSize;
            config.levelsOfDetail = 1;
            config.maxChunkSize = PlaneWorld.ChunkSize;
            config.chunkSize = PlaneWorld.ChunkSize;
            config.sizeInChunks = Vector3Int.one;
            var source = new PlaneDensitySource(0f);

            try {
                volume.UseDensitySource(source);

                Assert.AreSame(source, volume.DensitySource);

                volume.CreateEngine(config);

                Assert.AreSame(source, volume.GeoVolumeEngine.DensitySource);
                Assert.AreSame(source, volume.DensitySource);
            }
            finally {
                volume.GeoVolumeEngine?.Dispose();
                typeof(GeoForgeManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);
            }
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
