// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Observations;
using Spellbound.Core.Simulation;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    public class VoxelEditOwnerTests {
        private const ulong BaseFingerprint = 0x0A11UL;
        private const uint SourceId = 3u;
        private const float EditRadius = 6f;
        private const int DigStrength = 127;
        private const int GentleStrength = 50;
        private const int UploadBytes = 4096;
        private const int EchoedVoxel = 1500;

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3 FirstOwnersEdit = new(8f, 8f, 8f);
        private static readonly Vector3 SecondOwnersEdit = new(9f, 8f, 8f);
        private static readonly ParticipantId FirstOwner = new(101UL);
        private static readonly ParticipantId SecondOwner = new(202UL);
        private static readonly ParticipantId Observer = new(303UL);

        private readonly List<VoxelWorld> _worlds = new();
        private ObservationContext _observations;

        [SetUp]
        public void SetUp() {
            _observations = CoreKit.Observations.CreateContext("voxel_edit_owners_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown() {
            foreach (var world in _worlds)
                world.Dispose();

            _worlds.Clear();
            _observations.Dispose();
        }

        [Test]
        public void VoxelEditSource_TwoOwnersOneVoxel_EveryPeerEndsWithTheAuthoritysDigest() {
            var authorityWorld = Create("Owners Authority");
            var firstWorld = Create("Owners First");
            var secondWorld = Create("Owners Second");
            var observerWorld = Create("Owners Observer");
            var key = authorityWorld.Stores.ChunkRegions.RegionOf(FirstChunk);

            var authority = authorityWorld.Stores.SourceFor(key);
            using var hub = new RegionHub(key, ProtocolSettings.Default, _observations);
            hub.Bind(authority, WritePolicy.Owner);
            hub.Seal();

            var first = firstWorld.Stores.SourceFor(key);
            var firstWriter = new OwnerUploadWriter(new IReplicatedSource[] { first }, new[] { 0 },
                ProtocolSettings.Default, _observations);
            first.PendingEchoes = firstWriter.Mask;
            var second = secondWorld.Stores.SourceFor(key);
            var secondWriter = new OwnerUploadWriter(new IReplicatedSource[] { second }, new[] { 0 },
                ProtocolSettings.Default, _observations);
            second.PendingEchoes = secondWriter.Mask;
            var observer = observerWorld.Stores.SourceFor(key);

            Assert.IsTrue(GeoForgeCommands.RemoveSphere(firstWorld.GeoVolume, FirstOwnersEdit, EditRadius,
                DigStrength));
            Assert.IsTrue(GeoForgeCommands.RemoveSphere(secondWorld.GeoVolume, SecondOwnersEdit, EditRadius,
                GentleStrength));
            Assert.Greater(ContestedVoxels(firstWorld, secondWorld), 0);

            var firstUpload = new byte[UploadBytes];
            var secondUpload = new byte[UploadBytes];
            Assert.IsTrue(firstWriter.TryWriteUpload(new Tick(1), firstUpload, out var firstLength));
            Assert.IsTrue(secondWriter.TryWriteUpload(new Tick(1), secondUpload, out var secondLength));

            var firstBefore = VoxelEditDigest.Records(firstWorld);
            Assert.IsTrue(hub.TryApplyOwnerUpload(SecondOwner, secondUpload.AsSpan(0, secondLength), new Tick(2)));
            var earlier = new byte[VoxelBatches.FlushBytes];
            Assert.IsTrue(hub.TryFlush(new Tick(2), earlier, out var earlierLength));
            VoxelBatches.Apply(earlier, earlierLength, Tick.Zero, Observer, null, observer);
            VoxelBatches.Apply(earlier, earlierLength, Tick.Zero, FirstOwner, first.PendingEchoes, first);
            VoxelBatches.Apply(earlier, earlierLength, Tick.Zero, SecondOwner, second.PendingEchoes, second);
            Assert.AreEqual(0, ChangedOf(firstWorld, firstBefore),
                "the first owner's edits waiting for their echo were overwritten by the second owner's");

            Assert.IsTrue(hub.TryApplyOwnerUpload(FirstOwner, firstUpload.AsSpan(0, firstLength), new Tick(3)));
            Assert.AreEqual(0, authority.PendingChanges);

            var batch = new byte[VoxelBatches.FlushBytes];
            Assert.IsTrue(hub.TryFlush(new Tick(3), batch, out var written));
            VoxelBatches.Apply(batch, written, new Tick(2), Observer, null, observer);
            VoxelBatches.Apply(batch, written, new Tick(2), FirstOwner, first.PendingEchoes, first);
            VoxelBatches.Apply(batch, written, new Tick(2), SecondOwner, second.PendingEchoes, second);

            var digest = VoxelEditDigest.Of(authorityWorld);
            Assert.Greater(VoxelEditDigest.CountOf(authorityWorld), 0);
            Assert.AreEqual(digest, VoxelEditDigest.Of(observerWorld), "observer");
            Assert.AreEqual(digest, VoxelEditDigest.Of(firstWorld), "first owner");
            Assert.AreEqual(digest, VoxelEditDigest.Of(secondWorld), "second owner");
            Assert.AreEqual(authorityWorld.VoxelAt(SecondOwnersEdit), observerWorld.VoxelAt(SecondOwnersEdit));
            Assert.AreEqual(authorityWorld.VoxelAt(SecondOwnersEdit), firstWorld.VoxelAt(SecondOwnersEdit));
            Assert.AreEqual(authorityWorld.VoxelAt(SecondOwnersEdit), secondWorld.VoxelAt(SecondOwnersEdit));
            Assert.AreEqual(firstWorld.VoxelAt(FirstOwnersEdit), authorityWorld.VoxelAt(FirstOwnersEdit));
        }

        [Test]
        public void LocalWriteBeforeUpload_IsNotOverwrittenByItsEcho() {
            var authorityWorld = Create("Echo Authority");
            var ownerWorld = Create("Echo Owner");
            var key = authorityWorld.Stores.ChunkRegions.RegionOf(FirstChunk);
            using var hub = new RegionHub(key, ProtocolSettings.Default, _observations);
            hub.Bind(authorityWorld.Stores.SourceFor(key), WritePolicy.Owner);
            hub.Seal();
            var owner = ownerWorld.Stores.SourceFor(key);
            var writer = new OwnerUploadWriter(new IReplicatedSource[] { owner }, new[] { 0 },
                ProtocolSettings.Default, _observations);
            owner.PendingEchoes = writer.Mask;
            var store = ownerWorld.StoreOf(FirstChunk);
            var firstDig = VoxelData.CreateImmature(-20, VoxelData.NullSentinelValue);
            var secondDig = VoxelData.CreateImmature(-90, VoxelData.NullSentinelValue);

            store.Write(new List<(int, VoxelData)> { (EchoedVoxel, firstDig) });
            var upload = new byte[UploadBytes];
            Assert.IsTrue(writer.TryWriteUpload(new Tick(1), upload, out var length));
            store.Write(new List<(int, VoxelData)> { (EchoedVoxel, secondDig) });

            Assert.IsTrue(hub.TryApplyOwnerUpload(FirstOwner, upload.AsSpan(0, length), new Tick(2)));
            var batch = new byte[VoxelBatches.FlushBytes];
            Assert.IsTrue(hub.TryFlush(new Tick(2), batch, out var written));
            VoxelBatches.Apply(batch, written, Tick.Zero, FirstOwner, owner.PendingEchoes, owner);

            Assert.IsTrue(store.TryRead(EchoedVoxel, out var held));
            Assert.AreEqual(secondDig, held);

            Assert.IsTrue(writer.TryWriteUpload(new Tick(3), upload, out length));
            Assert.IsTrue(hub.TryApplyOwnerUpload(FirstOwner, upload.AsSpan(0, length), new Tick(4)));
            Assert.IsTrue(hub.TryFlush(new Tick(4), batch, out written));
            VoxelBatches.Apply(batch, written, new Tick(2), FirstOwner, owner.PendingEchoes, owner);

            Assert.IsTrue(authorityWorld.StoreOf(FirstChunk).TryRead(EchoedVoxel, out var authorityHeld));
            Assert.AreEqual(secondDig, authorityHeld);
            Assert.AreEqual(VoxelEditDigest.Of(authorityWorld), VoxelEditDigest.Of(ownerWorld));
        }

        private VoxelWorld Create(string name) {
            var world = new VoxelWorld(name,
                new VoxelEditSourceSettings(BaseFingerprint, SourceId, null, 0, VoxelWorld.ChunkSize), _observations,
                FirstChunk, SecondChunk);
            _worlds.Add(world);

            return world;
        }

        private static int ChangedOf(VoxelWorld world,
            List<(Vector3Int Chunk, int Index, VoxelData Voxel)> expected) {
            var now = new Dictionary<(Vector3Int, int), VoxelData>();

            foreach (var (chunk, index, voxel) in VoxelEditDigest.Records(world))
                now[(chunk, index)] = voxel;

            var changed = 0;

            foreach (var (chunk, index, voxel) in expected) {
                if (!now.TryGetValue((chunk, index), out var held) || held != voxel)
                    changed++;
            }

            return changed;
        }

        private static int ContestedVoxels(VoxelWorld first, VoxelWorld second) {
            var firstEdits = new Dictionary<(Vector3Int, int), VoxelData>();

            foreach (var (chunk, index, voxel) in VoxelEditDigest.Records(first))
                firstEdits[(chunk, index)] = voxel;

            var contested = 0;

            foreach (var (chunk, index, voxel) in VoxelEditDigest.Records(second)) {
                if (firstEdits.TryGetValue((chunk, index), out var other) && other != voxel)
                    contested++;
            }

            return contested;
        }
    }
}