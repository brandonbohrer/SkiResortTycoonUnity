using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Manages non-intrusive toast notifications.
    /// Displays temporary messages that fade in and out.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }
        
        [Header("Toast Settings")]
        [SerializeField] private GameObject _toastPrefab;
        [SerializeField] private Transform _toastContainer;
        [SerializeField] private float _fadeInDuration = 0.2f;
        [SerializeField] private float _displayDuration = 3f;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private int _maxVisibleToasts = 3;
        [SerializeField] private float _toastSpacing = 8f;
        
        private Queue<ToastData> _pendingToasts = new Queue<ToastData>();
        private List<ActiveToast> _activeToasts = new List<ActiveToast>();
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        
        /// <summary>
        /// Shows an info toast notification
        /// </summary>
        public void ShowInfo(string message)
        {
            ShowToast(message, ToastType.Info);
        }
        
        /// <summary>
        /// Shows a success toast notification
        /// </summary>
        public void ShowSuccess(string message)
        {
            ShowToast(message, ToastType.Success);
        }
        
        /// <summary>
        /// Shows a warning toast notification
        /// </summary>
        public void ShowWarning(string message)
        {
            ShowToast(message, ToastType.Warning);
        }
        
        /// <summary>
        /// Shows an error toast notification
        /// </summary>
        public void ShowError(string message)
        {
            ShowToast(message, ToastType.Error);
        }
        
        /// <summary>
        /// Shows a toast notification of the specified type
        /// </summary>
        public void ShowToast(string message, ToastType type, float? duration = null)
        {
            var toastData = new ToastData
            {
                Message = message,
                Type = type,
                Duration = duration ?? _displayDuration
            };
            
            // If we have room, show immediately
            if (_activeToasts.Count < _maxVisibleToasts)
            {
                StartCoroutine(ShowToastCoroutine(toastData));
            }
            else
            {
                // Queue for later
                _pendingToasts.Enqueue(toastData);
            }
        }
        
        private IEnumerator ShowToastCoroutine(ToastData data)
        {
            if (_toastPrefab == null || _toastContainer == null)
            {
                Debug.LogWarning("[NotificationManager] Toast prefab or container not set!");
                yield break;
            }
            
            // Create toast object
            var toastObj = Instantiate(_toastPrefab, _toastContainer);
            var canvasGroup = toastObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = toastObj.AddComponent<CanvasGroup>();
            }
            
            // Set up toast content
            var text = toastObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = data.Message;
            }
            
            // Set color based on type
            var background = toastObj.GetComponent<Image>();
            if (background != null)
            {
                background.color = GetToastColor(data.Type);
            }
            
            // Track this toast
            var activeToast = new ActiveToast
            {
                GameObject = toastObj,
                CanvasGroup = canvasGroup,
                Data = data
            };
            _activeToasts.Add(activeToast);
            
            // Start hidden
            canvasGroup.alpha = 0f;
            
            // Fade in
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            
            // Hold
            yield return new WaitForSecondsRealtime(data.Duration);
            
            // Fade out
            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
                yield return null;
            }
            
            // Clean up
            _activeToasts.Remove(activeToast);
            Destroy(toastObj);
            
            // Show next pending toast if any
            if (_pendingToasts.Count > 0 && _activeToasts.Count < _maxVisibleToasts)
            {
                StartCoroutine(ShowToastCoroutine(_pendingToasts.Dequeue()));
            }
        }
        
        private Color GetToastColor(ToastType type)
        {
            UITheme theme = UIManager.Instance?.Theme;
            
            switch (type)
            {
                case ToastType.Success:
                    return theme?.SuccessColor ?? new Color(0.2f, 0.6f, 0.2f, 0.9f);
                case ToastType.Warning:
                    return theme?.WarningColor ?? new Color(0.9f, 0.6f, 0f, 0.9f);
                case ToastType.Error:
                    return theme?.ErrorColor ?? new Color(0.8f, 0.2f, 0.2f, 0.9f);
                case ToastType.Info:
                default:
                    return theme?.PrimaryColor ?? new Color(0.2f, 0.4f, 0.8f, 0.9f);
            }
        }
        
        private struct ToastData
        {
            public string Message;
            public ToastType Type;
            public float Duration;
        }
        
        private class ActiveToast
        {
            public GameObject GameObject;
            public CanvasGroup CanvasGroup;
            public ToastData Data;
        }
    }
    
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
