using System;
using UnityEngine;

namespace Lifeline
{
    [DisallowMultipleComponent]
    public sealed class PixelEnemyView : MonoBehaviour
    {
        [Serializable]
        public struct FrameHeight
        {
            public Sprite sprite;
            public float scale;
        }

        [Serializable]
        public class FrameSet
        {
            public Sprite[] travel, attack, dash, waterShot, basicAttack;
            [Min(.1f)] public float width = 2.4f;
            [Min(1)] public float framesPerSecond = 8;
            [Tooltip("Frames already share a physical size and body pivot through their Sprite import settings.")]
            public bool registeredSprites;
            public FrameHeight[] frameHeightCorrections;

            public Vector3 RegisteredScale(Sprite frame, float size)
            {
                if (frameHeightCorrections != null)
                    foreach (var correction in frameHeightCorrections)
                        if (correction.sprite == frame) return new Vector3(size, size * correction.scale, size);
                return Vector3.one * size;
            }
        }

        public LifelineEnemy enemy;
        public SpriteRenderer body;
        public FrameSet primary, variant;
        EnemyState lastState;
        EnemyKind lastKind;
        float stateAge, swimClock, oldHealth, flash;

        void Awake()
        {
            if (enemy == null) enemy = GetComponent<LifelineEnemy>();
        }

        void LateUpdate()
        {
            if (enemy == null || body == null || enemy.session == null) return;
            bool special = enemy.kind == EnemyKind.WhiteShark || enemy.kind == EnemyKind.ArmoredBird;
            FrameSet art = special && variant != null && variant.travel != null && variant.travel.Length != 0 ? variant : primary;
            if (art == null || art.travel == null || art.travel.Length == 0) return;
            if (lastState != enemy.state || lastKind != enemy.kind) stateAge = enemy.stateTime;
            else stateAge += Time.deltaTime;
            lastState = enemy.state;
            lastKind = enemy.kind;
            swimClock += Time.deltaTime * (enemy.damagedRetreat ? 1.55f : 1f);
            if (enemy.health < oldHealth) flash = .15f;
            oldHealth = enemy.health;
            flash = Mathf.Max(0, flash - Time.deltaTime);

            Sprite[] action = ActionFrames(art, out float progress);
            Sprite[] sequence = action != null && action.Length > 0 ? action : art.travel;
            int frame = sequence == art.travel ? Mathf.FloorToInt(swimClock * art.framesPerSecond) % sequence.Length
                : Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(Mathf.Clamp01(progress) * sequence.Length));
            body.sprite = sequence[frame];
            if (body.sprite == null) return;
            // The authored sprites carry their pivot. A sequence uses one scale to prevent each flap or bite from resizing the body.
            float size = enemy.SizeMultiplier;
            if (art.registeredSprites) body.transform.localScale = art.RegisteredScale(body.sprite, size);
            else
            {
                float widest = 0;
                foreach (Sprite sprite in sequence) if (sprite != null) widest = Mathf.Max(widest, sprite.bounds.size.x);
                body.transform.localScale = Vector3.one * (size * art.width / Mathf.Max(.01f, widest));
            }
            if (Mathf.Abs(enemy.facing.x) > .02f) body.flipX = enemy.facing.x < 0;
            float pitch = Mathf.Asin(Mathf.Clamp(enemy.facing.y, -1, 1)) * Mathf.Rad2Deg;
            body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(pitch, -24, 24) * (body.flipX ? -1 : 1));
            Color tint = flash > 0 || enemy.damageFlash > .05f ? new Color(1f, .64f, .58f) : Color.white;
            if (enemy.state == EnemyState.Defeated) tint.a = Mathf.Clamp01(1 - stateAge / Mathf.Max(.01f, enemy.destroyDelay));
            body.color = tint;
        }

        Sprite[] ActionFrames(FrameSet art, out float progress)
        {
            progress = 0;
            if (enemy.kind == EnemyKind.Shadow && enemy.shadowBasicClock > 0)
            {
                progress = 1 - enemy.shadowBasicClock / Mathf.Max(.01f, enemy.session.tuning.shadowBasicDuration);
                return art.basicAttack;
            }
            if (enemy.state != EnemyState.Telegraph && enemy.state != EnemyState.Lunge) return null;
            if (enemy.IsAirborne)
            {
                progress = stateAge / LifelineEnemy.BirdStrikeWindup;
                return art.attack;
            }
            if (enemy.kind == EnemyKind.Shadow)
            {
                var tuning = enemy.session.tuning;
                float clock = Mathf.Max(enemy.shadowAttackClock, stateAge + (enemy.shadowPhase == 2 ? tuning.shadowWindup : 0));
                float duration = enemy.shadowAttackIndex == 2 ? tuning.shadowWaterShotWarning
                    : tuning.shadowWindup + (enemy.shadowAttackIndex == 1 ? tuning.shadowDashSeconds : 0f);
                progress = clock / Mathf.Max(.01f, duration);
                return enemy.shadowAttackIndex == 1 ? art.dash : enemy.shadowAttackIndex == 2 ? art.waterShot : art.attack;
            }
            progress = enemy.state == EnemyState.Telegraph
                ? .5f * stateAge / Mathf.Max(.01f, enemy.AttackWindup)
                : .5f + .5f * stateAge / Mathf.Max(.01f, enemy.sharkLungeSeconds);
            return art.attack;
        }
    }
}
