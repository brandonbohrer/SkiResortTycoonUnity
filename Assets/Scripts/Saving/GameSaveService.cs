using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.Saving
{
    /// <summary>
    /// Info for one save slot in the UI (name, day, money, path).
    /// </summary>
    public struct SaveSlotInfo
    {
        public string Path;
        public string DisplayName;
        public int Day;
        public int Money;
    }

    /// <summary>
    /// Handles file I/O for game saves and capture/apply of game state.
    /// </summary>
    public static class GameSaveService
    {
        private const string SaveExtension = ".json";
        private const string SavesFolderName = "Saves";

        public static string GetSaveDirectory()
        {
            string dir = Path.Combine(Application.persistentDataPath, SavesFolderName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Returns all save slots (path, display name, day, money). Reads each file to get meta.
        /// </summary>
        public static List<SaveSlotInfo> ListSaves()
        {
            var list = new List<SaveSlotInfo>();
            string dir = GetSaveDirectory();
            if (!Directory.Exists(dir)) return list;

            foreach (string path in Directory.GetFiles(dir, "*" + SaveExtension))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<GameSaveData>(json);
                    if (data?.simulationState != null)
                    {
                        list.Add(new SaveSlotInfo
                        {
                            Path = path,
                            DisplayName = string.IsNullOrEmpty(data.resortName) ? "Unnamed Resort" : data.resortName,
                            Day = data.simulationState.dayIndex,
                            Money = data.simulationState.money
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameSaveService] Could not read save: {path}. {e.Message}");
                }
            }

            return list;
        }

        /// <summary>
        /// Saves data to the given path. Path can be full path or just filename (extension added if missing).
        /// </summary>
        public static void Save(string path, GameSaveData data)
        {
            if (data == null) return;
            string fullPath = path;
            if (!Path.IsPathRooted(fullPath))
                fullPath = Path.Combine(GetSaveDirectory(), fullPath);
            if (!fullPath.EndsWith(SaveExtension, StringComparison.OrdinalIgnoreCase))
                fullPath += SaveExtension;

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(fullPath, json);
        }

        /// <summary>
        /// Loads save data from path. Returns null on error.
        /// </summary>
        public static GameSaveData Load(string path)
        {
            string fullPath = path;
            if (!Path.IsPathRooted(fullPath))
                fullPath = Path.Combine(GetSaveDirectory(), path);
            if (!fullPath.EndsWith(SaveExtension, StringComparison.OrdinalIgnoreCase))
                fullPath += SaveExtension;

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[GameSaveService] Save file not found: {fullPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSaveService] Load failed: {e.Message}");
                return null;
            }
        }

        public static void Delete(string path)
        {
            string fullPath = path;
            if (!Path.IsPathRooted(fullPath))
                fullPath = Path.Combine(GetSaveDirectory(), path);
            if (!fullPath.EndsWith(SaveExtension, StringComparison.OrdinalIgnoreCase))
                fullPath += SaveExtension;

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        /// <summary>
        /// Renames the save by updating the display name (resort name) inside the file.
        /// </summary>
        public static void Rename(string path, string newDisplayName)
        {
            var data = Load(path);
            if (data == null) return;
            data.resortName = newDisplayName ?? "";
            Save(path, data);
        }

        /// <summary>
        /// Returns the path of the most recently modified save file, or null if none exist.
        /// Use for "Continue" to auto-load last played save.
        /// </summary>
        public static string GetMostRecentSavePath()
        {
            string dir = GetSaveDirectory();
            if (!Directory.Exists(dir)) return null;
            string[] files = Directory.GetFiles(dir, "*" + SaveExtension);
            if (files.Length == 0) return null;
            string latest = null;
            DateTime latestTime = DateTime.MinValue;
            foreach (string path in files)
            {
                var time = File.GetLastWriteTimeUtc(path);
                if (time > latestTime)
                {
                    latestTime = time;
                    latest = path;
                }
            }
            return latest;
        }

        /// <summary>
        /// Creates a new empty save (day 1, default money, no lifts/trails/lodges).
        /// Use when user clicks "+ New Game" and Accept on the load menu.
        /// </summary>
        public static GameSaveData CreateEmptySave(string resortName)
        {
            return new GameSaveData
            {
                resortName = string.IsNullOrEmpty(resortName) ? "Unnamed Resort" : resortName,
                simulationState = new SimulationStateDto
                {
                    dayIndex = 1,
                    timeMinutes = 540f,
                    visitorsToday = 0,
                    money = 1000000,
                    liftsBuilt = 0,
                    trailsBuilt = 0,
                    lodgesBuilt = 0,
                    todayRevenue = 0f,
                    todayExpenses = 0f,
                    todayLodgeRevenue = 0f
                },
                timeController = new TimeControllerDto { isPaused = false, speedMultiplier = 1f },
                economy = new EconomyDto
                {
                    ticketPrice = 30f,
                    currentFairPrice = 50f,
                    history = new List<DailyFinancialRecordDto>()
                },
                lifts = new List<LiftDataDto>(),
                trails = new List<TrailDataDto>(),
                lodges = new List<LodgeDataDto>()
            };
        }

        /// <summary>
        /// Captures current game state from the running simulation and Unity builders.
        /// </summary>
        public static GameSaveData CaptureFromGame(
            SimulationRunner runner,
            LiftBuilder liftBuilder,
            TrailDrawer trailDrawer,
            LodgeManager lodgeManager)
        {
            var data = new GameSaveData
            {
                lifts = new List<LiftDataDto>(),
                trails = new List<TrailDataDto>(),
                lodges = new List<LodgeDataDto>(),
                economy = new EconomyDto { history = new List<DailyFinancialRecordDto>() }
            };

            if (runner?.Sim == null) return data;

            SimulationState state = runner.Sim.State;
            data.simulationState = new SimulationStateDto
            {
                dayIndex = state.DayIndex,
                timeMinutes = state.TimeMinutes,
                visitorsToday = state.VisitorsToday,
                money = state.Money,
                liftsBuilt = state.LiftsBuilt,
                trailsBuilt = state.TrailsBuilt,
                lodgesBuilt = state.LodgesBuilt,
                todayRevenue = state.TodayRevenue,
                todayExpenses = state.TodayExpenses,
                todayLodgeRevenue = state.TodayLodgeRevenue
            };

            if (runner.Sim.TimeController != null)
            {
                data.timeController = new TimeControllerDto
                {
                    isPaused = runner.Sim.TimeController.IsPaused,
                    speedMultiplier = runner.Sim.TimeController.SpeedMultiplier
                };
            }

            if (runner.Sim.EconomySystem != null)
            {
                var econ = runner.Sim.EconomySystem;
                data.economy.ticketPrice = econ.TicketPricing.TicketPrice;
                data.economy.currentFairPrice = econ.CurrentFairPrice;
                if (econ.History != null)
                {
                    foreach (var r in econ.History)
                    {
                        data.economy.history.Add(new DailyFinancialRecordDto
                        {
                            dayIndex = r.DayIndex,
                            visitorCount = r.VisitorCount,
                            fairPrice = r.FairPrice,
                            ticketPrice = r.TicketPrice,
                            ticketRevenue = r.TicketRevenue,
                            lodgeRevenue = r.LodgeRevenue,
                            liftExpenses = r.LiftExpenses,
                            lodgeExpenses = r.LodgeExpenses,
                            trailExpenses = r.TrailExpenses
                        });
                    }
                }
            }

            if (liftBuilder?.LiftSystem?.Lifts != null)
            {
                foreach (var lift in liftBuilder.LiftSystem.Lifts)
                    data.lifts.Add(LiftDataDto.From(lift));
            }

            if (trailDrawer?.TrailSystem?.Trails != null)
            {
                foreach (var trail in trailDrawer.TrailSystem.Trails)
                    data.trails.Add(TrailDataDto.From(trail));
            }

            if (lodgeManager?.AllLodges != null)
            {
                foreach (var lodge in lodgeManager.AllLodges)
                {
                    if (lodge == null) continue;
                    var t = lodge.transform;
                    var dto = new LodgeDataDto
                    {
                        posX = t.position.x,
                        posY = t.position.y,
                        posZ = t.position.z,
                        rotX = t.rotation.x,
                        rotY = t.rotation.y,
                        rotZ = t.rotation.z,
                        rotW = t.rotation.w,
                        hasBathroom = lodge.HasBathroom,
                        hasFood = lodge.HasFood,
                        hasRest = lodge.HasRest,
                        capacity = lodge.Capacity,
                        snapRadius = lodge.SnapRadius,
                        footprintRadius = lodge.FootprintRadius
                    };
                    data.lodges.Add(dto);
                }
            }

            return data;
        }

        /// <summary>
        /// Returns the display name for the current game (e.g. for pre-filling "New Save").
        /// Uses the last known resort name if set on SaveGameManager, otherwise empty.
        /// </summary>
        public static string GetCurrentResortNameFromData(SimulationRunner runner)
        {
            if (runner?.Sim?.State == null) return "";
            return "";
        }

        /// <summary>
        /// Applies loaded save data to the running game (simulation state, time, economy).
        /// Call from the game scene when PendingSavePath was set by the main menu.
        /// </summary>
        public static void ApplyToGame(GameSaveData data, SimulationRunner runner)
        {
            if (data == null || runner?.Sim == null) return;

            if (data.simulationState != null)
            {
                var state = runner.Sim.State;
                state.DayIndex = data.simulationState.dayIndex;
                state.TimeMinutes = data.simulationState.timeMinutes;
                state.VisitorsToday = data.simulationState.visitorsToday;
                state.Money = data.simulationState.money;
                state.LiftsBuilt = data.simulationState.liftsBuilt;
                state.TrailsBuilt = data.simulationState.trailsBuilt;
                state.LodgesBuilt = data.simulationState.lodgesBuilt;
                state.TodayRevenue = data.simulationState.todayRevenue;
                state.TodayExpenses = data.simulationState.todayExpenses;
                state.TodayLodgeRevenue = data.simulationState.todayLodgeRevenue;
            }

            if (data.timeController != null && runner.Sim.TimeController != null)
            {
                runner.Sim.TimeController.IsPaused = data.timeController.isPaused;
                runner.Sim.TimeController.SpeedMultiplier = data.timeController.speedMultiplier;
            }

            if (data.economy != null && runner.Sim.EconomySystem != null)
                runner.Sim.EconomySystem.TicketPricing.TicketPrice = data.economy.ticketPrice;

            Debug.Log($"[GameSaveService] Applied save. Day {runner.Sim.State.DayIndex}, Money: ${runner.Sim.State.Money:N0}");
        }
    }
}
