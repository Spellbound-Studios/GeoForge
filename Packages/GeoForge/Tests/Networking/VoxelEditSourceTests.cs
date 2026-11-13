// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Intents;
using Spellbound.Core.Observations;
using Spellbound.Core.Packing;
using Spellbound.Core.ReferenceFrames;
using Spellbound.Core.Simulation;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    public class VoxelEditSourceTests {
        private const ulong BaseFingerprint = 0xB0A7UL;
        private const uint SourceId = 7u;
        private const float WideRadius = 6f;
        private const float NarrowRadius = 4f;
        private const float SmallestRadius = 2f;
        private const int DigStrength = 127;
        private const short GentleStrength = 60;
        private const int SmallPageBytes = 64;
        private const int LargePageBytes = 4096;
        private const int LedgerWindow = 64;
        private const ulong DepartureGraceTicks = 100UL;
        private const int WarmUps = 3;
        private const float TwoChunksWide = 2f * VoxelWorld.ChunkSize;

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3 InsideTheFirstChunk = new(8f, 8f, 8f);
        private static readonly Vector3 BesideTheFirstEdit = new(10f, 9f, 8f);
        private static readonly Vector3 CornerOfEightChunks = new(16f, 16f, 16f);
        private static readonly ParticipantId Sender = new(7UL);

        private static readonly Vector3Int[] ThreeChunks = { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };

        private static readonly Vector3Int[] EightChunks = {
            new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0),
            new(0, 0, 1), new(1, 0, 1), new(0, 1, 1), new(1, 1, 1)
        };

        private readonly List<VoxelWorld> _worlds = new();
        private ObservationContext _observations;

        [SetUp]
        public void SetUp() {
            _observations = CoreKit.Observations.CreateContext("voxel_edit_source_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown() {
            foreach (var world in _worlds)
                world.Dispose();

            _worlds.Clear();
            _observations.Dispose();
        }

        [Test]
        public void VoxelEditSource_SnapshotThenChangesEqualsOneSnapshot() {
            var authority = Create("Snapshot Authority", null, FirstChunk, SecondChunk);
            var joiner = Create("Snapshot Joiner", null, FirstChunk, SecondChunk);
            var late = Create("Snapshot Late", null, FirstChunk, SecondChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheFirstChunk, WideRadius,
                DigStrength));
            source.ClearChanges();

            var pages = VoxelEditTransfer.CopySnapshot(source, joiner.Stores.SourceFor(key), SmallPageBytes);
            Assert.Greater(pages, 1);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, BesideTheFirstEdit, NarrowRadius,
                GentleStrength));
            Assert.Greater(source.PendingChanges, 0);
            VoxelEditTransfer.CopyChanges(source, joiner.Stores.SourceFor(key));

            VoxelEditTransfer.CopySnapshot(source, late.Stores.SourceFor(key), LargePageBytes);

            var digest = VoxelEditDigest.Of(authority);
            Assert.Greater(VoxelEditDigest.CountOf(authority), 0);
            Assert.AreEqual(digest, VoxelEditDigest.Of(joiner));
            Assert.AreEqual(digest, VoxelEditDigest.Of(late));
            Assert.AreEqual(authority.VoxelAt(InsideTheFirstChunk), joiner.VoxelAt(InsideTheFirstChunk));
            Assert.AreEqual(authority.VoxelAt(BesideTheFirstEdit), joiner.VoxelAt(BesideTheFirstEdit));
            Assert.AreEqual(authority.VoxelAt(BesideTheFirstEdit), late.VoxelAt(BesideTheFirstEdit));
        }

        [Test]
        public void VoxelEditSource_OverOccupancySkip_SnapshotThenChangesEqualsOneState() {
            var authorityGround = new GroundOccupancySource(VoxelWorld.GroundHeight);
            var joinerGround = new GroundOccupancySource(VoxelWorld.GroundHeight);
            var lateGround = new GroundOccupancySource(VoxelWorld.GroundHeight);
            var authority = CreateOverOccupancy("Skip Snapshot Authority", authorityGround, VoxelWorld.ChunkSize, true,
                FirstChunk, SecondChunk);
            var joiner = CreateOverOccupancy("Skip Snapshot Joiner", joinerGround, VoxelWorld.ChunkSize, true,
                FirstChunk, SecondChunk);
            var late = CreateOverOccupancy("Skip Snapshot Late", lateGround, VoxelWorld.ChunkSize, true, FirstChunk,
                SecondChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);
            Assert.AreEqual(0, authorityGround.Fills + joinerGround.Fills + lateGround.Fills);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheFirstChunk, WideRadius,
                DigStrength));
            source.ClearChanges();

            var pages = VoxelEditTransfer.CopySnapshot(source, joiner.Stores.SourceFor(key), SmallPageBytes);
            Assert.Greater(pages, 1);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, BesideTheFirstEdit, NarrowRadius,
                GentleStrength));
            Assert.Greater(source.PendingChanges, 0);
            VoxelEditTransfer.CopyChanges(source, joiner.Stores.SourceFor(key));

            VoxelEditTransfer.CopySnapshot(source, late.Stores.SourceFor(key), LargePageBytes);

            var digest = VoxelEditDigest.Of(authority);
            Assert.Greater(VoxelEditDigest.CountOf(authority), 0);
            Assert.AreEqual(digest, VoxelEditDigest.Of(joiner));
            Assert.AreEqual(digest, VoxelEditDigest.Of(late));
            Assert.AreEqual(1, authorityGround.Fills);
            Assert.AreEqual(1, joinerGround.Fills);
            Assert.AreEqual(1, lateGround.Fills);
            AssertEveryRecordIsApplied(authority);
            AssertEveryRecordIsApplied(joiner);
            AssertEveryRecordIsApplied(late);
            Assert.AreEqual(authority.VoxelAt(InsideTheFirstChunk), joiner.VoxelAt(InsideTheFirstChunk));
            Assert.AreEqual(authority.VoxelAt(BesideTheFirstEdit), joiner.VoxelAt(BesideTheFirstEdit));
            Assert.AreEqual(authority.VoxelAt(BesideTheFirstEdit), late.VoxelAt(BesideTheFirstEdit));
        }

        [Test]
        public void VoxelEditSource_OverOccupancySkip_IsFilledWaitsForTheRealFill() {
            var ground = new GroundOccupancySource(VoxelWorld.GroundHeight);
            var world = CreateOverOccupancy("Skip Filled", ground, TwoChunksWide, false, FirstChunk, SecondChunk);
            var key = world.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = world.Stores.SourceFor(key);
            Assert.AreEqual(key, world.Stores.ChunkRegions.RegionOf(SecondChunk));

            world.StoreOf(FirstChunk).Write(Edits(3, 40, 7));
            Assert.IsFalse(source.IsReady);

            world.Fill(SecondChunk);
            Assert.IsFalse(source.IsReady);
            Assert.AreEqual(0, ground.Fills);

            world.Fill(FirstChunk);
            Assert.IsTrue(source.IsReady);
            Assert.AreEqual(1, ground.Fills);
            AssertEveryRecordIsApplied(world);
        }

        [Test]
        public void VoxelEditSource_ApplyTwiceIsOneState() {
            var authority = Create("Twice Authority", null, FirstChunk);
            var once = Create("Twice Once", null, FirstChunk);
            var twice = Create("Twice Twice", null, FirstChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(authority.GeoVolume, InsideTheFirstChunk, WideRadius,
                DigStrength));
            var buffer = new byte[VoxelEditTransfer.SectionBytes];
            var records = VoxelEditTransfer.WriteChanges(source, buffer, out var bytes);
            Assert.Greater(records, 0);

            VoxelEditTransfer.ApplyChanges(once.Stores.SourceFor(key), buffer, bytes, records);
            VoxelEditTransfer.ApplyChanges(twice.Stores.SourceFor(key), buffer, bytes, records);
            VoxelEditTransfer.ApplyChanges(twice.Stores.SourceFor(key), buffer, bytes, records);

            Assert.AreEqual(VoxelEditDigest.Of(authority), VoxelEditDigest.Of(once));
            Assert.AreEqual(VoxelEditDigest.Of(once), VoxelEditDigest.Of(twice));
            Assert.AreEqual(once.VoxelAt(InsideTheFirstChunk), twice.VoxelAt(InsideTheFirstChunk));
            Assert.AreEqual(0, twice.Stores.SourceFor(key).PendingChanges);
        }

        [Test]
        public void VoxelEditSource_KeyOrderPacksTheSameBytes() {
            var forwards = CreateOverRegions("Order Forwards", TwoChunksWide, FirstChunk, SecondChunk);
            var backwards = CreateOverRegions("Order Backwards", TwoChunksWide, FirstChunk, SecondChunk);
            var firstEdits = Edits(3, 40, 7);
            var secondEdits = Edits(5, 30, 11);

            forwards.StoreOf(FirstChunk).Write(new List<(int, VoxelData)>(firstEdits));
            forwards.StoreOf(SecondChunk).Write(new List<(int, VoxelData)>(secondEdits));

            for (var index = secondEdits.Count - 1; index >= 0; index--)
                backwards.StoreOf(SecondChunk).Write(new List<(int, VoxelData)> { secondEdits[index] });

            for (var index = firstEdits.Count - 1; index >= 0; index--)
                backwards.StoreOf(FirstChunk).Write(new List<(int, VoxelData)> { firstEdits[index] });

            var key = forwards.Stores.ChunkRegions.RegionOf(FirstChunk);
            var forwardsBuffer = new byte[VoxelEditTransfer.SectionBytes];
            var backwardsBuffer = new byte[VoxelEditTransfer.SectionBytes];
            var forwardsRecords = VoxelEditTransfer.WriteChanges(forwards.Stores.SourceFor(key), forwardsBuffer,
                out var forwardsBytes);
            var backwardsRecords = VoxelEditTransfer.WriteChanges(backwards.Stores.SourceFor(key), backwardsBuffer,
                out var backwardsBytes);

            Assert.AreEqual(firstEdits.Count + secondEdits.Count, forwardsRecords);
            Assert.AreEqual(forwardsRecords, backwardsRecords);
            Assert.AreEqual(forwardsBytes, backwardsBytes);
            CollectionAssert.AreEqual(forwardsBuffer.AsSpan(0, forwardsBytes).ToArray(),
                backwardsBuffer.AsSpan(0, backwardsBytes).ToArray());
        }

        [Test]
        public void VoxelEditSource_CommandAppliedOnceWithItsReceipt() {
            var authority = Create("Command Authority", null, FirstChunk);
            var reference = Create("Command Reference", null, FirstChunk);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            using var hub = new RegionHub(key, ProtocolSettings.Default, _observations);
            hub.Bind(authority.Stores.SourceFor(key), WritePolicy.Server);
            hub.Seal();

            var command = VoxelEditCommand.RemoveSphere(InsideTheFirstChunk, WideRadius, DigStrength);
            var commandBuffer = new byte[64];
            var commandWriter = new BitWriter(commandBuffer);
            Assert.IsTrue(command.TryWrite(ref commandWriter));
            var commandBytes = commandWriter.BytesWritten;
            var commandReader = new BitReader(new ReadOnlySpan<byte>(commandBuffer, 0, commandBytes));
            Assert.IsTrue(VoxelEditCommand.TryRead(ref commandReader, out var readBack));
            Assert.AreEqual(command, readBack);

            var writer = new CommandWriter(new IntentSequence(Sender.ToIntentActor()));
            var envelope = new byte[256];
            Assert.IsTrue(writer.TryWrite(0, commandBuffer.AsSpan(0, commandBytes), envelope, out var written,
                out _));
            var ledger = new IntentLedger(LedgerWindow, new TickSpan(DepartureGraceTicks), _observations);

            var verdict = hub.TryApplyCommand(Sender, envelope.AsSpan(0, written), ledger, new Tick(1),
                out var receipt);

            Assert.AreEqual(CommandVerdict.Applied, verdict);
            Assert.AreEqual(new IntentReceipt(VoxelEditObservations.CommandApplied, 1u), receipt);
            var digestOnce = VoxelEditDigest.Of(authority);
            Assert.Greater(VoxelEditDigest.CountOf(authority), 0);

            var repeat = hub.TryApplyCommand(Sender, envelope.AsSpan(0, written), ledger, new Tick(2),
                out var repeated);

            Assert.AreEqual(CommandVerdict.Repeated, repeat);
            Assert.AreEqual(receipt, repeated);
            Assert.AreEqual(digestOnce, VoxelEditDigest.Of(authority));

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(reference.GeoVolume, InsideTheFirstChunk, WideRadius,
                DigStrength));
            Assert.AreEqual(VoxelEditDigest.Of(reference), digestOnce);

            var batch = new byte[VoxelBatches.FlushBytes];
            Assert.IsTrue(hub.TryFlush(new Tick(2), batch, out var batchBytes));
            Assert.Greater(batchBytes, 0);

            Assert.IsTrue(writer.TryWrite(0, commandBuffer.AsSpan(0, 3), envelope, out written, out _));
            var malformed = hub.TryApplyCommand(Sender, envelope.AsSpan(0, written), ledger, new Tick(3),
                out var refusal);

            Assert.AreEqual(CommandVerdict.Refused, malformed);
            Assert.AreEqual(VoxelEditObservations.CommandMalformed.Id, refusal.Reason.Id);
            Assert.AreEqual(digestOnce, VoxelEditDigest.Of(authority));
        }

        [Test]
        public void VoxelEditSource_BorderVoxelCost() {
            var oneVoxel = MeasureEdits("One Voxel", SmallestRadius);
            var sphere = MeasureEdits("Sphere", WideRadius);

            Debug.Log("VoxelEditSource_BorderVoxelCost: one voxel, " + oneVoxel + "; a sphere of radius " +
                      WideRadius + ", " + sphere);

            foreach (var costs in new[] { oneVoxel, sphere }) {
                Assert.Greater(costs.Interior.Voxels, 0);
                Assert.Greater(costs.Corner.Voxels, 0);
                Assert.AreEqual(costs.Interior.Voxels, costs.Interior.Records);
                Assert.Greater(costs.Corner.Records, costs.Corner.Voxels);
                Assert.AreEqual(costs.Corner.Records, costs.CornerInOneRegion.Records);
                Assert.Greater(costs.Corner.BytesPerVoxel, costs.Interior.BytesPerVoxel);
                Assert.Greater(costs.CornerInOneRegion.BytesPerVoxel, costs.Interior.BytesPerVoxel);
            }

            Assert.AreEqual(8, oneVoxel.Corner.Records);
        }

        [Test]
        public void Attach_MarksTheNotifierWhenChangesWait() {
            var authority = Create("Attach Authority", null, FirstChunk);
            var source = authority.Stores.SourceFor(authority.Stores.ChunkRegions.RegionOf(FirstChunk));
            var idle = new CountingDirtyNotifier();
            source.Attach(idle);
            Assert.AreEqual(0, idle.Marks);

            authority.StoreOf(FirstChunk).Write(Edits(3, 10, 2));
            var waiting = new CountingDirtyNotifier();
            source.Attach(waiting);

            Assert.AreEqual(1, waiting.Marks);
        }

        [Test]
        public void WriteChanges_NoAllocation() {
            var authority = CreateOverRegions("Allocation Authority", TwoChunksWide, ThreeChunks);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            var source = authority.Stores.SourceFor(key);
            var edits = Edits(3, 200, 1);
            var buffer = new byte[VoxelEditTransfer.SectionBytes];

            for (var warmUp = 0; warmUp < WarmUps; warmUp++) {
                WriteEveryChunk(authority, edits);
                var warmWriter = new BitWriter(buffer);
                source.WriteChanges(ref warmWriter);
                source.ClearChanges();
            }

            WriteEveryChunk(authority, edits);
            Assert.AreEqual(ThreeChunks.Length * edits.Count, source.PendingChanges);
            var records = 0;

            GcAllocations.AssertNone(() => {
                var writer = new BitWriter(buffer);
                records = source.WriteChanges(ref writer);
            }, WarmUps);

            Assert.AreEqual(ThreeChunks.Length * edits.Count, records);
        }

        [Test]
        public void ApplyChanges_NoAllocation() {
            var authority = CreateOverRegions("Apply Authority", TwoChunksWide, ThreeChunks);
            var receiver = CreateOverRegionsUnfilled("Apply Receiver", TwoChunksWide, ThreeChunks);
            var key = authority.Stores.ChunkRegions.RegionOf(FirstChunk);
            WriteEveryChunk(authority, Edits(3, 200, 1));
            var buffer = new byte[VoxelEditTransfer.SectionBytes];
            var records = VoxelEditTransfer.WriteChanges(authority.Stores.SourceFor(key), buffer, out var bytes);
            var target = receiver.Stores.SourceFor(key);

            GcAllocations.AssertNone(() => {
                var reader = new BitReader(new ReadOnlySpan<byte>(buffer, 0, bytes));
                target.ApplyChanges(ref reader, records);
            }, WarmUps);

            Assert.AreEqual(ThreeChunks.Length * 200, records);
            Assert.AreEqual(VoxelEditDigest.Of(authority), VoxelEditDigest.Of(receiver));
        }

        [Test]
        public void MarkChanged_NoAllocation() {
            var authority = CreateOverRegionsUnfilled("Mark Authority", TwoChunksWide, ThreeChunks);
            var source = authority.Stores.SourceFor(authority.Stores.ChunkRegions.RegionOf(FirstChunk));
            var edits = Edits(3, 200, 1);
            var buffer = new byte[VoxelEditTransfer.SectionBytes];

            var marked = 0;

            GcAllocations.AssertNone(() => {
                WriteEveryChunk(authority, edits);
                marked = source.PendingChanges;
                var writer = new BitWriter(buffer);
                source.WriteChanges(ref writer);
                source.ClearChanges();
            }, WarmUps);

            Assert.AreEqual(ThreeChunks.Length * edits.Count, marked);
            Assert.AreEqual(0, source.PendingChanges);
        }

        private static void WriteEveryChunk(VoxelWorld world, List<(int, VoxelData)> edits) {
            for (var index = 0; index < ThreeChunks.Length; index++)
                world.StoreOf(ThreeChunks[index]).Write(edits);
        }

        private VoxelWorld CreateOverOccupancy(string name, GroundOccupancySource ground, float cellSizeMetres,
            bool fill, params Vector3Int[] chunks) {
            var world = new VoxelWorld(name, new VoxelEditSourceSettings(BaseFingerprint, SourceId, null, 0,
                cellSizeMetres), _observations, ground);
            _worlds.Add(world);

            foreach (var chunk in chunks)
                world.AddChunk(chunk, fill);

            return world;
        }

        private static void AssertEveryRecordIsApplied(VoxelWorld world) {
            foreach (var (chunk, index, voxel) in VoxelEditDigest.Records(world)) {
                var applied = world.Engine.GetChunkByCoord(chunk).GeoChunkEngine.GetVoxelData(index);
                Assert.AreEqual(voxel.Density, applied.Density, $"chunk {chunk}, index {index}");
                Assert.AreEqual(voxel.GetPlainMatIndex(), applied.GetPlainMatIndex(), $"chunk {chunk}, index {index}");
            }
        }

        private VoxelWorld Create(string name, IChunkRegionMap map, params Vector3Int[] chunks) {
            return CreateWith(name, map, VoxelWorld.ChunkSize, chunks);
        }

        private VoxelWorld CreateOverRegions(string name, float cellSizeMetres, params Vector3Int[] chunks) {
            return CreateWith(name, null, cellSizeMetres, chunks);
        }

        private VoxelWorld CreateOverRegionsUnfilled(string name, float cellSizeMetres, params Vector3Int[] chunks) {
            var world = CreateWith(name, null, cellSizeMetres);

            foreach (var chunk in chunks)
                world.AddChunk(chunk, false);

            return world;
        }

        private VoxelWorld CreateWith(string name, IChunkRegionMap map, float cellSizeMetres,
            params Vector3Int[] chunks) {
            var world = new VoxelWorld(name,
                new VoxelEditSourceSettings(BaseFingerprint, SourceId, map, 0, cellSizeMetres), _observations,
                chunks);
            _worlds.Add(world);

            return world;
        }

        private static List<(int, VoxelData)> Edits(int first, int count, int stride) {
            var edits = new List<(int, VoxelData)>(count);

            for (var index = 0; index < count; index++) {
                var density = (sbyte)(index % 2 == 0 ? -40 : 30);
                var material = (byte)(index % 2 == 0 ? VoxelData.NullSentinelValue : 2);
                edits.Add((first + index * stride, VoxelData.CreateImmature(density, material)));
            }

            return edits;
        }

        private BorderCosts MeasureEdits(string name, float radius) {
            var corner = Create(name + " Corner", null, EightChunks);
            var cornerInOneRegion = CreateOverRegions(name + " Corner In One Region", TwoChunksWide,
                EightChunks);
            var interior = Create(name + " Interior", null, EightChunks);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(corner.GeoVolume, CornerOfEightChunks, radius,
                DigStrength));
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(cornerInOneRegion.GeoVolume, CornerOfEightChunks, radius,
                DigStrength));
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(interior.GeoVolume, InsideTheFirstChunk, radius,
                DigStrength));

            return new BorderCosts(Measure(corner), Measure(cornerInOneRegion), Measure(interior));
        }

        private static BorderCost Measure(VoxelWorld world) {
            var voxels = new HashSet<Vector3Int>();

            foreach (var (chunk, index, _) in VoxelEditDigest.Records(world))
                voxels.Add(chunk * VoxelWorld.ChunkSize + LocalOf(index));

            var buffer = new byte[VoxelEditTransfer.SectionBytes];
            var keys = new HashSet<RegionKey>();

            foreach (var coordinate in world.Coordinates)
                keys.Add(world.Stores.ChunkRegions.RegionOf(coordinate));

            var records = 0;
            var bytes = 0;
            var sections = 0;

            foreach (var key in keys) {
                if (!world.Stores.TryGetSource(key, out var source) || source.PendingChanges == 0)
                    continue;

                records += VoxelEditTransfer.WriteChanges(source, buffer, out var sectionBytes);
                bytes += sectionBytes + CountedSection.HeaderBytes;
                sections++;
            }

            return new BorderCost(voxels.Count, records, bytes, sections);
        }

        private static Vector3Int LocalOf(int index) {
            var width = ChunkLayout.DataWidth(VoxelWorld.ChunkSize);
            var area = width * width;

            return new Vector3Int(index % width, index / area, index / width % width);
        }
    }
}