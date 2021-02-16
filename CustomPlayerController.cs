using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class CustomPlayerController : UdonSharpBehaviour
{
    // Debug
    public Text currentStateText;
    public Text nextStateText;
    public Text jumpText;
    public Text dashText;
    public Text groundedText;
    public Text groundedGraceText;
    public Text distanceToGroundText;

    // Stats
    public float slideVelocity = 10;
    public float groundPoundVelocity = 15;
    public float jumpImpulseOverride = 10;

    public float dashVelocity = 20;
    public float dashTime = 0.13f;
    public float dashJuiceMax = 3;
    public float dashRegenRate = 0.3f;
    public float ignoreNotGroundedMaxTime = 1;

    public float groundPoundTooCloseDistance = 0.1f;
    public LayerMask groundMask;

    float distanceToGroundLast;
    sbyte _tooCloseToGroundThisFrame = -1;

    // Input
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode dashKey = KeyCode.LeftShift;

    VRCPlayerApi player;
    bool _playerGrounded;
    float _timeNotGrounded;

    #region States

    /// <summary>
    /// Idle = 0,
    /// Jumping = 1,
    /// Sliding = 2,
    /// GroundPound = 3,
    /// Dashing = 4
    /// </summary>
    int state = 0;
    int nextState = -1;

    string StateToString(int state)
    {
        switch(state)
        {
            case 0:
                return "Idle";
            case 1:
                return "Jump";
            case 2:
                return "Slide";
            case 3:
                return "Ground Pound";
            case 4:
                return "Dash";
            case -1:
                return "None";
            default:
                return "Error";
        }
    }

    #endregion

    void Start()
    {
        player = Networking.LocalPlayer;
        player.SetJumpImpulse(0);
        dashJuice = dashJuiceMax;
    }

    #region Input

    bool _isHoldingSlideKey;
    bool _pressedSlide;
    bool _pressedJump;
    bool _pressedDash;

    void HandleInput()
    {
        _isHoldingSlideKey = Input.GetKey(slideKey);

        if(!_pressedSlide)
            _pressedSlide = Input.GetKeyDown(slideKey);
        if(!_pressedJump)
            _pressedJump = Input.GetKeyDown(jumpKey);
        if(!_pressedDash)
            _pressedDash = Input.GetKeyDown(dashKey);
    }

    void ResetFrameVariables()
    {
        _pressedSlide = false;
        _pressedJump = false;
        _pressedDash = false;
        _tooCloseToGroundThisFrame = (sbyte)-1;
    }

    bool PressedDash()
    {
        return _pressedDash;
    }

    bool IsHoldingSlide()
    {
        return _isHoldingSlideKey;
    }

    bool PressedSlide()
    {
        return _pressedSlide;
    }

    bool PressedGroundPound()
    {
        return _pressedSlide;
    }

    bool PressedJump()
    {
        return _pressedJump;
    }

    #endregion

    #region Debug

    private void UpdateDebugText()
    {
        if(currentStateText)
            currentStateText.text = StateToString(state);
        if(nextStateText)
            nextStateText.text = StateToString(nextState);
        if(jumpText)
            jumpText.text = PressedJump().ToString();
        if(groundedText)
            groundedText.text = IsGrounded().ToString();
        if(groundedGraceText)
            groundedGraceText.text = IsGroundedGrace().ToString();
        if(dashText)
            dashText.text = $"{dashJuice}/{dashJuiceMax}";
        if(distanceToGroundText)
            distanceToGroundText.text = distanceToGroundLast > -1 ? distanceToGroundLast.ToString() : "didn't hit";
    }

    #endregion

    #region Updates and Checks

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        HandleGroundCheck();
        RegenerateDash(Time.fixedDeltaTime);
        ExecuteMainStateMachine();
        ResetFrameVariables();
        UpdateDebugText();
    }

    #endregion

    #region Ground Checks

    void HandleGroundCheck()
    {
        if(_playerGrounded = player.IsPlayerGrounded())
        {
            _timeNotGrounded = 0;
            return;
        }

        if(_timeNotGrounded < ignoreNotGroundedMaxTime)
            _timeNotGrounded += Time.fixedDeltaTime;
    }

    bool IsGroundedGrace()
    {
        return _playerGrounded || (_timeNotGrounded <= ignoreNotGroundedMaxTime);
    }

    bool IsGrounded()
    {
        return _playerGrounded;
    }

    #endregion

    #region Main State Machines

    void ExecuteMainStateMachine()
    {
        // Assign state
        if(nextState != -1)
        {
            switch(nextState)
            {
                case 1:
                    EnterJump();
                    break;
                case 2:
                    EnterSlide();
                    break;
                case 3:
                    EnterGroundPound();
                    break;
                case 4:
                    EnterDash();
                    break;
                default:
                    break;
            }
            state = nextState;
            nextState = -1;
        }
        else
        {
            // Execute current state
            switch(state)
            {
                case 0:
                    IdleStateMachine();
                    break;
                case 1:
                    JumpStateMachine();
                    break;
                case 2:
                    SlideStateMachine();
                    break;
                case 3:
                    GroundPoundStateMachine();
                    break;
                case 4:
                    DashStateMachine();
                    break;
                default:
                    break;
            }
        }
    }

    void IdleStateMachine()
    {
        if(PressedSlide())
        {
            if(CanStartSlide() || TooCloseToPound())
                nextState = 2;
            else if(CanGroundPound())
                nextState = 3;
        }
        else if(PressedJump() && CanJump())
            nextState = 1;
        else if(PressedDash() && CanStartDash())
            nextState = 4;
    }

    #endregion

    #region Slide State

    Vector3 slideDirection;

    void SlideStateMachine()
    {
        if(PressedJump())
        {
            ExitSlide(1);
            return;
        }

        if(PressedDash() && CanStartDash())
        {
            ExitSlide(4);
            return;
        }

        if(IsHoldingSlide()) //&& CanContinueSlide()) TODO: Add back
        {
            ExecuteSlide();
            return;
        }

        ExitSlide(0);
    }

    bool CanStartSlide()
    {
        return state == 0 && _playerGrounded;
    }

    bool CanContinueSlide()
    {
        return IsGroundedGrace();
    }

    void EnterSlide()
    {
        slideDirection = player.GetRotation() * Vector3.forward;
    }

    void ExecuteSlide()
    {
        SetHorizontalVelocity(player, slideDirection * slideVelocity);
    }

    void ExitSlide(int nextState)
    {
        this.nextState = nextState;
    }

    #endregion

    #region Jump State

    void JumpStateMachine()
    {
        if(IsGroundedGrace())
        {
            ExitJump(0);
            return;
        }

        if(PressedGroundPound() && CanGroundPound())
        {
            ExitJump(3);
            return;
        }

        if(PressedDash() && CanStartDash())
        {
            ExitJump(4);
            return;
        }
    }

    bool CanJump()
    {
        return IsGrounded();
    }

    void EnterJump()
    {
        SetVeritcalVelocity(player, jumpImpulseOverride * Vector3.up);
    }

    void ExitJump(int nextState)
    {
        this.nextState = nextState;
    }

    #endregion

    #region Ground Pound State

    void GroundPoundStateMachine()
    {
        if(IsGroundedGrace())
            ExitGroundPound(0);
        else if(PressedDash() && CanStartDash())
            ExitGroundPound(4);
    }

    bool TooCloseToPound()
    {
        if(_tooCloseToGroundThisFrame == -1)
        {
            Ray ray = new Ray(player.GetPosition(), Vector3.down);
            RaycastHit hit;
            bool wasHit = Physics.Raycast(ray, out hit, groundPoundTooCloseDistance, groundMask, QueryTriggerInteraction.UseGlobal);
            distanceToGroundLast = hit.distance;
            _tooCloseToGroundThisFrame = wasHit ? (sbyte)1 : (sbyte)0;
        }

        return _tooCloseToGroundThisFrame == 1;
    }

    bool CanGroundPound()
    {
        return !IsGroundedGrace() && !TooCloseToPound();
    }

    void EnterGroundPound()
    {
        player.SetVelocity(groundPoundVelocity * Vector3.down);
    }

    void ExitGroundPound(int nextState)
    {
        this.nextState = nextState;
    }

    #endregion

    #region Dash State

    float dashJuice = 0;
    float dashTimeLeft = 0;
    Vector3 dashDir;


    bool CanStartDash()
    {
        if(dashJuice >= 1)
        {
            dashTimeLeft = dashTime;
            return true;
        }
        return false;
    }

    void DashStateMachine()
    {
        if(dashTimeLeft <= 0)
        {
            ExitDash(0);
            return;
        }

        player.SetVelocity(dashDir * dashVelocity);
        dashTimeLeft -= Time.fixedDeltaTime;
    }

    void EnterDash()
    {
        dashJuice -= 1;
        var vel = player.GetVelocity();
        if(vel.x == 0 && vel.z == 0)
        {
            dashDir = player.GetRotation() * Vector3.forward;
            return;
        }

        vel.y = 0;
        dashDir = vel.normalized;
    }

    void ExitDash(int nextState)
    {
        this.nextState = nextState;
        player.SetVelocity(Vector3.zero);
    }

    void RegenerateDash(float deltaTime)
    {
        // Can't regenerate while sliding or dashing
        if(state == 2 || state == 4)
            return;


        if(dashJuice < dashJuiceMax)
            dashJuice += deltaTime * dashRegenRate;
        else
            dashJuice = dashJuiceMax;
    }

    #endregion

    #region Helpers

    void AddVelocity(VRCPlayerApi player, Vector3 velocity)
    {
        player.SetVelocity(player.GetVelocity() + velocity);
    }

    void SetHorizontalVelocity(VRCPlayerApi player, Vector3 velocity)
    {
        var current = player.GetVelocity();
        velocity.y = current.y;
        player.SetVelocity(velocity);
    }

    void SetVeritcalVelocity(VRCPlayerApi player, Vector3 velocity)
    {
        var current = player.GetVelocity();
        current.y = velocity.y;
        player.SetVelocity(current);
    }

    #endregion
}
