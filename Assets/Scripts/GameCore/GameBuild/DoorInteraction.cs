using UnityEngine;

[DisallowMultipleComponent]
public class DoorInteraction : BuildInteractionBehaviour
{
    private const string DefaultDoorPivotName = "Door";
    private const string DefaultSoundGroup = "Game";

    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openYaw = 110f;
    [SerializeField] private bool invertOpenDirection;
    [SerializeField] private string soundGroup = DefaultSoundGroup;
    [SerializeField] private string openSoundName = "OpenDoor";
    [SerializeField] private string closeSoundName = "CloseDoor";
    [SerializeField] private float soundVolume = 1f;

    private bool isOpen;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = FindChildByName(transform, DefaultDoorPivotName);
        }

        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        SetDoorYaw(0f);
    }

    protected override void Interact()
    {
        isOpen = !isOpen;
        SetDoorYaw(isOpen ? GetOpenYawForPlayerSide() : 0f);
        PlayDoorSound(isOpen ? openSoundName : closeSoundName);
    }

    protected override string GetInteractionPromptText()
    {
        return isOpen ? "关门" : "开门";
    }

    private float GetOpenYawForPlayerSide()
    {
        if (doorPivot == null || PlayerTransform == null)
        {
            return openYaw;
        }

        Vector3 localPlayerPosition = doorPivot.InverseTransformPoint(PlayerTransform.position);
        float direction = localPlayerPosition.z >= 0f ? 1f : -1f;
        if (invertOpenDirection)
        {
            direction *= -1f;
        }

        return openYaw * direction;
    }

    private void SetDoorYaw(float yaw)
    {
        if (doorPivot != null)
        {
            Vector3 euler = doorPivot.localEulerAngles;
            euler.y = yaw;
            doorPivot.localEulerAngles = euler;
        }
    }

    private void PlayDoorSound(string soundName)
    {
        if (GameMgr.Audio == null || string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        Vector3 position = doorPivot != null ? doorPivot.position : transform.position;
        GameMgr.Audio.PlayAt(soundGroup, soundName, position, soundVolume);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
