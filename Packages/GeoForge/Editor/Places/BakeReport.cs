// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Spellbound.Places;
using Unity.Collections;
using UnityEditor;

namespace Spellbound.GeoForge.Places {
    public static class BakeReport {
        private const string Extension = ".bake.txt";
        private const string LineEnd = "\r\n";
        private const int East = 0;
        private const int West = 1;
        private const int Up = 2;
        private const int Down = 3;
        private const int North = 4;
        private const int South = 5;

        private static readonly string[] FaceNames = { "east", "west", "up", "down", "north", "south" };

        public static IReadOnlyList<string> Lines(PlaceDefinition place, VoxelLayer layer,
            GeoForgeDataGenerator generator, VoxelVolumeConfig config, float voxelSize) {
            if (place == null) {
                throw new ArgumentNullException(nameof(place));
            }

            if (layer == null) {
                throw new ArgumentNullException(nameof(layer));
            }

            if (generator == null) {
                throw new ArgumentNullException(nameof(generator));
            }

            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }

            var open = new int[FaceNames.Length];
            var faceTotal = new int[FaceNames.Length];
            var runs = 0;
            var interior = 0;
            var empty = 0;

            foreach (var chunk in layer.Chunks) {
                runs += chunk.Runs.Count;

                if (chunk.Runs.Count > 0) {
                    ReadChunk(layer, chunk, open, faceTotal, ref interior, ref empty);
                }
            }

            var width = layer.DataWidth.ToString(CultureInfo.InvariantCulture);
            var lines = new List<string> {
                "place: " + place.name,
                "layer: " + layer.LayerName,
                "generator: " + generator.name,
                "voxel config: " + config.name,
                "dimensions: " + width + " x " + width + " x " + width,
                "chunks: " + layer.Chunks.Count.ToString(CultureInfo.InvariantCulture),
                "resolution: " + voxelSize.ToString(CultureInfo.InvariantCulture),
                "runs: " + runs.ToString(CultureInfo.InvariantCulture),
                "empty fraction: " + Fraction(empty, interior),
                string.Empty,
                "face      open fraction"
            };

            for (var face = 0; face < FaceNames.Length; face++) {
                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0,-9} {1}", FaceNames[face],
                    Fraction(open[face], faceTotal[face])));
            }

            return lines;
        }

        public static string Write(string assetPath, IReadOnlyList<string> lines) {
            if (string.IsNullOrEmpty(assetPath)) {
                throw new ArgumentException("A report is written beside an asset path.", nameof(assetPath));
            }

            if (lines == null) {
                throw new ArgumentNullException(nameof(lines));
            }

            var reportPath = Path.ChangeExtension(assetPath, null) + Extension;
            File.WriteAllText(reportPath, string.Join(LineEnd, lines) + LineEnd);
            AssetDatabase.ImportAsset(reportPath);

            return reportPath;
        }

        private static string Fraction(int count, int total) {
            var fraction = total == 0 ? 0f : (float)count / total;

            return fraction.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static void ReadChunk(VoxelLayer layer, VoxelLayerChunk chunk, int[] open, int[] faceTotal,
            ref int interior, ref int empty) {
            var first = 1;
            var last = layer.ChunkSize;
            var width = layer.DataWidth;
            var extent = layer.ChunkExtent;
            var offset = chunk.Offset;
            var voxels = VoxelRunDecoder.Decode(chunk.Runs, layer.ChunkDataVolume, Allocator.Temp);

            try {
                for (var y = first; y <= last; y++) {
                    for (var z = first; z <= last; z++) {
                        for (var x = first; x <= last; x++) {
                            interior++;

                            if (IsEmpty(voxels, x, y, z, width)) {
                                empty++;
                            }
                        }
                    }
                }

                for (var a = first; a <= last; a++) {
                    for (var b = first; b <= last; b++) {
                        if (offset.x == extent.x - 1) {
                            CountFace(East, IsEmpty(voxels, last, a, b, width), open, faceTotal);
                        }

                        if (offset.x == 0) {
                            CountFace(West, IsEmpty(voxels, first, a, b, width), open, faceTotal);
                        }

                        if (offset.y == extent.y - 1) {
                            CountFace(Up, IsEmpty(voxels, a, last, b, width), open, faceTotal);
                        }

                        if (offset.y == 0) {
                            CountFace(Down, IsEmpty(voxels, a, first, b, width), open, faceTotal);
                        }

                        if (offset.z == extent.z - 1) {
                            CountFace(North, IsEmpty(voxels, a, b, last, width), open, faceTotal);
                        }

                        if (offset.z == 0) {
                            CountFace(South, IsEmpty(voxels, a, b, first, width), open, faceTotal);
                        }
                    }
                }
            } finally {
                voxels.Dispose();
            }
        }

        private static void CountFace(int face, bool isOpen, int[] open, int[] faceTotal) {
            faceTotal[face]++;

            if (isOpen) {
                open[face]++;
            }
        }

        private static bool IsEmpty(NativeArray<VoxelData> voxels, int x, int y, int z, int width) {
            return voxels[GfStaticHelper.Coord3DToIndex(x, y, z, width * width, width)].Density < 0;
        }
    }
}
