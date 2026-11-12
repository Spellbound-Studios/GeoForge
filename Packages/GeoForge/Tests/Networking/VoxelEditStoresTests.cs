// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Observations;
using Spellbound.Core.ReferenceFrames;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    public class VoxelEditStoresTests {
        private const ulong BaseFingerprint = 0x5EEDUL;
        private const uint SourceId = 41u;
        private const float EditRadius = 4f;
        private const int EditStrength = 127;

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3Int UpperChunk = Vector3Int.up;
        private static readonly Vector3 InsideTheFirstChunk = new(8f, 8f, 8f);
        private static readonly Vector3 OnTheBorderOfBoth = new(16f, 8f, 8f);
        private static readonly Vector3 OnTheBorderAbove = new(8f, 16f, 8f);

        private readonly List<VoxelWorld> _worlds = new();
        private ObservationContext _observations;

        [SetUp]
        public void SetUp() {
            _observations = CoreKit.Observations.CreateContext("voxel_edit_stores_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown() {
            foreach (var world in _worlds)
                world.Dispose();

            _worlds.Clear();
            _observations.Dispose();
        }

        [Test]
        public void VoxelEditStores_RoutesAChunkToItsRegionsSource() {
            var world = Create("Routing", null, FirstChunk, SecondChunk);
            var stores = world.Stores;

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(world.GeoVolume, InsideTheFirstChunk, EditRadius,
                EditStrength));

            var first = stores.ChunkRegions.RegionOf(FirstChunk);
            var second = stores.ChunkRegions.RegionOf(SecondChunk);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(stores.TryGetSource(first, out var source));
            Assert.Greater(source.PendingChanges, 0);
            Assert.AreEqual(EditsOf(world.StoreOf(FirstChunk)), source.PendingChanges);
            Assert.AreEqual(0, EditsOf(world.StoreOf(SecondChunk)));
            Assert.IsFalse(stores.TryGetSource(second, out _));
        }

        [Test]
        public void VoxelEditStores_SourceForAnswersOneSourcePerRegion() {
            var world = Create("Regions", null, FirstChunk);
            var stores = world.Stores;
            var first = stores.ChunkRegions.RegionOf(FirstChunk);
            var second = stores.ChunkRegions.RegionOf(SecondChunk);

            var firstSource = stores.SourceFor(first);

            Assert.AreSame(firstSource, stores.SourceFor(first));
            Assert.AreNotSame(firstSource, stores.SourceFor(second));
            Assert.AreEqual(2, stores.SourceCount);
            Assert.AreEqual(first, firstSource.Key);
            Assert.AreEqual(SourceId, firstSource.SourceId);
            Assert.AreEqual(BaseFingerprint, firstSource.BaseFingerprint);
            Assert.IsTrue(firstSource.IsReady);
            Assert.IsFalse(stores.SourceFor(second).IsReady);

            world.AddChunk(SecondChunk, false);
            Assert.IsFalse(stores.SourceFor(second).IsReady);

            world.Fill(SecondChunk);
            Assert.IsTrue(stores.SourceFor(second).IsReady);
        }

        [Test]
        public void VoxelEditStores_BorderChunkChangesReachOnlyTheCoveringRegion() {
            var authority = Create("Border Authority", null, FirstChunk, SecondChunk);
            var receiver = Create("Border Receiver", null, FirstChunk, SecondChunk);
            var stores = authority.Stores;

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, OnTheBorderOfBoth, EditRadius,
                EditStrength));

            var firstEdits = EditsOf(authority.StoreOf(FirstChunk));
            var secondEdits = EditsOf(authority.StoreOf(SecondChunk));
            Assert.Greater(firstEdits, 0);
            Assert.Greater(secondEdits, 0);

            var first = stores.SourceFor(stores.ChunkRegions.RegionOf(FirstChunk));
            var second = stores.SourceFor(stores.ChunkRegions.RegionOf(SecondChunk));
            Assert.AreEqual(firstEdits, first.PendingChanges);
            Assert.AreEqual(secondEdits, second.PendingChanges);

            VoxelEditTransfer.CopyChanges(first, receiver.Stores.SourceFor(first.Key));

            CollectionAssert.AreEquivalent(authority.StoreOf(FirstChunk).ReadAllEdits(),
                receiver.StoreOf(FirstChunk).ReadAllEdits());
            Assert.AreEqual(0, EditsOf(receiver.StoreOf(SecondChunk)));
        }

        [Test]
        public void ChunkRegionMap_CustomImplementation() {
            var byDefault = new VoxelWorld("Default Regions", new VoxelEditSourceSettings(BaseFingerprint, SourceId),
                _observations);
            _worlds.Add(byDefault);
            Assert.IsInstanceOf<GridChunkRegionMap>(byDefault.Stores.ChunkRegions);
            Assert.AreEqual(VoxelEditSourceSettings.DefaultRegionCellSizeMetres,
                ((GridChunkRegionMap)byDefault.Stores.ChunkRegions).CellSizeMetres);

            var map = new ColumnChunkRegionMap(0, 1);
            var authority = Create("Column Authority", map, FirstChunk, UpperChunk);
            var receiver = Create("Column Receiver", map, FirstChunk, UpperChunk);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, OnTheBorderAbove, EditRadius,
                EditStrength));

            var lowerEdits = EditsOf(authority.StoreOf(FirstChunk));
            var upperEdits = EditsOf(authority.StoreOf(UpperChunk));
            Assert.Greater(lowerEdits, 0);
            Assert.Greater(upperEdits, 0);
            Assert.Greater(map.RegionsAsked, 0);
            Assert.AreEqual(1, authority.Stores.SourceCount);

            var source = authority.Stores.SourceFor(map.RegionOf(FirstChunk));
            Assert.AreEqual(lowerEdits + upperEdits, source.PendingChanges);

            VoxelEditTransfer.CopySnapshot(source, receiver.Stores.SourceFor(source.Key), 256);

            Assert.AreEqual(VoxelEditDigest.Of(authority), VoxelEditDigest.Of(receiver));
            Assert.AreEqual(authority.VoxelAt(OnTheBorderAbove), receiver.VoxelAt(OnTheBorderAbove));
        }

        [Test]
        public void Dispose_HandsTheVolumeBackTheDefaultStores() {
            var world = Create("Disposed", null, FirstChunk);
            Assert.AreSame(world.Stores, world.Volume.EditStores);

            world.Stores.Dispose();

            Assert.IsInstanceOf<GeoEditStoreSource>(world.Volume.EditStores);
            Assert.IsInstanceOf<GeoEditStore>(world.Volume.EditStores.CreateFor(SecondChunk));
        }

        [Test]
        public void GridChunkRegionMap_AgreesWithTheRegionGeometry() {
            var chunks = new[] { new Vector3Int(0, 0, 0), new Vector3Int(3, -1, 2), new Vector3Int(-2, 4, -5) };
            var cellSizes = new[] { 16f, 64f };
            var world = new VoxelWorld("Geometry", new VoxelEditSourceSettings(BaseFingerprint, SourceId),
                _observations);
            _worlds.Add(world);
            world.Volume.transform.position = new Vector3(5.5f, -3.25f, 7.75f);
            var placed = new List<Vector3>();

            foreach (var chunk in chunks)
                placed.Add(world.AddChunk(chunk, false).transform.position);

            var inCell = new List<Vector3Int>();

            foreach (var cellSize in cellSizes) {
                var map = new GridChunkRegionMap(world.GeoVolume, cellSize);
                var geometry = new GridRegionGeometry(cellSize);

                for (var index = 0; index < chunks.Length; index++) {
                    var expected = geometry.KeyOf(ReferenceFrameId.World, placed[index]);

                    var label = "chunk " + chunks[index] + " at " + cellSize;
                    Assert.AreEqual(expected, map.RegionOf(chunks[index]), label);
                    map.ChunksOf(expected, inCell);
                    CollectionAssert.Contains(inCell, chunks[index], label);

                    foreach (var other in inCell)
                        Assert.AreEqual(expected, map.RegionOf(other));
                }
            }
        }

        private VoxelWorld Create(string name, IChunkRegionMap map, params Vector3Int[] chunks) {
            var world = new VoxelWorld(name,
                new VoxelEditSourceSettings(BaseFingerprint, SourceId, map, 0, VoxelWorld.ChunkSize),
                _observations, chunks);
            _worlds.Add(world);

            return world;
        }

        private static int EditsOf(IGeoEditStore store) {
            var count = 0;

            foreach (var _ in store.ReadAllEdits())
                count++;

            return count;
        }
    }
}