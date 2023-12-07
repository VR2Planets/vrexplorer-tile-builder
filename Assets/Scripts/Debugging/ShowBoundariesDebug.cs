using System;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using Gizmos = Popcron.Gizmos;

namespace Debugging
{
    /// <summary>
    /// Display a "Gizmo" around tiles and a debug text wth provided information.
    /// </summary>
    public class ShowBoundariesDebug : MonoBehaviour
    {
        public float FontScale = 0.02f;

        private Camera _camera;

        private Renderer _renderer;
        private TextMeshPro _text;
        private Bounds _worldSpaceBounds;
        private Color _color;
        private bool _hasCamera = false;
        private bool _hasText = false;

        private static Material _overlayMaterial;
        private float _textSize;
        private Bounds _bounds;

        public static ShowBoundariesDebug CreateGameObject(Transform parentGameObject, string name)
        {
            var go = new GameObject(name);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.transform.SetParent(parentGameObject, false);
            return go.AddComponent<ShowBoundariesDebug>();
        }

        [ContextMenu("Recalculate")]
        public void Recalculate(Transform parent, MeshFilter mesh, Renderer targetedRenderer, int lod, string fullRezCount,
            string textureCompressionRatio, string geometricError,
            Camera previewCamera, bool isMostDetailed, bool displayDebugDebug)
        {
            if (_overlayMaterial == null)
            {
                _overlayMaterial = Resources.Load<Material>("TMPOverlay");
            }
            _camera = previewCamera;
            _renderer = targetedRenderer;

            var sharedMeshBounds = mesh.sharedMesh.bounds;
            var meshTransform = mesh.transform;
            Vector3 min = meshTransform.TransformPoint(sharedMeshBounds.min);
            Vector3 max = meshTransform.TransformPoint(sharedMeshBounds.max);
            _worldSpaceBounds = new Bounds();
            _worldSpaceBounds.SetMinMax(min, max);

            _color = lod switch
            {
                0 => Color.white,
                1 => Color.red,
                2 => Color.blue,
                3 => Color.green,
                4 => Color.yellow,
                5 => Color.magenta,
                _ => Color.gray
            };
            if (isMostDetailed)
            {
                _color = _color / 2 + new Color(0.3f, 0.3f, 0.3f);
            }
            if (_text == null && displayDebugDebug)
            {
                var textGO = new GameObject("DebugText");
                // Add the debug text under LOD0Parent to auto destroy them when the cutting is done several times
                //if (MainUI.Instance != null) textGO.transform.SetParent(MainUI.Instance.ContentArea);
                textGO.transform.position = _worldSpaceBounds.center;
                textGO.transform.localScale = new Vector3(-1, 1, 1);
                _text = textGO.AddComponent<TextMeshPro>();
                _text.fontSize = 18 * FontScale * _textSize;
                _text.color = _color;
                _text.alignment = TextAlignmentOptions.Center;
                _text.richText = false;
                _text.material = _overlayMaterial;
            }
            if (_text != null)
            {
                if (isMostDetailed)
                {
                    string txt = $"{fullRezCount}";
                    _text.text = txt;
                }
                else
                {
                    string txt = $"triangles: {(mesh.sharedMesh.triangles.Length / 3)}/{fullRezCount}\n"
                        + $"texture: x{textureCompressionRatio}\n"
                        + $"err: {geometricError}";
                    _text.text = txt;
                }
            }

            _hasCamera = _camera != null;
            _hasText = _text != null;
            _bounds = mesh.sharedMesh.bounds;
        }

        private void Update()
        {
            bool isEnabled = _renderer == null || _renderer.enabled;
            if (_text != null) _text.SmartActive(isEnabled);
            if (!isEnabled) return;

            /*
            if (MainUI.Instance != null && _text != null)
            {
                var textSize = MainUI.Instance.PreviewArea.TextSizeSlider.value;
                _text.SmartActive(textSize >= 0.1001f);
                if (Math.Abs(textSize - _textSize) > 0.01f)
                {
                    _textSize = textSize;
                    _text.fontSize = 18 * FontScale * _textSize;
                }
            }
            */
            
            var meshTransform = transform;
            Gizmos.Cube(
                _worldSpaceBounds.center,
                meshTransform.rotation,
                _bounds.size * meshTransform.lossyScale.x,
                _color,
                false
            );

            if (_hasText && _hasCamera)
            {
                // var camDist = Vector3.Distance(_camera!.transform.position, _text.transform.position);
                // _text.transform.localScale = new Vector3(-camDist, camDist, camDist);
                if (_text != null) _text.transform.LookAt(_camera!.transform);
            }
        }
    }
}