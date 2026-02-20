using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDriver : MonoBehaviour
{
    Vector2 move;
    private CharacterController controller;
    private GameObject mainCamera;
    public float speed = 6.0f;
    float targetRot = 0.0f;
    public float rotSmoothTime = 0.05f;
    float rotVelocity;
    void Start()
    {
        if(mainCamera == null)
        {
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        controller = GetComponent<CharacterController>();
    }
    void Update()
    {
        Vector3 velocity = new Vector3(0, -1, 0);
        move = GameMgr.input.Data.dirKeyAxis;
        if(move != Vector2.zero)
        {
            Vector3 inputDir = new Vector3(move.x, 0.0f, move.y).normalized;
            targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRot, ref rotVelocity, rotSmoothTime);
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            Vector3 targetDir = Quaternion.Euler(0.0f, targetRot, 0.0f) * Vector3.forward;
            velocity += targetDir.normalized * (speed * Time.deltaTime);
        }
        controller.Move(velocity);
    }
}
