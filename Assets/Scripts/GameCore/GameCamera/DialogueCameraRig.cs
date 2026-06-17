using Cinemachine;
using UnityEngine;

public class DialogueCameraRig : MonoBehaviour
{
    [Header("Dialogue Camera")]
    [SerializeField] private float sideOffset = 1.4f;
    [SerializeField] private float backOffset = 2.6f;
    [SerializeField] private float cameraHeight = 0.72f;
    [SerializeField] private float lookHeight = 0.9f;
    [SerializeField] private float positionLerpSpeed = 10f;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private int dialoguePriority = 100;
    [SerializeField] private float fieldOfView = 35f;

    private Transform playerTarget;
    private Transform npcTarget;
    private Transform lookTarget;
    private CinemachineVirtualCamera dialogueCamera;
    private bool isActive;

    public void BeginDialogue(Transform player, Transform npc)
    {
        if (player == null || npc == null)
        {
            return;
        }

        playerTarget = player;
        npcTarget = npc;

        EnsureRig();
        SnapToDialogueView();

        dialogueCamera.gameObject.SetActive(true);
        dialogueCamera.Priority = dialoguePriority;
        isActive = true;
    }

    public void EndDialogue()
    {
        isActive = false;
        playerTarget = null;
        npcTarget = null;

        if (dialogueCamera != null)
        {
            dialogueCamera.Priority = 0;
            dialogueCamera.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (!isActive || playerTarget == null || npcTarget == null || dialogueCamera == null)
        {
            return;
        }

        UpdateDialogueView(false);
    }

    private void EnsureRig()
    {
        if (lookTarget == null)
        {
            GameObject lookTargetObject = new GameObject("DialogueCameraLookTarget");
            lookTargetObject.transform.SetParent(transform, false);
            lookTarget = lookTargetObject.transform;
        }

        if (dialogueCamera == null)
        {
            GameObject cameraObject = new GameObject("DialogueVirtualCamera");
            cameraObject.transform.SetParent(transform, false);
            dialogueCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            dialogueCamera.LookAt = lookTarget;
            dialogueCamera.m_Lens.FieldOfView = fieldOfView;
            dialogueCamera.Priority = 0;
            cameraObject.SetActive(false);
        }
    }

    private void SnapToDialogueView()
    {
        UpdateDialogueView(true);
    }

    private void UpdateDialogueView(bool snap)
    {
        Vector3 playerFocus = playerTarget.position + Vector3.up * lookHeight;
        Vector3 npcFocus = npcTarget.position + Vector3.up * lookHeight;
        Vector3 midpoint = (playerFocus + npcFocus) * 0.5f;

        Vector3 line = npcTarget.position - playerTarget.position;
        line.y = 0f;
        if (line.sqrMagnitude < 0.001f)
        {
            line = Vector3.forward;
        }

        line.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, line).normalized;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 toCamera = mainCamera.transform.position - midpoint;
            if (Vector3.Dot(toCamera, side) < 0f)
            {
                side = -side;
            }
        }

        Vector3 desiredPosition = midpoint + side * sideOffset - line * backOffset;
        desiredPosition.y = midpoint.y + cameraHeight;

        Quaternion desiredRotation = Quaternion.LookRotation(midpoint - desiredPosition, Vector3.up);

        if (snap)
        {
            lookTarget.position = midpoint;
            dialogueCamera.transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            return;
        }

        lookTarget.position = Vector3.Lerp(lookTarget.position, midpoint, Time.unscaledDeltaTime * positionLerpSpeed);
        dialogueCamera.transform.position = Vector3.Lerp(dialogueCamera.transform.position, desiredPosition, Time.unscaledDeltaTime * positionLerpSpeed);
        dialogueCamera.transform.rotation = Quaternion.Slerp(dialogueCamera.transform.rotation, desiredRotation, Time.unscaledDeltaTime * rotationLerpSpeed);
    }
}
