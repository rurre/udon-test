
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

[SelectionBase]
public class JumpPadReflect : UdonSharpBehaviour
{
    [Header("Settings")]
    public bool active = true;

    public float pushVelocity = 10f;

    Collider trigger;
    AudioSource sound;
    ParticleSystem particles;

    void Start()
    {
        var colliders = GetComponentsInChildren<Collider>(true);
        foreach(var col in colliders)
        {
            if(col.isTrigger)
            {
                trigger = col;
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

        var vel = player.GetVelocity();


        if(Vector3.Dot(vel, transform.up) <= 0f)
        {
            var playerPos = player.GetPosition();
            var localVel = transform.worldToLocalMatrix.MultiplyVector(vel);

            localVel.y = -Mathf.Max(Mathf.Abs(localVel.y), pushVelocity);
            vel = transform.localToWorldMatrix.MultiplyVector(localVel);

            vel = Vector3.Reflect(vel, transform.up);
            player.SetVelocity(vel);
        }
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
