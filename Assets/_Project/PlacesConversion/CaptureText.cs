// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Spellbound.GeoForge;
using UnityEngine;

namespace PlacesConversion {
    public static class CaptureText {
        public const int CaptureChunkSize = 64;

        private const string DimensionsKey = "dimensions:";
        private const string RunsKey = "sparseVoxelData:";
        private const string PropsKey = "props:";
        private const string RunItemLine = "- Voxel:";
        private const string DensityKey = "Density:";
        private const string MaterialIndexKey = "MaterialIndex:";
        private const string StartIndexKey = "startIndex:";

        private static readonly Regex DimensionsPattern =
            new(@"^dimensions: \{x: (-?\d+), y: (-?\d+), z: (-?\d+)\}$", RegexOptions.CultureInvariant);

        public static CaptureTextResult Parse(string text) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            var dataWidth = ChunkLayout.DataWidth(CaptureChunkSize);
            Vector3Int? dimensions = null;
            var runs = new List<SparseVoxelData>();
            var readingRuns = false;
            var lineNumber = 0;

            using var reader = new StringReader(text);

            while (reader.ReadLine() is { } line) {
                lineNumber++;
                var trimmed = line.Trim();

                if (!readingRuns) {
                    if (trimmed.StartsWith(DimensionsKey, StringComparison.Ordinal)) {
                        dimensions = ReadDimensions(trimmed, lineNumber);
                    }
                    else if (trimmed == RunsKey) {
                        readingRuns = true;
                    }

                    continue;
                }

                if (trimmed == PropsKey) {
                    break;
                }

                if (trimmed != RunItemLine) {
                    throw new InvalidDataException("line " + lineNumber + " is '" + trimmed
                                                   + "' where a run starts with '" + RunItemLine + "'.");
                }

                var density = ReadValue(reader, DensityKey, ref lineNumber);
                var materialIndex = ReadValue(reader, MaterialIndexKey, ref lineNumber);
                var startIndex = ReadValue(reader, StartIndexKey, ref lineNumber);

                var voxel = new VoxelData {
                    Density = sbyte.Parse(density, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                    MaterialIndex = byte.Parse(materialIndex, NumberStyles.None, CultureInfo.InvariantCulture)
                };

                runs.Add(new SparseVoxelData(voxel,
                    int.Parse(startIndex, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)));
            }

            if (dimensions == null) {
                throw new InvalidDataException("the capture text has no '" + DimensionsKey + "' line.");
            }

            var size = dimensions.Value;

            if (size.x != dataWidth || size.y != dataWidth || size.z != dataWidth) {
                throw new InvalidDataException("the capture is " + size + " voxels where a chunk of size "
                                               + CaptureChunkSize + " holds " + dataWidth + " along each axis.");
            }

            if (!readingRuns) {
                throw new InvalidDataException("the capture text has no '" + RunsKey + "' line.");
            }

            return new CaptureTextResult(size, runs);
        }

        private static Vector3Int ReadDimensions(string trimmed, int lineNumber) {
            var match = DimensionsPattern.Match(trimmed);

            if (!match.Success) {
                throw new InvalidDataException("line " + lineNumber + " is '" + trimmed
                                               + "' where the dimensions read '{x: <n>, y: <n>, z: <n>}'.");
            }

            return new Vector3Int(
                int.Parse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[3].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        }

        private static string ReadValue(StringReader reader, string key, ref int lineNumber) {
            var line = reader.ReadLine();
            lineNumber++;

            if (line == null) {
                throw new InvalidDataException("the capture text ends at line " + lineNumber + " where '" + key
                                               + "' is expected.");
            }

            var trimmed = line.Trim();

            if (!trimmed.StartsWith(key, StringComparison.Ordinal)) {
                throw new InvalidDataException("line " + lineNumber + " is '" + trimmed + "' where '" + key
                                               + "' is expected.");
            }

            return trimmed.Substring(key.Length).Trim();
        }
    }
}