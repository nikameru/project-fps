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
    public float slideSpeedChangeFactor;

    public float slideScaleY;
    private float startScaleY;

    private bool isAbleToSlide;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode dashKey = KeyCode.LeftShift;

    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask groundLayer;

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

    bool isOnGround;
    bool wasOnGround;
    bool isJumping;
    bool isDashing;
    bool isSliding;

    Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        startScaleY = transform.localScale.y;

        state = PlayerState.walking;

        // TODO: Revise initial definitions, they may be redundant
        isJumping = false;
        isAbleToJump = true;

        isDashing = false;
        isDashOnCooldown = false;
        dashesLeft = maxDashes;

        isSliding = false;
        isAbleToSlide = true;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void Update()
    {
        if (lastState == PlayerState.dashing || lastState == PlayerState.sliding)
            keepMomentum = true;

        // Handling speed changes based on whether momentum has to be kept or not
        if (targetMoveSpeed != lastTargetMoveSpeed)
        {
            StopCoroutine(ChangeMoveSpeedSmoothly());

            if (keepMomentum)
                StartCoroutine(ChangeMoveSpeedSmoothly());
            else
                moveSpeed = targetMoveSpeed;
        }


        lastTargetMoveSpeed = targetMoveSpeed;
        lastState = state;
        wasOnGround = isOnGround;

        // TODO: Consider getting 'playerHeight' value *directly* if this will cause issues
        float maxGroundDistance = playerHeight * 0.5f + 0.2f;
        isOnGround = Physics.Raycast(transform.position, Vector3.down, maxGroundDistance, groundLayer);

        if (isOnGround && !wasOnGround) 
            isJumping = false;

        // Apply friction
        if (isOnGround && state != PlayerState.dashing)
            rb.drag = groundDrag;
        else
            rb.drag = 0;

        HandleState();
        HandleInput();

        LimitSpeed();

        float currentSpeed = new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z).magnitude;

        // Debug info
        print(
            "State: " + state + " // " +
            "Dashes: " + dashesLeft + "/" + maxDashes + " // " +
            "Speed: " + currentSpeed + " // " +
            "isJumping: " + isJumping + " // " +
            "isDashing: " + isDashing + " // " +
            "isSliding: " + isSliding
        );
    }

    private void HandleInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // These are short actions rather than states, so we handle then separately
        if (Input.GetKey(jumpKey) && isAbleToJump && isOnGround)
        {
            // Sliding needs to be interrupted in order to jump properly
            if (state == PlayerState.sliding)
                InterruptSlide();

            isJumping = true;
            Jump();

            isAbleToJump = false;

            Invoke(nameof(FinishJumpCooldown), jumpCooldown);
        }

        if (Input.GetKey(dashKey) && IsAbleToDash())
        {
            // Sliding needs to be interrupted in order to dash properly
            if (state == PlayerState.sliding)
                InterruptSlide();

            isDashing = true;
            Dash();

            dashesLeft--;
            isDashOnCooldown = true;

            Invoke(nameof(FinishDashCooldown), dashUseCooldown);
            Invoke(nameof(RestoreDash), dashRestoreTime);
        }

        if (Input.GetKey(slideKey) && isAbleToSlide)
        {
            isSliding = true;
            Slide();
        }
        else if (Input.GetKeyUp(slideKey) && state == PlayerState.sliding)
            StopSlide();
    }

    private void HandleState()
    {
        // Dashing state
        if (isDashing)
        {
            state = PlayerState.dashing;
            targetMoveSpeed = dashingSpeed;
            speedChangeFactor = dashSpeedChangeFactor;
        }

        // Sliding state
        else if (isSliding)
        {
            state = PlayerState.sliding;
            targetMoveSpeed = slidingSpeed;
            speedChangeFactor = slideSpeedChangeFactor;
        }

        // Walking state
        else if (isOnGround && !isJumping)//(isOnGround && !isJumping) || (isOnGround && !wasOnGround))
        {
            state = PlayerState.walking;
            targetMoveSpeed = walkingSpeed;

            isJumping = false;

            // Restore the ability to slide after interrupting it in order to jump/dash
            isAbleToSlide = true;
        }
        
        // Airborne state
        else if (!isOnGround)
        {   
            state = PlayerState.airborne;
        }

        // Debug case
        else
        {
            print("Watch out!");
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        /*
        if (state == PlayerState.walking || state == PlayerState.sliding)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        else if (!isOnGround)
            // Apply airborne bonus to the speed if necessary
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
        */

        rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // Apply airborne bonus to the speed if necessary
        if (!isOnGround)
            rb.AddForce(airMultiplier * 10f * moveSpeed * moveDirection.normalized, ForceMode.Force);
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
    private IEnumerator ChangeMoveSpeedSmoothly()
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
        if (verticalInput != 0 && horizontalInput != 0)
            moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        else
            moveDirection = orientation.forward;

        calculatedDashForce = moveDirection.normalized * dashDirectionForce;

        Invoke(nameof(ApplyDashForce), 0.025f);
        Invoke(nameof(StopDash), dashDuration);
    }

    private void ApplyDashForce()
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
        transform.localScale = new Vector3(
                transform.localScale.x,
                slideScaleY,
                transform.localScale.z
            );

        rb.AddForce(Vector3.down * 0.5f, ForceMode.Impulse);
    }

    private void StopSlide()
    {
        isSliding = false;

        transform.localScale = new Vector3(
                transform.localScale.x,
                startScaleY,
                transform.localScale.z
            );
    }

    private void InterruptSlide()
    {
        isAbleToSlide = false;
        StopSlide();
    }
}
