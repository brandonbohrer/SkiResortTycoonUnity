using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.UI
{
    /// <summary>
    /// Drives the Pricing tab in the Manager screen.
    ///
    /// Implemented:
    ///   - Ticket Price display + +/- buttons ($1 increments)
    ///   - Lodge Buffet Price display + +/- buttons ($1 increments)
    ///
    /// Wired but not yet implemented (display only — will show "--"):
    ///   - Ticket Demand
    ///   - Ticket Perception
    ///   - Food Demand
    ///   - Food Satisfaction
    /// </summary>
    public class PricingTabDisplay : MonoBehaviour
    {
        // ── Ticket Price ──────────────────────────────────────────────────
        [Header("Ticket Price")]
        [SerializeField] private TextMeshProUGUI _ticketPriceText;
        [SerializeField] private Button          _ticketPriceUp;
        [SerializeField] private Button          _ticketPriceDown;

        // ── Lodge Buffet Price ────────────────────────────────────────────
        [Header("Lodge Buffet Price")]
        [SerializeField] private TextMeshProUGUI _lodgePriceText;
        [SerializeField] private Button          _lodgePriceUp;
        [SerializeField] private Button          _lodgePriceDown;

        // ── Not yet implemented (display only) ────────────────────────────
        [Header("Not Yet Implemented (display only)")]
        [SerializeField] private TextMeshProUGUI _ticketDemandText;
        [SerializeField] private TextMeshProUGUI _ticketPerceptionText;
        [SerializeField] private TextMeshProUGUI _foodDemandText;
        [SerializeField] private TextMeshProUGUI _foodSatisfactionText;

        // ── References ────────────────────────────────────────────────────
        [Header("References (auto-found if null)")]
        [SerializeField] private SimulationRunner _simulationRunner;
        [SerializeField] private LodgeManager     _lodgeManager;

        private const float PriceStep = 1f;
        private const float MinPrice  = 0f;

        // Stored food price — persists even when no lodges are built yet
        private float _storedFoodPrice = 8f;

        // ────────────────────────────────────────────────────────────────

        void Start()
        {
            if (_simulationRunner == null)
                _simulationRunner = FindObjectOfType<SimulationRunner>();
            if (_lodgeManager == null)
                _lodgeManager = FindObjectOfType<LodgeManager>();

            // Read starting food price from lodges if any exist, else keep default
            if (_lodgeManager != null && _lodgeManager.LodgeCount > 0)
                _storedFoodPrice = _lodgeManager.GlobalFoodPrice;

            // Ticket price buttons
            if (_ticketPriceUp   != null) _ticketPriceUp.onClick.AddListener(TicketPriceUp);
            if (_ticketPriceDown != null) _ticketPriceDown.onClick.AddListener(TicketPriceDown);

            // Lodge buffet price buttons
            if (_lodgePriceUp   != null) _lodgePriceUp.onClick.AddListener(LodgePriceUp);
            if (_lodgePriceDown != null) _lodgePriceDown.onClick.AddListener(LodgePriceDown);

            // Placeholder text for unimplemented fields
            Set(_ticketDemandText,      "--");
            Set(_ticketPerceptionText,  "--");
            Set(_foodDemandText,        "--");
            Set(_foodSatisfactionText,  "--");
        }

        void Update()
        {
            if (_simulationRunner == null || _simulationRunner.Sim == null) return;

            // Late-find lodge manager in case it wasn't ready at Start
            if (_lodgeManager == null)
                _lodgeManager = FindObjectOfType<LodgeManager>();

            float ticketPrice = _simulationRunner.Sim.EconomySystem.TicketPricing.TicketPrice;

            // Sync stored food price to all current lodges
            if (_lodgeManager != null)
                _lodgeManager.GlobalFoodPrice = _storedFoodPrice;

            Set(_ticketPriceText, FormatPrice(ticketPrice));
            Set(_lodgePriceText,  FormatPrice(_storedFoodPrice));
        }

        // ── Button handlers ───────────────────────────────────────────────

        private void TicketPriceUp()
        {
            if (_simulationRunner?.Sim == null) return;
            _simulationRunner.Sim.EconomySystem.TicketPricing.TicketPrice += PriceStep;
        }

        private void TicketPriceDown()
        {
            if (_simulationRunner?.Sim == null) return;
            var pricing = _simulationRunner.Sim.EconomySystem.TicketPricing;
            pricing.TicketPrice = Mathf.Max(MinPrice, pricing.TicketPrice - PriceStep);
        }

        private void LodgePriceUp()
        {
            _storedFoodPrice = Mathf.Min(LodgePricing.MaxFoodPrice, _storedFoodPrice + PriceStep);
        }

        private void LodgePriceDown()
        {
            _storedFoodPrice = Mathf.Max(MinPrice, _storedFoodPrice - PriceStep);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void Set(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }

        private static string FormatPrice(float price)
            => "$" + Mathf.RoundToInt(price).ToString();
    }
}
