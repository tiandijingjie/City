using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace WarField
{
    public class EffectDefines
    {
        // ── Effect type IDs ──────────────────────────────────────────────────────
        // Used as effectAnimId values passed to EffectCtrl.BindEffectAnimWithEntity.
        // Kept as a flat enum so callers have a stable numeric identifier without
        // hard-coding magic numbers.
        public enum EffectType
        {
            MIN = 0,
            // Soldier skill VFX
            POLYMORPH,
            THRONSNARE,
            FLAMEFROUND,
            ASSASSINSKILL,
            // Building attack VFX
            EXPLOSION,
            FROZEN,
            // Projectiles
            ARROW,
            FIREBALL,
            ICESHOT,
            // Hero skill VFX
            HEROINVINCIBLE   = 500,
            HEROFLAMESLASH,
            HEROWHIRLWINDSLASH,
            HEROCRISISUNLEASHED,
            HEROFROSTARROW,
            HEROARROWRAIN,
            HEROFROZENSEAL,
            HEROSTORMFURY,
            // Other
            TRANSPORTIN      = 1000,
            TRANSPORTOUT,
            SELFEXPLOSION,
            MAX,
        }

        // ── Skill indicator types ────────────────────────────────────────────────
        public enum SkillIndicatorType
        {
            MIN = 0,
            AREA,
            DIRCTION,
            SINGLE,
            MAX,
        }
    }

    // ── EffectHandle ─────────────────────────────────────────────────────────────
    // Lightweight token returned by EffectCtrl.AddEffectAt.
    // Use it for all subsequent operations on the spawned effect (move, release, etc.).
    // Becomes invalid after ReleaseEffect is called; check IsValid() before use.
    public struct EffectHandle
    {
        internal int    p_renderSlot;
        internal Entity p_entity;

        public bool IsValid() => p_entity != Entity.Null && p_renderSlot >= 0;

        public static readonly EffectHandle Invalid = new EffectHandle
        {
            p_renderSlot = -1,
            p_entity     = Entity.Null,
        };
    }

    // ── Skill indicator callbacks (kept — indicators are still GameObject-based) ─
    public interface EffectIndicatorCb
    {
        void GiveUpEffect();
    }

    public interface AreaEffectIndicatorCb : EffectIndicatorCb
    {
        void TriggerEffect(Vector2 center);
        void CheckPosition(Vector2 position);
    }

    public interface DirectionEffectIndicatorCb : EffectIndicatorCb
    {
        void TriggerEffect(float angle);
    }

    public interface SingleEffectIndicatorCb : EffectIndicatorCb
    {
        void TriggerEffect(Vector2 position);
        void CheckPosition(Vector2 position);
    }
}
