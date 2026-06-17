using UnityEngine;
using System.Collections.Generic;

public struct BuildPlacementResult
{
    public bool isValid;
    public bool showPreview;
    public Vector3 position;
    public Quaternion rotation;
    public string reason;
    public BuildSnapPoint snapPoint;
}

public static class BuildPlacementValidator
{
    private const float OverlapSkin = 0.035f;
    private static readonly Collider[] OverlapResults = new Collider[32];
    private static readonly RaycastHit[] SupportHits = new RaycastHit[32];
    private static BuildSnapPoint lockedSnapPoint;
    private static BuildSnapPoint lockedAnchorPoint;
    private static string lockedRecipeId;
    private static Quaternion lockedSnapRotation = Quaternion.identity;

    private struct SnapCandidate
    {
        public BuildSnapPoint point;
        public BuildSnapPoint anchor;
        public Quaternion rotation;
        public Vector3 position;
        public float score;
        public bool blocked;
    }

    public static void ResetSnapLock()
    {
        ClearLockedSnapPoint();
    }

    public static BuildPlacementResult Validate(
        BuildRecipeSO recipe,
        Ray placementRay,
        float maxDistance,
        LayerMask groundLayer,
        LayerMask blockingLayer,
        float yaw,
        float heightOffset,
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders = null,
        IReadOnlyList<BuildSnapPoint> previewAnchors = null)
    {
        BuildPlacementResult result = new BuildPlacementResult
        {
            isValid = false,
            showPreview = true,
            rotation = Quaternion.Euler(0f, yaw, 0f),
            reason = "No recipe"
        };

        if (recipe == null)
        {
            return result;
        }

        recipe.ApplyBuildTypeDefaults();
        if (ShouldHideFoundationPreviewOverExistingFoundation(recipe, placementRay, maxDistance, groundLayer, blockingLayer))
        {
            result.showPreview = false;
            result.reason = "Foundation occupied";
            return result;
        }

        Vector3 aimPoint = GetAimPoint(placementRay, maxDistance, groundLayer, blockingLayer);

        if (TryFindSnapPoint(
                recipe,
                placementRay,
                maxDistance,
                aimPoint,
                blockingLayer,
                previewTransform,
                previewColliders,
                previewAnchors,
                out BuildSnapPoint snapPoint,
                out BuildSnapPoint snapAnchor,
                out Quaternion snapRotation,
                out Vector3 snapPosition))
        {
            result.snapPoint = snapPoint;
            result.rotation = snapRotation;
            result.position = snapPosition;
            MovePreviewForCheck(previewTransform, result.position, result.rotation);
            BuildableObject supportBuildable = snapPoint.GetComponentInParent<BuildableObject>();
            Transform supportRoot = supportBuildable != null ? supportBuildable.transform : null;
            result.isValid = !IsBlocked(recipe, result.position, result.rotation, blockingLayer, previewTransform, previewColliders, supportRoot, null, true, out result.reason);
            return result;
        }

        if (recipe.placementRule == BuildPlacementRule.SnapPointOnly)
        {
            result.position = aimPoint;
            result.rotation = Quaternion.Euler(0f, yaw, 0f);
            result.reason = "Need snap point";
            return result;
        }

        Collider supportCollider = null;
        if (recipe.requireGround)
        {
            if (!TryFindSupportHit(recipe, placementRay, maxDistance, groundLayer, out RaycastHit hit))
            {
                result.reason = "No support";
                return result;
            }

            supportCollider = hit.collider;
            Quaternion placementRotation = recipe.alignToGroundNormal
                ? Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, yaw, 0f)
                : Quaternion.Euler(0f, yaw, 0f);

            Vector3 position = hit.point;
            if (recipe.snapToGrid && recipe.gridSize > 0f)
            {
                position = Snap(position, recipe.gridSize);
            }

            float groundPlacementOffset = GetGroundPlacementOffset(
                recipe,
                previewTransform,
                previewColliders,
                hit.point,
                placementRotation);
            position.y = hit.point.y + groundPlacementOffset + heightOffset;

            result.position = position;
            result.rotation = placementRotation;
        }
        else
        {
            result.position = placementRay.origin + placementRay.direction * Mathf.Max(1f, maxDistance * 0.5f);
            result.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        Transform supportTransform = supportCollider != null ? supportCollider.transform : null;
        MovePreviewForCheck(previewTransform, result.position, result.rotation);
        if (IsBlocked(recipe, result.position, result.rotation, blockingLayer, previewTransform, previewColliders, supportTransform, supportCollider, false, out result.reason))
        {
            return result;
        }

        result.isValid = true;
        result.reason = string.Empty;
        return result;
    }

    private static bool TryFindSupportHit(
        BuildRecipeSO recipe,
        Ray placementRay,
        float maxDistance,
        LayerMask groundLayer,
        out RaycastHit bestHit)
    {
        bestHit = default;
        int hitCount = Physics.RaycastNonAlloc(
            placementRay,
            SupportHits,
            maxDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool hasHit = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = SupportHits[i];
            Collider collider = hit.collider;
            if (collider == null || hit.distance >= bestDistance)
            {
                continue;
            }

            if (collider.GetComponentInParent<PlayerStateDriver>() != null)
            {
                continue;
            }

            if (!IsValidSupport(recipe, collider))
            {
                continue;
            }

            bestDistance = hit.distance;
            bestHit = hit;
            hasHit = true;
        }

        return hasHit;
    }

    private static bool ShouldHideFoundationPreviewOverExistingFoundation(
        BuildRecipeSO recipe,
        Ray placementRay,
        float maxDistance,
        LayerMask groundLayer,
        LayerMask blockingLayer)
    {
        if (recipe == null || recipe.buildType != BuildType.Foundation)
        {
            return false;
        }

        int mask = groundLayer.value | blockingLayer.value;
        int hitCount = Physics.RaycastNonAlloc(
            placementRay,
            SupportHits,
            maxDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        BuildableObject bestBuildable = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = SupportHits[i];
            Collider collider = hit.collider;
            if (collider == null || hit.distance >= bestDistance)
            {
                continue;
            }

            if (collider.GetComponentInParent<PlayerStateDriver>() != null)
            {
                continue;
            }

            BuildableObject buildable = collider.GetComponentInParent<BuildableObject>();
            if (buildable == null)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestBuildable = buildable;
        }

        return bestBuildable != null && bestBuildable.PieceType == BuildPieceType.Foundation;
    }

    private static void MovePreviewForCheck(Transform previewTransform, Vector3 position, Quaternion rotation)
    {
        if (previewTransform != null)
        {
            previewTransform.SetPositionAndRotation(position, rotation);
        }
    }

    private static float GetGroundPlacementOffset(
        BuildRecipeSO recipe,
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders,
        Vector3 groundPoint,
        Quaternion rotation)
    {
        if (recipe.buildType == BuildType.Foundation)
        {
            return 0f;
        }

        if (previewTransform == null)
        {
            return recipe.groundOffset;
        }

        Vector3 originalPosition = previewTransform.position;
        Quaternion originalRotation = previewTransform.rotation;
        previewTransform.SetPositionAndRotation(groundPoint, rotation);

        bool hasBottom = TryGetPreviewBottomY(previewTransform, previewColliders, out float bottomY);
        previewTransform.SetPositionAndRotation(originalPosition, originalRotation);

        if (!hasBottom)
        {
            return recipe.groundOffset;
        }

        return groundPoint.y - bottomY - recipe.groundEmbedDepth;
    }

    private static bool TryGetPreviewBottomY(
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders,
        out float bottomY)
    {
        bottomY = float.MaxValue;
        bool hasBottom = false;

        if (previewColliders != null)
        {
            for (int i = 0; i < previewColliders.Count; i++)
            {
                Collider previewCollider = previewColliders[i];
                if (previewCollider == null)
                {
                    continue;
                }

                if (TryGetColliderBottomY(previewCollider, out float colliderBottomY))
                {
                    bottomY = Mathf.Min(bottomY, colliderBottomY);
                    hasBottom = true;
                }
            }
        }

        if (hasBottom)
        {
            return true;
        }

        Renderer[] renderers = previewTransform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (bounds.size == Vector3.zero)
            {
                continue;
            }

            bottomY = Mathf.Min(bottomY, bounds.min.y);
            hasBottom = true;
        }

        return hasBottom;
    }

    private static bool TryGetColliderBottomY(Collider collider, out float bottomY)
    {
        bottomY = float.MaxValue;
        if (collider is BoxCollider box)
        {
            Vector3 half = box.size * 0.5f;
            Vector3 center = box.center;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localPoint = center + new Vector3(half.x * x, half.y * y, half.z * z);
                        bottomY = Mathf.Min(bottomY, box.transform.TransformPoint(localPoint).y);
                    }
                }
            }

            return true;
        }

        if (collider is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float scale = Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x), Mathf.Abs(sphere.transform.lossyScale.y), Mathf.Abs(sphere.transform.lossyScale.z));
            bottomY = center.y - sphere.radius * scale;
            return true;
        }

        if (collider is CapsuleCollider capsule)
        {
            GetCapsuleWorldPoints(capsule, out Vector3 pointA, out Vector3 pointB, out float radius);
            bottomY = Mathf.Min(pointA.y, pointB.y) - radius;
            return true;
        }

        Bounds bounds = collider.bounds;
        if (bounds.size == Vector3.zero)
        {
            return false;
        }

        bottomY = bounds.min.y;
        return true;
    }

    private static bool IsBlocked(
        BuildRecipeSO recipe,
        Vector3 position,
        Quaternion rotation,
        LayerMask blockingLayer,
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders,
        Transform supportTransform,
        Collider supportCollider,
        bool ignoreWorldForSnapPlacement,
        out string reason)
    {
        reason = string.Empty;
        if (previewColliders != null && previewColliders.Count > 0)
        {
            for (int i = 0; i < previewColliders.Count; i++)
            {
                Collider previewCollider = previewColliders[i];
                if (previewCollider == null)
                {
                    continue;
                }

                int overlapCount = OverlapPreviewCollider(previewCollider, blockingLayer);
                if (HasBlockingOverlap(overlapCount, previewTransform, supportTransform, supportCollider, ignoreWorldForSnapPlacement, out reason))
                {
                    return true;
                }
            }

            return false;
        }

        Vector3 halfExtents = ShrinkHalfExtents(Vector3.Max(recipe.placementSize, Vector3.one * 0.05f) * 0.5f);
        Vector3 checkCenter = position + Vector3.up * halfExtents.y;
        int count = Physics.OverlapBoxNonAlloc(
            checkCenter,
            halfExtents,
            OverlapResults,
            rotation,
            blockingLayer,
            QueryTriggerInteraction.Ignore);

        return HasBlockingOverlap(count, previewTransform, supportTransform, supportCollider, ignoreWorldForSnapPlacement, out reason);
    }

    private static bool HasBlockingOverlap(
        int count,
        Transform previewTransform,
        Transform supportTransform,
        Collider supportCollider,
        bool ignoreWorldForSnapPlacement,
        out string reason)
    {
        reason = string.Empty;
        for (int i = 0; i < count; i++)
        {
            Collider collider = OverlapResults[i];
            if (collider == null)
            {
                continue;
            }

            if (previewTransform != null && collider.transform.IsChildOf(previewTransform))
            {
                continue;
            }

            if (supportTransform != null && (collider.transform == supportTransform || collider.transform.IsChildOf(supportTransform)))
            {
                continue;
            }

            if (supportCollider != null && collider == supportCollider)
            {
                continue;
            }

            if (collider.GetComponentInParent<PlayerStateDriver>() != null)
            {
                reason = "Player is blocking";
                return true;
            }

            if (collider.GetComponentInParent<BuildableObject>() != null)
            {
                reason = "Blocked";
                return true;
            }

            if (ignoreWorldForSnapPlacement)
            {
                continue;
            }

            if (!collider.isTrigger)
            {
                reason = "Blocked";
                return true;
            }
        }

        return false;
    }

    private static int OverlapPreviewCollider(Collider previewCollider, LayerMask blockingLayer)
    {
        if (previewCollider is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = ShrinkHalfExtents(Vector3.Scale(box.size * 0.5f, Abs(box.transform.lossyScale)));
            return Physics.OverlapBoxNonAlloc(center, halfExtents, OverlapResults, box.transform.rotation, blockingLayer, QueryTriggerInteraction.Ignore);
        }

        if (previewCollider is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float scale = Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x), Mathf.Abs(sphere.transform.lossyScale.y), Mathf.Abs(sphere.transform.lossyScale.z));
            return Physics.OverlapSphereNonAlloc(center, ShrinkRadius(sphere.radius * scale), OverlapResults, blockingLayer, QueryTriggerInteraction.Ignore);
        }

        if (previewCollider is CapsuleCollider capsule)
        {
            GetCapsuleWorldPoints(capsule, out Vector3 pointA, out Vector3 pointB, out float radius);
            return Physics.OverlapCapsuleNonAlloc(pointA, pointB, ShrinkRadius(radius), OverlapResults, blockingLayer, QueryTriggerInteraction.Ignore);
        }

        Bounds bounds = previewCollider.bounds;
        return Physics.OverlapBoxNonAlloc(bounds.center, ShrinkHalfExtents(bounds.extents), OverlapResults, Quaternion.identity, blockingLayer, QueryTriggerInteraction.Ignore);
    }

    private static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 pointA, out Vector3 pointB, out float radius)
    {
        Transform transform = capsule.transform;
        Vector3 scale = Abs(transform.lossyScale);
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 axis;
        float axisScale;
        float maxRadiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = transform.right;
                axisScale = scale.x;
                maxRadiusScale = Mathf.Max(scale.y, scale.z);
                break;
            case 1:
                axis = transform.up;
                axisScale = scale.y;
                maxRadiusScale = Mathf.Max(scale.x, scale.z);
                break;
            default:
                axis = transform.forward;
                axisScale = scale.z;
                maxRadiusScale = Mathf.Max(scale.x, scale.y);
                break;
        }

        radius = capsule.radius * maxRadiusScale;
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        pointA = center + axis * halfSegment;
        pointB = center - axis * halfSegment;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static Vector3 ShrinkHalfExtents(Vector3 halfExtents)
    {
        return new Vector3(
            ShrinkExtent(halfExtents.x),
            ShrinkExtent(halfExtents.y),
            ShrinkExtent(halfExtents.z));
    }

    private static float ShrinkExtent(float extent)
    {
        return Mathf.Max(0.01f, extent - Mathf.Min(OverlapSkin, extent * 0.45f));
    }

    private static float ShrinkRadius(float radius)
    {
        return Mathf.Max(0.01f, radius - Mathf.Min(OverlapSkin, radius * 0.45f));
    }

    private static bool IsValidSupport(BuildRecipeSO recipe, Collider collider)
    {
        BuildableObject supportBuildable = collider != null ? collider.GetComponentInParent<BuildableObject>() : null;
        switch (recipe.placementRule)
        {
            case BuildPlacementRule.GroundOnly:
                return supportBuildable == null;
            case BuildPlacementRule.FloorOnly:
                return supportBuildable != null &&
                       (supportBuildable.PieceType == BuildPieceType.Foundation || supportBuildable.PieceType == BuildPieceType.Board);
            case BuildPlacementRule.GroundOrFloor:
                return supportBuildable == null ||
                       supportBuildable.PieceType == BuildPieceType.Foundation ||
                       supportBuildable.PieceType == BuildPieceType.Board;
            case BuildPlacementRule.SnapPointOnly:
                return false;
            default:
                return false;
        }
    }

    private static bool TryFindSnapPoint(
        BuildRecipeSO recipe,
        Ray placementRay,
        float maxDistance,
        Vector3 aimPoint,
        LayerMask blockingLayer,
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders,
        IReadOnlyList<BuildSnapPoint> previewAnchors,
        out BuildSnapPoint bestPoint,
        out BuildSnapPoint bestAnchor,
        out Quaternion bestRotation,
        out Vector3 bestPosition)
    {
        bestPoint = null;
        bestAnchor = null;
        bestRotation = Quaternion.identity;
        bestPosition = Vector3.zero;
        if (recipe == null || !recipe.preferSnapPoint)
        {
            ClearLockedSnapPoint();
            return false;
        }

        IReadOnlyList<BuildSnapPoint> points = BuildSnapPoint.ActivePoints;
        IReadOnlyList<BuildSnapPoint> anchors = previewAnchors ?? GetPreviewAnchors(previewTransform);
        if (anchors == null || anchors.Count == 0)
        {
            ClearLockedSnapPoint();
            return false;
        }

        SnapCandidate bestValidCandidate = default;
        SnapCandidate bestBlockedCandidate = default;
        bool hasValidCandidate = false;
        bool hasBlockedCandidate = false;
        float maxPointDistance = GetSnapCatchRadius(recipe);
        float releaseDistance = GetSnapReleaseDistance(recipe, maxPointDistance);
        float releaseRayDistance = GetSnapReleaseRayDistance(recipe, releaseDistance);
        Vector3 rayDirection = placementRay.direction.normalized;

        if (TryKeepLockedSnapPoint(
                recipe,
                placementRay,
                maxDistance,
                aimPoint,
                releaseDistance,
                releaseRayDistance,
                previewTransform,
                previewColliders,
                out bestPoint,
                out bestAnchor,
                out bestRotation,
                out bestPosition))
        {
            return true;
        }

        for (int i = 0; i < points.Count; i++)
        {
            BuildSnapPoint point = points[i];
            if (point == null || IsPreviewSnapPoint(point, previewTransform) || !point.CanAccept(recipe))
            {
                continue;
            }

            Vector3 pointPosition = point.transform.position;
            float pointCatchRadius = GetPointCatchRadius(recipe, point);
            float pointRayCatchRadius = GetPointRayCatchRadius(recipe, pointCatchRadius);
            float rayDepth = Vector3.Dot(pointPosition - placementRay.origin, rayDirection);
            if (rayDepth < -0.1f || rayDepth > maxDistance + pointCatchRadius)
            {
                continue;
            }

            Vector3 closest = ClosestPointOnRay(placementRay, point.transform.position, maxDistance);
            float rayDistance = Vector3.Distance(closest, pointPosition);
            float aimDistance = Vector3.Distance(aimPoint, pointPosition);
            if (aimDistance > pointCatchRadius && rayDistance > pointRayCatchRadius)
            {
                continue;
            }

            float depthPenalty = Mathf.Abs(Vector3.Distance(placementRay.origin, aimPoint) - rayDepth) * 0.08f;
            int rotationCount = GetSnapRotationCount(recipe, point);
            for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
            {
                BuildSnapPoint anchor = anchors[anchorIndex];
                if (anchor == null || !anchor.CanAnchorTo(point, recipe))
                {
                    continue;
                }

                for (int rotationIndex = 0; rotationIndex < rotationCount; rotationIndex++)
                {
                    Quaternion candidateRotation = GetSnapRotation(recipe, point, anchor, previewTransform, rotationIndex);
                    if (!IsSnapRotationAllowed(recipe, point, candidateRotation))
                    {
                        continue;
                    }

                    Vector3 candidatePosition = GetSnapPlacementPosition(recipe, point, anchor, candidateRotation, previewTransform);
                    if (IsSnapPlacementOccupied(recipe, candidatePosition, candidateRotation, previewTransform))
                    {
                        continue;
                    }

                    float orientationPenalty = GetSnapOrientationPenalty(recipe, pointPosition, candidateRotation, aimPoint, placementRay);
                    float placementIntentPenalty = Vector3.Distance(candidatePosition, aimPoint) * GetPlacementIntentWeight(recipe);
                    float score = aimDistance * 0.55f +
                                  rayDistance * 0.28f +
                                  depthPenalty +
                                  orientationPenalty +
                                  placementIntentPenalty +
                                  GetSnapTypePenalty(recipe, point) -
                                  point.Priority * 0.08f -
                                  anchor.Priority * 0.04f;
                    bool blocked = IsSnapCandidateBlocked(
                        recipe,
                        point,
                        candidatePosition,
                        candidateRotation,
                        blockingLayer,
                        previewTransform,
                        previewColliders);
                    SnapCandidate candidate = new SnapCandidate
                    {
                        point = point,
                        anchor = anchor,
                        rotation = candidateRotation,
                        position = candidatePosition,
                        score = score,
                        blocked = blocked
                    };

                    if (!candidate.blocked)
                    {
                        if (!hasValidCandidate || candidate.score < bestValidCandidate.score)
                        {
                            bestValidCandidate = candidate;
                            hasValidCandidate = true;
                        }
                    }
                    else if (!hasBlockedCandidate || candidate.score < bestBlockedCandidate.score)
                    {
                        bestBlockedCandidate = candidate;
                        hasBlockedCandidate = true;
                    }
                }
            }
        }

        if (hasValidCandidate)
        {
            bestPoint = bestValidCandidate.point;
            bestAnchor = bestValidCandidate.anchor;
            bestRotation = bestValidCandidate.rotation;
            bestPosition = bestValidCandidate.position;
            LockSnapPoint(recipe, bestPoint, bestAnchor, bestRotation);
        }
        else if (hasBlockedCandidate)
        {
            bestPoint = bestBlockedCandidate.point;
            bestAnchor = bestBlockedCandidate.anchor;
            bestRotation = bestBlockedCandidate.rotation;
            bestPosition = bestBlockedCandidate.position;
            LockSnapPoint(recipe, bestPoint, bestAnchor, bestRotation);
        }
        else
        {
            ClearLockedSnapPoint();
        }

        return bestPoint != null;
    }

    private static bool IsPreviewSnapPoint(BuildSnapPoint point, Transform previewTransform)
    {
        return point != null &&
               previewTransform != null &&
               (point.transform == previewTransform || point.transform.IsChildOf(previewTransform));
    }

    private static BuildSnapPoint[] GetPreviewAnchors(Transform previewTransform)
    {
        if (previewTransform == null)
        {
            return new BuildSnapPoint[0];
        }

        BuildSnapPoint[] points = previewTransform.GetComponentsInChildren<BuildSnapPoint>(true);
        List<BuildSnapPoint> anchors = new List<BuildSnapPoint>();
        for (int i = 0; i < points.Length; i++)
        {
            BuildSnapPoint point = points[i];
            if (point != null && point.IsAnchor)
            {
                anchors.Add(point);
            }
        }

        return anchors.ToArray();
    }

    private static bool TryKeepLockedSnapPoint(
        BuildRecipeSO recipe,
        Ray placementRay,
        float maxDistance,
        Vector3 aimPoint,
        float releaseDistance,
        float releaseRayDistance,
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders,
        out BuildSnapPoint snapPoint,
        out BuildSnapPoint snapAnchor,
        out Quaternion snapRotation,
        out Vector3 snapPosition)
    {
        snapPoint = null;
        snapAnchor = null;
        snapRotation = Quaternion.identity;
        snapPosition = Vector3.zero;
        if (lockedSnapPoint == null ||
            lockedAnchorPoint == null ||
            lockedSnapPoint.gameObject == null ||
            lockedAnchorPoint.gameObject == null ||
            !string.Equals(lockedRecipeId, recipe.recipeId, System.StringComparison.Ordinal) ||
            !lockedSnapPoint.CanAccept(recipe) ||
            !lockedAnchorPoint.CanAnchorTo(lockedSnapPoint, recipe))
        {
            ClearLockedSnapPoint();
            return false;
        }

        Vector3 pointPosition = lockedSnapPoint.transform.position;
        Vector3 closest = ClosestPointOnRay(placementRay, pointPosition, maxDistance);
        float aimDistance = Vector3.Distance(aimPoint, pointPosition);
        float rayDistance = Vector3.Distance(closest, pointPosition);

        if (aimDistance <= releaseDistance || rayDistance <= releaseRayDistance)
        {
            if (!IsSnapRotationAllowed(recipe, lockedSnapPoint, lockedSnapRotation))
            {
                ClearLockedSnapPoint();
                return false;
            }

            Vector3 position = GetSnapPlacementPosition(recipe, lockedSnapPoint, lockedAnchorPoint, lockedSnapRotation, previewTransform);
            if (IsSnapPlacementOccupied(recipe, position, lockedSnapRotation, previewTransform))
            {
                ClearLockedSnapPoint();
                return false;
            }

            snapPoint = lockedSnapPoint;
            snapAnchor = lockedAnchorPoint;
            snapRotation = lockedSnapRotation;
            snapPosition = position;
            return true;
        }

        ClearLockedSnapPoint();
        return false;
    }

    private static bool IsSnapCandidateBlocked(
        BuildRecipeSO recipe,
        BuildSnapPoint point,
        Vector3 position,
        Quaternion rotation,
        LayerMask blockingLayer,
        Transform previewTransform,
        IReadOnlyList<Collider> previewColliders)
    {
        if (point == null)
        {
            return true;
        }

        MovePreviewForCheck(previewTransform, position, rotation);
        BuildableObject supportBuildable = point.GetComponentInParent<BuildableObject>();
        Transform supportRoot = supportBuildable != null ? supportBuildable.transform : null;
        return IsBlocked(recipe, position, rotation, blockingLayer, previewTransform, previewColliders, supportRoot, null, true, out _);
    }

    private static bool IsSnapPlacementOccupied(
        BuildRecipeSO recipe,
        Vector3 position,
        Quaternion rotation,
        Transform previewTransform)
    {
        if (recipe == null)
        {
            return true;
        }

        IReadOnlyList<BuildableObject> buildables = BuildableObject.ActiveObjects;
        for (int i = 0; i < buildables.Count; i++)
        {
            BuildableObject buildable = buildables[i];
            if (buildable == null || !IsSamePieceType(recipe, buildable))
            {
                continue;
            }

            if (previewTransform != null && (buildable.transform == previewTransform || buildable.transform.IsChildOf(previewTransform)))
            {
                continue;
            }

            if (IsSameOccupiedSlot(recipe, position, rotation, buildable.transform))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSamePieceType(BuildRecipeSO recipe, BuildableObject buildable)
    {
        if (recipe == null || buildable == null)
        {
            return false;
        }

        return buildable.PieceType == recipe.pieceType;
    }

    private static bool IsSameOccupiedSlot(
        BuildRecipeSO recipe,
        Vector3 position,
        Quaternion rotation,
        Transform placedTransform)
    {
        if (recipe == null || placedTransform == null)
        {
            return false;
        }

        Vector3 delta = placedTransform.position - position;
        float verticalTolerance = GetSlotVerticalTolerance(recipe);
        if (Mathf.Abs(delta.y) > verticalTolerance)
        {
            return false;
        }

        delta.y = 0f;
        if (delta.sqrMagnitude > GetSlotHorizontalTolerance(recipe) * GetSlotHorizontalTolerance(recipe))
        {
            return false;
        }

        if (!IsWallLike(recipe))
        {
            return true;
        }

        Vector3 candidateForward = rotation * Vector3.forward;
        Vector3 placedForward = placedTransform.forward;
        candidateForward.y = 0f;
        placedForward.y = 0f;
        if (candidateForward.sqrMagnitude < 0.0001f || placedForward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        candidateForward.Normalize();
        placedForward.Normalize();
        return Mathf.Abs(Vector3.Dot(candidateForward, placedForward)) > 0.85f;
    }

    private static float GetSlotHorizontalTolerance(BuildRecipeSO recipe)
    {
        switch (recipe.buildType)
        {
            case BuildType.Wall:
            case BuildType.DoorWall:
                return Mathf.Max(0.25f, Mathf.Min(recipe.placementSize.x, recipe.placementSize.z) * 0.75f);
            case BuildType.Foundation:
            case BuildType.Board:
                return 0.35f;
            default:
                return 0.25f;
        }
    }

    private static float GetSlotVerticalTolerance(BuildRecipeSO recipe)
    {
        switch (recipe.buildType)
        {
            case BuildType.Wall:
            case BuildType.DoorWall:
                return Mathf.Max(0.25f, recipe.placementSize.y * 0.2f);
            case BuildType.Foundation:
            case BuildType.Board:
                return 0.35f;
            default:
                return 0.25f;
        }
    }

    private static Vector3 GetSnapPlacementPosition(
        BuildRecipeSO recipe,
        BuildSnapPoint socket,
        BuildSnapPoint anchor,
        Quaternion rotation,
        Transform previewTransform)
    {
        Vector3 socketPosition = socket.transform.position + socket.PlacementOffset;
        Vector3 anchorLocalPosition = GetAnchorLocalPosition(anchor, previewTransform);
        Vector3 anchorScaledLocalPosition = ScalePreviewLocalPosition(anchorLocalPosition, previewTransform);
        Vector3 snapOffset = recipe != null ? recipe.snapOffset : Vector3.zero;
        return socketPosition - rotation * anchorScaledLocalPosition + snapOffset;
    }

    private static Vector3 ScalePreviewLocalPosition(Vector3 localPosition, Transform previewTransform)
    {
        if (previewTransform == null)
        {
            return localPosition;
        }

        Vector3 scale = previewTransform.lossyScale;
        return new Vector3(
            localPosition.x * scale.x,
            localPosition.y * scale.y,
            localPosition.z * scale.z);
    }

    private static Vector3 GetAnchorLocalPosition(BuildSnapPoint anchor, Transform previewTransform)
    {
        if (anchor == null)
        {
            return Vector3.zero;
        }

        if (previewTransform != null && anchor.transform.IsChildOf(previewTransform))
        {
            return previewTransform.InverseTransformPoint(anchor.transform.position);
        }

        return anchor.transform.localPosition;
    }

    private static Quaternion GetAnchorLocalRotation(BuildSnapPoint anchor, Transform previewTransform)
    {
        if (anchor == null)
        {
            return Quaternion.identity;
        }

        if (previewTransform != null && anchor.transform.IsChildOf(previewTransform))
        {
            return Quaternion.Inverse(previewTransform.rotation) * anchor.transform.rotation;
        }

        return anchor.transform.localRotation;
    }

    private static float GetPointCatchRadius(BuildRecipeSO recipe, BuildSnapPoint point)
    {
        float radius = point != null && point.CatchRadiusOverride > 0f
            ? point.CatchRadiusOverride
            : GetSnapCatchRadius(recipe);

        switch (point != null ? point.SnapPointType : BuildSnapPointType.Floor)
        {
            case BuildSnapPointType.WallJoint:
                return IsWallLike(recipe)
                    ? Mathf.Min(Mathf.Max(radius, 0.9f), 1.35f)
                    : Mathf.Max(radius, 1.35f);
            case BuildSnapPointType.FoundationEdge:
                return IsWallLike(recipe)
                    ? Mathf.Min(Mathf.Max(radius, 0.85f), 1.2f)
                    : Mathf.Max(radius, 1.6f);
            case BuildSnapPointType.WallTop:
            case BuildSnapPointType.BoardEdge:
                return recipe != null && recipe.buildType == BuildType.Board
                    ? Mathf.Min(Mathf.Max(radius, 1.0f), 1.55f)
                    : Mathf.Max(radius, 1.55f);
            default:
                return radius;
        }
    }

    private static float GetPointRayCatchRadius(BuildRecipeSO recipe, float pointCatchRadius)
    {
        if (recipe == null)
        {
            return pointCatchRadius * 0.9f;
        }

        switch (recipe.buildType)
        {
            case BuildType.Wall:
            case BuildType.DoorWall:
                return pointCatchRadius * 0.55f;
            case BuildType.Board:
                return pointCatchRadius * 0.75f;
            default:
                return pointCatchRadius * 0.9f;
        }
    }

    private static float GetSnapTypePenalty(BuildRecipeSO recipe, BuildSnapPoint point)
    {
        if (recipe == null || point == null)
        {
            return 0f;
        }

        if (point.SnapPointType == recipe.targetSnapPointType)
        {
            return -0.18f;
        }

        if (IsWallLike(recipe))
        {
            switch (point.SnapPointType)
            {
                case BuildSnapPointType.WallJoint:
                    return -0.28f;
                case BuildSnapPointType.FoundationEdge:
                    return -0.16f;
            }
        }

        if (recipe.buildType == BuildType.Board)
        {
            switch (point.SnapPointType)
            {
                case BuildSnapPointType.WallTop:
                    return -0.22f;
                case BuildSnapPointType.BoardEdge:
                    return -0.14f;
            }
        }

        return 0f;
    }

    private static float GetPlacementIntentWeight(BuildRecipeSO recipe)
    {
        if (recipe == null)
        {
            return 0f;
        }

        switch (recipe.buildType)
        {
            case BuildType.Board:
                return 0.35f;
            case BuildType.Wall:
            case BuildType.DoorWall:
                return 0.18f;
            default:
                return 0.08f;
        }
    }

    private static bool IsSnapRotationAllowed(BuildRecipeSO recipe, BuildSnapPoint point, Quaternion rotation)
    {
        if (recipe == null || point == null)
        {
            return false;
        }

        if (!IsWallLike(recipe))
        {
            return true;
        }

        Vector3 forward = rotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        forward.Normalize();
        Vector3 pointForward = point.transform.forward;
        pointForward.y = 0f;
        if (pointForward.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        pointForward.Normalize();
        Vector3 pointRight = point.transform.right;
        pointRight.y = 0f;
        if (pointRight.sqrMagnitude > 0.0001f)
        {
            pointRight.Normalize();
        }

        switch (point.SnapPointType)
        {
            case BuildSnapPointType.FoundationEdge:
                return Vector3.Dot(forward, pointForward) > 0.92f;
            case BuildSnapPointType.WallJoint:
            case BuildSnapPointType.Wall:
                return Mathf.Abs(Vector3.Dot(forward, pointForward)) > 0.08f ||
                       Mathf.Abs(Vector3.Dot(forward, pointRight)) > 0.08f;
            default:
                return true;
        }
    }

    private static int GetSnapRotationCount(BuildRecipeSO recipe, BuildSnapPoint point)
    {
        if (recipe.buildType == BuildType.Board && point.SnapPointType == BuildSnapPointType.WallTop)
        {
            return 2;
        }

        if (!IsWallLike(recipe))
        {
            return 1;
        }

        if (point.SnapPointType == BuildSnapPointType.WallJoint)
        {
            return 4;
        }

        return 1;
    }

    private static Quaternion GetSnapRotation(
        BuildRecipeSO recipe,
        BuildSnapPoint point,
        BuildSnapPoint anchor,
        Transform previewTransform,
        int rotationIndex)
    {
        Quaternion baseRotation = point.transform.rotation;
        if (recipe.buildType == BuildType.Board && point.SnapPointType == BuildSnapPointType.WallTop)
        {
            baseRotation *= Quaternion.Euler(0f, 180f * rotationIndex, 0f);
        }
        else if (IsWallLike(recipe) && GetSnapRotationCount(recipe, point) > 1)
        {
            baseRotation *= Quaternion.Euler(0f, 90f * rotationIndex, 0f);
        }

        return baseRotation * Quaternion.Inverse(GetAnchorLocalRotation(anchor, previewTransform));
    }

    private static float GetSnapOrientationPenalty(
        BuildRecipeSO recipe,
        Vector3 pointPosition,
        Quaternion candidateRotation,
        Vector3 aimPoint,
        Ray placementRay)
    {
        if (!IsWallLike(recipe))
        {
            return 0f;
        }

        Vector3 intent = aimPoint - pointPosition;
        intent.y = 0f;
        if (intent.sqrMagnitude < 0.04f)
        {
            intent = placementRay.direction;
            intent.y = 0f;
        }

        if (intent.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        intent.Normalize();
        Vector3 forward = candidateRotation * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        forward.Normalize();
        float alignment = Vector3.Dot(forward, intent);
        return (1f - alignment) * 0.35f;
    }

    private static float GetSnapCatchRadius(BuildRecipeSO recipe)
    {
        float configuredRadius = Mathf.Max(0.1f, recipe.snapSearchRadius);
        switch (recipe.buildType)
        {
            case BuildType.Wall:
            case BuildType.DoorWall:
                return Mathf.Min(Mathf.Max(0.85f, configuredRadius), 1.25f);
            case BuildType.Board:
                return Mathf.Min(Mathf.Max(1f, configuredRadius), 1.55f);
            case BuildType.Foundation:
                return Mathf.Max(2f, configuredRadius);
            default:
                return Mathf.Max(1.6f, configuredRadius);
        }
    }

    private static float GetSnapReleaseDistance(BuildRecipeSO recipe, float catchRadius)
    {
        if (recipe == null)
        {
            return catchRadius * 1.65f;
        }

        switch (recipe.buildType)
        {
            case BuildType.Wall:
            case BuildType.DoorWall:
                return catchRadius * 1.15f;
            case BuildType.Board:
                return catchRadius * 1.35f;
            default:
                return catchRadius * 1.65f;
        }
    }

    private static float GetSnapReleaseRayDistance(BuildRecipeSO recipe, float releaseDistance)
    {
        if (recipe == null)
        {
            return releaseDistance * 0.9f;
        }

        switch (recipe.buildType)
        {
            case BuildType.Wall:
            case BuildType.DoorWall:
                return releaseDistance * 0.6f;
            case BuildType.Board:
                return releaseDistance * 0.75f;
            default:
                return releaseDistance * 0.9f;
        }
    }

    private static void LockSnapPoint(BuildRecipeSO recipe, BuildSnapPoint snapPoint, BuildSnapPoint anchorPoint, Quaternion snapRotation)
    {
        lockedSnapPoint = snapPoint;
        lockedAnchorPoint = anchorPoint;
        lockedRecipeId = recipe != null ? recipe.recipeId : string.Empty;
        lockedSnapRotation = snapRotation;
    }

    private static void ClearLockedSnapPoint()
    {
        lockedSnapPoint = null;
        lockedAnchorPoint = null;
        lockedRecipeId = string.Empty;
        lockedSnapRotation = Quaternion.identity;
    }

    private static bool IsWallLike(BuildRecipeSO recipe)
    {
        return recipe != null && (recipe.buildType == BuildType.Wall || recipe.buildType == BuildType.DoorWall);
    }

    private static Vector3 GetAimPoint(
        Ray placementRay,
        float maxDistance,
        LayerMask groundLayer,
        LayerMask blockingLayer)
    {
        int mask = groundLayer.value | blockingLayer.value;
        if (Physics.Raycast(placementRay, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return placementRay.origin + placementRay.direction.normalized * Mathf.Max(1f, maxDistance * 0.5f);
    }

    private static Vector3 ClosestPointOnRay(Ray ray, Vector3 point, float maxDistance)
    {
        float distance = Vector3.Dot(point - ray.origin, ray.direction.normalized);
        distance = Mathf.Clamp(distance, 0f, maxDistance);
        return ray.origin + ray.direction.normalized * distance;
    }

    private static Vector3 Snap(Vector3 position, float gridSize)
    {
        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;
        return position;
    }
}
