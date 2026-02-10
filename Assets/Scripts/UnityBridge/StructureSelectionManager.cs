using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.UI;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Manages structure selection in the game world.
    /// Handles raycasting, cursor changes, hover effects, and opening the details panel.
    /// Only active when no build tool is selected.
    /// </summary>
    public class StructureSelectionManager : MonoBehaviour
    {
        public static StructureSelectionManager Instance { get; private set; }
        
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private StructureDetailsPanel _detailsPanel;
        
        [Header("Cursor Textures")]
        [SerializeField] private Texture2D _pointerCursor;
        [SerializeField] private Vector2 _pointerHotspot = new Vector2(0, 0);
        
        [Header("Settings")]
        [SerializeField] private float _raycastDistance = 1000f;
        [SerializeField] private LayerMask _selectableLayers = -1; // All layers by default
        [SerializeField] private bool _enableSelection = false; // DISABLED BY DEFAULT - enable in inspector when stable
        [SerializeField] private bool _enableTrailHover = true; // Toggle to disable trail hover (performance)
        
        // State
        private SelectableStructure _hoveredStructure;
        private SelectableStructure _selectedStructure;
        private bool _isCustomCursorActive;
        private bool _hasError; // Disable system if errors occur
        private int _errorCount;
        private const int MAX_ERRORS = 5; // Disable after too many errors
        private bool _handlingClick; // Re-entrancy guard for click handling
        
        // Cached selectables (to avoid expensive FindObjectsOfType every frame)
        private System.Collections.Generic.List<SelectableStructure> _cachedSelectables = new System.Collections.Generic.List<SelectableStructure>();
        private float _lastCacheTime;
        private const float CACHE_REFRESH_INTERVAL = 1f; // Refresh cache every 1 second
        
        // Properties
        public SelectableStructure HoveredStructure => _hoveredStructure;
        public SelectableStructure SelectedStructure => _selectedStructure;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        void Start()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
            
            // Create default pointer cursor if not assigned
            if (_pointerCursor == null)
            {
                CreateDefaultPointerCursor();
            }
            
            // DON'T scan for existing structures automatically - causes crashes
            // Users can manually call ScanAndSetupExistingStructures() if needed
            // Invoke(nameof(ScanAndSetupExistingStructures), 0.5f);
            
            // Create details panel if not assigned (wrap in try-catch to prevent crashes)
            if (_detailsPanel == null)
            {
                try
                {
                    CreateDefaultDetailsPanel();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Selection] Failed to create default details panel: {e.Message}");
                }
            }
            
            Debug.Log($"[Selection] StructureSelectionManager started (enabled: {_enableSelection})");
        }
        
        /// <summary>
        /// Creates a basic details panel at runtime if none is assigned.
        /// </summary>
        private void CreateDefaultDetailsPanel()
        {
            // Find or create canvas
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("RuntimeCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            
            // Ensure the canvas has a GraphicRaycaster (required for button clicks)
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            // Ensure an EventSystem exists (required for any UI interaction)
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            // Create panel
            var panelObj = new GameObject("StructureDetailsPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1, 0.5f);
            panelRect.anchorMax = new Vector2(1, 0.5f);
            panelRect.pivot = new Vector2(1, 0.5f);
            panelRect.anchoredPosition = new Vector2(-20, 0);
            panelRect.sizeDelta = new Vector2(280, 400);
            
            var canvasGroup = panelObj.AddComponent<CanvasGroup>();
            
            // Background
            var bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            
            // Add vertical layout
            var layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 15, 15);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            // Header
            var headerObj = new GameObject("Header");
            headerObj.transform.SetParent(panelObj.transform, false);
            var headerLayout = headerObj.AddComponent<LayoutElement>();
            headerLayout.minHeight = 60;
            
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(headerObj.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            var defaultFont = TMPro.TMP_Settings.defaultFontAsset;
            if (defaultFont != null) titleText.font = defaultFont;
            titleText.text = "Structure Details";
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            
            // Stats container
            var statsObj = new GameObject("StatsContainer");
            statsObj.transform.SetParent(panelObj.transform, false);
            var statsLayout = statsObj.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 5;
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = false;
            statsLayout.childForceExpandWidth = true;
            var statsLayoutElem = statsObj.AddComponent<LayoutElement>();
            statsLayoutElem.flexibleHeight = 1;
            
            // Buttons container
            var buttonsObj = new GameObject("Buttons");
            buttonsObj.transform.SetParent(panelObj.transform, false);
            var buttonsLayout = buttonsObj.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 10;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            var buttonsLayoutElem = buttonsObj.AddComponent<LayoutElement>();
            buttonsLayoutElem.minHeight = 40;
            
            // Delete button
            var deleteBtn = CreateButton(buttonsObj.transform, "Delete", new Color(0.8f, 0.2f, 0.2f));
            
            // Close button
            var closeBtn = CreateButton(buttonsObj.transform, "Close", new Color(0.3f, 0.3f, 0.4f));
            
            // Add component
            _detailsPanel = panelObj.AddComponent<StructureDetailsPanel>();
            
            // Use reflection to set private fields (not ideal but works for runtime creation)
            var panelType = typeof(StructureDetailsPanel);
            panelType.GetField("_panelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_detailsPanel, panelObj);
            panelType.GetField("_canvasGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_detailsPanel, canvasGroup);
            panelType.GetField("_titleText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_detailsPanel, titleText);
            panelType.GetField("_statsContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_detailsPanel, statsObj.transform);
            panelType.GetField("_deleteButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_detailsPanel, deleteBtn);
            panelType.GetField("_closeButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(_detailsPanel, closeBtn);
            
            // Wire up button listeners directly here — don't rely on Start() timing
            // with reflection-set fields. Use reflection to call private methods.
            var onDeleteMethod = panelType.GetMethod("OnDeleteClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var onCloseMethod = panelType.GetMethod("OnCloseClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (deleteBtn != null && onDeleteMethod != null)
            {
                deleteBtn.onClick.AddListener(() => onDeleteMethod.Invoke(_detailsPanel, null));
            }
            if (closeBtn != null && onCloseMethod != null)
            {
                closeBtn.onClick.AddListener(() => onCloseMethod.Invoke(_detailsPanel, null));
            }
            
            panelObj.SetActive(false);
            Debug.Log("[Selection] Created runtime StructureDetailsPanel");
        }
        
        private Button CreateButton(Transform parent, string text, Color bgColor)
        {
            var btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent, false);
            
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = bgColor;
            
            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;
            
            var layoutElem = btnObj.AddComponent<LayoutElement>();
            layoutElem.minWidth = 80;
            layoutElem.minHeight = 35;
            
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            var btnFont = TMPro.TMP_Settings.defaultFontAsset;
            if (btnFont != null) tmp.font = btnFont;
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            return btn;
        }
        
        /// <summary>
        /// Scans the scene for existing structures and ensures they have SelectableStructure components.
        /// Called at startup to retrofit structures that were created before this system existed.
        /// </summary>
        public void ScanAndSetupExistingStructures()
        {
            try
            {
                Debug.Log("[Selection] Scanning scene for existing structures to retrofit...");
                
                // Find existing lifts
                var liftBuilder = FindObjectOfType<LiftBuilder>();
                if (liftBuilder?.LiftSystem != null && liftBuilder.PrefabBuilder != null)
                {
                    foreach (var lift in liftBuilder.LiftSystem.GetAllLifts())
                    {
                        var instance = liftBuilder.PrefabBuilder.GetLiftInstance(lift.LiftId);
                        if (instance?.Root != null)
                        {
                            var selectable = instance.Root.GetComponent<SelectableStructure>();
                            if (selectable == null)
                            {
                                selectable = instance.Root.AddComponent<SelectableStructure>();
                                selectable.InitializeAsLift(lift);
                                Debug.Log($"[Selection] Retrofitted lift {lift.Name} with SelectableStructure");
                            }
                        }
                    }
                }
                
                // Find existing lodges
                var lodges = FindObjectsOfType<LodgeFacility>();
                foreach (var lodge in lodges)
                {
                    var selectable = lodge.GetComponent<SelectableStructure>();
                    if (selectable == null)
                    {
                        selectable = lodge.gameObject.AddComponent<SelectableStructure>();
                        selectable.InitializeAsLodge(lodge);
                        Debug.Log($"[Selection] Retrofitted lodge {lodge.gameObject.name} with SelectableStructure");
                    }
                }
                
                Debug.Log("[Selection] Scene scan complete");
                
                // Note: Trails are handled by TrailVisualizer which creates their GameObjects
                // They will get SelectableStructure when TrailVisualizer creates/updates them
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Selection] Error scanning scene for structures: {e.Message}\n{e.StackTrace}");
                // Don't let this crash the system
                _hasError = true;
                _enableSelection = false;
            }
        }
        
        void Update()
        {
            // Skip if selection is disabled or system has been shut down due to errors
            if (!_enableSelection || _hasError) return;
            
            try
            {
                // Don't do selection while a build tool is active
                if (UIManager.Instance != null && UIManager.Instance.HasActiveTool())
                {
                    // Clear hover state when entering tool mode
                    if (_hoveredStructure != null)
                    {
                        _hoveredStructure.OnHoverExit();
                        _hoveredStructure = null;
                        ResetCursor();
                    }
                    return;
                }
                
                // Don't do selection over UI
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    if (_hoveredStructure != null)
                    {
                        _hoveredStructure.OnHoverExit();
                        _hoveredStructure = null;
                        ResetCursor();
                    }
                    return;
                }
                
                UpdateHover();
                HandleClick();
                
                // Reset error count on successful frame
                _errorCount = 0;
            }
            catch (System.Exception e)
            {
                _errorCount++;
                Debug.LogWarning($"[Selection] Error in Update ({_errorCount}/{MAX_ERRORS}): {e.Message}\n{e.StackTrace}");
                
                // Disable the system after too many errors
                if (_errorCount >= MAX_ERRORS)
                {
                    Debug.LogError("[Selection] Too many errors - disabling structure selection system. Check console for details.");
                    _hasError = true;
                    _enableSelection = false;
                    ResetCursor();
                }
            }
        }
        
        private void UpdateHover()
        {
            if (_camera == null) return;
            
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            SelectableStructure hitStructure = null;
            
            // Single closest-hit raycast. Each structure part (pillar, chair, turn
            // wheel, lodge mesh) has its own tight BoxCollider, so the closest hit
            // IS the thing directly under the cursor. No radius, no bounding-box
            // approximation — pixel-precise.
            if (Physics.Raycast(ray, out RaycastHit hit, _raycastDistance))
            {
                // Walk up the hierarchy to find the SelectableStructure root
                hitStructure = hit.collider.GetComponentInParent<SelectableStructure>();
            }
            
            // Trails now have capsule colliders along their path, so the raycast
            // above handles them with the same precision as lifts. No screen-space
            // fallback needed.
            
            // Handle hover state changes
            if (hitStructure != _hoveredStructure)
            {
                // Exit old hover
                if (_hoveredStructure != null)
                {
                    _hoveredStructure.OnHoverExit();
                }
                
                // Enter new hover
                _hoveredStructure = hitStructure;
                
                if (_hoveredStructure != null)
                {
                    _hoveredStructure.OnHoverEnter();
                    SetPointerCursor();
                }
                else
                {
                    ResetCursor();
                }
            }
        }
        
        /// <summary>
        /// Refreshes the cached list of selectable structures.
        /// Called periodically to avoid expensive FindObjectsOfType every frame.
        /// </summary>
        private void RefreshSelectablesCache()
        {
            if (Time.time - _lastCacheTime < CACHE_REFRESH_INTERVAL) return;
            
            _lastCacheTime = Time.time;
            _cachedSelectables.Clear();
            
            var selectables = FindObjectsOfType<SelectableStructure>();
            if (selectables != null)
            {
                _cachedSelectables.AddRange(selectables);
            }
        }
        
        /// <summary>
        /// Register a new selectable (called when structures are created).
        /// </summary>
        public void RegisterSelectable(SelectableStructure selectable)
        {
            if (selectable != null && !_cachedSelectables.Contains(selectable))
            {
                _cachedSelectables.Add(selectable);
            }
        }
        
        /// <summary>
        /// Unregister a selectable (called when structures are destroyed).
        /// </summary>
        public void UnregisterSelectable(SelectableStructure selectable)
        {
            _cachedSelectables.Remove(selectable);
            
            if (_hoveredStructure == selectable)
            {
                _hoveredStructure = null;
                ResetCursor();
            }
            if (_selectedStructure == selectable)
            {
                _selectedStructure = null;
            }
        }
        
        /// <summary>
        /// Check if the mouse is hovering over a trail line renderer.
        /// Uses screen-space distance checking.
        /// </summary>
        private SelectableStructure CheckTrailHover()
        {
            // Periodically refresh the cache
            RefreshSelectablesCache();
            
            float hoverThreshold = 15f; // Pixels
            float closestDistance = float.MaxValue;
            SelectableStructure closestTrail = null;
            
            // Use cached selectables instead of FindObjectsOfType
            for (int s = 0; s < _cachedSelectables.Count; s++)
            {
                var selectable = _cachedSelectables[s];
                if (selectable == null) continue;
                if (selectable.Type != StructureType.Trail) continue;
                if (selectable.TrailData == null) continue;
                
                var lineRenderer = selectable.GetComponent<LineRenderer>();
                if (lineRenderer == null || lineRenderer.positionCount < 2) continue;
                
                // Check distance from mouse to line in screen space
                Vector3 mousePos = Input.mousePosition;
                
                // Limit how many segments we check per trail for performance
                int maxSegments = Mathf.Min(lineRenderer.positionCount - 1, 50);
                int step = Mathf.Max(1, (lineRenderer.positionCount - 1) / maxSegments);
                
                for (int i = 0; i < lineRenderer.positionCount - 1; i += step)
                {
                    Vector3 p1World = lineRenderer.GetPosition(i);
                    Vector3 p2World = lineRenderer.GetPosition(Mathf.Min(i + step, lineRenderer.positionCount - 1));
                    
                    Vector3 p1Screen = _camera.WorldToScreenPoint(p1World);
                    Vector3 p2Screen = _camera.WorldToScreenPoint(p2World);
                    
                    // Skip if behind camera
                    if (p1Screen.z < 0 || p2Screen.z < 0) continue;
                    
                    float dist = DistancePointToLineSegment(mousePos, p1Screen, p2Screen);
                    
                    if (dist < hoverThreshold && dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestTrail = selectable;
                        break; // Found a close segment, no need to check more on this trail
                    }
                }
            }
            
            return closestTrail;
        }
        
        /// <summary>
        /// Calculate the distance from a point to a line segment.
        /// </summary>
        private float DistancePointToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 line = lineEnd - lineStart;
            float lineLength = line.magnitude;
            
            if (lineLength < 0.001f)
                return Vector3.Distance(point, lineStart);
            
            Vector3 lineDir = line / lineLength;
            Vector3 toPoint = point - lineStart;
            
            float projection = Vector3.Dot(toPoint, lineDir);
            projection = Mathf.Clamp(projection, 0, lineLength);
            
            Vector3 closestPoint = lineStart + lineDir * projection;
            return Vector3.Distance(point, closestPoint);
        }
        
        private void HandleClick()
        {
            // Re-entrancy guard to prevent recursive click handling
            if (_handlingClick)
            {
                Debug.LogWarning("[Selection] HandleClick called recursively - ignoring to prevent stack overflow");
                return;
            }
            
            _handlingClick = true;
            try
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (_hoveredStructure != null)
                    {
                        SelectStructure(_hoveredStructure);
                    }
                    else
                    {
                        // Clicked on empty space - deselect
                        DeselectStructure();
                    }
                }
                
                // Right-click to deselect
                if (Input.GetMouseButtonDown(1))
                {
                    DeselectStructure();
                }
            }
            finally
            {
                _handlingClick = false;
            }
        }
        
        /// <summary>
        /// Select a structure and open its details panel.
        /// </summary>
        public void SelectStructure(SelectableStructure structure)
        {
            if (structure == null) return;
            
            // Deselect previous
            if (_selectedStructure != null && _selectedStructure != structure)
            {
                _selectedStructure.OnDeselect();
            }
            
            _selectedStructure = structure;
            _selectedStructure.OnSelect();
            
            // Open details panel
            if (_detailsPanel != null)
            {
                _detailsPanel.ShowStructure(_selectedStructure);
            }
            
            Debug.Log($"[Selection] Selected {structure.Type}: {structure.StructureName}");
        }
        
        /// <summary>
        /// Deselect the current structure.
        /// </summary>
        public void DeselectStructure()
        {
            if (_selectedStructure == null) return; // IMPORTANT: idempotent early return
            
            _selectedStructure.OnDeselect();
            _selectedStructure = null;
            
            // Hide details panel (defer to next frame to break recursion)
            if (_detailsPanel != null)
            {
                StartCoroutine(HidePanelNextFrame());
            }
        }
        
        private System.Collections.IEnumerator HidePanelNextFrame()
        {
            yield return null;
            if (_detailsPanel != null)
            {
                _detailsPanel.Hide();
            }
        }
        
        private void SetPointerCursor()
        {
            if (_pointerCursor != null && !_isCustomCursorActive)
            {
                Cursor.SetCursor(_pointerCursor, _pointerHotspot, CursorMode.Auto);
                _isCustomCursorActive = true;
            }
        }
        
        private void ResetCursor()
        {
            if (_isCustomCursorActive)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _isCustomCursorActive = false;
            }
        }
        
        private void CreateDefaultPointerCursor()
        {
            // Create a simple pointer cursor texture programmatically
            int size = 32;
            _pointerCursor = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // Fill with transparent
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }
            
            // Draw a pointing hand shape
            Color cursorColor = Color.white;
            Color outlineColor = Color.black;
            
            // Simple pointer/hand icon
            // Index finger pointing up-left
            DrawLine(pixels, size, 8, 8, 16, 0, outlineColor, 3);
            DrawLine(pixels, size, 8, 8, 16, 0, cursorColor, 1);
            
            // Palm
            DrawCircle(pixels, size, 10, 14, 6, outlineColor);
            DrawCircle(pixels, size, 10, 14, 4, cursorColor);
            
            // Other fingers (simplified)
            DrawLine(pixels, size, 14, 10, 20, 8, outlineColor, 2);
            DrawLine(pixels, size, 14, 10, 20, 8, cursorColor, 1);
            
            DrawLine(pixels, size, 14, 14, 22, 14, outlineColor, 2);
            DrawLine(pixels, size, 14, 14, 22, 14, cursorColor, 1);
            
            DrawLine(pixels, size, 14, 18, 20, 20, outlineColor, 2);
            DrawLine(pixels, size, 14, 18, 20, 20, cursorColor, 1);
            
            _pointerCursor.SetPixels(pixels);
            _pointerCursor.Apply();
            
            _pointerHotspot = new Vector2(8, 0);
        }
        
        private void DrawLine(Color[] pixels, int size, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            
            while (true)
            {
                for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
                {
                    for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                    {
                        int px = x0 + tx;
                        int py = y0 + ty;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            pixels[py * size + px] = color;
                        }
                    }
                }
                
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }
        
        private void DrawCircle(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                        {
                            pixels[py * size + px] = color;
                        }
                    }
                }
            }
        }
        
        void OnDestroy()
        {
            ResetCursor();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
