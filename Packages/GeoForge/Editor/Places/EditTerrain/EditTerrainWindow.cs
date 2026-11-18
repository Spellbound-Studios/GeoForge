// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using Spellbound.Core.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Spellbound.GeoForge.Places {
    public sealed class EditTerrainWindow : EditorWindow {
        public const string CloseButtonName = "edit-terrain-close";
        public const string CancelButtonName = "edit-terrain-cancel";
        public const string SealedListName = "edit-terrain-sealed";

        private const string WindowTitle = "Edit terrain";
        private static readonly Vector2 MinimumWindowSize = new(260f, 160f);

        private EditTerrainSession _session;
        private Label _status;
        private VisualElement _sealedList;

        public EditTerrainSession Session => _session;

        public static EditTerrainWindow Open(EditTerrainSession session) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }

            var window = CreateInstance<EditTerrainWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = MinimumWindowSize;
            window._session = session;
            window.ShowUtility();
            window.Refresh();

            return window;
        }

        public void CreateGUI() {
            _status = new Label();
            rootVisualElement.Add(_status);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(new Button(CloseSession) { name = CloseButtonName, text = "Close" });
            buttons.Add(new Button(CancelSession) { name = CancelButtonName, text = "Cancel" });
            rootVisualElement.Add(buttons);

            _sealedList = new ScrollView { name = SealedListName };
            rootVisualElement.Add(_sealedList);
            Refresh();
        }

        private void OnDestroy() {
            if (_session != null && _session.IsOpen) {
                _session.Cancel();
            }
        }

        private void CloseSession() {
            if (_session == null || !_session.IsOpen) {
                Close();
                return;
            }

            var report = new ValidationReport(_session.Place.name);

            if (_session.TryClose(report)) {
                Close();
                return;
            }

            Refresh(report);
        }

        private void CancelSession() {
            _session?.Cancel();
            Close();
        }

        private void Refresh(ValidationReport report = null) {
            if (_status == null || _sealedList == null || _session == null) {
                return;
            }

            _sealedList.Clear();

            if (_session.SealedPlacements.Count > 0) {
                _status.text = "Placements sealed in rock: move them or carve to them.";

                foreach (var location in _session.SealedPlacements) {
                    _sealedList.Add(new Label(location));
                }

                return;
            }

            if (report != null && report.HasErrors) {
                _status.text = "The terrain could not be captured.";

                foreach (var finding in report.Findings) {
                    _sealedList.Add(new Label(finding.Code + ": " + finding.Message));
                }

                return;
            }

            _status.text = "Editing '" + _session.Place.name + "'. Close captures the terrain, Cancel discards it.";
        }
    }
}
