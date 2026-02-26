using UnityEngine;
using SkiResortTycoon.Core;
using System.Collections.Generic;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Renders the trail-in-progress as a 3D mesh strip projected onto the terrain.
    /// Two layers: a semi-transparent fill and a solid outline border.
    /// The "preview segment" (last anchor → cursor) can be toggled independently.
    /// </summary>
    public class TrailPreviewRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MountainManager _mountainManager;

        [Header("Materials")]
        [SerializeField] private Material _fillMaterialTemplate;
        [SerializeField] private Material _outlineMaterialTemplate;

        [Header("Visual Settings")]
        [SerializeField] private Color _fillColor = new Color(0.29f, 0.56f, 0.85f, 0.15f);
        [SerializeField] private Color _outlineColor = new Color(0.29f, 0.56f, 0.85f, 0.9f);
        [SerializeField] private float _outlineWidth = 0.25f;
        [SerializeField] private float _heightOffset = 0.15f;

        // Committed segments mesh
        private Mesh _fillMesh;
        private Mesh _outlineMesh;
        private Material _fillMat;
        private Material _outlineMat;

        // Preview segment mesh (last anchor → cursor)
        private Mesh _previewFillMesh;
        private Mesh _previewOutlineMesh;

        private bool _showPreview;

        void Awake()
        {
            _fillMesh = new Mesh { name = "TrailPreviewFill" };
            _outlineMesh = new Mesh { name = "TrailPreviewOutline" };
            _previewFillMesh = new Mesh { name = "TrailPreviewSegFill" };
            _previewOutlineMesh = new Mesh { name = "TrailPreviewSegOutline" };

            _fillMat = CreateMaterial(_fillColor);
            _outlineMat = CreateMaterial(_outlineColor);
        }

        void OnDestroy()
        {
            if (_fillMesh != null) Destroy(_fillMesh);
            if (_outlineMesh != null) Destroy(_outlineMesh);
            if (_previewFillMesh != null) Destroy(_previewFillMesh);
            if (_previewOutlineMesh != null) Destroy(_previewOutlineMesh);
            if (_fillMat != null) Destroy(_fillMat);
            if (_outlineMat != null) Destroy(_outlineMat);
        }

        void Update()
        {
            if (_fillMat == null) return;

            Graphics.DrawMesh(_fillMesh, Matrix4x4.identity, _fillMat, 0);
            Graphics.DrawMesh(_outlineMesh, Matrix4x4.identity, _outlineMat, 0);

            if (_showPreview)
            {
                Graphics.DrawMesh(_previewFillMesh, Matrix4x4.identity, _fillMat, 0);
                Graphics.DrawMesh(_previewOutlineMesh, Matrix4x4.identity, _outlineMat, 0);
            }
        }

        // ── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Rebuild the committed-trail mesh from a list of centerline points.
        /// </summary>
        public void SetCommittedPath(List<Vector3> centerPoints, float width)
        {
            if (centerPoints == null || centerPoints.Count < 2)
            {
                _fillMesh.Clear();
                _outlineMesh.Clear();
                return;
            }

            BuildStripMeshes(centerPoints, width, _fillMesh, _outlineMesh);
        }

        /// <summary>
        /// Rebuild the preview-segment mesh (last anchor → cursor).
        /// </summary>
        public void SetPreviewSegment(List<Vector3> centerPoints, float width)
        {
            if (centerPoints == null || centerPoints.Count < 2)
            {
                _previewFillMesh.Clear();
                _previewOutlineMesh.Clear();
                _showPreview = false;
                return;
            }

            BuildStripMeshes(centerPoints, width, _previewFillMesh, _previewOutlineMesh);
            _showPreview = true;
        }

        public void HidePreview()
        {
            _showPreview = false;
            _previewFillMesh.Clear();
            _previewOutlineMesh.Clear();
        }

        public void HideAll()
        {
            _fillMesh.Clear();
            _outlineMesh.Clear();
            HidePreview();
        }

        // ── Mesh generation ──────────────────────────────────────────────

        private void BuildStripMeshes(List<Vector3> center, float width, Mesh fillMesh, Mesh outlineMesh)
        {
            float halfW = width * 0.5f;
            int n = center.Count;

            // Generate left/right edges
            var leftEdge = new List<Vector3>(n);
            var rightEdge = new List<Vector3>(n);

            for (int i = 0; i < n; i++)
            {
                Vector3 fwd;
                if (i == 0)
                    fwd = (center[1] - center[0]).normalized;
                else if (i == n - 1)
                    fwd = (center[n - 1] - center[n - 2]).normalized;
                else
                    fwd = ((center[i + 1] - center[i]).normalized + (center[i] - center[i - 1]).normalized).normalized;

                Vector3 perp = Vector3.Cross(fwd, Vector3.up).normalized;
                Vector3 c = center[i] + Vector3.up * _heightOffset;
                leftEdge.Add(c + perp * halfW);
                rightEdge.Add(c - perp * halfW);
            }

            BuildFillMesh(leftEdge, rightEdge, fillMesh);
            BuildOutlineMesh(leftEdge, rightEdge, outlineMesh);
        }

        private static void BuildFillMesh(List<Vector3> left, List<Vector3> right, Mesh mesh)
        {
            mesh.Clear();
            int n = left.Count;
            if (n < 2) return;

            var verts = new Vector3[n * 2];
            var tris = new int[(n - 1) * 6];
            var colors = new Color[n * 2];

            for (int i = 0; i < n; i++)
            {
                verts[i * 2] = left[i];
                verts[i * 2 + 1] = right[i];
                colors[i * 2] = Color.white;
                colors[i * 2 + 1] = Color.white;
            }

            for (int i = 0; i < n - 1; i++)
            {
                int bi = i * 6;
                int vi = i * 2;
                tris[bi] = vi;
                tris[bi + 1] = vi + 2;
                tris[bi + 2] = vi + 1;
                tris[bi + 3] = vi + 1;
                tris[bi + 4] = vi + 2;
                tris[bi + 5] = vi + 3;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = colors;
            mesh.RecalculateNormals();
        }

        private void BuildOutlineMesh(List<Vector3> left, List<Vector3> right, Mesh mesh)
        {
            mesh.Clear();
            int n = left.Count;
            if (n < 2) return;

            int totalVerts = n * 4; // 2 edges × 2 verts per point (inner + outer)
            int totalTris = (n - 1) * 2 * 6; // 2 edges × 2 tris per segment × 3 indices
            var verts = new Vector3[totalVerts];
            var tris = new int[totalTris];

            float hw = _outlineWidth * 0.5f;

            // Left edge outline
            for (int i = 0; i < n; i++)
            {
                Vector3 dir;
                if (i == 0) dir = (left[1] - left[0]).normalized;
                else if (i == n - 1) dir = (left[n - 1] - left[n - 2]).normalized;
                else dir = ((left[i + 1] - left[i]).normalized + (left[i] - left[i - 1]).normalized).normalized;

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
                verts[i * 2] = left[i] + perp * hw;
                verts[i * 2 + 1] = left[i] - perp * hw;
            }

            // Right edge outline
            int rOff = n * 2;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir;
                if (i == 0) dir = (right[1] - right[0]).normalized;
                else if (i == n - 1) dir = (right[n - 1] - right[n - 2]).normalized;
                else dir = ((right[i + 1] - right[i]).normalized + (right[i] - right[i - 1]).normalized).normalized;

                Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
                verts[rOff + i * 2] = right[i] + perp * hw;
                verts[rOff + i * 2 + 1] = right[i] - perp * hw;
            }

            int ti = 0;
            // Left edge quads
            for (int i = 0; i < n - 1; i++)
            {
                int v = i * 2;
                tris[ti++] = v; tris[ti++] = v + 2; tris[ti++] = v + 1;
                tris[ti++] = v + 1; tris[ti++] = v + 2; tris[ti++] = v + 3;
            }
            // Right edge quads
            for (int i = 0; i < n - 1; i++)
            {
                int v = rOff + i * 2;
                tris[ti++] = v; tris[ti++] = v + 2; tris[ti++] = v + 1;
                tris[ti++] = v + 1; tris[ti++] = v + 2; tris[ti++] = v + 3;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }

        // ── Material helpers ─────────────────────────────────────────────

        private Material CreateMaterial(Color color)
        {
            // Use the template if assigned; otherwise fall back to a basic unlit transparent shader
            Material mat;
            if (_fillMaterialTemplate != null)
            {
                mat = new Material(_fillMaterialTemplate);
            }
            else
            {
                mat = new Material(Shader.Find("Sprites/Default"));
            }

            mat.color = color;
            mat.renderQueue = 3100;
            return mat;
        }
    }
}
