using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class CustomPlayerController : UdonSharpBehaviour
{
    // Debug
    public Text currentStateDebugText;
    public Text nextStateDebugText;
    public Text jumpDebugText;

    // Stats
    public float slideVelocity = 10;
    public float groundPoundVelocity = 15;
    public float jumpImpulseOverride = 10;

    public float groundPoundTooCloseDistance = 0.1f;
    public LayerMask groundMask;

    // Input
    public KeyCode slideKey = KeyCode.LeftControl;
    public KeyCode jumpKey = KeyCode.Space;

    VRCPlayerApi player;

    #region States

    /// <summary>
    /// 0 - Idle,
    /// 1 - Jumping,
    /// 2 - Sliding,
    /// 3 - GroundPound,
    /// 4 - Dashing
    /// </summary>
    int state = 0;
    int lastState = 0;
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
    }

    #region Input

    bool _isHoldingSlideKey = false;
    bool _pressedSlide = false;
    bool _pressedJump = false;

    void HandleInput()
    {
        _isHoldingSlideKey = Input.GetKey(slideKey);

        if(!_pressedSlide)
            _pressedSlide = Input.GetKeyDown(slideKey);
        if(!_pressedJump)
            _pressedJump = Input.GetKeyDown(jumpKey);
    }

    void ConsumeInputs()
    {
        _pressedSlide = false;
        _pressedJump = false;
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

    #region Updates

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        ExecuteMainStateMachine();
        ConsumeInputs();
    }

    #endregion

    #region State Machines

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
                default:
                    break;
            }
            lastState = state;
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
                default:
                    break;
            }
        }

        if(currentStateDebugText)
            currentStateDebugText.text = StateToString(state);
        if(nextStateDebugText)
            nextStateDebugText.text = StateToString(nextState);
        if(jumpDebugText)
            jumpDebugText.text = PressedJump().ToString();
    }

    void IdleStateMachine()
    {
        if(PressedSlide() && CanStartSlide())
            nextState = 2;
        else if(PressedJump() && CanJump())
            nextState = 1;
    }

    void SlideStateMachine()
    {
        if(PressedJump())
        {
            ExitSlide(1);
            return;
        }

        if(IsHoldingSlide())
            ExecuteSlide();
        else
            ExitSlide(0);
    }

    void JumpStateMachine()
    {
        if(player.IsPlayerGrounded())
            ExitJump(0);
        else if(PressedGroundPound() && CanGroundPound())
            ExitJump(3);
    }

    void GroundPoundStateMachine()
    {
        if(player.IsPlayerGrounded())
            ExitGroundPound(0);
    }

    #endregion

    #region Slide State

    Vector3 slideDirection;

    bool CanStartSlide()
    {
        return state == 0 && player.IsPlayerGrounded();
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
        Debug.Log("Exitting slide, going to " + StateToString(nextState));
        this.nextState = nextState;
    }

    #endregion

    #region Jump State

    bool CanJump()
    {
        return player.IsPlayerGrounded();
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

    bool CanGroundPound()
    {
        if(player.IsPlayerGrounded())
            return false;

        Ray ray = new Ray(player.GetPosition(), Vector3.down);
        RaycastHit hit;
        bool tooCloseToGround = Physics.Raycast(ray, out hit, groundPoundTooCloseDistance, groundMask, QueryTriggerInteraction.UseGlobal);
        string name = hit.collider ? hit.collider.name : "nothing";
        Debug.Log($"{tooCloseToGround}, hit {name}");
        Debug.DrawRay(ray.origin, ray.direction, Color.yellow, 10);

        return !tooCloseToGround;
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
