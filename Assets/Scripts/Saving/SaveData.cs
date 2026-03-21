using System;
using System.Collections.Generic;
using SkiResortTycoon.Core;

namespace SkiResortTycoon.Saving
{
    // ─── DTOs for JSON serialization (Unity JsonUtility requires [Serializable] + public fields) ───

    [Serializable]
    public class Vector3fDto
    {
        public float x;
        public float y;
        public float z;

        public static Vector3fDto From(Vector3f v)
        {
            return new Vector3fDto { x = v.X, y = v.Y, z = v.Z };
        }

        public Vector3f ToVector3f()
        {
            return new Vector3f(x, y, z);
        }
    }

    [Serializable]
    public class TileCoordDto
    {
        public int x;
        public int y;

        public static TileCoordDto From(TileCoord t)
        {
            return new TileCoordDto { x = t.X, y = t.Y };
        }

        public TileCoord ToTileCoord()
        {
            return new TileCoord(x, y);
        }
    }

    [Serializable]
    public class LiftDataDto
    {
        public int liftId;
        public string name;
        public Vector3fDto startPosition;
        public Vector3fDto endPosition;
        public TileCoordDto bottomStation;
        public TileCoordDto topStation;
        public int type; // LiftType enum
        public float length;
        public float elevationGain;
        public int capacity;
        public int buildCost;
        public bool isValid;

        public static LiftDataDto From(LiftData l)
        {
            return new LiftDataDto
            {
                liftId = l.LiftId,
                name = l.Name ?? $"Lift {l.LiftId}",
                startPosition = Vector3fDto.From(l.StartPosition),
                endPosition = Vector3fDto.From(l.EndPosition),
                bottomStation = TileCoordDto.From(l.BottomStation),
                topStation = TileCoordDto.From(l.TopStation),
                type = (int)l.Type,
                length = l.Length,
                elevationGain = l.ElevationGain,
                capacity = l.Capacity,
                buildCost = l.BuildCost,
                isValid = l.IsValid
            };
        }

        public LiftData ToLiftData()
        {
            var lift = new LiftData(liftId)
            {
                Name = name,
                StartPosition = startPosition?.ToVector3f() ?? default,
                EndPosition = endPosition?.ToVector3f() ?? default,
                BottomStation = bottomStation?.ToTileCoord() ?? default,
                TopStation = topStation?.ToTileCoord() ?? default,
                Type = (LiftType)type,
                Length = length,
                ElevationGain = elevationGain,
                Capacity = capacity,
                BuildCost = buildCost,
                IsValid = isValid
            };
            return lift;
        }
    }

    [Serializable]
    public class TrailAnchorPointDto
    {
        public Vector3fDto position;
        public bool hasHandleIn;
        public Vector3fDto handleIn;
        public bool hasHandleOut;
        public Vector3fDto handleOut;
        public int sourceMode; // TrailDrawMode enum
    }

    [Serializable]
    public class TrailDataDto
    {
        public int trailId;
        public string name;
        public List<Vector3fDto> worldPathPoints;
        public List<Vector3fDto> leftBoundaryPoints;
        public List<Vector3fDto> rightBoundaryPoints;
        public float trailWidth;
        public List<TileCoordDto> pathPoints;
        public List<TrailAnchorPointDto> anchors;
        public int difficulty; // TrailDifficulty enum
        public int length;
        public float averageSlope;
        public float maxSlope;
        public float totalElevationDrop;
        public bool isValid;

        public static TrailDataDto From(TrailData t)
        {
            var dto = new TrailDataDto
            {
                trailId = t.TrailId,
                name = t.Name ?? $"Trail {t.TrailId}",
                worldPathPoints = new List<Vector3fDto>(),
                leftBoundaryPoints = new List<Vector3fDto>(),
                rightBoundaryPoints = new List<Vector3fDto>(),
                pathPoints = new List<TileCoordDto>(),
                anchors = new List<TrailAnchorPointDto>(),
                difficulty = (int)t.Difficulty,
                length = t.Length,
                averageSlope = t.AverageSlope,
                maxSlope = t.MaxSlope,
                totalElevationDrop = t.TotalElevationDrop,
                trailWidth = t.TrailWidth,
                isValid = t.IsValid
            };
            if (t.WorldPathPoints != null)
                foreach (var p in t.WorldPathPoints)
                    dto.worldPathPoints.Add(Vector3fDto.From(p));
            if (t.LeftBoundaryPoints != null)
                foreach (var p in t.LeftBoundaryPoints)
                    dto.leftBoundaryPoints.Add(Vector3fDto.From(p));
            if (t.RightBoundaryPoints != null)
                foreach (var p in t.RightBoundaryPoints)
                    dto.rightBoundaryPoints.Add(Vector3fDto.From(p));
            if (t.PathPoints != null)
                foreach (var p in t.PathPoints)
                    dto.pathPoints.Add(TileCoordDto.From(p));
            if (t.Anchors != null)
            {
                foreach (var a in t.Anchors)
                {
                    var adto = new TrailAnchorPointDto
                    {
                        position = Vector3fDto.From(a.Position),
                        sourceMode = (int)a.SourceMode
                    };
                    if (a.HandleIn.HasValue) { adto.hasHandleIn = true; adto.handleIn = Vector3fDto.From(a.HandleIn.Value); }
                    if (a.HandleOut.HasValue) { adto.hasHandleOut = true; adto.handleOut = Vector3fDto.From(a.HandleOut.Value); }
                    dto.anchors.Add(adto);
                }
            }
            return dto;
        }

        public TrailData ToTrailData()
        {
            var trail = new TrailData(trailId);
            trail.Name = name ?? $"Trail {trailId}";
            trail.TrailWidth = trailWidth;
            trail.Difficulty = (TrailDifficulty)difficulty;
            trail.AverageSlope = averageSlope;
            trail.MaxSlope = maxSlope;
            trail.TotalElevationDrop = totalElevationDrop;
            trail.IsValid = isValid;
            if (pathPoints != null)
                foreach (var p in pathPoints)
                    trail.AddPoint(p.ToTileCoord());
            if (worldPathPoints != null)
                foreach (var p in worldPathPoints)
                    trail.AddWorldPoint(p.ToVector3f());
            if (anchors != null)
            {
                foreach (var a in anchors)
                {
                    var anchor = new TrailAnchorPoint(a.position.ToVector3f(), (TrailDrawMode)a.sourceMode);
                    if (a.hasHandleIn && a.handleIn != null) anchor.HandleIn = a.handleIn.ToVector3f();
                    if (a.hasHandleOut && a.handleOut != null) anchor.HandleOut = a.handleOut.ToVector3f();
                    trail.Anchors.Add(anchor);
                }
            }
            if (leftBoundaryPoints != null)
                foreach (var p in leftBoundaryPoints)
                    trail.LeftBoundaryPoints.Add(p.ToVector3f());
            if (rightBoundaryPoints != null)
                foreach (var p in rightBoundaryPoints)
                    trail.RightBoundaryPoints.Add(p.ToVector3f());
            return trail;
        }
    }

    [Serializable]
    public class LodgeDataDto
    {
        public string displayName;
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ, rotW;
        public bool hasBathroom;
        public bool hasFood;
        public bool hasRest;
        public int capacity;
        public float snapRadius;
        public float footprintRadius;
    }

    /// <summary>Per-skier state for save/load. Saves names, skill, needs, progress.</summary>
    [Serializable]
    public class SkierDto
    {
        public int skierId;
        public string displayName;
        public int skill; // SkillLevel enum
        public int currentState; // SkierState
        public int currentLiftId;
        public int currentTrailId;
        public float pathProgress;
        public int runsCompleted;
        public bool wasServed;
        public float timeOnMountain;
        public int desiredRuns;
        public int preferredRunsCompleted;
        // Needs
        public float hunger;
        public float fatigue;
        public float bladder;
        public float satisfaction;
        public float totalWalkingDistance;
        public float totalWaitTime;
        public int unfulfilledNeedAttempts;
        public float timeWithUrgentNeeds;
        public float cumulativePricePenalty;
        public int lodgeVisitCount;
        public int fallCount;
        public int fallsOnMislabeledTrails;
        public float ticketPriceRatio;
        public float skillAccessibleTrailRatio;
        public float preferredAccessibleTrailRatio;
        // World position when saved (for restore-at-place)
        public float worldX, worldY, worldZ;
        // Facing direction (quaternion) so skiers restore same rotation
        public float rotX, rotY, rotZ, rotW;
        // Lift: exact chair and progress (0-1 along lift) so we can restore seat
        public int liftChairIndex;
        public float liftChairProgress;
        // Fall state: exact spot and timer
        public bool isFalling;
        public bool hasFallen;
        public float fallenTimerMinutes;
        public float fallAnimTimer;
        // In lodge: lodge world position (to find it on load) and rest timer
        public float inLodgePosX, inLodgePosY, inLodgePosZ;
        public float lodgeRestTimer;
        // Queue state (when IsQueuedForLift)
        public bool isQueuedForLift;
        public int queuedLiftId;
        public int queuedTrailId;
    }

    /// <summary>One feeder queue at a lift: trail id and ordered skier ids.</summary>
    [Serializable]
    public class FeederQueueSnapshotDto
    {
        public int trailId;
        public List<int> skierIds;
    }

    /// <summary>All feeder queues at one lift, for exact queue restore.</summary>
    [Serializable]
    public class LiftQueueSnapshotDto
    {
        public int liftId;
        public List<FeederQueueSnapshotDto> feeders;
    }

    [Serializable]
    public class SimulationStateDto
    {
        public int dayIndex;
        public float timeMinutes;
        public int visitorsToday;
        public int money;
        public int liftsBuilt;
        public int trailsBuilt;
        public int lodgesBuilt;
        public float todayRevenue;
        public float todayExpenses;
        public float todayLodgeRevenue;
        public float demandMomentum;
        public int consecutiveStrongServiceDays;
        public float smoothedTargetActiveSkiers;
    }

    [Serializable]
    public class DailyFinancialRecordDto
    {
        public int dayIndex;
        public int visitorCount;
        public float fairPrice;
        public float ticketPrice;
        public float ticketRevenue;
        public float lodgeRevenue;
        public float liftExpenses;
        public float lodgeExpenses;
        public float trailExpenses;
    }

    [Serializable]
    public class EconomyDto
    {
        public float ticketPrice;
        public float currentFairPrice;
        public List<DailyFinancialRecordDto> history;
    }

    [Serializable]
    public class TimeControllerDto
    {
        public bool isPaused;
        public float speedMultiplier;
    }

    /// <summary>
    /// Root save payload. Display name for the save is ResortName.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public string resortName;
        public SimulationStateDto simulationState;
        public TimeControllerDto timeController;
        public EconomyDto economy;
        public List<LiftDataDto> lifts;
        public List<TrailDataDto> trails;
        public List<LodgeDataDto> lodges;
        public List<SkierDto> skiers;
        public List<LiftQueueSnapshotDto> liftQueueSnapshots;
    }
}
