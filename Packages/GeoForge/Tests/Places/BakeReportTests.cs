// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using NUnit.Framework;
using Spellbound.Places;
using UnityEngine;

namespace Spellbound.GeoForge.Places.Tests {
    public class BakeReportTests {
        private const int ChunkSize = 64;
        private const int DataWidth = 67;
        private const int FirstAirLayer = 33;

        private readonly List<Object> _created = new();

        [TearDown]
        public void DestroyCreated() {
            foreach (var created in _created) {
                if (created != null) {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheReportHasEveryLineInOrder() {
            var place = Create<PlaceDefinition>("ReportPlace");
            var generator = Create<PerlinTerrainDataFactory>("ReportGenerator");
            var config = Create<VoxelVolumeConfig>("ReportConfig");
            var layer = new VoxelLayer { LayerName = "voxels" };
            var runs = new List<SparseVoxelData> {
                new(VoxelData.CreateImmature(sbyte.MaxValue, 0), 0),
                new(VoxelData.CreateImmature(sbyte.MinValue, 0), FirstAirLayer * DataWidth * DataWidth)
            };
            layer.SetChunks(ChunkSize, Vector3Int.one,
                new List<VoxelLayerChunk> { new(Vector3Int.zero, runs) });

            var lines = BakeReport.Lines(place, layer, generator, config, 1f);

            var expected = new[] {
                "place: ReportPlace",
                "layer: voxels",
                "generator: ReportGenerator",
                "voxel config: ReportConfig",
                "dimensions: 67 x 67 x 67",
                "chunks: 1",
                "resolution: 1",
                "runs: 2",
                "empty fraction: 0.500",
                "",
                "face      open fraction",
                "east      0.500",
                "west      0.500",
                "up        1.000",
                "down      0.000",
                "north     0.500",
                "south     0.500"
            };
            CollectionAssert.AreEqual(expected, lines);
        }

        private T Create<T>(string name) where T : ScriptableObject {
            var created = ScriptableObject.CreateInstance<T>();
            created.name = name;
            _created.Add(created);

            return created;
        }
    }
}
