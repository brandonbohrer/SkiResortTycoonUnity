using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Collections;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Reusable confirmation dialog for build actions and other confirmations.
    /// Shows title, message, cost breakdown, and confirm/cancel buttons.
    /// </summary>
    public class ConfirmationDialog : MonoBehaviour
    {
        public static ConfirmationDialog Instance { get; private set; }
        
        [Header("UI Elements")]
        [SerializeField] private GameObject _dialogContainer;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _confirmButtonText;
        [SerializeField] private TextMeshProUGUI _cancelButtonText;
        
        [Header("Backdrop")]
        [SerializeField] private Image _backdrop;
        [SerializeField] private float _backdropAlpha = 0.4f;
        
        [Header("Animation")]
        [SerializeField] private float _animationDuration = 0.15f;
        
        private UnityAction _onConfirm;
        private UnityAction _onCancel;
        private CanvasGroup _canvasGroup;
        private Coroutine _animationCoroutine;
        
        /// <summary>
        /// Whether the dialog is currently visible
        /// </summary>
        public bool IsVisible => _dialogContainer != null && _dialogContainer.activeSelf;
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            _canvasGroup = _dialogContainer?.GetComponent<CanvasGroup>();
            if (_canvasGroup == null && _dialogContainer != null)
            {
                _canvasGroup = _dialogContainer.AddComponent<CanvasGroup>();
            }
            
            // Start hidden
            if (_dialogContainer != null)
            {
                _dialogContainer.SetActive(false);
            }
        }
        
        void Start()
        {
            // Set up button listeners
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }
            
            if (_cancelButton != null)
            {
                _cancelButton.onClick.AddListener(OnCancelClicked);
            }
            
            // Set up backdrop click to cancel
            if (_backdrop != null)
            {
                var button = _backdrop.gameObject.GetComponent<Button>();
                if (button == null)
                {
                    button = _backdrop.gameObject.AddComponent<Button>();
                }
                button.onClick.AddListener(OnCancelClicked);
            }
        }
        
        void Update()
        {
            // ESC to cancel
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancelClicked();
            }
            
            // Enter to confirm
            if (IsVisible && Input.GetKeyDown(KeyCode.Return))
            {
                OnConfirmClicked();
            }
        }
        
        /// <summary>
        /// Shows a simple confirmation dialog
        /// </summary>
        public void Show(string title, string message, UnityAction onConfirm, UnityAction onCancel = null)
        {
            ShowWithCost(title, message, null, onConfirm, onCancel);
        }
        
        /// <summary>
        /// Shows a confirmation dialog with cost information
        /// </summary>
        public void ShowWithCost(string title, string message, float? cost, UnityAction onConfirm, UnityAction onCancel = null, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            
            // Set content
            if (_titleText != null)
            {
                _titleText.text = title;
            }
            
            if (_messageText != null)
            {
                _messageText.text = message;
            }
            
            if (_costText != null)
            {
                if (cost.HasValue)
                {
                    _costText.gameObject.SetActive(true);
                    _costText.text = $"Cost: ${cost.Value:N0}";
                    
                    // Check if player can afford
                    float playerMoney = UIManager.Instance?.SimulationRunner?.Sim?.State?.Money ?? 0f;
                    bool canAfford = playerMoney >= cost.Value;
                    
                    _costText.color = canAfford ? 
                        (UIManager.Instance?.Theme?.SuccessColor ?? Color.white) :
                        (UIManager.Instance?.Theme?.ErrorColor ?? Color.red);
                    
                    // Disable confirm if can't afford
                    if (_confirmButton != null)
                    {
                        _confirmButton.interactable = canAfford;
                    }
                }
                else
                {
                    _costText.gameObject.SetActive(false);
                    if (_confirmButton != null)
                    {
                        _confirmButton.interactable = true;
                    }
                }
            }
            
            if (_confirmButtonText != null)
            {
                _confirmButtonText.text = confirmText;
            }
            
            if (_cancelButtonText != null)
            {
                _cancelButtonText.text = cancelText;
            }
            
            // Show dialog with animation
            if (_dialogContainer != null)
            {
                _dialogContainer.SetActive(true);
                
                if (_animationCoroutine != null)
                {
                    StopCoroutine(_animationCoroutine);
                }
                _animationCoroutine = StartCoroutine(AnimateShow());
            }
        }
        
        /// <summary>
        /// Shows a build confirmation dialog with detailed cost breakdown
        /// </summary>
        public void ShowBuildConfirmation(string itemName, float baseCost, float? maintenanceCost = null, UnityAction onConfirm = null, UnityAction onCancel = null)
        {
            string message = $"Build {itemName}?";
            
            if (maintenanceCost.HasValue)
            {
                message += $"\n\nMaintenance: ${maintenanceCost.Value:N0}/day";
            }
            
            ShowWithCost($"Build {itemName}", message, baseCost, onConfirm, onCancel, "Build", "Cancel");
        }
        
        /// <summary>
        /// Hides the dialog
        /// </summary>
        public void Hide()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(AnimateHide());
        }
        
        private void OnConfirmClicked()
        {
            _onConfirm?.Invoke();
            Hide();
        }
        
        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
            Hide();
        }
        
        private IEnumerator AnimateShow()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            
            if (_backdrop != null)
            {
                var color = _backdrop.color;
                color.a = 0f;
                _backdrop.color = color;
            }
            
            float elapsed = 0f;
            
            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                
                // Ease out
                t = 1f - Mathf.Pow(1f - t, 2f);
                
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = t;
                }
                
                if (_backdrop != null)
                {
                    var color = _backdrop.color;
                    color.a = t * _backdropAlpha;
                    _backdrop.color = color;
                }
                
                yield return null;
            }
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }
            
            _animationCoroutine = null;
        }
        
        private IEnumerator AnimateHide()
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup?.alpha ?? 1f;
            float startBackdrop = _backdrop?.color.a ?? _backdropAlpha;
            
            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                
                // Ease in
                t = t * t;
                
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }
                
                if (_backdrop != null)
                {
                    var color = _backdrop.color;
                    color.a = Mathf.Lerp(startBackdrop, 0f, t);
                    _backdrop.color = color;
                }
                
                yield return null;
            }
            
            if (_dialogContainer != null)
            {
                _dialogContainer.SetActive(false);
            }
            
            _animationCoroutine = null;
        }
    }
}
