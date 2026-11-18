// Copyright (c) Spellbound Studios. All rights reserved.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Spellbound.GeoForge.PlacesMarker {
    public sealed class CaveCamera : IDisposable {
        public const int Width = 1280;
        public const int Height = 720;
        public const float FieldOfView = 60f;

        private static readonly Color Background = new(0.55f, 0.66f, 0.78f);

        private readonly GameObject _cameraObject;
        private readonly Camera _camera;
        private readonly RenderTexture _target;

        public CaveCamera(Scene scene, Vector3 position, Vector3 target) {
            _cameraObject = new GameObject("Cave Camera");
            SceneManager.MoveGameObjectToScene(_cameraObject, scene);
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.fieldOfView = FieldOfView;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 500f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.targetTexture = _target;
            Aim(position, target);
        }

        public Vector3 Position => _cameraObject.transform.position;

        public Vector3 Target { get; private set; }

        public void Aim(Vector3 position, Vector3 target) {
            _cameraObject.transform.position = position;
            _cameraObject.transform.LookAt(target);
            Target = target;
        }

        public byte[] Render() {
            _camera.Render();
            var previous = RenderTexture.active;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            try {
                RenderTexture.active = _target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();

                return ImageConversion.EncodeToPNG(texture);
            } finally {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
            }
        }

        public void Dispose() {
            if (_camera != null) {
                _camera.targetTexture = null;
            }

            if (_cameraObject != null) {
                Object.DestroyImmediate(_cameraObject);
            }

            if (_target != null) {
                _target.Release();
                Object.DestroyImmediate(_target);
            }
        }
    }
}
