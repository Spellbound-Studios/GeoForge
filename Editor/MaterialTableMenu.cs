// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.Logging;
using UnityEditor;

namespace Spellbound.GeoForge.Materials.Editor {
    internal static class MaterialTableMenu {
        private const string ValidateAllPath = "Spellbound/GeoForge/Validate All Material Side Tables";
        private const string SyncAllPath = "Spellbound/GeoForge/Sync All Material Side Tables";

        [MenuItem(ValidateAllPath)]
        private static void ValidateAll() {
            var tables = FindAllTables();
            var anyProblems = false;

            foreach (var table in tables) {
                var problems = table.Validate();

                if (problems.Count == 0) {
                    continue;
                }

                anyProblems = true;
                MaterialTableConsole.ReportProblems(table, problems);
            }

            if (!anyProblems) {
                Log.Info("All " + tables.Count + " material side table(s) are in sync.");
            }
        }

        [MenuItem(SyncAllPath)]
        private static void SyncAll() {
            var tables = FindAllTables();

            foreach (var table in tables) {
                table.Sync();
            }

            AssetDatabase.SaveAssets();
            Log.Info("Synced " + tables.Count + " material side table(s).");
        }

        private static List<MaterialSideTableBase> FindAllTables() {
            var tables = new List<MaterialSideTableBase>();

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(MaterialSideTableBase))) {
                var table = AssetDatabase.LoadAssetAtPath<MaterialSideTableBase>(AssetDatabase.GUIDToAssetPath(guid));

                if (table != null) {
                    tables.Add(table);
                }
            }

            return tables;
        }
    }
}
