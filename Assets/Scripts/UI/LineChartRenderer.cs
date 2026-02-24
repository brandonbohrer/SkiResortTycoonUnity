using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Renders a 14-day two-line chart (Revenue vs Expenses) using pure Unity UI.
    /// Lines are built by cloning SegmentTemplate (rotated Image rectangles) and
    /// optionally PointTemplate (circle dots) parented under each Line_ GameObject.
    ///
    /// Hierarchy expected:
    ///   PlotArea
    ///     Lines
    ///       Line_Green   ← revenue
    ///         SegmentTemplate  (inactive)
    ///         PointTemplate    (inactive, optional)
    ///       Line_Red     ← expenses
    ///         SegmentTemplate  (inactive)
    ///         PointTemplate    (inactive, optional)
    /// </summary>
    public class LineChartRenderer : MonoBehaviour
    {
        [Header("Hierarchy References")]
        [Tooltip("The PlotArea RectTransform whose width/height drive all coordinate math.")]
        [SerializeField] private RectTransform _plotArea;

        [Tooltip("Parent under Line_Green. Must contain a child named 'SegmentTemplate' (inactive).")]
        [SerializeField] private RectTransform _greenLineParent;

        [Tooltip("Parent under Line_Red. Must contain a child named 'SegmentTemplate' (inactive).")]
        [SerializeField] private RectTransform _redLineParent;

        [Header("Appearance")]
        [SerializeField] private float _segmentThickness = 3f;
        [SerializeField] private bool  _showPoints = true;
        [SerializeField] private float _pointRadius = 5f;

        [Header("Colors")]
        [SerializeField] private Color _greenColor = new Color(0.2f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color _redColor   = new Color(0.95f, 0.3f, 0.3f, 1f);

        // ── Template references (found at runtime) ──────────────────────
        private RectTransform _greenSegTemplate;
        private RectTransform _greenPointTemplate;
        private RectTransform _redSegTemplate;
        private RectTransform _redPointTemplate;

        // ── Pooled instances ────────────────────────────────────────────
        private readonly List<GameObject> _greenInstances = new List<GameObject>();
        private readonly List<GameObject> _redInstances   = new List<GameObject>();

        // ── Constants ───────────────────────────────────────────────────
        private const int TotalDays = 14;

        // ────────────────────────────────────────────────────────────────

        void Awake()
        {
            _greenSegTemplate   = FindTemplate(_greenLineParent, "SegmentTemplate");
            _greenPointTemplate = FindTemplate(_greenLineParent, "PointTemplate");
            _redSegTemplate     = FindTemplate(_redLineParent,   "SegmentTemplate");
            _redPointTemplate   = FindTemplate(_redLineParent,   "PointTemplate");
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>
        /// Renders the chart.
        ///
        /// <param name="revenue">Per-day revenue values. Index 0 = Day 1.</param>
        /// <param name="expenses">Per-day expense values. Index 0 = Day 1.</param>
        /// <param name="totalDays">Fixed timeline width (default 14).</param>
        ///
        /// X-spacing math:
        ///   stepX = plotWidth / (totalDays - 1)
        ///   x[i]  = i * stepX          (anchored at left edge of PlotArea)
        ///
        ///   When data.Count = 1 (only Day 1):
        ///     point sits at x[0] = 0, which is the leftmost slot in the 14-day
        ///     timeline — correctly positioned regardless of PlotArea width.
        ///
        /// Y-normalisation:
        ///   globalMax = max value across BOTH arrays
        ///   y[i]      = (value[i] / globalMax) * plotHeight
        ///   If globalMax == 0 all points sit on the baseline (y = 0).
        /// </summary>
        public void RenderChart(List<float> revenue, List<float> expenses, int totalDays = TotalDays)
        {
            ClearInstances();

            // Require PlotArea to be laid out (use rect after layout pass)
            float plotW = _plotArea.rect.width;
            float plotH = _plotArea.rect.height;

            if (plotW <= 0f || plotH <= 0f) return;

            // ── X spacing ────────────────────────────────────────────────
            // Divide the full width into (totalDays - 1) equal intervals.
            // Day i sits at x = i * stepX, measured from the left edge.
            // With totalDays = 14: slots 0–13, regardless of how many data
            // points exist. Missing days simply have no drawn element.
            float stepX = totalDays > 1 ? plotW / (totalDays - 1) : 0f;

            // ── Y normalisation ──────────────────────────────────────────
            float globalMax = 0f;
            foreach (float v in revenue)  globalMax = Mathf.Max(globalMax, v);
            foreach (float v in expenses) globalMax = Mathf.Max(globalMax, v);

            // Build world-space sample points for each line.
            // Points are in PlotArea local space, origin = bottom-left.
            Vector2[] greenPts = BuildPoints(revenue,  stepX, plotH, globalMax, totalDays);
            Vector2[] redPts   = BuildPoints(expenses, stepX, plotH, globalMax, totalDays);

            // ── Draw ─────────────────────────────────────────────────────
            DrawLine(_greenLineParent, _greenSegTemplate, _greenPointTemplate,
                     greenPts, _greenColor, _greenInstances);

            DrawLine(_redLineParent,   _redSegTemplate,   _redPointTemplate,
                     redPts,   _redColor,   _redInstances);
        }

        // ── Build points ────────────────────────────────────────────────

        /// <summary>
        /// Returns local-space positions (relative to PlotArea bottom-left) for each data point.
        /// Only data.Count positions are returned — positions for days without data are omitted.
        /// </summary>
        private static Vector2[] BuildPoints(
            List<float> data, float stepX, float plotH, float globalMax, int totalDays)
        {
            int count = Mathf.Min(data.Count, totalDays);
            var pts = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                // X: evenly spaced slot i within the 14-day timeline
                float x = i * stepX;

                // Y: normalised height (0 = bottom, plotH = top)
                float y = globalMax > 0f ? (data[i] / globalMax) * plotH : 0f;

                pts[i] = new Vector2(x, y);
            }

            return pts;
        }

        // ── Draw a single line ──────────────────────────────────────────

        private void DrawLine(
            RectTransform parent,
            RectTransform segTemplate,
            RectTransform pointTemplate,
            Vector2[] pts,
            Color color,
            List<GameObject> pool)
        {
            if (segTemplate == null || pts.Length == 0) return;

            // Draw segments between consecutive points
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];

                GameObject seg = Instantiate(segTemplate.gameObject, parent);
                seg.SetActive(true);
                pool.Add(seg);

                RectTransform rt = seg.GetComponent<RectTransform>();
                Image img        = seg.GetComponent<Image>();
                if (img != null) img.color = color;

                // ── Segment math ────────────────────────────────────────
                // Place the segment's pivot at point A, stretch it to point B.
                //
                // Length  = distance(A, B)
                // Angle   = atan2(Δy, Δx)  [degrees, measured from +X axis]
                // Pivot   = left-centre of the rect → anchoredPosition = A
                //           (assumes template pivot is (0, 0.5))

                Vector2 delta  = b - a;
                float   length = delta.magnitude;
                float   angle  = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

                rt.sizeDelta        = new Vector2(length, _segmentThickness);
                rt.pivot            = new Vector2(0f, 0.5f);
                rt.anchorMin        = Vector2.zero;   // bottom-left of PlotArea
                rt.anchorMax        = Vector2.zero;
                rt.anchoredPosition = a;
                rt.localEulerAngles = new Vector3(0f, 0f, angle);
            }

            // Draw point circles (optional)
            if (_showPoints && pointTemplate != null)
            {
                foreach (Vector2 pt in pts)
                {
                    GameObject dot = Instantiate(pointTemplate.gameObject, parent);
                    dot.SetActive(true);
                    pool.Add(dot);

                    RectTransform rt = dot.GetComponent<RectTransform>();
                    Image img        = dot.GetComponent<Image>();
                    if (img != null) img.color = color;

                    rt.sizeDelta        = new Vector2(_pointRadius * 2f, _pointRadius * 2f);
                    rt.pivot            = new Vector2(0.5f, 0.5f);
                    rt.anchorMin        = Vector2.zero;
                    rt.anchorMax        = Vector2.zero;
                    rt.anchoredPosition = pt;
                }
            }
        }

        // ── Clear ───────────────────────────────────────────────────────

        /// <summary>
        /// Destroys all previously spawned segment and point GameObjects.
        /// Templates (inactive) are never touched.
        /// </summary>
        private void ClearInstances()
        {
            foreach (var go in _greenInstances) if (go != null) Destroy(go);
            foreach (var go in _redInstances)   if (go != null) Destroy(go);
            _greenInstances.Clear();
            _redInstances.Clear();
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static RectTransform FindTemplate(RectTransform parent, string name)
        {
            if (parent == null) return null;
            Transform t = parent.Find(name);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }

        // ── Editor test ─────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Test: Render Sample Data")]
        private void TestRender()
        {
            RenderChart(
                new List<float> { 1000f, 1500f, 1200f, 1800f, 2100f },
                new List<float> {  600f,  700f,  650f,  800f,  750f }
            );
        }
#endif
    }
}
