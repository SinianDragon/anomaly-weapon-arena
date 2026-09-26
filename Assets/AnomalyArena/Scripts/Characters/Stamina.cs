using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// 冲刺体力（0–1）。满体力能连续冲刺 sprintDuration 秒；不冲刺时 recoverDuration 秒从空回满。
    /// 体力耗尽后要先回到 minToRestart 才能再冲：否则在空体力附近会冲一帧、停一帧，一顿一顿的。
    /// 不依赖场景和物理，方便单独测试。
    /// </summary>
    [Serializable]
    public class Stamina
    {
        [Tooltip("满体力能连续冲刺几秒")] public float sprintDuration = 1f;
        [Tooltip("从空回满需要几秒")] public float recoverDuration = 3f;
        [Tooltip("冲刺时的移动速度倍率")] public float sprintMultiplier = 2f;
        [Tooltip("耗尽后至少回到多少（0–1）才能再冲刺")] [Range(0f, 1f)] public float minToRestart = 0.25f;

        /// <summary>当前体力，0–1。</summary>
        public float Value { get; private set; } = 1f;
        public bool Sprinting { get; private set; }
        /// <summary>刚耗尽、还没回到 minToRestart。</summary>
        public bool Exhausted { get; private set; }

        /// <summary>推进 dt 秒；wantSprint 是玩家这一刻想不想冲刺。返回这一刻的移动速度倍率。</summary>
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
