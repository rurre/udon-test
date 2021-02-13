using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CustomPlayerController : UdonSharpBehaviour
{
    VRCPlayerApi player;
    bool holdingSlide = false;
    bool waitingToJump = false;

    public float jumpVelocity = 10;
    public float slideVelocity = 10;

    bool inSlideState = false;

    void Start()
    {
        player = Networking.LocalPlayer;
    }

    private void Update()
    {
        HandleInput();
    }

    private void FixedUpdate()
    {
        ExecuteSlide();
        Debug.Log(player.GetVelocity());
    }

    private void HandleInput()
    {
        holdingSlide = Input.GetKey(KeyCode.LeftControl);
        waitingToJump = Input.GetKey(KeyCode.Space);
    }

    protected void EnterSlide()
    {
        Vector3 dir = player.GetVelocity().normalized;
        player.SetVelocity(dir * slideVelocity);
    }

    protected void ExitSlide()
    {

    }

    public void ExecuteSlide()
    {
        if(!CanExecuteSlide() && !inSlideState)
            return;

        if(!inSlideState)
        {
            EnterSlide();
            inSlideState = true;
            return;
        }

        inSlideState = false;
        ExitSlide();

        if(waitingToJump)
        {
            player.SetVelocity(player.GetVelocity() + new Vector3(0,jumpVelocity,0));
            waitingToJump = false;
        }
    }


    protected bool CanExecuteSlide()
    {
        return player != null && player.IsPlayerGrounded() && Input.GetKey(KeyCode.LeftControl);
    }
}
