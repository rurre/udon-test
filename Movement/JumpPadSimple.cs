using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[SelectionBase]
public class JumpPadSimple : UdonSharpBehaviour
{
    [Header("Settings")]
    public float velocity = 10;
    public bool active = true;


    Collider trigger;
    AudioSource sound;
    ParticleSystem particles;

    void Start()
    {
        var triggers = GetComponentsInChildren<Collider>(true);
        for(int i = 0; i < triggers.Length; i++)
        {
            if(triggers[i].isTrigger)
            {
                trigger = triggers[i];
                break;
            }
        }

        particles = GetComponentInChildren<ParticleSystem>(true);
        sound = GetComponentInChildren<AudioSource>(true);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if(!active)
            return;

        player.SetVelocity(velocity * transform.up);
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if(!active)
            return;

        if(sound != null && sound.clip != null)
            sound.Play();

        if(particles)
            particles.Play(true);
    }
}