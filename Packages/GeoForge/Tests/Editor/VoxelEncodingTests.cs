// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Spellbound.GeoForge.Tests {
    public class VoxelEncodingTests {
        private const int FractionBitStride = 1024;

        private static readonly float[] Gradients = { 0.5f, 1f, 8f, 32f, 127f, 200f };

        [Test]
        public void SolidityIsTheZeroSplitOverEveryDensity() {
            for (int density = sbyte.MinValue; density <= sbyte.MaxValue; density++)
                Assert.AreEqual(density >= 0, VoxelEncoding.IsSolid((sbyte)density), $"density {density}");
        }

        [Test]
        public void SurfaceWeightIsTheDensityOverTheLargestDensity() {
            for (int density = sbyte.MinValue; density <= sbyte.MaxValue; density++)
                Assert.AreEqual(density / (float)sbyte.MaxValue, VoxelEncoding.SurfaceWeight((sbyte)density),
                    $"density {density}");
        }

        [Test]
        public void EveryEncodedMaterialRoundTripsAndMatchesVoxelData() {
            for (int value = byte.MinValue; value <= byte.MaxValue; value++) {
                var encoded = (byte)value;
                var plainIndex = (byte)(encoded % 128);
                var voxel = VoxelData.CreateImmature(0, encoded);
                voxel.MaterialIndex = encoded;

                Assert.AreEqual(plainIndex, VoxelEncoding.MaterialIndex(encoded), $"encoded {value}");
                Assert.AreEqual(encoded >= 128, VoxelEncoding.IsMature(encoded), $"encoded {value}");
                Assert.AreEqual(voxel.GetPlainMatIndex(), VoxelEncoding.MaterialIndex(encoded), $"encoded {value}");
                Assert.AreEqual(voxel.IsMature(), VoxelEncoding.IsMature(encoded), $"encoded {value}");

                Assert.AreEqual((byte)(encoded % 128), VoxelEncoding.EncodeMaterial(encoded, false), $"index {value}");
                Assert.AreEqual((byte)(encoded % 128 + 128), VoxelEncoding.EncodeMaterial(encoded, true),
                    $"index {value}");
                Assert.AreEqual(VoxelData.CreateImmature(0, encoded).MaterialIndex,
                    VoxelEncoding.EncodeMaterial(encoded, false), $"index {value}");
                Assert.AreEqual(VoxelData.CreateMature(0, encoded).MaterialIndex,
                    VoxelEncoding.EncodeMaterial(encoded, true), $"index {value}");

                var reencoded = VoxelEncoding.EncodeMaterial(VoxelEncoding.MaterialIndex(encoded),
                    VoxelEncoding.IsMature(encoded));
                Assert.AreEqual(encoded, reencoded, $"encoded {value}");
            }
        }

        [Test]
        public void TheMaterialConstantsAreTheVoxelDataConstants() {
            Assert.AreEqual(128, VoxelEncoding.MatureBit);
            Assert.AreEqual(127, VoxelEncoding.NullMaterialIndex);
            Assert.AreEqual(VoxelData.MatureBitValue, VoxelEncoding.MatureBit);
            Assert.AreEqual(VoxelData.NullSentinelValue, VoxelEncoding.NullMaterialIndex);
        }

        [Test]
        public void DensityFromSignedDistanceIsTheGeneratorsRule() {
            foreach (var gradient in Gradients) {
                for (var step = -4000; step <= 4000; step++) {
                    var signedDistance = step * 0.0625f;

                    Assert.AreEqual(GeneratorDensity(signedDistance, gradient),
                        VoxelEncoding.DensityFromSignedDistance(signedDistance, gradient),
                        $"signed distance {signedDistance}, gradient {gradient}");
                }
            }
        }

        [Test]
        public void VoxelEncoding_DensityFromFractionEqualsTheVoxelizeExpression() {
            var lastBits = math.asint(1f);
            var mismatches = 0;
            var firstMismatch = float.NaN;

            for (var bits = 0; bits <= lastBits; bits += FractionBitStride)
                CountFractionMismatch(math.asfloat(bits), ref mismatches, ref firstMismatch);

            for (var step = 0; step <= 256; step++) {
                CountFractionMismatch(step / 255f, ref mismatches, ref firstMismatch);
                CountFractionMismatch(step / 256f, ref mismatches, ref firstMismatch);
            }

            CountFractionMismatch(0f, ref mismatches, ref firstMismatch);
            CountFractionMismatch(1f, ref mismatches, ref firstMismatch);

            Assert.AreEqual(0, mismatches, $"first mismatch at fraction {firstMismatch:R}");
            Assert.AreEqual(sbyte.MinValue, VoxelEncoding.DensityFromFraction(0f));
            Assert.AreEqual(sbyte.MaxValue, VoxelEncoding.DensityFromFraction(1f));
        }

        [Test]
        public void SignedDistanceFromDensityInvertsWithinOneDensityStep() {
            foreach (var gradient in Gradients) {
                var limit = sbyte.MaxValue / gradient;

                for (var step = -1000; step <= 1000; step++) {
                    var signedDistance = step * 0.001f * limit;
                    var density = VoxelEncoding.DensityFromSignedDistance(signedDistance, gradient);
                    var recovered = VoxelEncoding.SignedDistanceFromDensity(density, gradient);

                    Assert.LessOrEqual(Mathf.Abs(recovered - signedDistance), 1f / gradient + 1e-5f,
                        $"signed distance {signedDistance}, gradient {gradient}");
                }

                for (int density = sbyte.MinValue + 1; density <= sbyte.MaxValue; density++) {
                    var signedDistance = VoxelEncoding.SignedDistanceFromDensity((sbyte)density, gradient);
                    var recovered = VoxelEncoding.DensityFromSignedDistance(signedDistance, gradient);

                    Assert.LessOrEqual(Mathf.Abs(recovered - density), 1, $"density {density}, gradient {gradient}");
                }
            }
        }

        private static sbyte GeneratorDensity(float signedDistance, float gradient) {
            var density = -signedDistance * gradient;

            return (sbyte)Mathf.Clamp(density, sbyte.MinValue, sbyte.MaxValue);
        }

        private static void CountFractionMismatch(float fraction, ref int mismatches, ref float firstMismatch) {
            if (VoxelEncoding.DensityFromFraction(fraction) == (sbyte)(fraction * byte.MaxValue + sbyte.MinValue))
                return;

            if (mismatches == 0)
                firstMismatch = fraction;

            mismatches++;
        }
    }
}
