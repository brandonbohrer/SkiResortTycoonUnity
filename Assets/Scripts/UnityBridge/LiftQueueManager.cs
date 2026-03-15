using System.Collections.Generic;
using SkiResortTycoon.Core;
using UnityEngine;

namespace SkiResortTycoon.UnityBridge
{
    /// <summary>
    /// Runtime lift queue state for physical, per-trail queues.
    /// Each lift can have many feeder queues (one per incoming trail).
    /// Boarding alternates by skip-empty round-robin across feeder queues.
    /// </summary>
    public class LiftQueueManager
    {
        private sealed class SkierAssignment
        {
            public int LiftId;
            public int FeederTrailId;
        }

        private sealed class FeederQueueState
        {
            public int TrailId;
            public TrailData Trail;
            public readonly List<int> SkierIds = new List<int>();
            public float BoardDistanceAlongTrail;
            public bool BoardDistanceInitialized;
        }

        private sealed class LiftQueueState
        {
            public LiftData Lift;
            public readonly Dictionary<int, FeederQueueState> FeederQueues = new Dictionary<int, FeederQueueState>();
            public int RoundRobinCursor;
        }

        private readonly Dictionary<int, LiftQueueState> _liftStates = new Dictionary<int, LiftQueueState>();
        private readonly Dictionary<int, SkierAssignment> _skierAssignments = new Dictionary<int, SkierAssignment>();
        private readonly System.Func<Vector3, float?> _terrainHeightSampler;

        private const float SlotSpacingMeters = 3.0f;
        private const float BoardOffsetMeters = 1.5f;
        private const float LodgeAvoidancePaddingMeters = 1.0f;
        private const int MaxLodgeAvoidanceSteps = 12;

        public LiftQueueManager(System.Func<Vector3, float?> terrainHeightSampler)
        {
            _terrainHeightSampler = terrainHeightSampler;
        }

        public void EnsureSkierQueued(int skierId, LiftData lift, int feederTrailId, TrailData feederTrail)
        {
            if (lift == null)
                return;

            RemoveSkier(skierId);

            LiftQueueState liftState = GetOrCreateLiftState(lift);
            FeederQueueState feederState = GetOrCreateFeederQueue(liftState, feederTrailId, feederTrail);

            feederState.SkierIds.Add(skierId);
            _skierAssignments[skierId] = new SkierAssignment
            {
                LiftId = lift.LiftId,
                FeederTrailId = feederTrailId
            };
        }

        public void RemoveSkier(int skierId)
        {
            if (!_skierAssignments.TryGetValue(skierId, out SkierAssignment assignment))
                return;

            _skierAssignments.Remove(skierId);

            if (!_liftStates.TryGetValue(assignment.LiftId, out LiftQueueState liftState))
                return;

            if (!liftState.FeederQueues.TryGetValue(assignment.FeederTrailId, out FeederQueueState feederState))
                return;

            feederState.SkierIds.Remove(skierId);
        }

        public bool TryGetAssignedSlotWorldPosition(int skierId, out Vector3 slotWorldPos)
        {
            slotWorldPos = Vector3.zero;

            if (!_skierAssignments.TryGetValue(skierId, out SkierAssignment assignment))
                return false;
            if (!_liftStates.TryGetValue(assignment.LiftId, out LiftQueueState liftState))
                return false;
            if (!liftState.FeederQueues.TryGetValue(assignment.FeederTrailId, out FeederQueueState feederState))
                return false;

            int queueIndex = feederState.SkierIds.IndexOf(skierId);
            if (queueIndex < 0)
                return false;

            slotWorldPos = ComputeSlotWorldPosition(liftState, feederState, queueIndex);
            return true;
        }

        public bool CanSkierAttemptBoarding(int skierId, int liftId)
        {
            if (!_skierAssignments.TryGetValue(skierId, out SkierAssignment assignment))
                return false;
            if (assignment.LiftId != liftId)
                return false;
            if (!_liftStates.TryGetValue(liftId, out LiftQueueState liftState))
                return false;

            if (!TryGetRoundRobinFront(liftState, out int selectedTrailId, out int frontSkierId))
                return false;
            if (assignment.FeederTrailId != selectedTrailId)
                return false;

            return frontSkierId == skierId;
        }

        public void NotifySkierBoarded(int skierId)
        {
            if (!_skierAssignments.TryGetValue(skierId, out SkierAssignment assignment))
                return;
            if (!_liftStates.TryGetValue(assignment.LiftId, out LiftQueueState liftState))
                return;
            if (!liftState.FeederQueues.TryGetValue(assignment.FeederTrailId, out FeederQueueState feederState))
                return;

            if (feederState.SkierIds.Count > 0 && feederState.SkierIds[0] == skierId)
            {
                feederState.SkierIds.RemoveAt(0);
            }
            else
            {
                feederState.SkierIds.Remove(skierId);
            }

            _skierAssignments.Remove(skierId);
            AdvanceRoundRobinCursorAfterService(liftState, assignment.FeederTrailId);
        }

        private LiftQueueState GetOrCreateLiftState(LiftData lift)
        {
            if (_liftStates.TryGetValue(lift.LiftId, out LiftQueueState existing))
            {
                existing.Lift = lift;
                return existing;
            }

            var created = new LiftQueueState
            {
                Lift = lift,
                RoundRobinCursor = 0
            };
            _liftStates[lift.LiftId] = created;
            return created;
        }

        private FeederQueueState GetOrCreateFeederQueue(LiftQueueState liftState, int feederTrailId, TrailData feederTrail)
        {
            if (liftState.FeederQueues.TryGetValue(feederTrailId, out FeederQueueState existing))
            {
                if (feederTrail != null)
                    existing.Trail = feederTrail;
                return existing;
            }

            var created = new FeederQueueState
            {
                TrailId = feederTrailId,
                Trail = feederTrail
            };
            liftState.FeederQueues[feederTrailId] = created;
            return created;
        }

        private static List<int> GetOrderedTrailIds(LiftQueueState liftState)
        {
            var ids = new List<int>(liftState.FeederQueues.Keys);
            ids.Sort();
            return ids;
        }

        private static bool TryGetRoundRobinFront(LiftQueueState liftState, out int selectedTrailId, out int frontSkierId)
        {
            selectedTrailId = -1;
            frontSkierId = -1;

            List<int> orderedIds = GetOrderedTrailIds(liftState);
            int queueCount = orderedIds.Count;
            if (queueCount == 0)
                return false;

            int start = Mathf.Clamp(liftState.RoundRobinCursor, 0, queueCount - 1);
            for (int i = 0; i < queueCount; i++)
            {
                int idx = (start + i) % queueCount;
                int trailId = orderedIds[idx];
                FeederQueueState feeder = liftState.FeederQueues[trailId];
                if (feeder.SkierIds.Count == 0)
                    continue;

                selectedTrailId = trailId;
                frontSkierId = feeder.SkierIds[0];
                return true;
            }

            return false;
        }

        private static void AdvanceRoundRobinCursorAfterService(LiftQueueState liftState, int servedTrailId)
        {
            List<int> orderedIds = GetOrderedTrailIds(liftState);
            int queueCount = orderedIds.Count;
            if (queueCount == 0)
            {
                liftState.RoundRobinCursor = 0;
                return;
            }

            int servedIndex = orderedIds.IndexOf(servedTrailId);
            if (servedIndex < 0)
            {
                liftState.RoundRobinCursor = Mathf.Clamp(liftState.RoundRobinCursor, 0, queueCount - 1);
                return;
            }

            liftState.RoundRobinCursor = (servedIndex + 1) % queueCount;
        }

        private Vector3 ComputeSlotWorldPosition(LiftQueueState liftState, FeederQueueState feederState, int queueIndex)
        {
            if (feederState.Trail == null || feederState.Trail.WorldPathPoints == null || feederState.Trail.WorldPathPoints.Count < 2)
            {
                return ComputeFallbackSlotPosition(liftState.Lift, queueIndex);
            }

            if (!feederState.BoardDistanceInitialized)
            {
                Vector3 boardRef = new Vector3(
                    liftState.Lift.StartPosition.X,
                    liftState.Lift.StartPosition.Y,
                    liftState.Lift.StartPosition.Z
                );
                feederState.BoardDistanceAlongTrail = SkierMotionController.FindClosestDistanceOnTrail(boardRef, feederState.Trail);
                feederState.BoardDistanceInitialized = true;
            }

            float rawDistance = feederState.BoardDistanceAlongTrail - BoardOffsetMeters - queueIndex * SlotSpacingMeters;
            Vector3 slotPos = rawDistance >= 0f
                ? SampleTrailCenterlineAtDistance(feederState.Trail, rawDistance)
                : SampleTrailStartExtension(feederState.Trail, -rawDistance);
            slotPos = AvoidLodgeFootprints(feederState.Trail, Mathf.Max(0f, rawDistance), slotPos);
            return GroundToTerrain(slotPos);
        }

        private Vector3 ComputeFallbackSlotPosition(LiftData lift, int queueIndex)
        {
            Vector3 liftStart = new Vector3(lift.StartPosition.X, lift.StartPosition.Y, lift.StartPosition.Z);
            Vector3 liftEnd = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
            Vector3 dir = (liftEnd - liftStart);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;
            else
                dir.Normalize();

            Vector3 pos = liftStart - dir * (BoardOffsetMeters + queueIndex * SlotSpacingMeters);
            return GroundToTerrain(pos);
        }

        private Vector3 SampleTrailCenterlineAtDistance(TrailData trail, float distance)
        {
            var pts = trail.WorldPathPoints;
            if (pts == null || pts.Count == 0)
                return Vector3.zero;
            if (pts.Count == 1)
                return V3f(pts[0]);

            float totalLen = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
                totalLen += Vector3.Distance(V3f(pts[i]), V3f(pts[i + 1]));

            if (totalLen <= 0.001f)
                return V3f(pts[0]);

            float remaining = Mathf.Clamp(distance, 0f, totalLen);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = V3f(pts[i]);
                Vector3 b = V3f(pts[i + 1]);
                float segLen = Vector3.Distance(a, b);
                if (segLen <= 0.001f)
                    continue;
                if (remaining <= segLen)
                {
                    float t = remaining / segLen;
                    return Vector3.Lerp(a, b, t);
                }
                remaining -= segLen;
            }

            return V3f(pts[pts.Count - 1]);
        }

        private static Vector3 SampleTrailStartExtension(TrailData trail, float overflowDistance)
        {
            var pts = trail.WorldPathPoints;
            if (pts == null || pts.Count < 2)
                return Vector3.zero;

            Vector3 start = V3f(pts[0]);
            Vector3 next = V3f(pts[1]);
            Vector3 dir = next - start;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;
            else
                dir.Normalize();

            // Extend backward from trail start, preserving queue spacing when demand exceeds trail length.
            return start - dir * overflowDistance;
        }

        private Vector3 AvoidLodgeFootprints(TrailData trail, float startDistance, Vector3 startPos)
        {
            Vector3 candidate = startPos;
            LodgeManager lodgeMgr = LodgeManager.Instance;
            if (lodgeMgr == null || lodgeMgr.AllLodges == null || lodgeMgr.AllLodges.Count == 0)
                return candidate;
            if (trail == null || trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return candidate;

            float dist = startDistance;
            float step = Mathf.Max(0.5f, SlotSpacingMeters * 0.5f);

            for (int i = 0; i < MaxLodgeAvoidanceSteps; i++)
            {
                if (!IsInsideAnyLodgeFootprint(candidate, lodgeMgr))
                    return candidate;

                dist = Mathf.Max(0f, dist - step);
                candidate = SampleTrailCenterlineAtDistance(trail, dist);
            }

            return candidate;
        }

        private static bool IsInsideAnyLodgeFootprint(Vector3 pos, LodgeManager lodgeMgr)
        {
            foreach (var lodge in lodgeMgr.AllLodges)
            {
                if (lodge == null)
                    continue;

                Vector3 lodgePos = lodge.Position;
                Vector2 a = new Vector2(pos.x, pos.z);
                Vector2 b = new Vector2(lodgePos.x, lodgePos.z);
                float radius = lodge.FootprintRadius + LodgeAvoidancePaddingMeters;
                if (Vector2.Distance(a, b) <= radius)
                    return true;
            }
            return false;
        }

        private Vector3 GroundToTerrain(Vector3 pos)
        {
            if (_terrainHeightSampler == null)
                return pos;

            float? y = _terrainHeightSampler(pos);
            if (!y.HasValue)
                return pos;

            pos.y = y.Value;
            return pos;
        }

        private static Vector3 V3f(Vector3f v) => new Vector3(v.X, v.Y, v.Z);
    }
}
