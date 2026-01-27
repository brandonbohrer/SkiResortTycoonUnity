using UnityEngine;
using SkiResortTycoon.Core;

/// <summary>
/// Unity bridge that runs the core simulation.
/// This is the ONLY script that can use UnityEngine types.
/// </summary>
public class SimulationRunner : MonoBehaviour
{
    private Simulation _sim;
    private int _lastEndOfDayRevenue = 0;
    
    public Simulation Sim => _sim;
    public int LastEndOfDayRevenue => _lastEndOfDayRevenue;
    
    void Awake()
    {
        // Initialize the simulation with a time speed of 10 minutes per second
        _sim = new Simulation(timeSpeedMinutesPerSecond: 10f);
        
        Debug.Log($"Simulation started. Day {_sim.State.DayIndex}, Money: ${_sim.State.Money}");
    }
    
    void Update()
    {
        if (_sim == null) return;
        
        // Advance the simulation
        bool dayEnded = _sim.Tick(Time.deltaTime);
        
        // If the day just ended, handle end-of-day logic
        if (dayEnded)
        {
            _lastEndOfDayRevenue = _sim.EndDay();
            
            Debug.Log($"Day ended. Revenue: ${_lastEndOfDayRevenue}. Money now: ${_sim.State.Money}. Day: {_sim.State.DayIndex}");
        }
    }
}