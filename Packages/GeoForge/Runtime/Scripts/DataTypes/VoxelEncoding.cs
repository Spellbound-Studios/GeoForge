// Copyright (c) Spellbound Studios. All rights reserved.

using System.Runtime.CompilerServices;
using UnityEngine;

namespace Spellbound.GeoForge {
    public static class VoxelEncoding {
        public const byte MatureBit = 128;
        public const byte NullMaterialIndex = 127;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSolid(sbyte density) {
            return density >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SurfaceWeight(sbyte density) {
            return density / (float)sbyte.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte DensityFromFraction(float solidFraction) {
            return (sbyte)(solidFraction * byte.MaxValue + sbyte.MinValue);
        }

        public static sbyte DensityFromSignedDistance(float signedDistance, float gradient) {
            var density = -signedDistance * gradient;

            return (sbyte)Mathf.Clamp(density, sbyte.MinValue, sbyte.MaxValue);
        }

        public static float SignedDistanceFromDensity(sbyte density, float gradient) {
            return -density / gradient;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte EncodeMaterial(byte index, bool mature) {
            return mature ? (byte)(index % MatureBit + MatureBit) : (byte)(index % MatureBit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MaterialIndex(byte encoded) {
            return (byte)(encoded % MatureBit);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMature(byte encoded) {
            return encoded >= MatureBit;
        }
    }
}
