// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using Spellbound.Core.Logging;

namespace Spellbound.GeoForge.Materials.Editor {
    internal static class MaterialTableConsole {
        public static void ReportProblems(MaterialSideTableBase table, IReadOnlyList<string> problems) {
            Log.Error(table.name + ":\n  " + string.Join("\n  ", problems));
        }
    }
}
