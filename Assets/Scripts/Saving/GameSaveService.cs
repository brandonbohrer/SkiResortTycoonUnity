using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SkiResortTycoon.Core;
using SkiResortTycoon.Maps;
using SkiResortTycoon.UnityBridge;

namespace SkiResortTycoon.Saving
{
    /// <summary>
    /// Info for one save slot in the UI (name, day, money, map, path).
    /// </summary>
    public struct SaveSlotInfo
    {
        public string Path;
        public string DisplayName;
        public int Day;
        public int Money;
        public string MapId;
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
        /// Ordered by file last-write time, most recent first.
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
                            Money = data.simulationState.money,
                            MapId = data.mapId
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameSaveService] Could not read save: {path}. {e.Message}");
                }
            }

            list.Sort((a, b) =>
                File.GetLastWriteTimeUtc(b.Path).CompareTo(File.GetLastWriteTimeUtc(a.Path)));

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
        /// Reads the mapId from a save file. Returns LegacyMapId for old saves or on error.
        /// </summary>
        public static string GetMapIdFromSave(string path)
        {
            var data = Load(path);
            if (data == null || string.IsNullOrEmpty(data.mapId))
                return MapRegistry.LegacyMapId;
            return data.mapId;
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
        public static GameSaveData CreateEmptySave(string resortName, string mapId = null)
        {
            return new GameSaveData
            {
                resortName = string.IsNullOrEmpty(resortName) ? "Unnamed Resort" : resortName,
                mapId = string.IsNullOrEmpty(mapId) ? Maps.MapRegistry.LegacyMapId : mapId,
                simulationState = new SimulationStateDto
                {
                    dayIndex = 1,
                    timeMinutes = 540f,
                    visitorsToday = 0,
                    money = SimulationState.DefaultStartingMoney,
                    liftsBuilt = 0,
                    trailsBuilt = 0,
                    lodgesBuilt = 0,
                    todayRevenue = 0f,
                    todayExpenses = 0f,
                    todayLodgeRevenue = 0f,
                    powderDayTargetDay = 0,
                    powderDayModalDone = false,
                    activePowderChoice = 0,
                    powderDemandEventMultiplier = 1f,
                    powderSatisfactionEventMultiplier = 1f,
                    powderDayActiveSkierTargetMultiplier = 1f,
                    powderIntroCompleted = false,
                    visualPowderDayActive = false,
                    lawsuitFirstEventCompleted = false,
                    lawsuitGuaranteedTargetDay = 0,
                    unlockedLiftOneSeatHighSpeed = false,
                    unlockedLiftTwoSeatLowSpeed = false,
                    unlockedLiftTwoSeatHighSpeed = false,
                    liftResearchSlot0Done = false,
                    liftResearchSlot1Done = false,
                    liftResearchSlot2Done = false,
                    liftResearchSlot0CompletionDay = -1,
                    liftResearchSlot1CompletionDay = -1,
                    liftResearchSlot2CompletionDay = -1,
                    liftResearchSlot0PendingUnlockType = -1,
                    liftResearchSlot1PendingUnlockType = -1,
                    liftResearchSlot2PendingUnlockType = -1,
                    liftResearchSlot0PaidAmount = 0,
                    liftResearchSlot1PaidAmount = 0,
                    liftResearchSlot2PaidAmount = 0
                },
                timeController = new TimeControllerDto { isPaused = true, speedMultiplier = 1f },
                economy = new EconomyDto
                {
                    ticketPrice = 30f,
                    currentFairPrice = 50f,
                    history = new List<DailyFinancialRecordDto>()
                },
                lifts = new List<LiftDataDto>(),
                trails = new List<TrailDataDto>(),
                lodges = new List<LodgeDataDto>(),
                skiers = new List<SkierDto>(),
                liftQueueSnapshots = new List<LiftQueueSnapshotDto>()
            };
        }

        /// <summary>
        /// Captures current game state from the running simulation and Unity builders.
        /// Pass skierVisualizer to include skiers (names, skills, needs, progress).
        /// </summary>
        public static GameSaveData CaptureFromGame(
            SimulationRunner runner,
            LiftBuilder liftBuilder,
            TrailDrawer trailDrawer,
            LodgeManager lodgeManager,
            SkierVisualizer skierVisualizer = null)
        {
            var mountainMgr = trailDrawer != null ? trailDrawer.GridRenderer : null;
            var data = new GameSaveData
            {
                mapId = mountainMgr != null ? mountainMgr.ActiveMapId : MapRegistry.LegacyMapId,
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
                todayLodgeRevenue = state.TodayLodgeRevenue,
                demandMomentum = state.DemandMomentum,
                consecutiveStrongServiceDays = state.ConsecutiveStrongServiceDays,
                smoothedTargetActiveSkiers = state.SmoothedTargetActiveSkiers,
                powderDayTargetDay = state.PowderDayTargetDay,
                powderDayModalDone = state.PowderDayModalDone,
                activePowderChoice = (int)state.ActivePowderChoice,
                powderDemandEventMultiplier = state.PowderDemandEventMultiplier,
                powderSatisfactionEventMultiplier = state.PowderSatisfactionEventMultiplier,
                powderDayActiveSkierTargetMultiplier = state.PowderDayActiveSkierTargetMultiplier,
                powderIntroCompleted = state.PowderIntroCompleted,
                visualPowderDayActive = state.VisualPowderDayActive,
                lawsuitFirstEventCompleted = state.LawsuitFirstEventCompleted,
                lawsuitGuaranteedTargetDay = state.LawsuitGuaranteedTargetDay,
                unlockedLiftOneSeatHighSpeed = state.UnlockedLiftOneSeatHighSpeed,
                unlockedLiftTwoSeatLowSpeed = state.UnlockedLiftTwoSeatLowSpeed,
                unlockedLiftTwoSeatHighSpeed = state.UnlockedLiftTwoSeatHighSpeed,
                liftResearchSlot0Done = state.LiftResearchSlot0Done,
                liftResearchSlot1Done = state.LiftResearchSlot1Done,
                liftResearchSlot2Done = state.LiftResearchSlot2Done,
                liftResearchSlot0CompletionDay = state.LiftResearchSlot0CompletionDay,
                liftResearchSlot1CompletionDay = state.LiftResearchSlot1CompletionDay,
                liftResearchSlot2CompletionDay = state.LiftResearchSlot2CompletionDay,
                liftResearchSlot0PendingUnlockType = state.LiftResearchSlot0PendingUnlockType,
                liftResearchSlot1PendingUnlockType = state.LiftResearchSlot1PendingUnlockType,
                liftResearchSlot2PendingUnlockType = state.LiftResearchSlot2PendingUnlockType,
                liftResearchSlot0PaidAmount = state.LiftResearchSlot0PaidAmount,
                liftResearchSlot1PaidAmount = state.LiftResearchSlot1PaidAmount,
                liftResearchSlot2PaidAmount = state.LiftResearchSlot2PaidAmount
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
                        displayName = lodge.gameObject.name,
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

            if (skierVisualizer != null)
                data.skiers = skierVisualizer.CaptureSkiersForSave();
            if (data.skiers == null)
                data.skiers = new List<SkierDto>();

            if (skierVisualizer != null)
                data.liftQueueSnapshots = skierVisualizer.GetLiftQueueSnapshot();
            if (data.liftQueueSnapshots == null)
                data.liftQueueSnapshots = new List<LiftQueueSnapshotDto>();

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
                state.DemandMomentum = data.simulationState.demandMomentum;
                state.ConsecutiveStrongServiceDays = data.simulationState.consecutiveStrongServiceDays;
                state.SmoothedTargetActiveSkiers = data.simulationState.smoothedTargetActiveSkiers;
                state.PowderDayTargetDay = data.simulationState.powderDayTargetDay;
                state.PowderDayModalDone = data.simulationState.powderDayModalDone;
                state.ActivePowderChoice = (PowderDayChoice)data.simulationState.activePowderChoice;
                state.PowderDemandEventMultiplier = data.simulationState.powderDemandEventMultiplier > 0f
                    ? data.simulationState.powderDemandEventMultiplier
                    : 1f;
                state.PowderSatisfactionEventMultiplier = data.simulationState.powderSatisfactionEventMultiplier > 0f
                    ? data.simulationState.powderSatisfactionEventMultiplier
                    : 1f;
                state.PowderDayActiveSkierTargetMultiplier = data.simulationState.powderDayActiveSkierTargetMultiplier > 0f
                    ? data.simulationState.powderDayActiveSkierTargetMultiplier
                    : 1f;
                state.PowderIntroCompleted = data.simulationState.powderIntroCompleted;
                state.VisualPowderDayActive = data.simulationState.visualPowderDayActive;
                state.LawsuitFirstEventCompleted = data.simulationState.lawsuitFirstEventCompleted;
                state.LawsuitGuaranteedTargetDay = data.simulationState.lawsuitGuaranteedTargetDay;
                state.UnlockedLiftOneSeatHighSpeed = data.simulationState.unlockedLiftOneSeatHighSpeed;
                state.UnlockedLiftTwoSeatLowSpeed = data.simulationState.unlockedLiftTwoSeatLowSpeed;
                state.UnlockedLiftTwoSeatHighSpeed = data.simulationState.unlockedLiftTwoSeatHighSpeed;
                state.LiftResearchSlot0Done = data.simulationState.liftResearchSlot0Done;
                state.LiftResearchSlot1Done = data.simulationState.liftResearchSlot1Done;
                state.LiftResearchSlot2Done = data.simulationState.liftResearchSlot2Done;
                state.LiftResearchSlot0CompletionDay = data.simulationState.liftResearchSlot0CompletionDay;
                state.LiftResearchSlot1CompletionDay = data.simulationState.liftResearchSlot1CompletionDay;
                state.LiftResearchSlot2CompletionDay = data.simulationState.liftResearchSlot2CompletionDay;
                state.LiftResearchSlot0PendingUnlockType = data.simulationState.liftResearchSlot0PendingUnlockType;
                state.LiftResearchSlot1PendingUnlockType = data.simulationState.liftResearchSlot1PendingUnlockType;
                state.LiftResearchSlot2PendingUnlockType = data.simulationState.liftResearchSlot2PendingUnlockType;
                state.LiftResearchSlot0PaidAmount = data.simulationState.liftResearchSlot0PaidAmount;
                state.LiftResearchSlot1PaidAmount = data.simulationState.liftResearchSlot1PaidAmount;
                state.LiftResearchSlot2PaidAmount = data.simulationState.liftResearchSlot2PaidAmount;
                MigrateLiftResearchIfOldSave(data.simulationState, state);
                MigratePowderIntroIfOldSave(state);
            }

            if (data.timeController != null && runner.Sim.TimeController != null)
            {
                runner.Sim.TimeController.IsPaused = data.timeController.isPaused;
                runner.Sim.TimeController.SpeedMultiplier = data.timeController.speedMultiplier;
            }
            else if (runner.Sim.TimeController != null)
            {
                // Legacy saves without persisted time control: simulation used to run from the first frame
                runner.Sim.TimeController.Resume();
            }

            if (data.economy != null && runner.Sim.EconomySystem != null)
                runner.Sim.EconomySystem.TicketPricing.TicketPrice = data.economy.ticketPrice;

            var powder = UnityEngine.Object.FindObjectOfType<RandomEventController>();
            powder?.SyncPowderDayUi();

            Debug.Log($"[GameSaveService] Applied save. Day {runner.Sim.State.DayIndex}, Money: ${runner.Sim.State.Money:N0}");
        }

        /// <summary>
        /// Full apply: simulation state + lifts + trails + lodges (with names) + skiers (names, skills, progress).
        /// Call from game scene after LiftBuilder and TrailDrawer have initialized (e.g. after a short delay).
        /// </summary>
        public static void ApplyToGameFull(
            GameSaveData data,
            SimulationRunner runner,
            LiftBuilder liftBuilder,
            TrailDrawer trailDrawer,
            LodgeBuilder lodgeBuilder,
            LodgeManager lodgeManager,
            SkierVisualizer skierVisualizer)
        {
            if (data == null) return;
            ApplyToGame(data, runner);
            if (runner?.Sim == null) return;

            if (data.lifts != null && data.lifts.Count > 0 && liftBuilder != null)
            {
                var liftList = new List<LiftData>();
                foreach (var dto in data.lifts)
                    liftList.Add(dto.ToLiftData());
                liftBuilder.LoadLiftsFromSave(liftList);
            }

            if (data.trails != null && data.trails.Count > 0 && trailDrawer != null && trailDrawer.TrailSystem != null)
            {
                var trailList = new List<TrailData>();
                foreach (var dto in data.trails)
                    trailList.Add(dto.ToTrailData());
                trailDrawer.TrailSystem.LoadTrails(trailList);
                foreach (var trail in trailDrawer.TrailSystem.Trails)
                    trailDrawer.ApplyTrailAfterLoad(trail);
                if (liftBuilder != null && liftBuilder.Connectivity != null)
                    liftBuilder.Connectivity.RebuildConnections();
            }

            if (data.lodges != null && lodgeBuilder != null)
            {
                foreach (var dto in data.lodges)
                {
                    var pos = new Vector3(dto.posX, dto.posY, dto.posZ);
                    var rot = new Quaternion(dto.rotX, dto.rotY, dto.rotZ, dto.rotW);
                    lodgeBuilder.PlaceLodgeFromSave(pos, rot, dto.capacity, dto.hasBathroom, dto.hasFood, dto.hasRest,
                        dto.snapRadius, dto.footprintRadius, dto.displayName);
                }
            }

            if (data.skiers != null && data.skiers.Count > 0 && skierVisualizer != null)
            {
                skierVisualizer.LoadSkiersFromSave(data.skiers);
                if (data.liftQueueSnapshots != null && data.liftQueueSnapshots.Count > 0)
                    skierVisualizer.RestoreLiftQueues(data.liftQueueSnapshots);
            }

            if (skierVisualizer != null)
                skierVisualizer.InvalidateAllSkierGoals();

            Debug.Log($"[GameSaveService] Full apply done: lifts, trails, lodges, skiers restored.");
        }

        /// <summary>
        /// Saves created before lift research used 0 for "missing" ints; treat idle in-progress slots as -1.
        /// </summary>
        private static void MigrateLiftResearchIfOldSave(SimulationStateDto dto, SimulationState state)
        {
            if (dto.liftResearchSlot0CompletionDay == 0 && dto.liftResearchSlot0PaidAmount == 0)
            {
                state.LiftResearchSlot0CompletionDay = -1;
                state.LiftResearchSlot0PendingUnlockType = -1;
            }
            if (dto.liftResearchSlot1CompletionDay == 0 && dto.liftResearchSlot1PaidAmount == 0)
            {
                state.LiftResearchSlot1CompletionDay = -1;
                state.LiftResearchSlot1PendingUnlockType = -1;
            }
            if (dto.liftResearchSlot2CompletionDay == 0 && dto.liftResearchSlot2PaidAmount == 0)
            {
                state.LiftResearchSlot2CompletionDay = -1;
                state.LiftResearchSlot2PendingUnlockType = -1;
            }
        }

        private static void MigratePowderIntroIfOldSave(SimulationState state)
        {
            if (state.PowderIntroCompleted) return;
            if (state.PowderDayTargetDay <= 0) return;
            if (state.PowderDayModalDone && state.DayIndex > state.PowderDayTargetDay)
                state.PowderIntroCompleted = true;
            else if (state.DayIndex > 6)
                state.PowderIntroCompleted = true;
        }
    }
}
