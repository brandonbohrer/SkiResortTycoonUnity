using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Mountain Manager → Research tab: three lift research projects with cost, duration (in-game days), and unlock targets.
    /// </summary>
    public class LiftResearchPanelController : MonoBehaviour
    {
        [System.Serializable]
        public class ResearchCard
        {
            [Tooltip("One TMP for cost/time (e.g. $50k, 3 days). Script never changes this string.")]
            public TextMeshProUGUI initialText;

            [Tooltip("One TMP for the countdown; script sets text (e.g. Time left: 1 day / 2 days).")]
            public TextMeshProUGUI timeLeftText;

            public Button researchButton;
            public Button cancelButton;

            [Tooltip("Money deducted when research starts (refunded on cancel).")]
            public int researchCost = 50000;

            [Tooltip("In-game days from start until completion (end-of-day rollover).")]
            public int researchDurationDays = 3;

            [Tooltip("Lift type unlocked in the build dock when this project completes.")]
            public LiftType unlocksLiftType = LiftType.OneSeatHighSpeed;
        }

        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private ResearchCard[] _cards = new ResearchCard[3];

        private const string ResearchLockOverlayName = "ResearchLockRaycast";

        private void Start()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                int slot = i;
                var c = _cards[i];
                if (c.researchButton != null)
                    c.researchButton.onClick.AddListener(() => OnResearchClicked(slot));
                if (c.cancelButton != null)
                    c.cancelButton.onClick.AddListener(() => OnCancelClicked(slot));
            }

            RefreshAllSlots();
        }

        private void Update()
        {
            RefreshProgressDisplays();
        }

        private void OnEnable()
        {
            LiftResearchEvents.Changed += OnLiftResearchChanged;
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            LiftResearchEvents.Changed -= OnLiftResearchChanged;
        }

        private void OnLiftResearchChanged()
        {
            RefreshAllSlots();
        }

        private SimulationState State => _simulationRunner != null ? _simulationRunner.Sim?.State : null;

        private void OnResearchClicked(int slot)
        {
            var state = State;
            if (state == null || slot < 0 || slot >= _cards.Length) return;
            var card = _cards[slot];

            if (IsSlotDone(state, slot) || IsSlotResearching(state, slot))
                return;

            if (slot > 0 && !IsSlotDone(state, slot - 1))
            {
                NotificationManager.Instance?.ShowWarning(TooltipTexts.Research.LockedContent);
                return;
            }

            if (AnyOtherSlotResearching(state, slot))
            {
                NotificationManager.Instance?.ShowWarning("Another research project is already in progress.");
                return;
            }

            if (state.Money < card.researchCost)
            {
                NotificationManager.Instance?.ShowError($"Not enough money. Need ${card.researchCost:N0}.");
                return;
            }

            int completionDay = state.DayIndex + card.researchDurationDays;
            state.Money -= card.researchCost;

            switch (slot)
            {
                case 0:
                    state.LiftResearchSlot0CompletionDay = completionDay;
                    state.LiftResearchSlot0PendingUnlockType = (int)card.unlocksLiftType;
                    state.LiftResearchSlot0PaidAmount = card.researchCost;
                    break;
                case 1:
                    state.LiftResearchSlot1CompletionDay = completionDay;
                    state.LiftResearchSlot1PendingUnlockType = (int)card.unlocksLiftType;
                    state.LiftResearchSlot1PaidAmount = card.researchCost;
                    break;
                case 2:
                    state.LiftResearchSlot2CompletionDay = completionDay;
                    state.LiftResearchSlot2PendingUnlockType = (int)card.unlocksLiftType;
                    state.LiftResearchSlot2PaidAmount = card.researchCost;
                    break;
            }

            LiftResearchEvents.Raise();
            RefreshSlot(slot);
        }

        private void OnCancelClicked(int slot)
        {
            var state = State;
            if (state == null) return;

            int refund = 0;
            switch (slot)
            {
                case 0: refund = state.LiftResearchSlot0PaidAmount; break;
                case 1: refund = state.LiftResearchSlot1PaidAmount; break;
                case 2: refund = state.LiftResearchSlot2PaidAmount; break;
            }

            state.Money += refund;
            state.ClearLiftResearchInProgress(slot);
            LiftResearchEvents.Raise();
            RefreshSlot(slot);
        }

        private static bool AnyOtherSlotResearching(SimulationState state, int exceptSlot)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i == exceptSlot) continue;
                if (IsSlotResearching(state, i)) return true;
            }
            return false;
        }

        private static bool IsSlotResearching(SimulationState state, int slot)
        {
            return state.GetLiftResearchCompletionDay(slot) >= 0;
        }

        private static bool IsSlotDone(SimulationState state, int slot)
        {
            switch (slot)
            {
                case 0: return state.LiftResearchSlot0Done;
                case 1: return state.LiftResearchSlot1Done;
                case 2: return state.LiftResearchSlot2Done;
                default: return false;
            }
        }

        private void RefreshProgressDisplays()
        {
            var state = State;
            if (state == null) return;

            for (int i = 0; i < _cards.Length; i++)
            {
                if (!IsSlotResearching(state, i)) continue;
                var c = _cards[i];
                if (c.timeLeftText == null) continue;

                int completion = state.GetLiftResearchCompletionDay(i);
                int remaining = completion - state.DayIndex;
                if (remaining < 0) remaining = 0;
                c.timeLeftText.text = FormatTimeLeftLine(remaining);
            }
        }

        /// <summary>English copy: "1 day" vs "2 days".</summary>
        private static string FormatTimeLeftLine(int daysRemaining)
        {
            if (daysRemaining < 0) daysRemaining = 0;
            string dayWord = daysRemaining == 1 ? "day" : "days";
            return $"Time left: {daysRemaining} {dayWord}";
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < _cards.Length; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int slot)
        {
            var state = State;
            if (state == null || slot < 0 || slot >= _cards.Length) return;

            var c = _cards[slot];
            bool done = IsSlotDone(state, slot);
            bool researching = IsSlotResearching(state, slot);
            bool prerequisite = slot == 0 || IsSlotDone(state, slot - 1);
            bool blockedByOther = AnyOtherSlotResearching(state, slot);
            bool canStart = prerequisite && !blockedByOther;

            bool showInitial = !researching;
            if (c.initialText != null) c.initialText.gameObject.SetActive(showInitial);
            if (c.timeLeftText != null)
            {
                c.timeLeftText.gameObject.SetActive(researching);
                if (researching)
                    c.timeLeftText.text = FormatTimeLeftLine(
                        Mathf.Max(0, state.GetLiftResearchCompletionDay(slot) - state.DayIndex));
            }

            if (c.researchButton != null)
            {
                c.researchButton.gameObject.SetActive(!done && !researching);
                c.researchButton.interactable = canStart;
                EnsureResearchLockOverlay(c.researchButton, !prerequisite && !done && !researching);
                if (prerequisite && !done && !researching)
                    SetupResearchReadyTooltip(c.researchButton);
            }

            if (c.cancelButton != null)
            {
                c.cancelButton.gameObject.SetActive(researching);
                c.cancelButton.interactable = researching;
            }

            SetCardGreyedOut(c, !prerequisite && !done && !researching);
        }

        private static void SetupResearchReadyTooltip(Button button)
        {
            if (button == null) return;
            var tt = button.GetComponent<TooltipTrigger>();
            if (tt == null) tt = button.gameObject.AddComponent<TooltipTrigger>();
            tt.SetContent(TooltipTexts.Research.ResearchHeader, TooltipTexts.Research.ResearchContent);
        }

        private static void SetCardGreyedOut(ResearchCard card, bool greyed)
        {
            if (card.initialText == null) return;
            var cg = card.initialText.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = greyed ? 0.45f : 1f;
        }

        private void EnsureResearchLockOverlay(Button button, bool locked)
        {
            if (button == null) return;
            var existing = button.transform.Find(ResearchLockOverlayName);
            if (!locked)
            {
                if (existing != null)
                    Destroy(existing.gameObject);
                return;
            }
            if (existing != null) return;

            var go = new GameObject(ResearchLockOverlayName, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TooltipTrigger));
            go.transform.SetParent(button.transform, false);
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1f, 1f, 1f, 0.02f);
            image.raycastTarget = true;
            go.GetComponent<TooltipTrigger>().SetContent(TooltipTexts.Research.LockedHeader, TooltipTexts.Research.LockedContent);
        }
    }
}
