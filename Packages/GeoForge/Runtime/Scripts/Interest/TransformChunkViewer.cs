// Copyright (c) Spellbound Studios. All rights reserved.

using Spellbound.Core.Logging;
using UnityEngine;

namespace Spellbound.GeoForge {
    public sealed class TransformChunkViewer : MonoBehaviour {
        [Tooltip("The transform the volume on this object streams its chunks around, such as the player camera.")]
        [SerializeField]
        private Transform viewpoint;

        [Tooltip("World radius around the viewpoint inside which chunks collide. Zero takes one chunk's world size.")]
        [SerializeField]
        private float collisionRadius;

        [Tooltip("World radius around the viewpoint inside which chunks are drawn. Zero draws at every distance.")]
        [SerializeField]
        private float detailRadius;

        private readonly ChunkViewer[] _viewers = new ChunkViewer[1];

        private Vector3 _lastPosition;
        private bool _radiiResolved;
        private bool _reportedMissingViewpoint;
        private bool _sent;
        private IGeoVolume _volume;

        public Transform Viewpoint {
            get => viewpoint;
            set {
                viewpoint = value;
                _sent = false;
            }
        }

        private void Start() {
            TryGetComponent(out _volume);
            SendWhenMoved();
        }

        private void Update() {
            SendWhenMoved();
        }

        private void ResolveDefaultRadii(GeoVolumeEngine engine) {
            if (collisionRadius <= 0f)
                collisionRadius = engine.ConfigBlob.Value.UnitsPerChunk;

            _radiiResolved = true;
        }

        private void SendWhenMoved() {
            if (viewpoint == null) {
                ReportMissingViewpoint();

                return;
            }

            var engine = _volume?.GeoVolumeEngine;

            if (engine == null)
                return;

            if (!_radiiResolved)
                ResolveDefaultRadii(engine);

            var position = viewpoint.position;

            if (_sent && position == _lastPosition)
                return;

            _viewers[0] = new ChunkViewer(position, collisionRadius, detailRadius);
            _volume.SetViewers(_viewers);
            _lastPosition = position;
            _sent = true;
        }

        private void ReportMissingViewpoint() {
            if (_reportedMissingViewpoint)
                return;

            _reportedMissingViewpoint = true;
            Log.Emit(LogLevel.Warning,
                $"{name}: {nameof(TransformChunkViewer)} has no viewpoint, so it gives the volume no viewer.", this);
        }
    }
}
