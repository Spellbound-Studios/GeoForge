// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Core;
using Spellbound.Core.Intents;
using Spellbound.Core.Observations;
using Spellbound.Core.Simulation;
using Spellbound.Networking.Replication;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    public class VoxelEditCommandTests {
        private const ulong BaseFingerprint = 0xC0DEUL;
        private const uint SourceId = 9u;
        private const float PastTheLargestRadius = 70f;
        private const float EditRadius = 4f;
        private const short DigStrength = 127;
        private const int LedgerWindow = 64;
        private const ulong DepartureGraceTicks = 100UL;
        private const string TooLargeReason = "geoforge.voxel_edit.command_too_large";

        private static readonly Vector3Int FirstChunk = Vector3Int.zero;
        private static readonly Vector3Int SecondChunk = Vector3Int.right;
        private static readonly Vector3 InsideTheFirstChunk = new(8f, 8f, 8f);
        private static readonly Vector3 OnTheBorderOfBoth = new(16f, 8f, 8f);
        private static readonly ParticipantId Sender = new(11UL);

        private readonly List<VoxelWorld> _worlds = new();
        private ObservationContext _observations;
        private RegionHub _hub;
        private IntentLedger _ledger;
        private CommandWriter _writer;
        private ulong _tick;

        [SetUp]
        public void SetUp() {
            _observations = CoreKit.Observations.CreateContext("voxel_edit_command_" + Guid.NewGuid().ToString("N"));
            _ledger = new IntentLedger(LedgerWindow, new TickSpan(DepartureGraceTicks), _observations);
            _writer = new CommandWriter(new IntentSequence(Sender.ToIntentActor()));
            _tick = 0UL;
        }

        [TearDown]
        public void TearDown() {
            _hub?.Dispose();
            _hub = null;

            foreach (var world in _worlds)
                world.Dispose();

            _worlds.Clear();
            _observations.Dispose();
        }

        [Test]
        public void Command_PastTheLargestExtentIsRefused() {
            var world = CreateWithHub("Too Large");
            var envelope = VoxelCommandEnvelopes.Of(
                VoxelEditCommand.RemoveSphere(InsideTheFirstChunk, PastTheLargestRadius, DigStrength), _writer);

            var verdict = Apply(envelope, out var receipt);

            Assert.AreEqual(CommandVerdict.Refused, verdict);
            Assert.AreEqual(TooLargeReason, receipt.Reason.Name);
            Assert.AreEqual(0, VoxelEditDigest.CountOf(world));

            var fits = VoxelCommandEnvelopes.Of(
                VoxelEditCommand.RemoveSphere(InsideTheFirstChunk, EditRadius, DigStrength), _writer);
            Assert.AreEqual(CommandVerdict.Applied, Apply(fits, out _));
            Assert.Greater(VoxelEditDigest.CountOf(world), 0);
        }

        [Test]
        public void Command_NaNIsRefused() {
            var world = CreateWithHub("Refused Values");
            var sphere = VoxelEditShape.Sphere;
            var cube = VoxelEditShape.Cube;
            var refused = new List<byte[]> {
                VoxelCommandEnvelopes.Raw(sphere, false, 8f, 8f, 8f, float.NaN, 0, DigStrength, 127, _writer),
                VoxelCommandEnvelopes.Raw(sphere, false, float.NaN, 8f, 8f, EditRadius, 0, DigStrength, 127,
                    _writer),
                VoxelCommandEnvelopes.Raw(sphere, false, 8f, 8f, 8f, float.PositiveInfinity, 0, DigStrength, 127,
                    _writer),
                VoxelCommandEnvelopes.Raw(sphere, false, 8f, float.NegativeInfinity, 8f, EditRadius, 0, DigStrength,
                    127, _writer),
                VoxelCommandEnvelopes.Raw(sphere, false, 8f, 8f, 8f, -EditRadius, 0, DigStrength, 127, _writer),
                VoxelCommandEnvelopes.Raw(cube, false, 8f, 8f, 8f, 0f, -2, DigStrength, 0, _writer),
                VoxelCommandEnvelopes.Raw(sphere, false, 8f, 8f, 8f, EditRadius, 0, -DigStrength, 127, _writer),
                VoxelCommandEnvelopes.Raw(sphere, true, 8f, 8f, 8f, EditRadius, 0, DigStrength, 200, _writer),
                VoxelCommandEnvelopes.Raw(sphere, true, 8f, 8f, 8f, EditRadius, 0, DigStrength, 127, _writer),
                VoxelCommandEnvelopes.Raw(sphere, false, 8f, 8f, 8f, EditRadius, 0, DigStrength, 3, _writer)
            };

            for (var index = 0; index < refused.Count; index++) {
                var verdict = Apply(refused[index], out var receipt);

                Assert.AreEqual(CommandVerdict.Refused, verdict, "command " + index);
                Assert.AreEqual(VoxelEditObservations.CommandMalformed.Id, receipt.Reason.Id, "command " + index);
            }

            Assert.AreEqual(0, VoxelEditDigest.CountOf(world));
        }

        [Test]
        public void Command_AtABorderMarksBothRegionsDirty() {
            var world = CreateWithHub("Border Command");
            var envelope = VoxelCommandEnvelopes.Of(
                VoxelEditCommand.RemoveSphere(OnTheBorderOfBoth, EditRadius, DigStrength), _writer);

            Assert.AreEqual(CommandVerdict.Applied, Apply(envelope, out _));

            var stores = world.Stores;
            var first = stores.SourceFor(stores.ChunkRegions.RegionOf(FirstChunk));
            var second = stores.SourceFor(stores.ChunkRegions.RegionOf(SecondChunk));
            Assert.AreNotEqual(first.Key, second.Key);
            Assert.Greater(first.PendingChanges, 0);
            Assert.Greater(second.PendingChanges, 0);
            Assert.AreEqual(EditsOf(world.StoreOf(FirstChunk)), first.PendingChanges);
            Assert.AreEqual(EditsOf(world.StoreOf(SecondChunk)), second.PendingChanges);
        }

        private VoxelWorld CreateWithHub(string name) {
            var world = new VoxelWorld(name,
                new VoxelEditSourceSettings(BaseFingerprint, SourceId, null, 0, VoxelWorld.ChunkSize), _observations,
                FirstChunk, SecondChunk);
            _worlds.Add(world);
            var key = world.Stores.ChunkRegions.RegionOf(FirstChunk);
            _hub = new RegionHub(key, ProtocolSettings.Default, _observations);
            _hub.Bind(world.Stores.SourceFor(key), WritePolicy.Server);
            _hub.Seal();

            return world;
        }

        private CommandVerdict Apply(byte[] envelope, out IntentReceipt receipt) {
            _tick++;

            return _hub.TryApplyCommand(Sender, envelope, _ledger, new Tick(_tick), out receipt);
        }

        private static int EditsOf(IGeoEditStore store) {
            var count = 0;

            foreach (var _ in store.ReadAllEdits())
                count++;

            return count;
        }
    }
}