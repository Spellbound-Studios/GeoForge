// Copyright (c) Spellbound Studios. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Spellbound.Core.Controls.Editor;
using Spellbound.GeoForge.Materials.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Spellbound.GeoForge.Tests {
    public class MaterialTableInspectorTests {
        private const string TableName = "Fixture Material Table";
        private const string DatabaseName = "Fixture Material Database";
        private const string NoDatabaseProblem = "No VoxelMaterialDatabase assigned.";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown() {
            for (var index = _created.Count - 1; index >= 0; index--) {
                if (_created[index] != null) {
                    Object.DestroyImmediate(_created[index]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheInspectorDrawsTheDefaultFieldsThenTheDatabaseCardWithBothButtons() {
            var inspector = CreateInspector(CreateTable(null));
            var root = inspector.CreateInspectorGUI();

            CollectionAssert.AreEqual(new[] { "m_Script", "materialDatabase", "entries", "fallbackData" },
                BindingPaths(root));
            var card = root.Q<SectionCard>(className: MaterialTableInspector.DatabaseCardClassName);
            Assert.IsNotNull(card);
            Assert.AreSame(card, root[root.childCount - 1]);
            Assert.AreEqual(MaterialTableInspector.DatabaseCardTitle, card.Title);
            var sync = card.Header.Q<Button>(className: MaterialTableInspector.SyncButtonClassName);
            var validate = card.Header.Q<Button>(className: MaterialTableInspector.ValidateButtonClassName);
            Assert.IsNotNull(sync);
            Assert.IsNotNull(validate);
            Assert.AreEqual(MaterialTableInspector.SyncText, sync.text);
            Assert.AreEqual(MaterialTableInspector.ValidateText, validate.text);
            Assert.AreEqual(MaterialTableInspector.NotValidatedText, Status(root).Title);
            Assert.AreEqual(HeaderStates.Neutral, Status(root).State);
            Assert.IsTrue(root.ClassListContains(EditorLook.ThemeClassName));
        }

        [Test]
        public void ValidateWithoutADatabaseShowsTheOneProblemAndLogsIt() {
            var inspector = CreateInspector(CreateTable(null));
            var root = inspector.CreateInspectorGUI();
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(TableName + ":\n  " + NoDatabaseProblem)));

            var problems = inspector.ValidateTable();

            CollectionAssert.AreEqual(new[] { NoDatabaseProblem }, problems);
            var status = Status(root);
            Assert.AreEqual(HeaderStates.Error, status.State);
            Assert.IsTrue(status.ClassListContains(HeaderStates.ClassNameFor(HeaderStates.Error)));
            CollectionAssert.AreEqual(new[] { NoDatabaseProblem }, ProblemTexts(root));
        }

        [Test]
        public void ValidateWithEntriesThatMatchTheDatabaseShowsTheTableInSync() {
            var database = CreateDatabase("Stone", "Sand");
            var inspector = CreateInspector(CreateTable(database, "Stone", "Sand"));
            var root = inspector.CreateInspectorGUI();

            var problems = inspector.ValidateTable();

            Assert.IsEmpty(problems);
            var status = Status(root);
            Assert.AreEqual(HeaderStates.Passed, status.State);
            Assert.IsTrue(status.ClassListContains(HeaderStates.ClassNameFor(HeaderStates.Passed)));
            Assert.AreEqual(MaterialTableInspector.InSyncText, status.Title);
            Assert.IsEmpty(ProblemTexts(root));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SyncAddsTheMissingEntries() {
            var database = CreateDatabase("Stone", "Sand", "Clay");
            var table = CreateTable(database, "Stone");
            var inspector = CreateInspector(table);
            var root = inspector.CreateInspectorGUI();
            var dirtyCount = EditorUtility.GetDirtyCount(table);

            inspector.SyncTable();

            CollectionAssert.AreEqual(new[] { "Stone", "Sand", "Clay" }, EntryNames(inspector.serializedObject));
            Assert.IsEmpty(table.Validate());
            Assert.Greater(EditorUtility.GetDirtyCount(table), dirtyCount);
            Assert.AreEqual(HeaderStates.Passed, Status(root).State);
            Assert.IsEmpty(ProblemTexts(root));
        }

        private FixtureMaterialTable CreateTable(VoxelMaterialDatabase database, params string[] entryNames) {
            var table = Create<FixtureMaterialTable>(TableName);

            using (var serialized = new SerializedObject(table)) {
                serialized.FindProperty("materialDatabase").objectReferenceValue = database;
                SetMaterialNames(serialized.FindProperty("entries"), entryNames);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return table;
        }

        private VoxelMaterialDatabase CreateDatabase(params string[] materialNames) {
            var database = Create<VoxelMaterialDatabase>(DatabaseName);

            using (var serialized = new SerializedObject(database)) {
                SetMaterialNames(serialized.FindProperty("materials"), materialNames);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return database;
        }

        private T Create<T>(string objectName) where T : ScriptableObject {
            var created = ScriptableObject.CreateInstance<T>();
            created.name = objectName;
            _created.Add(created);
            return created;
        }

        private MaterialTableInspector CreateInspector(MaterialSideTableBase table) {
            var inspector = UnityEditor.Editor.CreateEditor(table);
            _created.Add(inspector);
            Assert.IsInstanceOf<MaterialTableInspector>(inspector);
            return (MaterialTableInspector)inspector;
        }

        private static void SetMaterialNames(SerializedProperty list, IReadOnlyList<string> materialNames) {
            list.arraySize = materialNames.Count;

            for (var index = 0; index < materialNames.Count; index++) {
                var element = list.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("materialName").stringValue = materialNames[index];
            }
        }

        private static List<string> EntryNames(SerializedObject serialized) {
            var entries = serialized.FindProperty("entries");
            var names = new List<string>();

            for (var index = 0; index < entries.arraySize; index++) {
                names.Add(entries.GetArrayElementAtIndex(index).FindPropertyRelative("materialName").stringValue);
            }

            return names;
        }

        private static List<string> BindingPaths(VisualElement root) {
            var paths = new List<string>();

            foreach (var field in root.Query<PropertyField>().ToList()) {
                paths.Add(field.bindingPath);
            }

            return paths;
        }

        private static HeaderBar Status(VisualElement root) {
            var status = root.Q<HeaderBar>(className: MaterialTableInspector.StatusClassName);
            Assert.IsNotNull(status);
            return status;
        }

        private static List<string> ProblemTexts(VisualElement root) {
            var texts = new List<string>();

            foreach (var row in root.Query<Label>(className: MaterialTableInspector.ProblemClassName).ToList()) {
                texts.Add(row.text);
            }

            return texts;
        }
    }
}
