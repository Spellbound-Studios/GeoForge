// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Observations;
using Spellbound.Core.Packing;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    public class VoxelEditRecordTests {
        private const ulong BaseFingerprint = 0x7EC0UL;
        private const uint SourceId = 5u;
        private const float EditRadius = 4f;
        private const int DigStrength = 127;
        private const int CarriedVoxels = 2000;
        private const int SmallBufferBytes = 4096;
        private const int LargestFlushes = 50;

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3 InsideTheFirstChunk = new(8f, 8f, 8f);
        private static readonly Vector3 InsideTheSecondChunk = new(24f, 8f, 8f);

        private readonly List<VoxelWorld> _worlds = new();
        private ObservationContext _observations;

        [SetUp]
        public void SetUp() {
            _observations = CoreKit.Observations.CreateContext("voxel_edit_record_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown() {
            foreach (var world in _worlds)
                world.Dispose();

            _worlds.Clear();
            _observations.Dispose();
        }

        [Test]
        public void ApplyChanges_RefusesARecordOutsideTheRegion() {
            var authority = Create("Outside Authority", FirstChunk, SecondChunk);
            var receiver = Create("Outside Receiver", FirstChunk, SecondChunk);
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheSecondChunk, EditRadius,
                DigStrength));
            var regions = authority.Stores.ChunkRegions;
            var secondSource = authority.Stores.SourceFor(regions.RegionOf(SecondChunk));
            Assert.Greater(secondSource.PendingChanges, 0);

            var buffer = new byte[VoxelEditTransfer.SectionBytes];
            var records = VoxelEditTransfer.WriteChanges(secondSource, buffer, out var bytes);
            VoxelEditTransfer.ApplyChanges(receiver.Stores.SourceFor(regions.RegionOf(FirstChunk)), buffer, bytes,
                records);

            Assert.AreEqual(0, VoxelEditDigest.CountOf(receiver));

            var page = new byte[VoxelEditTransfer.SectionBytes];
            var cursor = new Spellbound.Networking.Replication.SnapshotCursor(0UL, false);
            var pageWriter = new BitWriter(page);
            secondSource.WriteSnapshotPage(ref cursor, ref pageWriter);
            var pageReader = new BitReader(new ReadOnlySpan<byte>(page, 0, pageWriter.BytesWritten));
            receiver.Stores.SourceFor(regions.RegionOf(FirstChunk)).ApplySnapshotPage(ref pageReader);

            Assert.AreEqual(0, VoxelEditDigest.CountOf(receiver));
        }

        [Test]
        public void JoinerKeepsRecordsForChunksNotYetMade() {
            var authority = Create("Joiner Authority", FirstChunk, SecondChunk);
            var joiner = Create("Joiner", FirstChunk);
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheSecondChunk, EditRadius,
                DigStrength));
            var key = authority.Stores.ChunkRegions.RegionOf(SecondChunk);
            var source = authority.Stores.SourceFor(key);
            source.ClearChanges();

            VoxelEditTransfer.CopySnapshot(source, joiner.Stores.SourceFor(key), 512);
            joiner.AddChunk(SecondChunk, true);

            Assert.Greater(VoxelEditDigest.CountOf(authority), 0);
            Assert.AreEqual(VoxelEditDigest.Of(authority), VoxelEditDigest.Of(joiner));
            Assert.AreEqual(authority.VoxelAt(InsideTheSecondChunk), joiner.VoxelAt(InsideTheSecondChunk));
        }

        [Test]
        public void WriteChanges_LargerThanTheBufferCarriesOverTicks() {
            var authority = Create("Carry Authority", FirstChunk);
            var receiver = Create("Carry Receiver", FirstChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);
            var notifier = new CountingDirtyNotifier();
            source.Attach(notifier);
            var edits = new List<(int, VoxelData)>(CarriedVoxels);

            for (var index = 0; index < CarriedVoxels; index++) {
                var density = (sbyte)(index % 3 == 0 ? -60 : 40);
                var material = (byte)(index % 3 == 0 ? VoxelData.NullSentinelValue : 1 + index % 5);
                edits.Add((index * 3, VoxelData.CreateImmature(density, material)));
            }

            authority.StoreOf(FirstChunk).Write(edits);
            Assert.AreEqual(CarriedVoxels, source.PendingChanges);
            var marksBefore = notifier.Marks;
            var buffer = new byte[SmallBufferBytes];
            var flushes = 0;
            var carried = 0;

            while (source.PendingChanges > 0) {
                Assert.Less(++flushes, LargestFlushes, "the edit never finished carrying over");
                var records = VoxelEditTransfer.WriteChanges(source, buffer, out var bytes);
                Assert.Greater(records, 0);
                Assert.LessOrEqual(bytes, SmallBufferBytes);
                source.ClearChanges();
                VoxelEditTransfer.ApplyChanges(receiver.Stores.SourceFor(key), buffer, bytes, records);
                carried += records;
            }

            Assert.Greater(flushes, 1);
            Assert.AreEqual(CarriedVoxels, carried);
            Assert.AreEqual(flushes - 1, notifier.Marks - marksBefore);
            Assert.AreEqual(VoxelEditDigest.Of(authority), VoxelEditDigest.Of(receiver));
        }

        [Test]
        public void Reset_ReachesPeersAsTombstones() {
            var authority = Create("Reset Authority", FirstChunk);
            var peer = Create("Reset Peer", FirstChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);
            var procedural = authority.VoxelAt(InsideTheFirstChunk);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheFirstChunk, EditRadius,
                DigStrength));
            VoxelEditTransfer.CopyChanges(source, peer.Stores.SourceFor(key));
            Assert.Greater(VoxelEditDigest.CountOf(peer), 0);
            Assert.AreNotEqual(procedural, peer.VoxelAt(InsideTheFirstChunk));

            authority.Volume.ResetEditedChunksToProcedural();

            Assert.AreEqual(0, VoxelEditDigest.CountOf(authority));
            Assert.Greater(source.PendingChanges, 0);
            VoxelEditTransfer.CopyChanges(source, peer.Stores.SourceFor(key));

            Assert.AreEqual(0, VoxelEditDigest.CountOf(peer));
            Assert.AreEqual(procedural, authority.VoxelAt(InsideTheFirstChunk));
            Assert.AreEqual(procedural, peer.VoxelAt(InsideTheFirstChunk));
        }

        [Test]
        public void Tombstone_AppliedTwiceIsOneState() {
            var authority = Create("Tombstone Authority", FirstChunk);
            var peer = Create("Tombstone Peer", FirstChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);
            var procedural = authority.VoxelAt(InsideTheFirstChunk);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheFirstChunk, EditRadius,
                DigStrength));
            VoxelEditTransfer.CopyChanges(source, peer.Stores.SourceFor(key));
            authority.Volume.ResetEditedChunksToProcedural();

            var buffer = new byte[VoxelEditTransfer.SectionBytes];
            var records = VoxelEditTransfer.WriteChanges(source, buffer, out var bytes);
            Assert.Greater(records, 0);
            VoxelEditTransfer.ApplyChanges(peer.Stores.SourceFor(key), buffer, bytes, records);
            var once = VoxelEditDigest.Of(peer);
            VoxelEditTransfer.ApplyChanges(peer.Stores.SourceFor(key), buffer, bytes, records);

            Assert.AreEqual(0, VoxelEditDigest.CountOf(peer));
            Assert.AreEqual(once, VoxelEditDigest.Of(peer));
            Assert.AreEqual(VoxelEditDigest.Of(authority), VoxelEditDigest.Of(peer));
            Assert.AreEqual(procedural, peer.VoxelAt(InsideTheFirstChunk));
        }

        [Test]
        public void ResetToEmpty_OverARegionHoldingEdits() {
            var world = Create("Resync Holding Edits", FirstChunk);
            var fresh = Create("Resync Fresh", FirstChunk);
            var key = world.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = world.Stores.SourceFor(key);
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(world.GeoVolume, InsideTheFirstChunk, EditRadius,
                DigStrength));
            source.ClearChanges();
            Assert.Greater(VoxelEditDigest.CountOf(world), 0);
            Assert.AreNotEqual(fresh.VoxelAt(InsideTheFirstChunk), world.VoxelAt(InsideTheFirstChunk));

            source.ResetToEmpty();

            Assert.AreEqual(0, VoxelEditDigest.CountOf(world));
            Assert.AreEqual(VoxelEditDigest.Of(fresh), VoxelEditDigest.Of(world));
            Assert.AreEqual(fresh.VoxelAt(InsideTheFirstChunk), world.VoxelAt(InsideTheFirstChunk));
            Assert.AreEqual(0, source.PendingChanges);
        }

        [Test]
        public void VolumeReset_ClearsTheStoresOfUnloadedChunks() {
            var world = Create("Unloaded Reset", FirstChunk);
            var unloaded = world.Stores.CreateFor(SecondChunk);
            unloaded.Write(new List<(int, VoxelData)> {
                (40, VoxelData.CreateImmature(-50, VoxelData.NullSentinelValue))
            });
            Assert.IsTrue(unloaded.HasEdits);

            world.Volume.ResetEditedChunksToProcedural();

            Assert.IsFalse(unloaded.HasEdits);
        }

        private VoxelWorld Create(string name, params Vector3Int[] chunks) {
            var world = new VoxelWorld(name,
                new VoxelEditSourceSettings(BaseFingerprint, SourceId, null, 0, VoxelWorld.ChunkSize), _observations,
                chunks);
            _worlds.Add(world);

            return world;
        }
    }
}