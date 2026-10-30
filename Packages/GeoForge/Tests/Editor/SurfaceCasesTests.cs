// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class SurfaceCasesTests {
        private const int Width = 4;
        private const int Area = Width * Width;
        private const sbyte Air = -100;
        private const sbyte Solid = 100;

        private static readonly int3 Cell = new(1, 1, 1);

        private BlobAssetReference<McTablesBlobAsset> _tables;
        private NativeArray<VoxelData> _voxels;

        [SetUp]
        public void SetUp() {
            _tables = McTablesBlobCreator.CreateMcTablesBlobAsset();
            _voxels = new NativeArray<VoxelData>(Width * Area, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown() {
            if (_voxels.IsCreated)
                _voxels.Dispose();

            if (_tables.IsCreated)
                _tables.Dispose();
        }

        [Test]
        public void AnAllAirCellIsEmpty() {
            Fill(Air);

            var surfaceCase = Compute(Cell);

            Assert.AreEqual(0x00, surfaceCase.CaseCode);
            Assert.IsTrue(surfaceCase.IsEmpty);
            Assert.IsFalse(surfaceCase.IsSolid);
            Assert.IsFalse(surfaceCase.HasSurface);
        }

        [Test]
        public void AnAllSolidCellIsSolid() {
            Fill(Solid);

            var surfaceCase = Compute(Cell);

            Assert.AreEqual(0xFF, surfaceCase.CaseCode);
            Assert.IsTrue(surfaceCase.IsSolid);
            Assert.IsFalse(surfaceCase.IsEmpty);
            Assert.IsFalse(surfaceCase.HasSurface);
        }

        [Test]
        public void ACellWithItsBottomFourCornersSolidHasTheSurfaceAndTheMarchCaseCode() {
            Fill(Air);

            for (var x = 0; x <= 1; x++)
            for (var z = 0; z <= 1; z++)
                Set(Cell + new int3(x, 0, z), VoxelData.CreateMature(Solid, 3));

            var surfaceCase = Compute(Cell);
            var marchCorners = new NativeArray<VoxelData>(SurfaceCase.CornerCount, Allocator.Temp);
            var marchCaseCode = GfMarchHelper.GatherRegularCornersAndComputeCaseCode(_voxels, ref _tables.Value, Cell,
                0, 1, Area, Width, ref marchCorners);

            Assert.IsTrue(surfaceCase.HasSurface);
            Assert.IsFalse(surfaceCase.IsEmpty);
            Assert.IsFalse(surfaceCase.IsSolid);
            Assert.AreEqual(marchCaseCode, surfaceCase.CaseCode);
            Assert.AreEqual(0x0F, surfaceCase.CaseCode);
            Assert.AreEqual(new Vector3Int(1, 1, 1), surfaceCase.CellOrigin);

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++)
                Assert.AreEqual(marchCorners[corner], surfaceCase.Corner(corner));

            marchCorners.Dispose();
        }

        [Test]
        public void CornersFollowTheMarchCornerOrderAndBitOrder() {
            Fill(Air);

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++) {
                var density = corner == 5 ? Solid : Air;
                Set(Cell + _tables.Value.RegularCornerOffset[corner], VoxelData.CreateImmature(density, (byte)corner));
            }

            var surfaceCase = Compute(Cell);

            Assert.AreEqual(0x20, surfaceCase.CaseCode);

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++)
                Assert.AreEqual(corner, surfaceCase.Corner(corner).MaterialIndex);

            Assert.Throws<ArgumentOutOfRangeException>(() => surfaceCase.Corner(SurfaceCase.CornerCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => surfaceCase.Corner(-1));
        }

        [Test]
        public void FromCornersGivesTheSameCaseAsComputeOverTheDenseArray() {
            Fill(Air);

            for (var x = 0; x <= 1; x++)
            for (var z = 0; z <= 1; z++)
                Set(Cell + new int3(x, 0, z), VoxelData.CreateMature(Solid, 3));

            var computed = Compute(Cell);
            var corners = new NativeArray<VoxelData>(SurfaceCase.CornerCount, Allocator.Temp);

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++)
                corners[corner] = computed.Corner(corner);

            var fromCorners = SurfaceCases.FromCorners(new Vector3Int(7, 8, 9), corners);
            corners.Dispose();

            Assert.AreEqual(computed.CaseCode, fromCorners.CaseCode);
            Assert.AreEqual(new Vector3Int(7, 8, 9), fromCorners.CellOrigin);

            for (var corner = 0; corner < SurfaceCase.CornerCount; corner++)
                Assert.AreEqual(computed.Corner(corner), fromCorners.Corner(corner));
        }

        private SurfaceCase Compute(int3 cellPosition) {
            return SurfaceCases.Compute(_voxels, cellPosition, Area, Width, ref _tables.Value);
        }

        private void Fill(sbyte density) {
            for (var index = 0; index < _voxels.Length; index++)
                _voxels[index] = VoxelData.CreateImmature(density, 0);
        }

        private void Set(int3 position, VoxelData voxel) {
            _voxels[GfStaticHelper.Coord3DToIndex(position.x, position.y, position.z, Area, Width)] = voxel;
        }
    }
}
