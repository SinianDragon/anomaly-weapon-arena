using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Sprint stamina (0–1). Full stamina allows sprintDuration seconds of continuous sprinting; it refills from empty in recoverDuration seconds when not sprinting.
    /// After running out it has to recover to minToRestart before sprinting again; otherwise near empty it would sprint one frame, stop one frame, and stutter.
    /// Independent of the scene and physics, so it can be tested on its own.
    /// </summary>
    [Serializable]
    public class Stamina
    {
        [Tooltip("Seconds of continuous sprinting on full stamina")] public float sprintDuration = 1f;
        [Tooltip("Seconds to refill from empty")] public float recoverDuration = 3f;
        [Tooltip("Movement speed multiplier while sprinting")] public float sprintMultiplier = 2f;
        [Tooltip("After running out, minimum stamina (0–1) needed to sprint again")] [Range(0f, 1f)] public float minToRestart = 0.25f;

        /// <summary>Current stamina, 0–1.</summary>
        public float Value { get; private set; } = 1f;
        public bool Sprinting { get; private set; }
        /// <summary>Just ran out and has not recovered to minToRestart yet.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>Advances by dt seconds; wantSprint is whether the player wants to sprint right now. Returns the current movement speed multiplier.</summary>
        public float Tick(float dt, bool wantSprint)
        {
            Sprinting = wantSprint && !Exhausted && Value > 0f;
            if (Sprinting)
            {
                Value = Mathf.Max(0f, Value - dt / Mathf.Max(0.01f, sprintDuration));
                if (Value <= 0f) Exhausted = true;
            }
            else
            {
                Value = Mathf.Min(1f, Value + dt / Mathf.Max(0.01f, recoverDuration));
                if (Exhausted && Value >= minToRestart) Exhausted = false;
            }
            return Sprinting ? sprintMultiplier : 1f;
        }

        public void Refill()
        {
            Value = 1f;
            Exhausted = false;
            Sprinting = false;
        }
    }
}
