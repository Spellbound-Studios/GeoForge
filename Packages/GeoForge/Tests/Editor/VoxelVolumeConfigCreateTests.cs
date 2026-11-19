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

        [Test]
        public void CopiedConfigKeepsTheSourceSettingsWithTheGivenExtent() {
            var source = VoxelVolumeConfig.Create(64, 0.5f, new Vector3Int(1, 1, 1));
            var extent = new Vector3Int(2, 3, 1);
            var copy = VoxelVolumeConfig.Create(source, extent);

            try {
                Assert.That(copy, Is.Not.SameAs(source));
                Assert.That(copy.chunkSize, Is.EqualTo(source.chunkSize));
                Assert.That(copy.levelsOfDetail, Is.EqualTo(source.levelsOfDetail));
                Assert.That(copy.resolution, Is.EqualTo(source.resolution));
                Assert.That(copy.sizeInChunks, Is.EqualTo(extent));
                Assert.That(copy.isFiniteSize, Is.True);
                Assert.That(copy.volumeSize, Is.EqualTo((Vector3)extent * source.chunkSize * source.resolution));
                Assert.That(source.sizeInChunks, Is.EqualTo(Vector3Int.one));
            }
            finally {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(source);
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
