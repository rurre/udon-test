using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CustomPlayerController : UdonSharpBehaviour
{
    VRCPlayerApi player;
    bool holdingSlide = false;

    Vector3 _oldVelocity;

    float slideVelocity = 10;

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
    }

    protected void EnterSlide()
    {
        _oldVelocity = player.GetVelocity();
        Vector3 dir = player.GetVelocity().normalized;
        player.SetVelocity(dir * slideVelocity);
    }

    protected void ExitSlide()
    {
        player.SetVelocity(_oldVelocity);
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
    }


    protected bool CanExecuteSlide()
    {
        return player != null && player.IsPlayerGrounded() && Input.GetKey(KeyCode.LeftControl);
    }
}
