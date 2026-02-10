using UnityEngine;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Types of structures that can be selected.
    /// </summary>
    public enum StructureType
    {
        Lift,
        Trail,
        Lodge
    }
    
    /// <summary>
    /// Component that makes a structure selectable in the world.
    /// Attach to lift roots, trail objects, or lodges.
    /// Handles hover highlighting and pulse effects.
    /// </summary>
    public class SelectableStructure : MonoBehaviour
    {
        [Header("Structure Info")]
        [SerializeField] private StructureType _structureType;
        [SerializeField] private int _structureId;
        [SerializeField] private string _structureName;
        
        [Header("Highlight Settings")]
        [SerializeField] private Color _highlightColor = new Color(1f, 0.9f, 0.3f, 1f); // Golden yellow
        [SerializeField] private float _pulseSpeed = 3f;
        [SerializeField] private float _pulseIntensity = 0.3f;
        [SerializeField] private float _outlineWidth = 0.02f;
        
        // References to data objects (set after creation)
        private LiftData _liftData;
        private TrailData _trailData;
        private LodgeFacility _lodgeFacility;
        
        // State
        private bool _isHovered;
        private bool _isSelected;
        private float _pulseTimer;
        
        // Cached renderers and original materials
        private List<Renderer> _renderers = new List<Renderer>();
        private Dictionary<Renderer, Color[]> _originalColors = new Dictionary<Renderer, Color[]>();
        private LineRenderer _lineRenderer; // For trails
        private Color _originalLineColor;
        
        // Material property block for efficient, non-destructive rendering changes
        private MaterialPropertyBlock _propBlock;
        
        // Properties
        public StructureType Type => _structureType;
        public int StructureId => _structureId;
        public string StructureName => _structureName;
        public LiftData LiftData => _liftData;
        public TrailData TrailData => _trailData;
        public LodgeFacility Lodge => _lodgeFacility;
        public bool IsHovered => _isHovered;
        public bool IsSelected => _isSelected;
        
        /// <summary>
        /// Initialize this selectable structure for a lift.
        /// </summary>
        public void InitializeAsLift(LiftData lift)
        {
            _structureType = StructureType.Lift;
            _structureId = lift.LiftId;
            _structureName = lift.Name ?? $"Lift {lift.LiftId}";
            _liftData = lift;
            
            // Delay renderer caching to avoid issues during object construction
            // The lift hierarchy might not be fully built yet
            Invoke(nameof(DelayedLiftSetup), 0.1f);
        }
        
        private void DelayedLiftSetup()
        {
            if (this == null || gameObject == null) return; // Object was destroyed
            
            try
            {
                CacheRenderers();
                // Further delay collider addition - it's not critical and can crash
                Invoke(nameof(DelayedColliderSetup), 0.2f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SelectableStructure] Error in delayed lift setup: {e.Message}");
            }
        }
        
        private void DelayedColliderSetup()
        {
            if (this == null || gameObject == null) return; // Object was destroyed
            
            try
            {
                AddCollidersIfNeeded();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SelectableStructure] Error adding colliders (non-critical): {e.Message}");
                // This is non-critical - structure can still be selected via child colliders
            }
        }
        
        /// <summary>
        /// Initialize this selectable structure for a trail.
        /// </summary>
        public void InitializeAsTrail(TrailData trail, LineRenderer lineRenderer)
        {
            _structureType = StructureType.Trail;
            _structureId = trail.TrailId;
            _structureName = trail.Name ?? $"Trail {trail.TrailId}";
            _trailData = trail;
            _lineRenderer = lineRenderer;
            
            if (_lineRenderer != null)
            {
                _originalLineColor = _lineRenderer.startColor;
            }
            
            // For trails, we need a collider along the path
            AddTrailCollider();
        }
        
        /// <summary>
        /// Initialize this selectable structure for a lodge.
        /// </summary>
        public void InitializeAsLodge(LodgeFacility lodge)
        {
            _structureType = StructureType.Lodge;
            _structureId = lodge.GetInstanceID();
            _structureName = lodge.gameObject.name;
            _lodgeFacility = lodge;
            
            CacheRenderers();
            AddCollidersIfNeeded();
        }
        
        void Start()
        {
            // Register with selection manager (delay slightly to ensure manager is ready)
            StartCoroutine(RegisterAfterDelay());
        }
        
        private System.Collections.IEnumerator RegisterAfterDelay()
        {
            yield return null; // Wait one frame
            
            if (StructureSelectionManager.Instance != null)
            {
                StructureSelectionManager.Instance.RegisterSelectable(this);
            }
        }
        
        void Update()
        {
            // Only update pulse if we have renderers and are actually hovered/selected
            if ((_isHovered || _isSelected) && (_renderers.Count > 0 || _lineRenderer != null))
            {
                try
                {
                    UpdatePulseEffect();
                }
                catch (System.Exception e)
                {
                    // Don't spam errors, just disable highlighting for this structure
                    Debug.LogWarning($"[SelectableStructure] Error in pulse effect, disabling: {e.Message}");
                    _isHovered = false;
                    _isSelected = false;
                }
            }
        }
        
        /// <summary>
        /// Called when mouse enters this structure.
        /// </summary>
        public void OnHoverEnter()
        {
            if (_isHovered) return;
            
            _isHovered = true;
            _pulseTimer = 0f;
            ApplyHighlight();
        }
        
        /// <summary>
        /// Called when mouse exits this structure.
        /// </summary>
        public void OnHoverExit()
        {
            if (!_isHovered) return;
            
            _isHovered = false;
            
            if (!_isSelected)
            {
                RemoveHighlight();
            }
        }
        
        /// <summary>
        /// Called when this structure is selected (clicked).
        /// </summary>
        public void OnSelect()
        {
            _isSelected = true;
            ApplyHighlight();
        }
        
        /// <summary>
        /// Called when this structure is deselected.
        /// </summary>
        public void OnDeselect()
        {
            _isSelected = false;
            
            if (!_isHovered)
            {
                RemoveHighlight();
            }
        }
        
        private void CacheRenderers()
        {
            _renderers.Clear();
            _originalColors.Clear();
            
            // Get all renderers in this object and children
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in allRenderers)
            {
                // Skip particle systems and line renderers
                if (renderer is ParticleSystemRenderer) continue;
                if (renderer is LineRenderer) continue;
                
                _renderers.Add(renderer);
                
                // Cache original colors using sharedMaterials (doesn't create copies!)
                var sharedMats = renderer.sharedMaterials;
                if (sharedMats != null && sharedMats.Length > 0)
                {
                    Color[] colors = new Color[sharedMats.Length];
                    for (int i = 0; i < sharedMats.Length; i++)
                    {
                        var mat = sharedMats[i];
                        if (mat == null)
                        {
                            colors[i] = Color.white;
                            continue;
                        }
                        
                        if (mat.HasProperty("_Color"))
                        {
                            colors[i] = mat.color;
                        }
                        else if (mat.HasProperty("_BaseColor"))
                        {
                            colors[i] = mat.GetColor("_BaseColor");
                        }
                        else
                        {
                            colors[i] = Color.white;
                        }
                    }
                    _originalColors[renderer] = colors;
                }
            }
        }
        
        private void AddCollidersIfNeeded()
        {
            // Add a tight-fitting BoxCollider to each individual child renderer
            // that doesn't already have one. This gives pixel-precise selection
            // because only the actual visual part has a collider, not a giant
            // bounding box. GetComponentInParent<SelectableStructure>() on the
            // hit object walks up to find us.
            //
            // Do NOT use MeshCollider (crashes on complex meshes).
            // Do NOT use a single box on the root (way too large, imprecise).
            
            int added = 0;
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                if (renderer.GetComponent<Collider>() != null) continue; // already has one
                
                renderer.gameObject.AddComponent<BoxCollider>();
                added++;
            }
            
            if (added > 0)
            {
                Debug.Log($"[SelectableStructure] Added {added} BoxCollider(s) to parts of {_structureName}");
            }
        }
        
        private void AddTrailCollider()
        {
            // Build colliders from TrailData.WorldPathPoints (NOT from LineRenderer,
            // because LR positions haven't been set yet at init time).
            if (_trailData == null || _trailData.WorldPathPoints == null || _trailData.WorldPathPoints.Count < 2) return;
            
            var points = _trailData.WorldPathPoints;
            
            // Container to keep the hierarchy tidy
            var container = new GameObject("TrailColliders");
            container.transform.SetParent(transform, true);
            
            // Radius matches the visible trail line width
            float lineWidth = (_lineRenderer != null) ? _lineRenderer.startWidth : 0.8f;
            float radius = Mathf.Max(lineWidth * 0.5f, 0.5f);
            
            // Step through path points, placing one capsule per segment.
            // Cap at ~80 capsules for performance.
            int step = Mathf.Max(1, points.Count / 80);
            int created = 0;
            
            for (int i = 0; i < points.Count - step; i += step)
            {
                int endIdx = Mathf.Min(i + step, points.Count - 1);
                Vector3 p0 = MountainManager.ToUnityVector3(points[i]);
                Vector3 p1 = MountainManager.ToUnityVector3(points[endIdx]);
                
                float segLen = Vector3.Distance(p0, p1);
                if (segLen < 0.01f) continue;
                
                var segObj = new GameObject($"Seg_{i}");
                segObj.transform.SetParent(container.transform, true);
                
                // Position at midpoint, orient along segment
                segObj.transform.position = (p0 + p1) * 0.5f;
                segObj.transform.rotation = Quaternion.LookRotation(p1 - p0);
                
                var cap = segObj.AddComponent<CapsuleCollider>();
                cap.direction = 2; // Z axis (forward = segment direction)
                cap.radius = radius;
                cap.height = segLen + radius * 2f; // span the full segment
                created++;
            }
            
            Debug.Log($"[SelectableStructure] Added {created} capsule colliders along trail {_structureName}");
        }
        
        private void UpdatePulseEffect()
        {
            _pulseTimer += Time.deltaTime * _pulseSpeed;
            float pulse = (Mathf.Sin(_pulseTimer) + 1f) / 2f; // 0 to 1
            float intensity = pulse * _pulseIntensity;
            
            ApplyHighlightIntensity(intensity);
        }
        
        private void ApplyHighlight()
        {
            ApplyHighlightIntensity(0f);
        }
        
        private void ApplyHighlightIntensity(float intensity)
        {
            try
            {
                // Initialize property block if needed (reusable, avoids allocation)
                if (_propBlock == null)
                {
                    _propBlock = new MaterialPropertyBlock();
                }
                
                Color highlightWithIntensity = Color.Lerp(_highlightColor, Color.white, intensity);
                
                // Apply to mesh renderers using MaterialPropertyBlock (efficient & non-destructive)
                for (int r = 0; r < _renderers.Count; r++)
                {
                    var renderer = _renderers[r];
                    if (renderer == null) continue;
                    
                    // Get current property block
                    renderer.GetPropertyBlock(_propBlock);
                    
                    // Set emission color
                    _propBlock.SetColor("_EmissionColor", highlightWithIntensity * (0.5f + intensity));
                    
                    // Tint the base color
                    Color original = Color.white;
                    if (_originalColors.TryGetValue(renderer, out Color[] colors) && colors.Length > 0)
                    {
                        original = colors[0];
                    }
                    Color tintedColor = Color.Lerp(original, highlightWithIntensity, 0.3f + intensity * 0.2f);
                    _propBlock.SetColor("_Color", tintedColor);
                    _propBlock.SetColor("_BaseColor", tintedColor);
                    
                    renderer.SetPropertyBlock(_propBlock);
                }
                
                // Apply to line renderer (trails) - LineRenderer doesn't use MaterialPropertyBlock
                if (_lineRenderer != null)
                {
                    Color lineColor = Color.Lerp(_originalLineColor, highlightWithIntensity, 0.5f + intensity * 0.3f);
                    _lineRenderer.startColor = lineColor;
                    _lineRenderer.endColor = lineColor;
                    
                    // Make it wider when highlighted
                    float originalWidth = 0.8f; // Default from TrailVisualizer
                    _lineRenderer.startWidth = originalWidth * (1f + intensity * 0.5f);
                    _lineRenderer.endWidth = originalWidth * (1f + intensity * 0.5f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SelectableStructure] Error applying highlight: {e.Message}");
            }
        }
        
        private void RemoveHighlight()
        {
            // Clear property blocks to restore original appearance
            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;
                
                // Clear property block (restores original material properties)
                renderer.SetPropertyBlock(null);
            }
            
            // Restore line renderer
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = _originalLineColor;
                _lineRenderer.endColor = _originalLineColor;
                _lineRenderer.startWidth = 0.8f;
                _lineRenderer.endWidth = 0.8f;
            }
        }
        
        void OnDestroy()
        {
            // Stop any running coroutines
            StopAllCoroutines();
            
            // Unregister from selection manager
            if (StructureSelectionManager.Instance != null)
            {
                StructureSelectionManager.Instance.UnregisterSelectable(this);
            }
            
            // Clear property block reference
            _propBlock = null;
            
            // Note: Don't manually destroy child collider objects here - Unity handles child destruction
            // Manually destroying during OnDestroy can cause issues
        }
    }
    
    /// <summary>
    /// Helper component to reference back to the parent SelectableStructure from a collider object.
    /// </summary>
    public class SelectableStructureColliderRef : MonoBehaviour
    {
        public SelectableStructure ParentSelectable;
    }
}
