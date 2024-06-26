using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform orientation;

    [Header("General Parameters")]
    // Defines ground friction
    public float groundDrag;
    // Speed multiplier for the 'airborne' state
    public float airMultiplier;

    [Header("Walking")]
    public float walkingSpeed;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;

    private bool isAbleToJump;

    [Header("Wall Jumping")]
    public float wallJumpForce;

    public int maxWallJumps;

    private int wallJumpsLeft;

    [Header("Dashing")]
    public float dashingSpeed;
    public float dashDirectionForce;
    public float dashSpeedChangeFactor;

    public float dashDuration;
    public float dashUseCooldown;
    public float dashRestoreTime;

    public int maxDashes;

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

    [Header("Layer Masks")]
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    private float horizontalInput, verticalInput;

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

    private float maxGroundDistance;

    private bool isOnGround;
    private bool wasOnGround;

    private bool isTouchingWall;

    private bool isJumping;
    private bool isDashing;
    private bool isSliding;

    Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        startScaleY = transform.localScale.y;

        state = PlayerState.walking;

        wasOnGround = false;

        // TODO: Consider getting 'playerHeight' value *directly* if this will cause issues
        maxGroundDistance = playerHeight * 0.5f + 0.2f;

        // TODO: Revise initial definitions, they may be redundant
        isJumping = false;
        isAbleToJump = true;

        wallJumpsLeft = maxWallJumps;

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

        isOnGround = Physics.Raycast(
                    transform.position,
                    Vector3.down,
                    playerHeight * 0.5f + 0.2f,
                    groundLayer
                );

        /*
        isTouchingWall = Physics.Raycast(
                    transform.position,
                    Vector3.forward,
                    1000f,
                    wallLayer
                );
        */

        if (isOnGround)
            wallJumpsLeft = maxWallJumps;

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

        // Debug info
        float currentSpeed = new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z).magnitude;

        print(
            "State: " + state + " // " +
            "Dashes: " + dashesLeft + "/" + maxDashes + " // " +
            "Speed: " + currentSpeed + " // " +
            "isJumping: " + isJumping + " // " +
            "isDashing: " + isDashing + " // " +
            "isSliding: " + isSliding + " // " +
            "isTouchingWall: " + isTouchingWall
         );
    }

    private void HandleInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        // These are short actions rather than states, so we handle then separately

        if (Input.GetKey(jumpKey))
        {
            // Regular jump
            if (isAbleToJump && isOnGround)
            {
                // Sliding needs to be interrupted in order to jump properly
                if (state == PlayerState.sliding)
                    InterruptSlide();

                isJumping = true;
                Jump();

                isAbleToJump = false;

                Invoke(nameof(FinishJumpCooldown), jumpCooldown);
            }

            // Wall jump
            /*
            else if (isTouchingWall && !isOnGround && wallJumpsLeft > 0)
            {
                isJumping = true;
                WallJump();

                wallJumpsLeft--;
            }
            */
        }

        if (Input.GetKey(dashKey) && dashesLeft > 0 && !isDashOnCooldown)
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
        else if (isOnGround && !isJumping)
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

        Vector3 moveForce = 10f * moveSpeed * moveDirection.normalized;

        rb.AddForce(moveForce, ForceMode.Force);

        // Apply airborne bonus to the speed if necessary
        if (!isOnGround)
            rb.AddForce(moveForce * airMultiplier, ForceMode.Force);
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

    private void WallJump()
    {
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        
        rb.AddForce((transform.up) * wallJumpForce, ForceMode.Impulse);
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
