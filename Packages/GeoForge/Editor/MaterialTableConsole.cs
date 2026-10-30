// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.Logging;
using UnityEditor;

namespace Spellbound.GeoForge.Materials.Editor {
    internal static class MaterialTableConsole {
        public static void ReportProblems(MaterialSideTableBase table, IReadOnlyList<string> problems) {
            var message = table.name + ":\n  " + string.Join("\n  ", problems);

            if (EditorApplication.isPlaying) {
                Log.Error(message);
                return;
            }

            using (Log.AddScopedSink(new UnityConsoleSink(), null, LogLevel.Error)) {
                Log.Error(message);
            }
        }
    }
}
