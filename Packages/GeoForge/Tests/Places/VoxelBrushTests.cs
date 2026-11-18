// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spellbound.GeoForge.EditMode;
using Spellbound.GeoForge.Tests;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelBrushTests {
        private const int ChunkSize = 32;
        private const float VoxelSize = 1f;
        private const float CarveSize = 8f;
        private const float BoxEdge = 4f;
        private const float BlendSize = 6f;
        private const int StepEdge = 16;
        private const byte RockMaterial = 0;
        private const byte PaintMaterial = 2;
        private const sbyte Solid = sbyte.MaxValue;
        private const sbyte Empty = sbyte.MinValue;
        private const float BridgeSurfaceHeight = 20f;
        private const float RayLength = 64f;

        private static readonly Vector3Int Middle = Vector3Int.one * (ChunkSize / 2);

        private readonly List<Object> _created = new();
        private ScratchVolume _scratch;
        private BridgeTestWorld _world;
        private VoxelBrush _brush;

        [TearDown]
        public void TearDown() {
            _brush?.Dispose();
            _brush = null;
            _scratch?.Dispose();
            _scratch = null;
            _world?.Dispose();
            _world = null;

            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null) {
                    Object.DestroyImmediate(_created[index]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ARemoveSphereCarvesUndoRestoresAndRedoCarvesAgain() {
            var engine = CreateScratch(Solid);
            var centre = WorldOfVoxel(engine, Middle);
            var before = RunsOf(engine, Vector3Int.zero);

            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, centre,
                CarveSize, RockMaterial)), Is.True);
            Assert.That(IsSolid(centre), Is.False);
            Assert.That(_brush.Strokes.Count, Is.EqualTo(1));

            Assert.That(_brush.Undo(), Is.True);
            Assert.That(IsSolid(centre), Is.True);
            Assert.That(RunsOf(engine, Vector3Int.zero), Is.EqualTo(before));
            Assert.That(_brush.Strokes, Is.Empty);

            Assert.That(_brush.Redo(), Is.True);
            Assert.That(IsSolid(centre), Is.False);
            Assert.That(_brush.Strokes.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnitysUndoAndRedoWalkTheBrushHistory() {
            var engine = CreateScratch(Solid);
            var centre = WorldOfVoxel(engine, Middle);
            var before = RunsOf(engine, Vector3Int.zero);

            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, centre,
                CarveSize, RockMaterial)), Is.True);
            Assert.That(IsSolid(centre), Is.False);

            Undo.PerformUndo();
            Assert.That(IsSolid(centre), Is.True);
            Assert.That(RunsOf(engine, Vector3Int.zero), Is.EqualTo(before));
            Assert.That(_brush.Strokes, Is.Empty);

            Undo.PerformRedo();
            Assert.That(IsSolid(centre), Is.False);
            Assert.That(_brush.Strokes.Count, Is.EqualTo(1));
        }

        [Test]
        public void ABoxAddFillsItsEdgeInMetres() {
            var engine = CreateScratch(Empty);
            var halfEdgeInVoxels = Mathf.RoundToInt(BoxEdge * 0.5f / VoxelSize);

            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Box, VoxelStrokeMode.Add,
                WorldOfVoxel(engine, Middle), BoxEdge, RockMaterial)), Is.True);

            Assert.That(IsSolid(WorldOfVoxel(engine, Middle)), Is.True);
            Assert.That(IsSolid(WorldOfVoxel(engine, Middle + Vector3Int.right * (halfEdgeInVoxels - 1))), Is.True);
            Assert.That(IsSolid(WorldOfVoxel(engine, Middle + Vector3Int.right * (halfEdgeInVoxels + 2))), Is.False);
        }

        [Test]
        public void ASmoothStrokeBlendsTheStepEdge() {
            var engine = CreateScratch(StepEdge);
            var edge = new Vector3Int(StepEdge, Middle.y, Middle.z);

            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Smooth,
                WorldOfVoxel(engine, edge), BlendSize, RockMaterial)), Is.True);

            Assert.That(ReadVoxel(engine, edge).Density, Is.InRange(Empty + 1, Solid - 1));
            Assert.That(ReadVoxel(engine, edge + Vector3Int.forward * 8).Density, Is.EqualTo(Solid));
        }

        [Test]
        public void APaintStrokeRepaintsSolidVoxelsAndKeepsTheirDensity() {
            var engine = CreateScratch(StepEdge);
            var edge = new Vector3Int(StepEdge, Middle.y, Middle.z);

            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Paint,
                WorldOfVoxel(engine, edge), BlendSize, PaintMaterial)), Is.True);

            var painted = ReadVoxel(engine, edge + Vector3Int.left);
            Assert.That(painted.GetPlainMatIndex(), Is.EqualTo(PaintMaterial));
            Assert.That(painted.Density, Is.EqualTo(Solid));
            Assert.That(ReadVoxel(engine, edge + Vector3Int.forward * 8).GetPlainMatIndex(), Is.EqualTo(RockMaterial));
        }

        [Test]
        public void AStrokeOnAVolumeWithoutAManagerReturnsFalseAndAddsNoHistory() {
            IGeoVolume volume = Track(new GameObject("Volume Without Manager")).AddComponent<GeoVolumeDouble>();
            _brush = new VoxelBrush(volume, VoxelRegion.Origin);

            LogAssert.Expect(LogType.Error, new Regex(nameof(GeoForgeManager)));
            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, Vector3.zero,
                CarveSize, RockMaterial)), Is.False);

            Assert.That(_brush.Strokes, Is.Empty);
            Assert.That(_brush.Undo(), Is.False);
        }

        [Test]
        public void AStrokeAfterAnUndoDropsTheUndoneStroke() {
            var engine = CreateScratch(Solid);
            var centre = WorldOfVoxel(engine, Middle);
            var aside = WorldOfVoxel(engine, Middle + Vector3Int.right * 8);

            _brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, centre, CarveSize,
                RockMaterial));
            _brush.Undo();
            _brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, aside, CarveSize,
                RockMaterial));

            Assert.That(_brush.Strokes.Count, Is.EqualTo(1));
            Assert.That(_brush.Strokes[0].Centre, Is.EqualTo(aside));
            Assert.That(_brush.Redo(), Is.False);
            Assert.That(IsSolid(centre), Is.True);
            Assert.That(IsSolid(aside), Is.False);
        }

        [Test]
        public void ABrushOverABridgeTestWorldCarvesAndUndoes() {
            _world = new BridgeTestWorld(BridgeSurfaceHeight, Vector3Int.zero);
            _brush = new VoxelBrush(_world.Volume, VoxelRegion.Origin);
            var engine = _world.Engine;
            var centre = WorldOfVoxel(engine, new Vector3Int(32, 10, 32));
            Assert.That(IsSolid(centre), Is.True);

            Assert.That(_brush.Apply(new VoxelStroke(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, centre,
                CarveSize, RockMaterial)), Is.True);
            Assert.That(IsSolid(centre), Is.False);

            Assert.That(_brush.Undo(), Is.True);
            Assert.That(IsSolid(centre), Is.True);
        }

        [Test]
        public void TheMouseRayMeetsTheFirstSolidVoxel() {
            var engine = CreateScratch(StepEdge);
            var start = WorldOfVoxel(engine, new Vector3Int(StepEdge + 10, Middle.y, Middle.z));

            Assert.That(_brush.TryFindSolid(new Ray(start, Vector3.left), RayLength, out var point), Is.True);
            Assert.That(engine.WorldToVoxelSpace(point).x, Is.InRange(StepEdge - 1, StepEdge + 1));
            Assert.That(_brush.TryFindSolid(new Ray(start, Vector3.right), RayLength, out _), Is.False);
        }

        [Test]
        public void DisposeDestroysTheHistory() {
            var historiesBefore = Resources.FindObjectsOfTypeAll<VoxelBrushHistory>().Length;
            CreateScratch(Solid);
            Assert.That(Resources.FindObjectsOfTypeAll<VoxelBrushHistory>().Length, Is.EqualTo(historiesBefore + 1));

            _brush.Dispose();
            _brush = null;

            Assert.That(Resources.FindObjectsOfTypeAll<VoxelBrushHistory>().Length, Is.EqualTo(historiesBefore));
        }

        private GeoVolumeEngine CreateScratch(int lastSolidX) {
            _scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false);
            var engine = _scratch.Volume.GeoVolumeEngine;

            foreach (var chunk in engine.ChunkDict.Values) {
                FillStep(chunk, engine, lastSolidX);
            }

            _scratch.Complete();
            _brush = new VoxelBrush(_scratch.Volume, new VoxelRegion(Vector3Int.zero, Vector3Int.one));

            return engine;
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

        private bool IsSolid(Vector3 worldPosition) {
            var volume = _brush.Volume;
            Assert.That(volume.TryQuerySolid(worldPosition, out var solid), Is.True);

            return solid;
        }

        private VoxelData ReadVoxel(GeoVolumeEngine engine, Vector3Int voxel) {
            Assert.That(_brush.Volume.TryQueryVoxel(WorldOfVoxel(engine, voxel), out var data), Is.True);

            return data;
        }

        private List<SparseVoxelData> RunsOf(GeoVolumeEngine engine, Vector3Int coordinate) {
            var runs = new NativeList<SparseVoxelData>(Allocator.Temp);

            try {
                Assert.That(engine.TryCopyRunList(coordinate, runs, out _), Is.True);
                var copy = new List<SparseVoxelData>(runs.Length);

                for (var index = 0; index < runs.Length; index++) {
                    copy.Add(runs[index]);
                }

                return copy;
            } finally {
                runs.Dispose();
            }
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
