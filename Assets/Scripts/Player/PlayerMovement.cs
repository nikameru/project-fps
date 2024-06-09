using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;

    [Header("Walking")]
    public float walkingSpeed;

    // Defines ground friction
    public float groundDrag;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;

    // Speed multiplier for the 'airborne' state
    public float airMultiplier;

    private bool isAbleToJump;

    [Header("Dashing")]
    public float dashingSpeed;
    public float dashDirectionForce;
    public float dashSpeedChangeFactor;

    public float dashDuration;
    public float dashUseCooldown;
    public float dashRestoreTime;

    public int maxDashes;

    // Indicates how many dashes can be performed
    private int dashesLeft;
    private bool isDashOnCooldown;

    private Vector3 calculatedDashForce;

    [Header("Sliding")]
    public float slidingSpeed;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode dashKey = KeyCode.LeftShift;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask groundLayer;

    bool isOnGround;
    bool isDashing;

    float horizontalInput, verticalInput;

    Vector3 moveDirection;

    private float moveSpeed;
    private float targetMoveSpeed;
    private float lastTargetMoveSpeed;

    // Defines how fast is speed changed (when keeping momentum)
    private float speedChangeFactor;

    private PlayerState lastState;

    private bool keepMomentum;

    public enum PlayerState
    {
        walking,
        airborne,
        dashing,
        sliding
    }

    public PlayerState state;

    Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        state = PlayerState.walking;

        isAbleToJump = true;

        isDashing = false;
        isDashOnCooldown = false;
        dashesLeft = maxDashes;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void Update()
    {
        bool hasTargetMoveSpeedChanged = targetMoveSpeed != lastTargetMoveSpeed;

        if (lastState == PlayerState.dashing) keepMomentum = true;

        if (hasTargetMoveSpeedChanged)
        {
            StopCoroutine(changeMoveSpeedSmoothly());

            if (keepMomentum)
                StartCoroutine(changeMoveSpeedSmoothly());
            else
                moveSpeed = targetMoveSpeed;
        }


        lastTargetMoveSpeed = targetMoveSpeed;
        lastState = state;

        HandleState();
        HandleInput();

        LimitSpeed();

        // Apply friction
        if (state == PlayerState.walking)
            rb.drag = groundDrag;
        else
            rb.drag = 0;

        // Debug info
        print(
            "State: " + state + " // " +
            "Dashes: " + dashesLeft + "/" + maxDashes + " // " +
            "Speed: " + new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z).magnitude
        );
    }

    private void HandleInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // These are short actions rather than states, so we handle then separately
        if (Input.GetKey(jumpKey) && isAbleToJump && isOnGround)
        {
            isAbleToJump = false;

            Jump();

            Invoke(nameof(FinishJumpCooldown), jumpCooldown);
        }

        if (Input.GetKey(dashKey) && IsAbleToDash())
        {
            dashesLeft--;
            isDashOnCooldown = true;
            isDashing = true;

            Dash();

            Invoke(nameof(FinishDashCooldown), dashUseCooldown);
            Invoke(nameof(RestoreDash), dashRestoreTime);
        }
    }

    private void HandleState()
    {
        float maxGroundDistance = playerHeight * 0.5f + 0.2f;
        isOnGround = Physics.Raycast(transform.position, Vector3.down, maxGroundDistance, groundLayer);

        if (isDashing)
        {
            state = PlayerState.dashing;
            targetMoveSpeed = dashingSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }

        else if (isOnGround && Input.GetKey(slideKey))
        {
            state = PlayerState.sliding;
            targetMoveSpeed = slidingSpeed;
        }

        else if (isOnGround)
        {
            state = PlayerState.walking;
            targetMoveSpeed = walkingSpeed;
        }

        else if (!isOnGround)
        {
            state = PlayerState.airborne;
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        if (state == PlayerState.walking)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        else if (state == PlayerState.airborne)
            // Apply airborne bonus to the speed if necessary
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
    }

    private void LimitSpeed()
    {
        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (flatVelocity.magnitude > moveSpeed)
        {
            Vector3 limitedVelocity = flatVelocity.normalized * moveSpeed;

            rb.velocity = new Vector3(limitedVelocity.x, rb.velocity.y, limitedVelocity.z);
        }
    }

    // TODO: Get good at math
    private IEnumerator changeMoveSpeedSmoothly()
    {
        float time = 0;
        float diff = Mathf.Abs(targetMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < diff)
        {
            moveSpeed = Mathf.Lerp(startValue, targetMoveSpeed, time / diff);
            time += Time.deltaTime * speedChangeFactor;

            yield return null;
        }

        // Reset everything
        moveSpeed = targetMoveSpeed;
        speedChangeFactor = 1f;
        keepMomentum = false;
    }

    private void Jump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void FinishJumpCooldown()
    {
        isAbleToJump = true;
    }

    private void Dash()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        calculatedDashForce = moveDirection.normalized * dashDirectionForce;

        Invoke(nameof(ApplyDelayedDashForce), 0.025f);
        Invoke(nameof(StopDash), dashDuration);
    }

    private void ApplyDelayedDashForce()
    {
        rb.AddForce(calculatedDashForce, ForceMode.Impulse);
    }

    private void StopDash()
    {
        isDashing = false;
    }

    private bool IsAbleToDash()
    {
        return dashesLeft > 0 && isDashOnCooldown == false;
    }

    private void FinishDashCooldown()
    {
        isDashOnCooldown = false;
    }

    private void RestoreDash()
    {
        dashesLeft++;
    }

    private void Slide()
    {
        // Crouch + apply higher speed
    }
}
