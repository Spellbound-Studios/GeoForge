// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Spellbound.Core;
using Spellbound.Core.Authoring;
using Spellbound.Core.Authoring.Editor;
using Spellbound.Core.Logging;
using Spellbound.Core.Observations;
using Spellbound.Core.Operations;
using Spellbound.GeoForge.EditMode;
using Spellbound.GeoForge.Places;
using Spellbound.Places;
using Spellbound.Places.Editor;
using Spellbound.Places.Owners;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.PlacesMarker {
    public sealed class CaveSession {
        public const string SessionFolder = "Assets/_Project/PlacesMarkerLayer/Session";
        public const string ReadingsFileName = "readings.txt";

        private const string SessionFolderParent = "Assets/_Project/PlacesMarkerLayer";
        private const string SessionFolderName = "Session";
        private const string HostSceneName = "CaveSession.unity";
        private const string PlaceName = "CavePlace";
        private const string VoxelLayerName = "voxels";
        private const string MarkerLayerName = "marker";
        private const int ChunkSize = 32;
        private const float VoxelSize = 1f;
        private const short FillDelta = 255;
        private const int RockHalfExtent = 12;
        private const byte RockMaterial = 0;
        private const sbyte OpenAirDensity = sbyte.MinValue;
        private const byte FloorMaterial = 1;

        private static readonly Vector3 RockCentre = new(16f, 15f, 20f);
        private static readonly Vector3 MarkerPoint = new(22f, 8f, 5f);
        private static readonly Vector3 FixedCameraPosition = new(15f, 17f, -7f);
        private static readonly Vector3 FixedCameraTarget = new(15f, 11f, 9f);
        private static readonly Vector3 InsideCameraPosition = new(13f, 14.5f, 8.5f);
        private static readonly Vector3 InsideCameraTarget = new(13f, 9.5f, 19f);
        private static readonly Vector3 SunRotation = new(50f, -30f, 0f);

        private static readonly VoxelStroke[] CaveStrokes = {
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, new Vector3(13f, 13f, 9f), 8f, 0),
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, new Vector3(13f, 13f, 13f), 8f, 0),
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, new Vector3(13f, 13f, 17f), 8f, 0)
        };

        private static readonly VoxelStroke BoxStroke =
            new(VoxelStrokeShape.Box, VoxelStrokeMode.Remove, new Vector3(13f, 12f, 8f), 9f, 0);

        private static readonly VoxelStroke SmoothStroke =
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Smooth, new Vector3(13f, 13f, 12f), 12f, 0);

        private static readonly VoxelStroke PaintStroke =
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Paint, new Vector3(13f, 9f, 12f), 7f, FloorMaterial);

        private static readonly VoxelStroke SealStroke =
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Add, MarkerPoint, 8f, RockMaterial);

        private static readonly VoxelStroke LayerEditStroke =
            new(VoxelStrokeShape.Sphere, VoxelStrokeMode.Remove, new Vector3(22f, 20f, 8f), 8f, 0);

        private readonly string _evidenceFolder;
        private readonly List<string> _readings = new();
        private readonly List<string> _appliedStrokes = new();
        private CaveCamera _camera;
        private GameObject _sun;
        private PlaceDefinition _place;
        private string _placePath;
        private EditTerrainSession _session;
        private byte[] _previousCapture;
        private string _previousCaptureName;
        private int _identicalCount;

        private CaveSession(string evidenceFolder) {
            _evidenceFolder = evidenceFolder;
        }

        public static string Run(string evidenceFolder) {
            if (string.IsNullOrWhiteSpace(evidenceFolder)) {
                throw new ArgumentException("The cave session needs an evidence folder", nameof(evidenceFolder));
            }

            Directory.CreateDirectory(evidenceFolder);
            var session = new CaveSession(evidenceFolder);

            return session.RunSteps();
        }

        private string RunSteps() {
            string answer;

            try {
                OpenHostScene();
                CreatePlace();
                OpenEditTerrain();
                CarveCave();
                WidenAndSmooth();
                PaintFloor();
                UndoAndRedo();
                SealAndClose();
                ReopenAndCancel();
                EditTheRevision();
                answer = "succeeded: " + _readings.Count.ToString(CultureInfo.InvariantCulture) + " captures, "
                    + _identicalCount.ToString(CultureInfo.InvariantCulture) + " identical, written to "
                    + _evidenceFolder;
            } catch (Exception exception) {
                answer = "failed: " + exception;
                Log.Emit(LogLevel.Error, "Cave session: " + exception);
            } finally {
                CleanUp();
            }

            _readings.Add(answer);
            File.WriteAllLines(Path.Combine(_evidenceFolder, ReadingsFileName), _readings);

            return answer;
        }

        private void OpenHostScene() {
            if (!AssetDatabase.IsValidFolder(SessionFolder)) {
                AssetDatabase.CreateFolder(SessionFolderParent, SessionFolderName);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _sun = new GameObject("Sun");
            var light = _sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            _sun.transform.rotation = Quaternion.Euler(SunRotation);
            _camera = new CaveCamera(scene, FixedCameraPosition, FixedCameraTarget);
            EditorSceneManager.SaveScene(scene, SessionFolder + "/" + HostSceneName);
        }

        private void CreatePlace() {
            var region = new VoxelRegion(Vector3Int.zero, Vector3Int.one);
            var report = new ValidationReport(PlaceName);
            VoxelLayer captured;

            using (var scratch = ScratchVolume.Create(ChunkSize, VoxelSize, Vector3Int.one, false)) {
                PlaceFirstVoxelAtTheOrigin(scratch.Volume);
                new VoxelLayerOwner(scratch.Volume, region).Stamp(OpenAirLayer(), new PlaceStampContext(report));

                if (report.HasErrors) {
                    throw new InvalidOperationException("The open air could not be stamped: " + report.ToText());
                }

                scratch.Complete();

                if (!GeoForgeCommands.AddCube(scratch.Volume, RockCentre, RockHalfExtent, FillDelta, RockMaterial)) {
                    throw new InvalidOperationException("The rock could not be filled into the scratch volume.");
                }

                scratch.Complete();
                captured = GeoForgePlacesKit.Capture(scratch.Volume, region, VoxelLayerName, report);
            }

            if (captured == null || report.HasErrors) {
                throw new InvalidOperationException("The rock could not be captured: " + report.ToText());
            }

            _placePath = SessionFolder + "/" + PlaceName + ".asset";
            _place = ScriptableObject.CreateInstance<PlaceDefinition>();
            AssetDatabase.CreateAsset(_place, _placePath);
            VoxelRunStores.Write(_place, null, captured);
            _place.AddLayer(new MarkerLayer { LayerName = MarkerLayerName, point = MarkerPoint });
            EditorUtility.SetDirty(_place);
            AssetDatabase.SaveAssets();
        }

        private void OpenEditTerrain() {
            _session = EditTerrainSession.Open(_place, 0);
            Capture("01-open.png");
        }

        private void CarveCave() {
            foreach (var stroke in CaveStrokes) {
                Apply(stroke);
            }

            Capture("02a-cave-fixed.png");
            _camera.Aim(InsideCameraPosition, InsideCameraTarget);
            Capture("02b-cave-inside.png");
            _camera.Aim(FixedCameraPosition, FixedCameraTarget);
        }

        private void WidenAndSmooth() {
            Apply(BoxStroke);
            Capture("03a-box.png");
            Apply(SmoothStroke);
            Capture("03b-smooth.png");
        }

        private void PaintFloor() {
            Apply(PaintStroke);
            Capture("04-paint.png");
        }

        private void UndoAndRedo() {
            if (!_session.Brush.Undo()) {
                throw new InvalidOperationException("The brush refused to undo the paint stroke.");
            }

            _appliedStrokes.Add("undo");
            Capture("05a-undo.png");

            if (!_session.Brush.Redo()) {
                throw new InvalidOperationException("The brush refused to redo the paint stroke.");
            }

            _appliedStrokes.Add("redo");
            Capture("05b-redo.png");
        }

        private void SealAndClose() {
            Apply(SealStroke);
            var sealedReport = new ValidationReport(_place.name);
            var closed = _session.TryClose(sealedReport);
            var findingLines = new List<string> {
                "close with the marker sealed: " + (closed ? "closed" : "held"),
                "session open: " + _session.IsOpen
            };

            foreach (var finding in sealedReport.Findings) {
                findingLines.Add(finding.Code + " at " + finding.Location + ": " + finding.Message);
            }

            foreach (var location in _session.SealedPlacements) {
                findingLines.Add("sealed placement: " + location);
            }

            if (closed) {
                File.WriteAllLines(Path.Combine(_evidenceFolder, "06-finding.txt"), findingLines);
                throw new InvalidOperationException("The session closed with the marker sealed in rock.");
            }

            Capture("06-sealed.png");

            if (!_session.Brush.Undo()) {
                throw new InvalidOperationException("The brush refused to undo the sealing stroke.");
            }

            _appliedStrokes.Add("undo");
            var openReport = new ValidationReport(_place.name);
            var closedAfterUndo = _session.TryClose(openReport);
            findingLines.Add("close after the undo: " + (closedAfterUndo ? "closed" : "held"));

            foreach (var finding in openReport.Findings) {
                findingLines.Add(finding.Code + " at " + finding.Location + ": " + finding.Message);
            }

            File.WriteAllLines(Path.Combine(_evidenceFolder, "06-finding.txt"), findingLines);

            if (!closedAfterUndo) {
                throw new InvalidOperationException("The session did not close after the undo: "
                    + openReport.ToText());
            }

            _session = null;
        }

        private void ReopenAndCancel() {
            _session = OpenThroughTheInspector();
            _appliedStrokes.Clear();
            _appliedStrokes.Add("reopened");
            Capture("07-reopened.png");
            CancelAndCloseWindow();

            var storePath = VoxelRunStores.PathBeside(_placePath, VoxelLayerName);
            var placeBefore = HashOf(_placePath);
            var storeBefore = HashOf(storePath);
            _session = OpenThroughTheInspector();
            CancelAndCloseWindow();
            AssetDatabase.SaveAssets();
            var placeAfter = HashOf(_placePath);
            var storeAfter = HashOf(storePath);

            File.WriteAllLines(Path.Combine(_evidenceFolder, "07-hashes.txt"), new[] {
                "place before " + _placePath + " " + placeBefore,
                "place after " + _placePath + " " + placeAfter,
                "store before " + storePath + " " + storeBefore,
                "store after " + storePath + " " + storeAfter
            });
        }

        private void EditTheRevision() {
            var settings = CoreKit.Operations.Settings;
            var draftsFolder = DefinitionDrafts.Folder(settings);
            var draftsFolderExisted = AssetDatabase.IsValidFolder(draftsFolder);
            var revisionPath = DefinitionDrafts.PathFor(_place.name + DefinitionDrafts.RevisionSuffix, settings);
            var revisionStorePath = VoxelRunStores.PathBeside(revisionPath, VoxelLayerName);

            try {
                var revised = RunOperation("core revise --target asset:" + _placePath);

                if (!revised.Succeeded) {
                    throw new InvalidOperationException("core revise answered " + revised.Text);
                }

                var centre = LayerEditStroke.Centre;
                var answer = RunOperation("geoforge layer-edit --target asset:" + revisionPath
                    + " --mode remove --shape sphere --x " + Format(centre.x) + " --y " + Format(centre.y)
                    + " --z " + Format(centre.z) + " --size " + Format(LayerEditStroke.Size));
                File.WriteAllText(Path.Combine(_evidenceFolder, "08-answer.txt"),
                    answer.Text + Environment.NewLine);

                if (!answer.Succeeded) {
                    throw new InvalidOperationException("geoforge layer-edit answered " + answer.Text);
                }

                var revision = AssetDatabase.LoadAssetAtPath<PlaceDefinition>(revisionPath);
                var layer = (VoxelLayer)revision.FindLayer(VoxelLayerName);
                StampAndCapture(layer);
            } finally {
                AssetDatabase.DeleteAsset(revisionStorePath);
                AssetDatabase.DeleteAsset(revisionPath);

                if (!draftsFolderExisted && AssetDatabase.IsValidFolder(draftsFolder)
                    && AssetDatabase.FindAssets(string.Empty, new[] { draftsFolder }).Length == 0) {
                    AssetDatabase.DeleteAsset(draftsFolder);
                }
            }
        }

        private void StampAndCapture(VoxelLayer layer) {
            var report = new ValidationReport(layer.LayerName);
            var region = new VoxelRegion(Vector3Int.zero, layer.ChunkExtent);

            using var scratch = ScratchVolume.Create(layer.ChunkSize, VoxelSize, layer.ChunkExtent, true);
            PlaceFirstVoxelAtTheOrigin(scratch.Volume);
            new VoxelLayerOwner(scratch.Volume, region).Stamp(layer, new PlaceStampContext(report));

            if (report.HasErrors) {
                throw new InvalidOperationException("The revision could not be stamped: " + report.ToText());
            }

            scratch.Complete();
            _appliedStrokes.Clear();
            _appliedStrokes.Add("layer-edit " + Describe(LayerEditStroke));
            Capture("08-layer-edit.png");
        }

        private EditTerrainSession OpenThroughTheInspector() {
            var action = PlaceLayerInspectorActionRegistry.Default.ActionsFor(_place, 0)
                .FirstOrDefault(found => found.Label == EditTerrainInspectorActions.EditTerrainLabel);

            if (action == null) {
                throw new InvalidOperationException("The inspector offers no '"
                    + EditTerrainInspectorActions.EditTerrainLabel + "' action on the voxel layer.");
            }

            action.Invoke(_place, 0);

            foreach (var window in Resources.FindObjectsOfTypeAll<EditTerrainWindow>()) {
                if (window.Session != null && window.Session.IsOpen && window.Session.Place == _place) {
                    return window.Session;
                }
            }

            throw new InvalidOperationException("Edit terrain opened no window holding an open session.");
        }

        private void CancelAndCloseWindow() {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditTerrainWindow>()) {
                if (window.Session == _session) {
                    _session.Cancel();
                    window.Close();
                }
            }

            if (_session != null && _session.IsOpen) {
                _session.Cancel();
            }

            _session = null;
        }

        private void Apply(in VoxelStroke stroke) {
            if (!_session.Brush.Apply(stroke)) {
                throw new InvalidOperationException("The brush refused " + Describe(stroke) + ".");
            }

            _appliedStrokes.Add(Describe(stroke));
        }

        private void Capture(string fileName) {
            var png = _camera.Render();
            File.WriteAllBytes(Path.Combine(_evidenceFolder, fileName), png);
            var line = fileName + " camera " + Format(_camera.Position) + " looking at " + Format(_camera.Target)
                + " strokes: " + (_appliedStrokes.Count == 0 ? "none" : string.Join("; ", _appliedStrokes));

            if (_previousCapture != null && png.SequenceEqual(_previousCapture)) {
                line += " IDENTICAL to " + _previousCaptureName;
                _identicalCount++;
            }

            _readings.Add(line);
            _appliedStrokes.Clear();
            _previousCapture = png;
            _previousCaptureName = fileName;
        }

        private void CleanUp() {
            if (_session != null) {
                CancelAndCloseWindow();
            }

            if (_camera != null) {
                _camera.Dispose();
                _camera = null;
            }

            if (_sun != null) {
                Object.DestroyImmediate(_sun);
                _sun = null;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(SessionFolder);
            AssetDatabase.SaveAssets();
        }

        private static VoxelLayer OpenAirLayer() {
            var runs = new List<SparseVoxelData> { new(VoxelData.CreateMature(OpenAirDensity, RockMaterial), 0) };
            var layer = new VoxelLayer { LayerName = VoxelLayerName };
            layer.SetChunks(ChunkSize, Vector3Int.one, new[] { new VoxelLayerChunk(Vector3Int.zero, runs) });

            return layer;
        }

        private static void PlaceFirstVoxelAtTheOrigin(IGeoVolume volume) {
            var engine = volume.GeoVolumeEngine;
            ref var config = ref engine.ConfigBlob.Value;
            engine.Transform.position = -(Vector3)config.Offset * config.Resolution;
        }

        private static OperationResult RunOperation(string line) {
            var run = CoreKit.Operations.Run(line, OperationCaller.Development(), ObservationContext.Discard);

            if (!run.IsDone) {
                throw new InvalidOperationException("'" + line + "' did not finish.");
            }

            return run.Result;
        }

        private static string HashOf(string assetPath) {
            using var algorithm = SHA256.Create();
            var hash = algorithm.ComputeHash(File.ReadAllBytes(Path.GetFullPath(assetPath)));

            return string.Concat(hash.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string Describe(in VoxelStroke stroke) {
            var text = stroke.Mode.ToString().ToLowerInvariant() + " " + stroke.Shape.ToString().ToLowerInvariant()
                + " of " + Format(stroke.Size) + " m at " + Format(stroke.Centre);

            if (stroke.Mode == VoxelStrokeMode.Add || stroke.Mode == VoxelStrokeMode.Paint) {
                text += " with material " + stroke.Material.ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }

        private static string Format(Vector3 value) {
            return "(" + Format(value.x) + ", " + Format(value.y) + ", " + Format(value.z) + ")";
        }

        private static string Format(float value) {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
