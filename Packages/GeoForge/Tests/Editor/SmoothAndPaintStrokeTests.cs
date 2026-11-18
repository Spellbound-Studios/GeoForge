// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spellbound.GeoForge.EditMode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class SmoothAndPaintStrokeTests {
        private const int ChunkSize = 32;
        private const float VoxelSize = 1f;
        private const float StrokeSize = 6f;
        private const int CubeHalfExtent = 2;
        private const int EdgeInsideOneChunk = 16;
        private const byte RockMaterial = 0;
        private const byte PaintMaterial = 2;
        private const sbyte Solid = sbyte.MaxValue;
        private const sbyte Empty = sbyte.MinValue;

        private readonly List<Object> _created = new();
        private ScratchVolume _scratch;

        [TearDown]
        public void TearDown() {
            _scratch?.Dispose();
            _scratch = null;

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null)
                    Object.DestroyImmediate(_created[index]);
            }

            _created.Clear();
        }

        [Test]
        public void SmoothSphereOnAStepEdgeBlendsTheEdgeAndLeavesTheRestUnchanged() {
            var engine = CreateStepVolume(EdgeInsideOneChunk);
            var centre = new Vector3Int(EdgeInsideOneChunk, 16, 16);

            Assert.That(GeoForgeCommands.SmoothSphere(_scratch.Volume, WorldOfVoxel(engine, centre), StrokeSize),
                Is.True);

            _scratch.Complete();

            var solidSideOfEdge = ReadVoxel(engine, centre);
            var emptySideOfEdge = ReadVoxel(engine, centre + Vector3Int.right);
            Assert.That(solidSideOfEdge.Density, Is.InRange(Empty + 1, Solid - 1));
            Assert.That(emptySideOfEdge.Density, Is.InRange(Empty + 1, Solid - 1));
            Assert.That(solidSideOfEdge.Density, Is.GreaterThan(emptySideOfEdge.Density));
            Assert.That(solidSideOfEdge.GetPlainMatIndex(), Is.EqualTo(RockMaterial));

            Assert.That(ReadVoxel(engine, centre + Vector3Int.left * 2).Density, Is.EqualTo(Solid));
            Assert.That(ReadVoxel(engine, centre + Vector3Int.right * 3).Density, Is.EqualTo(Empty));
            Assert.That(ReadVoxel(engine, centre + Vector3Int.forward * 8).Density, Is.EqualTo(Solid));
            Assert.That(ReadVoxel(engine, centre + Vector3Int.forward * 8 + Vector3Int.right).Density,
                Is.EqualTo(Empty));
        }

        [Test]
        public void SmoothCubeOnAStepEdgeBlendsOnlyInsideTheCube() {
            var engine = CreateStepVolume(EdgeInsideOneChunk);
            var centre = new Vector3Int(EdgeInsideOneChunk, 16, 16);

            Assert.That(GeoForgeCommands.SmoothCube(_scratch.Volume, WorldOfVoxel(engine, centre), CubeHalfExtent),
                Is.True);

            _scratch.Complete();

            var corner = centre + new Vector3Int(0, CubeHalfExtent, CubeHalfExtent);
            Assert.That(ReadVoxel(engine, corner).Density, Is.InRange(Empty + 1, Solid - 1));

            var outside = centre + new Vector3Int(0, CubeHalfExtent + 1, 0);
            Assert.That(ReadVoxel(engine, outside).Density, Is.EqualTo(Solid));
        }

        [Test]
        public void SmoothingAtAChunkBoundaryReadsTheApronAndAgreesOnBothSides() {
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, new Vector3Int(2, 1, 1), false);
            var engine = _scratch.Volume.GeoVolumeEngine;
            var firstCoord = LowestChunkCoord(engine);
            var lastOfFirstChunk = new Vector3Int(firstCoord.x * ChunkSize + ChunkSize, 16, 16);
            FillEveryChunk(engine, lastOfFirstChunk.x);
            var firstOfSecondChunk = lastOfFirstChunk + Vector3Int.right;

            Assert.That(
                GeoForgeCommands.SmoothSphere(_scratch.Volume, WorldOfVoxel(engine, lastOfFirstChunk), StrokeSize),
                Is.True);

            _scratch.Complete();

            var ownedByFirst = engine.GetChunkByCoord(firstCoord).GeoChunkEngine;
            var ownedBySecond = engine.GetChunkByCoord(firstCoord + Vector3Int.right).GeoChunkEngine;
            var solidSide = ownedByFirst.GetVoxelDataFromVoxelPosition(lastOfFirstChunk);
            var emptySide = ownedBySecond.GetVoxelDataFromVoxelPosition(firstOfSecondChunk);

            Assert.That(solidSide.Density, Is.InRange(Empty + 1, Solid - 1));
            Assert.That(emptySide.Density, Is.InRange(Empty + 1, Solid - 1));
            Assert.That(ownedBySecond.GetVoxelDataFromVoxelPosition(lastOfFirstChunk), Is.EqualTo(solidSide));
            Assert.That(ownedByFirst.GetVoxelDataFromVoxelPosition(firstOfSecondChunk), Is.EqualTo(emptySide));
        }

        [Test]
        public void PaintSphereRepaintsSolidVoxelsAndChangesNoDensity() {
            var engine = CreateStepVolume(EdgeInsideOneChunk);
            var centre = new Vector3Int(EdgeInsideOneChunk, 16, 16);

            Assert.That(
                GeoForgeCommands.PaintSphere(_scratch.Volume, WorldOfVoxel(engine, centre), StrokeSize, PaintMaterial),
                Is.True);

            _scratch.Complete();

            var painted = ReadVoxel(engine, centre + Vector3Int.left);
            Assert.That(painted.GetPlainMatIndex(), Is.EqualTo(PaintMaterial));
            Assert.That(painted.Density, Is.EqualTo(Solid));

            var emptyInside = ReadVoxel(engine, centre + Vector3Int.right);
            Assert.That(emptyInside.Density, Is.EqualTo(Empty));
            Assert.That(emptyInside.GetPlainMatIndex(), Is.EqualTo(VoxelData.NullSentinelValue));

            var outside = ReadVoxel(engine, centre + Vector3Int.forward * 8);
            Assert.That(outside.GetPlainMatIndex(), Is.EqualTo(RockMaterial));
            Assert.That(outside.Density, Is.EqualTo(Solid));
        }

        [Test]
        public void PaintCubeRepaintsOnlyInsideTheCube() {
            var engine = CreateStepVolume(EdgeInsideOneChunk);
            var centre = new Vector3Int(EdgeInsideOneChunk - CubeHalfExtent, 16, 16);

            Assert.That(
                GeoForgeCommands.PaintCube(_scratch.Volume, WorldOfVoxel(engine, centre), CubeHalfExtent,
                    PaintMaterial),
                Is.True);

            _scratch.Complete();

            var corner = ReadVoxel(engine, centre + new Vector3Int(-CubeHalfExtent, CubeHalfExtent, CubeHalfExtent));
            Assert.That(corner.GetPlainMatIndex(), Is.EqualTo(PaintMaterial));
            Assert.That(corner.Density, Is.EqualTo(Solid));

            var outside = ReadVoxel(engine, centre + new Vector3Int(-CubeHalfExtent - 1, 0, 0));
            Assert.That(outside.GetPlainMatIndex(), Is.EqualTo(RockMaterial));
        }

        [Test]
        public void EveryStrokeRefusesAVolumeWithoutAManager() {
            IGeoVolume volume = Track(new GameObject("Volume Without Manager")).AddComponent<GeoVolumeDouble>();
            var pattern = new Regex(nameof(GeoForgeManager));

            LogAssert.Expect(LogType.Error, pattern);
            Assert.That(GeoForgeCommands.SmoothSphere(volume, Vector3.zero, StrokeSize), Is.False);
            LogAssert.Expect(LogType.Error, pattern);
            Assert.That(GeoForgeCommands.SmoothCube(volume, Vector3.zero, CubeHalfExtent), Is.False);
            LogAssert.Expect(LogType.Error, pattern);
            Assert.That(GeoForgeCommands.PaintSphere(volume, Vector3.zero, StrokeSize, PaintMaterial), Is.False);
            LogAssert.Expect(LogType.Error, pattern);
            Assert.That(GeoForgeCommands.PaintCube(volume, Vector3.zero, CubeHalfExtent, PaintMaterial), Is.False);
        }

        private GeoVolumeEngine CreateStepVolume(int lastSolidX) {
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false);
            var engine = _scratch.Volume.GeoVolumeEngine;
            FillEveryChunk(engine, lastSolidX);

            return engine;
        }

        private static void FillEveryChunk(GeoVolumeEngine engine, int lastSolidX) {
            foreach (var chunk in engine.ChunkDict.Values)
                FillStep(chunk, engine, lastSolidX);
        }

        private static Vector3Int LowestChunkCoord(GeoVolumeEngine engine) {
            var lowest = new Vector3Int(int.MaxValue, 0, 0);

            foreach (var coord in engine.ChunkDict.Keys) {
                if (coord.x < lowest.x)
                    lowest = coord;
            }

            return lowest;
        }

        private static void FillStep(IGeoChunk chunk, GeoVolumeEngine engine, int lastSolidX) {
            ref var config = ref engine.ConfigBlob.Value;
            var width = config.ChunkDataWidthSize;
            var voxels = new NativeArray<VoxelData>(config.ChunkDataVolumeSize, Allocator.Persistent);
            var originX = chunk.GeoChunkEngine.ChunkCoord.x * config.ChunkSize;

            for (var x = 0; x < width; x++)
            for (var y = 0; y < width; y++)
            for (var z = 0; z < width; z++) {
                var index = GfStaticHelper.Coord3DToIndex(x, y, z, config.ChunkDataAreaSize, width);

                voxels[index] = originX + x <= lastSolidX
                    ? VoxelData.CreateImmature(Solid, RockMaterial)
                    : VoxelData.CreateImmature(Empty, VoxelData.NullSentinelValue);
            }

            chunk.GeoChunkEngine.SetVoxels(voxels);
        }

        private VoxelData ReadVoxel(GeoVolumeEngine engine, Vector3Int voxel) {
            Assert.That(_scratch.Volume.TryQueryVoxel(WorldOfVoxel(engine, voxel), out var data), Is.True);

            return data;
        }

        private static Vector3 WorldOfVoxel(GeoVolumeEngine engine, Vector3Int voxel) {
            var offset = engine.ConfigBlob.Value.Offset;

            return engine.Transform.TransformPoint((Vector3)(voxel + offset) * engine.VoxelSize);
        }

        private T Track<T>(T created) where T : Object {
            _created.Add(created);

            return created;
        }
    }
}
