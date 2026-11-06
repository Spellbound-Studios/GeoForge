// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;

namespace Spellbound.GeoForge.Tests {
    public class EdgeVertexMaterialTests {
        private const int FirstMatureByte = VoxelEncoding.MatureBit;
        private const int ByteCount = byte.MaxValue + 1;

        [Test]
        public void AnImmatureFullCornerKeepsTheSeamBytesOfTheAdditiveRule() {
            for (var raw = 0; raw < FirstMatureByte; raw++) {
                foreach (var bothMature in new[] { false, true }) {
                    Assert.AreEqual(AdditiveSeamMaterial((byte)raw, bothMature),
                        GfMarchHelper.EdgeVertexMaterial((byte)raw, bothMature),
                        $"raw {raw}, both mature {bothMature}");
                }
            }
        }

        [Test]
        public void AMatureFullCornerIsMatureExactlyWhenBothCornersAre() {
            for (var raw = FirstMatureByte; raw < ByteCount; raw++) {
                var index = (byte)(raw - FirstMatureByte);

                Assert.AreEqual((byte)raw, GfMarchHelper.EdgeVertexMaterial((byte)raw, true), $"raw {raw}");
                Assert.AreEqual(index, GfMarchHelper.EdgeVertexMaterial((byte)raw, false), $"raw {raw}");
                Assert.AreNotEqual(AdditiveSeamMaterial((byte)raw, true),
                    GfMarchHelper.EdgeVertexMaterial((byte)raw, true), $"raw {raw}");
            }
        }

        [Test]
        public void TheLeafMeshKeepsItsBytesForEveryCornerPair() {
            for (var raw = 0; raw < ByteCount; raw++) {
                var fullMature = VoxelEncoding.IsMature((byte)raw);

                foreach (var emptyMature in new[] { false, true }) {
                    Assert.AreEqual(LeafMeshMaterial((byte)raw, emptyMature),
                        GfMarchHelper.EdgeVertexMaterial((byte)raw, fullMature && emptyMature),
                        $"raw {raw}, empty mature {emptyMature}");
                }
            }
        }

        private static byte AdditiveSeamMaterial(byte fullCornerMaterial, bool bothMature) {
            return (byte)(fullCornerMaterial + (bothMature ? VoxelData.MatureBitValue : 0));
        }

        private static byte LeafMeshMaterial(byte fullCornerMaterial, bool emptyMature) {
            return emptyMature ? fullCornerMaterial : (byte)(fullCornerMaterial % VoxelData.MatureBitValue);
        }
    }
}
