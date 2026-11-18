// Copyright (c) Spellbound Studios. All rights reserved.

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Tests {
    public class VoxelVolumeConfigCreateTests {
        [TestCase(32, 1f, 1, 1, 1)]
        [TestCase(64, 0.5f, 2, 1, 3)]
        [TestCase(128, 2f, 1, 4, 1)]
        public void CreatedConfigMatchesValidatedAsset(int chunkSize, float voxelSize, int x, int y, int z) {
            var sizeInChunks = new Vector3Int(x, y, z);
            var validated = ValidatedAsset(chunkSize, voxelSize, sizeInChunks);
            var created = VoxelVolumeConfig.Create(chunkSize, voxelSize, sizeInChunks);

            try {
                Assert.That(created.chunkSize, Is.EqualTo(validated.chunkSize));
                Assert.That(created.levelsOfDetail, Is.EqualTo(validated.levelsOfDetail));
                Assert.That(created.sizeInChunks, Is.EqualTo(validated.sizeInChunks));
                Assert.That(created.volumeSize, Is.EqualTo(validated.volumeSize));
                Assert.That(created.resolution, Is.EqualTo(validated.resolution));
                Assert.That(created.isFiniteSize, Is.True);
            }
            finally {
                Object.DestroyImmediate(created);
                Object.DestroyImmediate(validated);
            }
        }

        [Test]
        public void CreatedConfigClampsEmptyExtentToOneChunk() {
            var created = VoxelVolumeConfig.Create(32, 1f, Vector3Int.zero);

            try {
                Assert.That(created.sizeInChunks, Is.EqualTo(Vector3Int.one));
            }
            finally {
                Object.DestroyImmediate(created);
            }
        }

        private static VoxelVolumeConfig ValidatedAsset(int chunkSize, float voxelSize, Vector3Int sizeInChunks) {
            var asset = ScriptableObject.CreateInstance<VoxelVolumeConfig>();
            var serialized = new SerializedObject(asset);
            serialized.FindProperty(nameof(VoxelVolumeConfig.maxChunkSize)).intValue = chunkSize;
            serialized.FindProperty(nameof(VoxelVolumeConfig.resolution)).floatValue = voxelSize;
            serialized.FindProperty(nameof(VoxelVolumeConfig.sizeInChunks)).vector3IntValue = sizeInChunks;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return asset;
        }
    }
}
