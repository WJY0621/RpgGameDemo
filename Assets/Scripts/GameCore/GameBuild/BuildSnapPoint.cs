using System.Collections.Generic;
using UnityEngine;

public enum BuildSnapPointRole
{
    Socket,
    Anchor
}

[DisallowMultipleComponent]
public class BuildSnapPoint : MonoBehaviour
{
    private static readonly List<BuildSnapPoint> ActiveSnapPoints = new List<BuildSnapPoint>();

    [SerializeField] private BuildSnapPointType snapPointType = BuildSnapPointType.Floor;
    [SerializeField] private BuildSnapPointRole role = BuildSnapPointRole.Socket;
    [SerializeField] private int priority;
    [SerializeField] private float catchRadiusOverride;
    [HideInInspector][SerializeField] private bool usePointRotation = true;
    [HideInInspector][SerializeField] private Vector3 placementOffset;

    public BuildSnapPointType SnapPointType => snapPointType;
    public BuildSnapPointRole Role => role;
    public bool IsSocket => role == BuildSnapPointRole.Socket;
    public bool IsAnchor => role == BuildSnapPointRole.Anchor;
    public int Priority => priority;
    public float CatchRadiusOverride => catchRadiusOverride;
    public bool UsePointRotation => usePointRotation;
    public Vector3 PlacementOffset => placementOffset;
    public static IReadOnlyList<BuildSnapPoint> ActivePoints => ActiveSnapPoints;

    private void OnEnable()
    {
        if (!ActiveSnapPoints.Contains(this))
        {
            ActiveSnapPoints.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveSnapPoints.Remove(this);
    }

    public bool CanAccept(BuildRecipeSO recipe)
    {
        if (recipe == null || !IsSocket)
        {
            return false;
        }

        switch (recipe.buildType)
        {
            case BuildType.Foundation:
                return snapPointType == BuildSnapPointType.Foundation;
            case BuildType.Wall:
            case BuildType.DoorWall:
                return snapPointType == BuildSnapPointType.FoundationEdge ||
                       snapPointType == BuildSnapPointType.WallJoint;
            case BuildType.Board:
                return snapPointType == BuildSnapPointType.WallTop ||
                       snapPointType == BuildSnapPointType.BoardEdge;
            case BuildType.Bed:
            case BuildType.EquipmentStation:
            case BuildType.ConsumableStation:
                return snapPointType == BuildSnapPointType.Floor ||
                       snapPointType == BuildSnapPointType.Furniture;
            default:
                return false;
        }
    }

    public bool CanAnchorTo(BuildSnapPoint socket, BuildRecipeSO recipe)
    {
        if (socket == null || recipe == null || !IsAnchor || !socket.IsSocket)
        {
            return false;
        }

        if (!socket.CanAccept(recipe))
        {
            return false;
        }

        return snapPointType == socket.SnapPointType;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsAnchor ? Color.yellow : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.08f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.35f);
    }
}
