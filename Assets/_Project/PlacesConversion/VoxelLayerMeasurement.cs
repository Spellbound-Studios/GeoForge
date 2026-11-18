// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Spellbound.Places;
using UnityEditor;
using UnityEngine;

namespace PlacesConversion {
    public static class VoxelLayerMeasurement {
        public static string Measure(string placeAssetPath, int reimports, string readingsPath) {
            if (string.IsNullOrEmpty(placeAssetPath)) {
                throw new ArgumentException("A place asset path is required.", nameof(placeAssetPath));
            }

            if (reimports < 1) {
                throw new ArgumentOutOfRangeException(nameof(reimports), reimports, "At least one reimport is timed.");
            }

            if (string.IsNullOrEmpty(readingsPath)) {
                throw new ArgumentException("A readings path is required.", nameof(readingsPath));
            }

            var projectFolder = Path.GetDirectoryName(Application.dataPath);
            var placeBytes = new FileInfo(Path.Combine(projectFolder, placeAssetPath)).Length;
            var milliseconds = new List<long>(reimports);

            for (var reimport = 0; reimport < reimports; reimport++) {
                var stopwatch = Stopwatch.StartNew();
                AssetDatabase.ImportAsset(placeAssetPath, ImportAssetOptions.ForceUpdate);
                var place = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(placeAssetPath);
                stopwatch.Stop();

                if (place == null) {
                    throw new InvalidOperationException("'" + placeAssetPath
                                                        + "' did not load as a place after reimport "
                                                        + (reimport + 1) + ".");
                }

                milliseconds.Add(stopwatch.ElapsedMilliseconds);
            }

            var medianLine = "median: " + Median(milliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
            var readings = new StringBuilder();
            readings.Append("place bytes: ").Append(placeBytes.ToString(CultureInfo.InvariantCulture)).Append('\n');

            for (var reimport = 0; reimport < milliseconds.Count; reimport++) {
                readings.Append("reimport ").Append((reimport + 1).ToString(CultureInfo.InvariantCulture)).Append(": ")
                    .Append(milliseconds[reimport].ToString(CultureInfo.InvariantCulture)).Append(" ms\n");
            }

            readings.Append(medianLine).Append('\n');
            File.WriteAllText(readingsPath, readings.ToString());
            return medianLine;
        }

        private static long Median(List<long> values) {
            var sorted = new List<long>(values);
            sorted.Sort();
            var middle = sorted.Count / 2;

            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
    }
}