// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Spellbound.GeoForge.Places {
    public sealed class VoxelBrushHistory : ScriptableObject {
        private const string StrokeUndoName = "Voxel Brush Stroke";

        [SerializeField] private List<VoxelBrushHistoryEntry> entries = new();
        [SerializeField] private int cursor;

        public IReadOnlyList<VoxelBrushHistoryEntry> Entries => entries;

        public int Cursor => cursor;

        public bool CanUndo => cursor > 0;

        public bool CanRedo => cursor < entries.Count;

        public static VoxelBrushHistory Create() {
            var history = CreateInstance<VoxelBrushHistory>();
            history.hideFlags = HideFlags.HideAndDontSave;

            return history;
        }

        public void Record(VoxelBrushHistoryEntry entry) {
            entries.RemoveRange(cursor, entries.Count - cursor);
            entries.Add(entry);
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(this, StrokeUndoName);
            cursor++;
        }

        public void MoveCursor(int target) {
            cursor = Mathf.Clamp(target, 0, entries.Count);
        }
    }
}
