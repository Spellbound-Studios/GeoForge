// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelRegionTests {
        [Test]
        public void ChunksEnumerateInXThenYThenZOrderAndCountMatches() {
            var region = new VoxelRegion(new Vector3Int(1, 2, 3), new Vector3Int(2, 1, 2));

            var chunks = region.Chunks().ToList();

            CollectionAssert.AreEqual(new[] {
                new Vector3Int(1, 2, 3),
                new Vector3Int(2, 2, 3),
                new Vector3Int(1, 2, 4),
                new Vector3Int(2, 2, 4)
            }, chunks);
            Assert.AreEqual(4, region.ChunkCount);
            Assert.AreEqual(new Vector3Int(1, 2, 3), region.MinimumChunk);
            Assert.AreEqual(new Vector3Int(2, 1, 2), region.ChunkExtent);
        }

        [Test]
        public void OriginIsOneChunkAtZero() {
            var origin = VoxelRegion.Origin;

            Assert.AreEqual(Vector3Int.zero, origin.MinimumChunk);
            Assert.AreEqual(Vector3Int.one, origin.ChunkExtent);
            Assert.AreEqual(1, origin.ChunkCount);
            CollectionAssert.AreEqual(new[] { Vector3Int.zero }, origin.Chunks().ToList());
        }

        [Test]
        public void ContainsAndOffsetOfReadFromTheMinimumChunk() {
            var region = new VoxelRegion(new Vector3Int(1, 2, 3), new Vector3Int(2, 1, 2));

            Assert.IsTrue(region.Contains(new Vector3Int(1, 2, 3)));
            Assert.IsTrue(region.Contains(new Vector3Int(2, 2, 4)));
            Assert.IsFalse(region.Contains(new Vector3Int(3, 2, 3)));
            Assert.IsFalse(region.Contains(new Vector3Int(1, 3, 3)));
            Assert.IsFalse(region.Contains(new Vector3Int(1, 2, 2)));
            Assert.AreEqual(new Vector3Int(1, 0, 1), region.OffsetOf(new Vector3Int(2, 2, 4)));
        }

        [Test]
        public void AnExtentBelowOneThrows() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelRegion(Vector3Int.zero, new Vector3Int(0, 1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new VoxelRegion(Vector3Int.zero, new Vector3Int(1, -1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoxelRegion(Vector3Int.zero, new Vector3Int(1, 1, 0)));
        }

        [Test]
        public void RegionsWithTheSameChunksAreEqualAndReadAsText() {
            var region = new VoxelRegion(new Vector3Int(-1, 0, 2), new Vector3Int(3, 1, 2));
            var same = new VoxelRegion(new Vector3Int(-1, 0, 2), new Vector3Int(3, 1, 2));
            var other = new VoxelRegion(new Vector3Int(-1, 0, 2), new Vector3Int(3, 1, 1));

            Assert.IsTrue(region.Equals(same));
            Assert.IsTrue(region.Equals((object)same));
            Assert.IsTrue(region == same);
            Assert.IsFalse(region != same);
            Assert.AreEqual(region.GetHashCode(), same.GetHashCode());
            Assert.IsFalse(region.Equals(other));
            Assert.IsTrue(region != other);
            Assert.AreEqual("-1,0,2 x 3,1,2", region.ToString());
        }
    }
}
