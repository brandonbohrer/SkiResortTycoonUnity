using UnityEngine;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Abstract base class for all interactive tools.
    /// Tools handle world-space interactions like building trails, placing lifts, etc.
    /// </summary>
    public abstract class BaseTool : MonoBehaviour
    {
        [Header("Tool Info")]
        [SerializeField] protected string _toolName = "Tool";
        [SerializeField] protected Sprite _toolIcon;
        [SerializeField] protected string _toolDescription = "Description";
        
        /// <summary>
        /// Display name of this tool
        /// </summary>
        public virtual string ToolName => _toolName;
        
        /// <summary>
        /// Icon to display in the toolbar
        /// </summary>
        public virtual Sprite ToolIcon => _toolIcon;
        
        /// <summary>
        /// Description/tooltip text for this tool
        /// </summary>
        public virtual string ToolDescription => _toolDescription;
        
        /// <summary>
        /// Whether this tool is currently active
        /// </summary>
        public bool IsActive { get; private set; }
        
        /// <summary>
        /// Called when this tool is activated (selected by the user)
        /// </summary>
        public virtual void OnActivate()
        {
            IsActive = true;
            ShowPreview();
            Debug.Log($"[{ToolName}] Activated");
        }
        
        /// <summary>
        /// Called every frame while this tool is active
        /// </summary>
        public virtual void OnUpdate()
        {
            if (!IsActive) return;
            
            UpdatePreview();
            HandleInput();
        }
        
        /// <summary>
        /// Called when this tool is deactivated (another tool selected)
        /// </summary>
        public virtual void OnDeactivate()
        {
            IsActive = false;
            HidePreview();
            Debug.Log($"[{ToolName}] Deactivated");
        }
        
        /// <summary>
        /// Called when this tool is cancelled (ESC or right-click)
        /// </summary>
        public virtual void OnCancel()
        {
            OnDeactivate();
            Debug.Log($"[{ToolName}] Cancelled");
        }
        
        /// <summary>
        /// Shows the tool's preview/ghost (override in subclasses)
        /// </summary>
        protected virtual void ShowPreview() { }
        
        /// <summary>
        /// Updates the tool's preview based on mouse position (override in subclasses)
        /// </summary>
        protected virtual void UpdatePreview() { }
        
        /// <summary>
        /// Hides the tool's preview/ghost (override in subclasses)
        /// </summary>
        protected virtual void HidePreview() { }
        
        /// <summary>
        /// Handles mouse/keyboard input for this tool (override in subclasses)
        /// </summary>
        protected virtual void HandleInput()
        {
            // Right-click to cancel
            if (Input.GetMouseButtonDown(1))
            {
                UIManager.Instance?.CancelActiveTool();
            }
        }
        
        /// <summary>
        /// Gets the world position under the mouse cursor
        /// </summary>
        protected Vector3 GetMouseWorldPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            // Raycast onto a horizontal ground plane at y=0
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            
            return Vector3.zero;
        }
        
        /// <summary>
        /// Checks if the mouse is over a UI element (to prevent world interaction)
        /// </summary>
        protected bool IsMouseOverUI()
        {
            return UnityEngine.EventSystems.EventSystem.current != null && 
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
        
        void Update()
        {
            if (IsActive)
            {
                OnUpdate();
            }
        }
    }
}
