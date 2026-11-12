// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace Spellbound.GeoForge.Networking.Tests {
    internal static class VoxelEditDigest {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal static ulong Of(VoxelWorld world) {
            var records = Records(world);
            var digest = Offset;

            foreach (var (chunk, index, voxel) in records) {
                digest = Mix(digest, chunk.x);
                digest = Mix(digest, chunk.y);
                digest = Mix(digest, chunk.z);
                digest = Mix(digest, index);
                digest = Mix(digest, voxel.Density);
                digest = Mix(digest, voxel.MaterialIndex);
            }

            return digest;
        }

        internal static int CountOf(VoxelWorld world) {
            return Records(world).Count;
        }

        internal static List<(Vector3Int Chunk, int Index, VoxelData Voxel)> Records(VoxelWorld world) {
            var records = new List<(Vector3Int Chunk, int Index, VoxelData Voxel)>();

            foreach (var coordinate in world.Coordinates) {
                foreach (var (index, voxel) in world.Stores.CreateFor(coordinate).ReadAllEdits())
                    records.Add((coordinate, index, voxel));
            }

            records.Sort((left, right) => {
                var order = left.Chunk.x.CompareTo(right.Chunk.x);

                if (order == 0)
                    order = left.Chunk.y.CompareTo(right.Chunk.y);

                if (order == 0)
                    order = left.Chunk.z.CompareTo(right.Chunk.z);

                return order != 0 ? order : left.Index.CompareTo(right.Index);
            });

            return records;
        }

        private static ulong Mix(ulong digest, int value) {
            for (var shift = 0; shift < 32; shift += 8) {
                digest ^= (byte)(value >> shift);
                digest *= Prime;
            }

            return digest;
        }
    }
}