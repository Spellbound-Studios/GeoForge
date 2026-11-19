// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.Places.Tests {
    public class VoxelStrokeTests {
        [Test]
        public void TheShapesAreSphereAndBox() {
            CollectionAssert.AreEqual(new[] { "Sphere", "Box" }, Enum.GetNames(typeof(VoxelStrokeShape)));
            Assert.AreEqual(0, (int)VoxelStrokeShape.Sphere);
            Assert.AreEqual(1, (int)VoxelStrokeShape.Box);
        }

        [Test]
        public void TheModesAreAddRemoveSmoothAndPaint() {
            CollectionAssert.AreEqual(new[] { "Add", "Remove", "Smooth", "Paint" },
                Enum.GetNames(typeof(VoxelStrokeMode)));
            Assert.AreEqual(0, (int)VoxelStrokeMode.Add);
            Assert.AreEqual(1, (int)VoxelStrokeMode.Remove);
            Assert.AreEqual(2, (int)VoxelStrokeMode.Smooth);
            Assert.AreEqual(3, (int)VoxelStrokeMode.Paint);
        }

        [Test]
        public void AStrokeReadsBackTheValuesItWasBuiltWith() {
            var stroke = new VoxelStroke(VoxelStrokeShape.Box, VoxelStrokeMode.Smooth, new Vector3(1.5f, -2f, 3.25f),
                2.5f, 7);

            Assert.AreEqual(VoxelStrokeShape.Box, stroke.Shape);
            Assert.AreEqual(VoxelStrokeMode.Smooth, stroke.Mode);
            Assert.AreEqual(new Vector3(1.5f, -2f, 3.25f), stroke.Centre);
            Assert.AreEqual(2.5f, stroke.Size);
            Assert.AreEqual(7, stroke.Material);
        }

        [Test]
        public void ADefaultStrokeIsAnAddingSphereOfNoSize() {
            var stroke = default(VoxelStroke);

            Assert.AreEqual(VoxelStrokeShape.Sphere, stroke.Shape);
            Assert.AreEqual(VoxelStrokeMode.Add, stroke.Mode);
            Assert.AreEqual(Vector3.zero, stroke.Centre);
            Assert.AreEqual(0f, stroke.Size);
            Assert.AreEqual(0, stroke.Material);
        }

        [Test]
        public void AStrokeOnAScriptableObjectFieldReadsBackThroughSerializedObject() {
            var holder = ScriptableObject.CreateInstance<VoxelStrokeHolder>();

            try {
                holder.Stroke = new VoxelStroke(VoxelStrokeShape.Box, VoxelStrokeMode.Paint,
                    new Vector3(4f, 0.5f, -6f), 3f, 200);

                using var serialized = new SerializedObject(holder);
                var stroke = serialized.FindProperty("stroke");

                Assert.IsNotNull(stroke);
                Assert.AreEqual((int)VoxelStrokeShape.Box, stroke.FindPropertyRelative("shape").enumValueIndex);
                Assert.AreEqual((int)VoxelStrokeMode.Paint, stroke.FindPropertyRelative("mode").enumValueIndex);
                Assert.AreEqual(new Vector3(4f, 0.5f, -6f), stroke.FindPropertyRelative("centre").vector3Value);
                Assert.AreEqual(3f, stroke.FindPropertyRelative("size").floatValue);
                Assert.AreEqual(200, stroke.FindPropertyRelative("material").intValue);
            } finally {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AStrokeWrittenThroughSerializedObjectReachesTheField() {
            var holder = ScriptableObject.CreateInstance<VoxelStrokeHolder>();

            try {
                using (var serialized = new SerializedObject(holder)) {
                    var stroke = serialized.FindProperty("stroke");
                    stroke.FindPropertyRelative("shape").enumValueIndex = (int)VoxelStrokeShape.Sphere;
                    stroke.FindPropertyRelative("mode").enumValueIndex = (int)VoxelStrokeMode.Remove;
                    stroke.FindPropertyRelative("centre").vector3Value = new Vector3(-1f, 2f, 8f);
                    stroke.FindPropertyRelative("size").floatValue = 4.5f;
                    stroke.FindPropertyRelative("material").intValue = 12;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                Assert.AreEqual(VoxelStrokeShape.Sphere, holder.Stroke.Shape);
                Assert.AreEqual(VoxelStrokeMode.Remove, holder.Stroke.Mode);
                Assert.AreEqual(new Vector3(-1f, 2f, 8f), holder.Stroke.Centre);
                Assert.AreEqual(4.5f, holder.Stroke.Size);
                Assert.AreEqual(12, holder.Stroke.Material);
            } finally {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
