// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using Spellbound.Core.Authoring;
using Spellbound.Core.Controls.Editor;
using Spellbound.Core.Logging;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Spellbound.GeoForge.Materials.Editor {
    [CustomEditor(typeof(MaterialSideTableBase), true)]
    public class MaterialTableInspector : UnityEditor.Editor {
        public const string ClassName = "sb-material-table";
        public const string DatabaseCardClassName = ClassName + "__database";
        public const string SyncButtonClassName = ClassName + "__sync";
        public const string ValidateButtonClassName = ClassName + "__validate";
        public const string StatusClassName = ClassName + "__status";
        public const string ProblemsClassName = ClassName + "__problems";
        public const string ProblemClassName = ClassName + "__problem";
        public const string DatabaseCardTitle = "Material Database";
        public const string SyncText = "Sync";
        public const string ValidateText = "Validate";
        public const string NotValidatedText = "Not validated yet";
        public const string InSyncText = "In sync";
        public const string OutOfSyncText = "Out of sync";

        private HeaderBar _status;
        private VisualElement _problems;

        private MaterialSideTableBase Table => (MaterialSideTableBase)target;

        public override VisualElement CreateInspectorGUI() {
            var root = new VisualElement();
            root.AddToClassList(ClassName);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.Add(CreateDatabaseCard());
            return root;
        }

        public void SyncTable() {
            var table = Table;
            table.Sync();
            EditorUtility.SetDirty(table);
            serializedObject.Update();
            ShowProblems(table.Validate());
        }

        public IReadOnlyList<string> ValidateTable() {
            var table = Table;
            var problems = table.Validate();
            ShowProblems(problems);

            if (problems.Count == 0) {
                Log.Info(table.name + ": in sync.");
            }
            else {
                MaterialTableConsole.ReportProblems(table, problems);
            }

            return problems;
        }

        private SectionCard CreateDatabaseCard() {
            var card = new SectionCard(DatabaseCardTitle);
            card.AddToClassList(DatabaseCardClassName);
            card.Header.Add(CreateButton(SyncText, SyncButtonClassName, SyncTable));
            card.Header.Add(CreateButton(ValidateText, ValidateButtonClassName, () => ValidateTable()));
            _status = new HeaderBar(NotValidatedText);
            _status.AddToClassList(StatusClassName);
            card.Add(_status);
            _problems = new VisualElement();
            _problems.AddToClassList(ProblemsClassName);
            card.Add(_problems);
            return card;
        }

        private void ShowProblems(IReadOnlyList<string> problems) {
            if (_status == null) {
                return;
            }

            _problems.Clear();

            if (problems.Count == 0) {
                _status.Title = InSyncText;
                _status.Severity = FindingSeverity.Information;
                return;
            }

            _status.Title = OutOfSyncText + ": " + (problems.Count == 1 ? "1 problem" : problems.Count + " problems");
            _status.Severity = FindingSeverity.Error;

            foreach (var problem in problems) {
                var row = new Label(problem);
                row.AddToClassList(ProblemClassName);
                row.style.whiteSpace = WhiteSpace.Normal;
                _problems.Add(row);
            }
        }

        private static Button CreateButton(string text, string className, Action clicked) {
            var button = new Button(clicked) { text = text };
            button.AddToClassList(className);
            return button;
        }
    }
}
