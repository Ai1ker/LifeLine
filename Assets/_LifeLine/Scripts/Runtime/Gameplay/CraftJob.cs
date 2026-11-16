using System;
using UnityEngine;

namespace Lifeline
{
    [Serializable]
    public class CraftJob
    {
        [Header("自动制作 / shared queue")]
        public int id;
        public RecipeDefinition recipe;
        public CrewRole owner;
        [Tooltip("Actual real seconds since the job started.")] public float elapsed;
        [Tooltip("Work done in seconds, including successful QTE bonuses.")] public float progress;
        public float duration;
        [Tooltip("The next cue's centre, in elapsed job seconds.")] public float nextCue = 1f;
        [Tooltip("0=W/up, 1=A/left, 2=S/down, 3=D/right.")] public int noteDirection;
        public bool attempted;
        public int hits;
        public int misses;
        public bool completed;
        public string feedback;
        public float feedbackTime;
        public float NormalizedProgress => duration > 0 ? Mathf.Clamp01(progress / duration) : 1;
    }

    [Serializable]
    public class CrewBuffs
    {
        [Header("品质鱼即时效果 / seconds remaining")]
        public float move;
        public float qte;
        public float oxygen;
        public float attack;
        public float capacity;

        public void Tick(float delta)
        {
            move = Mathf.Max(0, move - delta);
            qte = Mathf.Max(0, qte - delta);
            oxygen = Mathf.Max(0, oxygen - delta);
            attack = Mathf.Max(0, attack - delta);
            capacity = Mathf.Max(0, capacity - delta);
        }
    }

    public enum EnemyKind { Shark, WhiteShark, Bird, ArmoredBird, Shadow }
    public enum EnemyState { Patrol, Detecting, Telegraph, Pursuit, Lunge, Retreat, Defeated }
}
