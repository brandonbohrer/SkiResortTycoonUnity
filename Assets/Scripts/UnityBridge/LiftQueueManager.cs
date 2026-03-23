using System.Collections.Generic;
using SkiResortTycoon.Core;
using SkiResortTycoon.Saving;
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
            public readonly Dictionary<int, int> SkierIndices = new Dictionary<int, int>();
            public float BoardDistanceAlongTrail;
            public bool BoardDistanceInitialized;
            public float[] SegmentCumulativeDistances;
            public float TotalTrailLength;
            public int CachedTrailPointCount;
        }

        private sealed class LiftQueueState
        {
            public LiftData Lift;
            public readonly Dictionary<int, FeederQueueState> FeederQueues = new Dictionary<int, FeederQueueState>();
            public int RoundRobinCursor;
        }

        private readonly Dictionary<int, LiftQueueState> _liftStates = new Dictionary<int, LiftQueueState>();
        private readonly Dictionary<int, SkierAssignment> _skierAssignments = new Dictionary<int, SkierAssignment>();
        private readonly Dictionary<int, bool> _skierReadyForBoarding = new Dictionary<int, bool>();
        private readonly System.Func<Vector3, float?> _terrainHeightSampler;

        private const float BoardOffsetMeters = 1.5f;
        private const float LodgeAvoidancePaddingMeters = 1.0f;
        private const int MaxLodgeAvoidanceSteps = 12;

        private const int SlotsPerLane = 5;
        private const float SnakeSlotSpacing = 2.0f;
        private const float SnakeLaneSpacing = 2.5f;
        /// <summary>Lateral spacing between two skiers in the same queue row (2-seat lifts).</summary>
        private const float QueuePairSideSpacing = 0.55f;

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
            feederState.SkierIndices[skierId] = feederState.SkierIds.Count - 1;
            _skierReadyForBoarding[skierId] = false;
            _skierAssignments[skierId] = new SkierAssignment
            {
                LiftId = lift.LiftId,
                FeederTrailId = feederTrailId
            };
        }

        public void SetSkierReadyForBoarding(int skierId, bool isReady)
        {
            if (!_skierAssignments.ContainsKey(skierId))
                return;
            _skierReadyForBoarding[skierId] = isReady;
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

            RemoveSkierFromFeederQueue(feederState, skierId);
            _skierReadyForBoarding.Remove(skierId);
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

            if (!feederState.SkierIndices.TryGetValue(skierId, out int queueIndex) || queueIndex < 0)
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

            if (!TryGetRoundRobinFrontBoarding(liftState, out int selectedTrailId, out bool pairMode, out int primarySkierId))
                return false;
            if (assignment.FeederTrailId != selectedTrailId)
                return false;

            // Pair boarding is driven only by the front skier (index 0); partner is pulled in the same frame.
            return skierId == primarySkierId;
        }

        /// <summary>
        /// If this skier is the primary front skier and a pair load is ready, returns the second skier id.
        /// </summary>
        public bool TryGetPairBoardingPartner(int skierId, int liftId, out int partnerSkierId)
        {
            partnerSkierId = -1;
            if (!_skierAssignments.TryGetValue(skierId, out SkierAssignment assignment))
                return false;
            if (assignment.LiftId != liftId)
                return false;
            if (!_liftStates.TryGetValue(liftId, out LiftQueueState liftState))
                return false;
            if (LiftTypeSpecs.GetSeatsPerChair(liftState.Lift.Type) < 2)
                return false;
            if (!TryGetRoundRobinFrontBoarding(liftState, out int selectedTrailId, out bool pairMode, out int primarySkierId))
                return false;
            if (assignment.FeederTrailId != selectedTrailId || skierId != primarySkierId || !pairMode)
                return false;
            if (!liftState.FeederQueues.TryGetValue(selectedTrailId, out FeederQueueState feeder))
                return false;
            if (feeder.SkierIds.Count < 2)
                return false;
            partnerSkierId = feeder.SkierIds[1];
            return true;
        }

        /// <summary>Clear all queue state (for restore-from-save).</summary>
        public void ClearAll()
        {
            _skierAssignments.Clear();
            _skierReadyForBoarding.Clear();
            _liftStates.Clear();
        }

        /// <summary>
        /// Removes all queue state for a demolished lift.
        /// Any skiers assigned to this lift are unassigned so they can re-route.
        /// </summary>
        public void RemoveLift(int liftId)
        {
            if (!_liftStates.TryGetValue(liftId, out LiftQueueState liftState))
                return;

            foreach (var feeder in liftState.FeederQueues.Values)
            {
                for (int i = 0; i < feeder.SkierIds.Count; i++)
                {
                    int skierId = feeder.SkierIds[i];
                    _skierAssignments.Remove(skierId);
                    _skierReadyForBoarding.Remove(skierId);
                }
                feeder.SkierIds.Clear();
                feeder.SkierIndices.Clear();
            }

            _liftStates.Remove(liftId);
        }

        /// <summary>Snapshot current queue state for save. Order per feeder is preserved.</summary>
        public List<LiftQueueSnapshotDto> GetSnapshot()
        {
            var list = new List<LiftQueueSnapshotDto>();
            foreach (var kv in _liftStates)
            {
                var dto = new LiftQueueSnapshotDto { liftId = kv.Key, feeders = new List<FeederQueueSnapshotDto>() };
                foreach (var fq in kv.Value.FeederQueues)
                {
                    if (fq.Value.SkierIds.Count == 0) continue;
                    dto.feeders.Add(new FeederQueueSnapshotDto { trailId = fq.Key, skierIds = new List<int>(fq.Value.SkierIds) });
                }
                if (dto.feeders.Count > 0)
                    list.Add(dto);
            }
            return list;
        }

        /// <summary>Restore queue state from save. Call after skiers are spawned. getLift/getTrail can be null to skip missing refs.</summary>
        public void RestoreSnapshot(List<LiftQueueSnapshotDto> snapshot, System.Func<int, LiftData> getLift, System.Func<int, TrailData> getTrail)
        {
            if (snapshot == null || getLift == null || getTrail == null) return;
            ClearAll();
            foreach (var liftSnap in snapshot)
            {
                var lift = getLift(liftSnap.liftId);
                if (lift == null) continue;
                if (liftSnap.feeders == null) continue;
                foreach (var feeder in liftSnap.feeders)
                {
                    var trail = getTrail(feeder.trailId);
                    if (feeder.skierIds == null) continue;
                    foreach (int skierId in feeder.skierIds)
                        EnsureSkierQueued(skierId, lift, feeder.trailId, trail);
                }
            }
        }

        public void NotifySkierBoarded(int skierId)
        {
            if (!_skierAssignments.TryGetValue(skierId, out SkierAssignment assignment))
                return;
            if (!_liftStates.TryGetValue(assignment.LiftId, out LiftQueueState liftState))
                return;
            if (!liftState.FeederQueues.TryGetValue(assignment.FeederTrailId, out FeederQueueState feederState))
                return;

            RemoveSkierFromFeederQueue(feederState, skierId);

            _skierAssignments.Remove(skierId);
            _skierReadyForBoarding.Remove(skierId);
            AdvanceRoundRobinCursorAfterService(liftState, assignment.FeederTrailId);
        }

        /// <summary>Remove two front skiers from the same feeder and advance round-robin once (pair load).</summary>
        public void NotifyPairBoarded(int skierIdA, int skierIdB)
        {
            if (!_skierAssignments.TryGetValue(skierIdA, out SkierAssignment a))
                return;
            if (!_skierAssignments.TryGetValue(skierIdB, out SkierAssignment b))
                return;
            if (a.LiftId != b.LiftId || a.FeederTrailId != b.FeederTrailId)
                return;
            if (!_liftStates.TryGetValue(a.LiftId, out LiftQueueState liftState))
                return;
            if (!liftState.FeederQueues.TryGetValue(a.FeederTrailId, out FeederQueueState feederState))
                return;

            RemoveSkierFromFeederQueue(feederState, skierIdA);
            RemoveSkierFromFeederQueue(feederState, skierIdB);

            _skierAssignments.Remove(skierIdA);
            _skierAssignments.Remove(skierIdB);
            _skierReadyForBoarding.Remove(skierIdA);
            _skierReadyForBoarding.Remove(skierIdB);

            AdvanceRoundRobinCursorAfterService(liftState, a.FeederTrailId);
        }

        private static void RemoveSkierFromFeederQueue(FeederQueueState feederState, int skierId)
        {
            if (feederState == null) return;
            if (!feederState.SkierIndices.TryGetValue(skierId, out int idx))
                return;

            feederState.SkierIds.RemoveAt(idx);
            feederState.SkierIndices.Remove(skierId);

            // Preserve FIFO ordering: shift index map for everyone after removed slot.
            for (int i = idx; i < feederState.SkierIds.Count; i++)
            {
                feederState.SkierIndices[feederState.SkierIds[i]] = i;
            }
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

        private bool TryGetRoundRobinFrontBoarding(LiftQueueState liftState, out int selectedTrailId, out bool pairMode, out int primarySkierId)
        {
            selectedTrailId = -1;
            pairMode = false;
            primarySkierId = -1;

            List<int> orderedIds = GetOrderedTrailIds(liftState);
            int queueCount = orderedIds.Count;
            if (queueCount == 0)
                return false;

            int seatsPerChair = LiftTypeSpecs.GetSeatsPerChair(liftState.Lift.Type);
            int start = Mathf.Clamp(liftState.RoundRobinCursor, 0, queueCount - 1);
            for (int i = 0; i < queueCount; i++)
            {
                int idx = (start + i) % queueCount;
                int trailId = orderedIds[idx];
                FeederQueueState feeder = liftState.FeederQueues[trailId];
                if (feeder.SkierIds.Count == 0)
                    continue;

                int skier0 = feeder.SkierIds[0];
                if (!_skierReadyForBoarding.TryGetValue(skier0, out bool ready0) || !ready0)
                    continue;

                if (seatsPerChair < 2)
                {
                    selectedTrailId = trailId;
                    pairMode = false;
                    primarySkierId = skier0;
                    return true;
                }

                // 2-seat lifts: pair only when two skiers are at the front row and both ready; else solo if alone.
                if (feeder.SkierIds.Count >= 2)
                {
                    int skier1 = feeder.SkierIds[1];
                    if (_skierReadyForBoarding.TryGetValue(skier1, out bool ready1) && ready1)
                    {
                        selectedTrailId = trailId;
                        pairMode = true;
                        primarySkierId = skier0;
                        return true;
                    }

                    // Second skier not ready yet — wait so they can load together when applicable.
                    continue;
                }

                selectedTrailId = trailId;
                pairMode = false;
                primarySkierId = skier0;
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

        private static int QueueSeatsPerRow(LiftData lift)
        {
            if (lift == null) return 1;
            return Mathf.Max(1, LiftTypeSpecs.GetSeatsPerChair(lift.Type));
        }

        private Vector3 ComputeSlotWorldPosition(LiftQueueState liftState, FeederQueueState feederState, int queueIndex)
        {
            int spr = QueueSeatsPerRow(liftState.Lift);
            int rowAlong = queueIndex / spr;
            int seatInRow = queueIndex % spr;

            if (feederState.Trail == null || feederState.Trail.WorldPathPoints == null || feederState.Trail.WorldPathPoints.Count < 2)
            {
                return ComputeFallbackSlotPosition(liftState.Lift, rowAlong, seatInRow, spr);
            }

            EnsureTrailCache(feederState);

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

            Vector3 liftBottom = new Vector3(
                liftState.Lift.StartPosition.X,
                liftState.Lift.StartPosition.Y,
                liftState.Lift.StartPosition.Z
            );
            Vector3 trailContact = SampleTrailCenterlineAtDistance(feederState, feederState.BoardDistanceAlongTrail);

            Vector3 toTrail = trailContact - liftBottom;
            toTrail.y = 0f;
            float bridgeDist = toTrail.magnitude;
            Vector3 bridgeDir = bridgeDist > 0.01f ? toTrail / bridgeDist : Vector3.zero;
            Vector3 bridgePerp = Vector3.Cross(Vector3.up, bridgeDir).normalized;
            if (bridgePerp.sqrMagnitude < 0.001f)
                bridgePerp = Vector3.right;

            int bridgeSlots = Mathf.Max(0, Mathf.FloorToInt(bridgeDist / SnakeSlotSpacing));

            if (rowAlong <= bridgeSlots)
            {
                float distFromLift = rowAlong * SnakeSlotSpacing;
                Vector3 pos = liftBottom + bridgeDir * Mathf.Min(distFromLift, bridgeDist);
                if (spr > 1)
                    pos += bridgePerp * (seatInRow - (spr - 1) * 0.5f) * QueuePairSideSpacing;
                return GroundToTerrain(pos);
            }

            int snakeIndex = rowAlong - bridgeSlots - 1;
            int lane = snakeIndex / SlotsPerLane;
            int posInLane = snakeIndex % SlotsPerLane;

            float alongOffset = (lane % 2 == 0)
                ? posInLane * SnakeSlotSpacing
                : (SlotsPerLane - 1 - posInLane) * SnakeSlotSpacing;

            float rawDistance = feederState.BoardDistanceAlongTrail - alongOffset;
            Vector3 slotPos = rawDistance >= 0f
                ? SampleTrailCenterlineAtDistance(feederState, rawDistance)
                : SampleTrailStartExtension(feederState.Trail, -rawDistance);

            Vector3 trailDirForPair = ComputeTrailDirectionAtBoarding(feederState);
            Vector3 perpDir = Vector3.Cross(Vector3.up, trailDirForPair).normalized;
            if (perpDir.sqrMagnitude < 0.001f)
                perpDir = Vector3.right;

            if (lane > 0)
                slotPos += perpDir * lane * SnakeLaneSpacing;

            if (spr > 1)
                slotPos += perpDir * (seatInRow - (spr - 1) * 0.5f) * QueuePairSideSpacing;

            slotPos = AvoidLodgeFootprints(feederState, Mathf.Max(0f, rawDistance), slotPos);
            return GroundToTerrain(slotPos);
        }

        private Vector3 ComputeTrailDirectionAtBoarding(FeederQueueState feederState)
        {
            float boardDist = feederState.BoardDistanceAlongTrail;
            float sampleOffset = 2f;
            Vector3 p1 = SampleTrailCenterlineAtDistance(feederState, Mathf.Max(0f, boardDist - sampleOffset));
            Vector3 p2 = SampleTrailCenterlineAtDistance(feederState, boardDist + sampleOffset);
            Vector3 dir = p2 - p1;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            return dir.normalized;
        }

        private Vector3 ComputeFallbackSlotPosition(LiftData lift, int rowAlong, int seatInRow, int spr)
        {
            Vector3 liftStart = new Vector3(lift.StartPosition.X, lift.StartPosition.Y, lift.StartPosition.Z);
            Vector3 liftEnd = new Vector3(lift.EndPosition.X, lift.EndPosition.Y, lift.EndPosition.Z);
            Vector3 dir = (liftEnd - liftStart);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;
            else
                dir.Normalize();

            int lane = rowAlong / SlotsPerLane;
            int posInLane = rowAlong % SlotsPerLane;
            float alongOffset = (lane % 2 == 0)
                ? posInLane * SnakeSlotSpacing
                : (SlotsPerLane - 1 - posInLane) * SnakeSlotSpacing;

            Vector3 perpDir = Vector3.Cross(Vector3.up, dir).normalized;
            if (perpDir.sqrMagnitude < 0.001f)
                perpDir = Vector3.right;

            Vector3 pos = liftStart - dir * (BoardOffsetMeters + alongOffset) + perpDir * lane * SnakeLaneSpacing;
            if (spr > 1)
                pos += perpDir * (seatInRow - (spr - 1) * 0.5f) * QueuePairSideSpacing;
            return GroundToTerrain(pos);
        }

        private void EnsureTrailCache(FeederQueueState feederState)
        {
            if (feederState == null || feederState.Trail == null || feederState.Trail.WorldPathPoints == null)
                return;

            var pts = feederState.Trail.WorldPathPoints;
            int pointCount = pts.Count;
            if (pointCount < 2)
            {
                feederState.SegmentCumulativeDistances = null;
                feederState.TotalTrailLength = 0f;
                feederState.CachedTrailPointCount = pointCount;
                return;
            }

            if (feederState.SegmentCumulativeDistances != null && feederState.CachedTrailPointCount == pointCount)
                return;

            int segCount = pointCount - 1;
            if (feederState.SegmentCumulativeDistances == null || feederState.SegmentCumulativeDistances.Length != segCount)
                feederState.SegmentCumulativeDistances = new float[segCount];

            float total = 0f;
            for (int i = 0; i < segCount; i++)
            {
                total += Vector3.Distance(V3f(pts[i]), V3f(pts[i + 1]));
                feederState.SegmentCumulativeDistances[i] = total;
            }

            feederState.TotalTrailLength = total;
            feederState.CachedTrailPointCount = pointCount;
        }

        private Vector3 SampleTrailCenterlineAtDistance(FeederQueueState feederState, float distance)
        {
            TrailData trail = feederState != null ? feederState.Trail : null;
            var pts = trail.WorldPathPoints;
            if (pts == null || pts.Count == 0)
                return Vector3.zero;
            if (pts.Count == 1)
                return V3f(pts[0]);

            EnsureTrailCache(feederState);
            float totalLen = feederState.TotalTrailLength;

            if (totalLen <= 0.001f)
                return V3f(pts[0]);

            float remaining = Mathf.Clamp(distance, 0f, totalLen);
            var cumulative = feederState.SegmentCumulativeDistances;
            int lo = 0;
            int hi = cumulative.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (cumulative[mid] < remaining) lo = mid + 1;
                else hi = mid;
            }

            int segIdx = lo;
            float segStart = segIdx > 0 ? cumulative[segIdx - 1] : 0f;
            float segLen = cumulative[segIdx] - segStart;
            if (segLen <= 0.001f)
                return V3f(pts[segIdx]);

            float t = Mathf.Clamp01((remaining - segStart) / segLen);
            return Vector3.Lerp(V3f(pts[segIdx]), V3f(pts[segIdx + 1]), t);
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

        private Vector3 AvoidLodgeFootprints(FeederQueueState feederState, float startDistance, Vector3 startPos)
        {
            Vector3 candidate = startPos;
            LodgeManager lodgeMgr = LodgeManager.Instance;
            if (lodgeMgr == null || lodgeMgr.AllLodges == null || lodgeMgr.AllLodges.Count == 0)
                return candidate;
            TrailData trail = feederState != null ? feederState.Trail : null;
            if (trail == null || trail.WorldPathPoints == null || trail.WorldPathPoints.Count < 2)
                return candidate;

            float dist = startDistance;
            float step = Mathf.Max(0.5f, SnakeSlotSpacing * 0.5f);

            for (int i = 0; i < MaxLodgeAvoidanceSteps; i++)
            {
                if (!IsInsideAnyLodgeFootprint(candidate, lodgeMgr))
                    return candidate;

                dist = Mathf.Max(0f, dist - step);
                candidate = SampleTrailCenterlineAtDistance(feederState, dist);
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
