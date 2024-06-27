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

    private bool _isAbleToJump;

    [Header("Wall Jumping")]
    public float wallJumpForce;

    public int maxWallJumps;

    private int _wallJumpsLeft;

    [Header("Dashing")]
    public float dashingSpeed;
    public float dashDirectionForce;
    public float dashSpeedChangeFactor;

    public float dashDuration;
    public float dashUseCooldown;
    public float dashRestoreTime;

    public int maxDashes;

    private int _dashesLeft;
    private bool _isDashOnCooldown;

    private Vector3 _calculatedDashForce;

    [Header("Sliding")]
    public float slidingSpeed;
    public float slideSpeedChangeFactor;

    public float slideScaleY;
    private float _startScaleY;

    private bool _isAbleToSlide;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode dashKey = KeyCode.LeftShift;

    [Header("Ground Check")]
    public float playerHeight;

    [Header("Layer Masks")]
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    private float _horizontalInput, _verticalInput;

    Vector3 moveDirection;

    private float _moveSpeed, _targetMoveSpeed, _lastTargetMoveSpeed;

    // Defines how fast is speed changed (when keeping momentum)
    private float _speedChangeFactor;

    private PlayerState _lastState;

    private bool _keepMomentum;

    public enum PlayerState
    {
        walking,
        airborne,
        dashing,
        sliding
    }

    public PlayerState state;

    private float _maxGroundDistance;
    private bool _isOnGround, _wasOnGround, _isTouchingWall;

    private bool _isJumping, _isDashing, _isSliding;

    private Rigidbody _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        _startScaleY = transform.localScale.y;

        state = PlayerState.walking;

        _wasOnGround = false;

        // TODO: Consider getting 'playerHeight' value *directly* if this will cause issues
        _maxGroundDistance = playerHeight * 0.5f + 0.2f;

        // TODO: Revise initial definitions, they may be redundant
        _isJumping = false;
        _isAbleToJump = true;

        _wallJumpsLeft = maxWallJumps;

        _isDashing = false;
        _isDashOnCooldown = false;
        _dashesLeft = maxDashes;

        _isSliding = false;
        _isAbleToSlide = true;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void Update()
    {
        if (_lastState == PlayerState.dashing || _lastState == PlayerState.sliding)
        {
            _keepMomentum = true;
        }

        // Handling speed changes based on whether momentum has to be kept or not
        if (_targetMoveSpeed != _lastTargetMoveSpeed)
        {
            StopCoroutine(ChangeMoveSpeedSmoothly());

            if (_keepMomentum)
            {
                StartCoroutine(ChangeMoveSpeedSmoothly());
            }
            else
            {
                _moveSpeed = _targetMoveSpeed;
            }
        }


        _lastTargetMoveSpeed = _targetMoveSpeed;
        _lastState = state;
        _wasOnGround = _isOnGround;

        _isOnGround = Physics.Raycast(transform.position, Vector3.down,
                                      playerHeight * 0.5f + 0.2f, groundLayer);

        /*
        _isTouchingWall = Physics.Raycast(transform.position, 
                                          Vector3.forward,
                                          1000f, wallLayer);

        if (_isOnGround)
        {
            _wallJumpsLeft = maxWallJumps;
        }
        */

        if (_isOnGround && !_wasOnGround) 
            _isJumping = false;

        // Apply friction
        if (_isOnGround && state != PlayerState.dashing)
        {
            _rb.drag = groundDrag;
        }
        else
        {
            _rb.drag = 0;
        }

        HandleState();
        HandleInput();

        LimitSpeed();

        // Debug info
        print(
            "State: " + state + " // " +
            "Dashes: " + _dashesLeft + "/" + maxDashes + " // " +
            "Speed: " + new Vector3(
                _rb.velocity.x, _rb.velocity.y, _rb.velocity.z).magnitude + " // " +
            "_isJumping: " + _isJumping + " // " +
            "_isDashing: " + _isDashing + " // " +
            "_isSliding: " + _isSliding + " // " +
            "_isTouchingWall: " + _isTouchingWall
         );
    }

    private void HandleInput()
    {
        _horizontalInput = Input.GetAxisRaw("Horizontal");
        _verticalInput = Input.GetAxisRaw("Vertical");

        // These are short actions rather than states, so we handle then separately
        if (Input.GetKey(jumpKey))
        {
            // Regular jump
            if (_isAbleToJump && _isOnGround)
            {
                // Sliding needs to be interrupted in order to jump properly
                if (state == PlayerState.sliding)
                {
                    InterruptSlide();
                }

                _isJumping = true;
                Jump();

                _isAbleToJump = false;

                Invoke(nameof(FinishJumpCooldown), jumpCooldown);
            }

            // Wall jump
            /*
            else if (_isTouchingWall && !_isOnGround && _wallJumpsLeft > 0)
            {
                _isJumping = true;
                WallJump();

                _wallJumpsLeft--;
            }
            */
        }

        if (Input.GetKey(dashKey) && _dashesLeft > 0 && !_isDashOnCooldown)
        {
            // Sliding needs to be interrupted in order to dash properly
            if (state == PlayerState.sliding)
            {
                InterruptSlide();
            }

            _isDashing = true;
            Dash();

            _dashesLeft--;
            _isDashOnCooldown = true;

            Invoke(nameof(FinishDashCooldown), dashUseCooldown);
            Invoke(nameof(RestoreDash), dashRestoreTime);
        }

        if (Input.GetKey(slideKey) && _isAbleToSlide)
        {
            _isSliding = true;
            Slide();
        }
        else if (Input.GetKeyUp(slideKey) && state == PlayerState.sliding)
        {
            StopSlide();
        }
    }

    private void HandleState()
    {
        // Dashing state
        if (_isDashing)
        {
            state = PlayerState.dashing;
            _targetMoveSpeed = dashingSpeed;
            _speedChangeFactor = dashSpeedChangeFactor;
        }
        // Sliding state
        else if (_isSliding)
        {
            state = PlayerState.sliding;
            _targetMoveSpeed = slidingSpeed;
            _speedChangeFactor = slideSpeedChangeFactor;
        }
        // Walking state
        else if (_isOnGround && !_isJumping)
        {
            state = PlayerState.walking;
            _targetMoveSpeed = walkingSpeed;

            _isJumping = false;

            // Restore the ability to slide after interrupting it in order to jump/dash
            _isAbleToSlide = true;
        }        
        // Airborne state
        else if (!_isOnGround)
        {   
            state = PlayerState.airborne;
        }
    }

    private void MovePlayer()
    {
        moveDirection = 
            orientation.forward * _verticalInput + orientation.right * _horizontalInput;

        Vector3 moveForce = 10f * _moveSpeed * moveDirection.normalized;

        _rb.AddForce(moveForce, ForceMode.Force);

        // Apply airborne bonus to the speed if necessary
        if (!_isOnGround)
        {
            _rb.AddForce(moveForce * airMultiplier, ForceMode.Force);
        }
    }

    private void LimitSpeed()
    {
        Vector3 flatVelocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);

        if (flatVelocity.magnitude > _moveSpeed)
        {
            Vector3 limitedVelocity = flatVelocity.normalized * _moveSpeed;

            _rb.velocity = new Vector3(limitedVelocity.x, _rb.velocity.y, limitedVelocity.z);
        }
    }

    // TODO: Get good at math
    private IEnumerator ChangeMoveSpeedSmoothly()
    {
        float time = 0;
        float diff = Mathf.Abs(_targetMoveSpeed - _moveSpeed);
        float startValue = _moveSpeed;

        while (time < diff)
        {
            _moveSpeed = Mathf.Lerp(startValue, _targetMoveSpeed, time / diff);
            time += Time.deltaTime * _speedChangeFactor;

            yield return null;
        }

        // Reset everything
        _moveSpeed = _targetMoveSpeed;
        _speedChangeFactor = 1f;
        _keepMomentum = false;
    }

    private void Jump()
    {
        _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);

        _rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void FinishJumpCooldown()
    {
        _isAbleToJump = true;
    }

    /*
    private void WallJump()
    {
        _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
        
        _rb.AddForce((transform.up) * wallJumpForce, ForceMode.Impulse);
    }
    */

    private void Dash()
    {
        if (_verticalInput != 0 && _horizontalInput != 0)
        {
            moveDirection = 
                orientation.forward * _verticalInput + orientation.right * _horizontalInput;
        }
        else
        {
            moveDirection = orientation.forward;
        }

        _calculatedDashForce = moveDirection.normalized * dashDirectionForce;

        Invoke(nameof(ApplyDashForce), 0.025f);
        Invoke(nameof(StopDash), dashDuration);
    }

    private void ApplyDashForce()
    {
        _rb.AddForce(_calculatedDashForce, ForceMode.Impulse);
    }

    private void StopDash()
    {
        _isDashing = false;
    }

    private void FinishDashCooldown()
    {
        _isDashOnCooldown = false;
    }

    private void RestoreDash()
    {
        _dashesLeft++;
    }

    private void Slide()
    {
        // Shrink
        transform.localScale = new Vector3(
            transform.localScale.x, slideScaleY, transform.localScale.z);

        _rb.AddForce(Vector3.down * 0.5f, ForceMode.Impulse);
    }

    private void StopSlide()
    {
        _isSliding = false;

        transform.localScale = new Vector3(
            transform.localScale.x, _startScaleY, transform.localScale.z);
    }

    private void InterruptSlide()
    {
        _isAbleToSlide = false;
        StopSlide();
    }
}
