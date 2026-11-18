// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using Spellbound.Core.Ground;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelLayerOccupancySourceTests {
        [Test]
        public void ABoxBelowTheSurfaceIsSolid() {
            using var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());

            Assert.AreEqual(Occupancy.Solid, source.Classify(new Bounds(new Vector3(5f, 2f, 5f), Vector3.one * 1.5f)));
        }

        [Test]
        public void ABoxAboveTheSurfaceIsEmpty() {
            using var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());

            Assert.AreEqual(Occupancy.Empty, source.Classify(new Bounds(new Vector3(5f, 8f, 5f), Vector3.one * 1.5f)));
        }

        [Test]
        public void ABoxAcrossTheSurfaceIsMixed() {
            using var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());

            Assert.AreEqual(Occupancy.Mixed,
                source.Classify(new Bounds(new Vector3(5f, 4.5f, 5f), Vector3.one * 1.5f)));
        }

        [Test]
        public void ABoxOutsideTheLayerIsEmpty() {
            using var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());

            Assert.AreEqual(Occupancy.Empty,
                source.Classify(new Bounds(new Vector3(-20f, 2f, 5f), Vector3.one * 1.5f)));
        }

        [Test]
        public void ABoxHalfOutsideTheLayerOverRockIsMixed() {
            using var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());

            Assert.AreEqual(Occupancy.Mixed,
                source.Classify(new Bounds(new Vector3(-0.5f, 2f, 5f), Vector3.one * 1.5f)));
        }

        [Test]
        public void TheWorldOriginAndVoxelSizePlaceTheLayer() {
            using var source =
                new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create(), new Vector3(100f, 0f, 0f), 2f);

            Assert.AreEqual(Occupancy.Solid,
                source.Classify(new Bounds(new Vector3(110f, 6f, 10f), Vector3.one * 1.5f)));
            Assert.AreEqual(Occupancy.Empty,
                source.Classify(new Bounds(new Vector3(110f, 14f, 10f), Vector3.one * 1.5f)));
            Assert.AreEqual(Occupancy.Empty,
                source.Classify(new Bounds(new Vector3(10f, 6f, 10f), Vector3.one * 1.5f)));
        }

        [Test]
        public void ABoxSmallerThanAVoxelReadsTheNearestVoxel() {
            using var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());

            Assert.AreEqual(Occupancy.Solid,
                source.Classify(new Bounds(new Vector3(5.2f, 2.2f, 5.2f), Vector3.one * 0.1f)));
        }

        [Test]
        public void ANullLayerOrAVoxelSizeOfZeroIsRefused() {
            Assert.Throws<ArgumentNullException>(() => new VoxelLayerOccupancySource(null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create(), Vector3.zero, 0f));
        }

        [Test]
        public void ADisposedSourceRefusesToClassify() {
            var source = new VoxelLayerOccupancySource(HalfSolidVoxelLayer.Create());
            source.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                source.Classify(new Bounds(new Vector3(5f, 2f, 5f), Vector3.one)));
        }
    }
}
