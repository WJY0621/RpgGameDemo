using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerContext
{
    public Vector3 move;
    public float moveSpeed;
    public float ySpeed;
    public Vector2 look;
    public bool grounded;
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f; 
    public float accel = 40f;
    public float jumpSpeed = 6f;
    public float fallSpeed = -15f;
    public bool jumpPressed;
    public Animator anim;
    public CharacterController cc;
    public int priority = 8;

    public bool isMoving = false;
    public bool isLeftPressed;
    public bool isRightPressed;
}

