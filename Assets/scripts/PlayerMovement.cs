using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
   [Header("Movement")]
   private float moveSpeed;
   public float walkSpeed;
   public float sprintSpeed;
   public float groundDrag;
   public float wallrunSpeed;

   [Header("Jumping")]
   public float jumpForce;
   public float jumpCooldown;
   public float airMultiplier;
   bool readyToJump;

   [Header("Crouching")]
   public float crouchSpeed;
   public float crouchYScale;
   private float startYScale;

   [Header("Keybinds")]
   public KeyCode jumpkey = KeyCode.Space;
   public KeyCode sprintkey = KeyCode.LeftShift;
   public KeyCode crouchkey = KeyCode.LeftControl;

   [Header("Ground Check")]
   public float playerHeight;
   public LayerMask whatIsGround;
   bool grounded;

   [Header("Slope Handling")]
   public float maxSlopeAngle;
   private RaycastHit slopeHit;
   private bool exitingSlope;

   public Transform orientation;

   float horizontalInput;
   float verticalInput;

   Vector3 moveDirection;

   Rigidbody rb;

   public MovementState state;

   public enum MovementState
   {
      freeze,
      walking,
      sprinting,
      wallrunning,
      crouching,
      air
   }

   public bool freeze;

   public bool activeGrapple;

   public bool wallrunning;

   private void Start()
   {
        readyToJump = true;

        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        startYScale = transform.localScale.y;
   }

   private void Update()
   {
        // ground check
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();

        // handle drag
        if (grounded && !activeGrapple)
          rb.linearDamping = groundDrag;
        else
          rb.linearDamping = 0;

   }

   private void FixedUpdate()
   {
        MovePlayer();
   }


   private void MyInput()
   {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // when to jump
        if(Input.GetKey(jumpkey) && readyToJump && grounded)
        {
          readyToJump = false;

          Jump();

          Invoke(nameof(ResetJump), jumpCooldown);
        }

        // start crouch
        if (Input.GetKeyDown(crouchkey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }

        // stop crouch
        if (Input.GetKeyUp(crouchkey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }

   }

   private void StateHandler()
   {
      // Mode - Freeze
      if (freeze)
      {
          state = MovementState.freeze;
          moveSpeed = 0;
          rb.linearVelocity = Vector3.zero;
      }

      // Mode - WallRunning
      if(wallrunning)
      {
          state = MovementState.wallrunning;
          moveSpeed = wallrunSpeed;
      }

      // Mode - Crouching
      else if (Input.GetKey(crouchkey))
      {
          state = MovementState.crouching;
          moveSpeed = crouchSpeed;
      }

      // Mode - Sprinting
      if(grounded && Input.GetKey(sprintkey))
      {
          state = MovementState.sprinting;
          moveSpeed = sprintSpeed;
      }

      // Mode - walking
      else if (grounded)
      {
          state = MovementState.walking;
          moveSpeed = walkSpeed;
      }

      // mode - air
      else
      {
          state = MovementState.air;
      }
   }

   private void MovePlayer()
   {
        if(activeGrapple) return;

        // Calculate movement Direction
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection() * moveSpeed * 20f, ForceMode.Force);

            if (rb.linearVelocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }

        // on ground
        if(grounded)
          rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // in air
        else if(!grounded)
          rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        // turn gravity off while on slope
        if(!wallrunning) rb.useGravity = !OnSlope();
   }

   private void SpeedControl()
   {
        if(activeGrapple) return;

        //limiting speed on slope
        if (OnSlope() && !exitingSlope)
        {
            if (rb.linearVelocity.magnitude > moveSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
        }

        // limiting speed on ground or in air
        else
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

          // limit velocity if needed
          if(flatVel.magnitude > moveSpeed)
          {
          Vector3 limitedVel = flatVel.normalized * moveSpeed;
          rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
          }
        }

   }

   private void Jump()
   {
        exitingSlope = true;

        // reset y velocity
        rb.linearVelocity = new Vector3(rb. linearVelocity.x, 0f, rb.linearVelocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
   }

   private void ResetJump()
   {
     readyToJump = true;

     exitingSlope = false;
   }

   private bool enableMovementOnNextTouch;

   public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
   {
      activeGrapple = true;

      velocityToSet = CalculateJumpVelocity(transform.position, targetPosition, trajectoryHeight);
      Invoke(nameof(SetVelocity), 0.1f);

      Invoke(nameof(ResetRestrictions), 3f);
   }

   private Vector3 velocityToSet;

   private void SetVelocity()
   {
      enableMovementOnNextTouch = true;
      rb.linearVelocity = velocityToSet;
   }

   private void ResetRestrictions()
   {
      activeGrapple = false;
   }

   private void OnCollisionEnter(Collision collision)
   {
      if (enableMovementOnNextTouch)
      {
          enableMovementOnNextTouch = false;
          ResetRestrictions();

          GetComponent<Grappling>().StopGrapple();
      }
   }

   private bool OnSlope()
   {
      if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
      {
          float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
          return angle < maxSlopeAngle && angle != 0;
      }

      return false;
   }

   private Vector3 GetSlopeMoveDirection()
   {
      return Vector3.ProjectOnPlane(moveDirection,slopeHit.normal).normalized;
   }

   public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
   {
     float gravity = Physics.gravity.y;
     float displacementY = endPoint.y - startPoint.y;
     Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

     Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * trajectoryHeight);
     Vector3 velocityXZ = displacementXZ / (Mathf.Sqrt(-2 * trajectoryHeight / gravity) 
         + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravity));

     return velocityXZ + velocityY;
   }
}
