using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{

    public class EffectDefines
    {
        //技能效果
        public enum EffectType
        {
            MIN = 0,
            //soldier skill effect
            POLYMORPH, //变形术
            THRONSNARE, //荆棘术
            FLAMEFROUND, //燃烧地面
            ASSASSINSKILL, //刺客技能特效

            //building attack effect
            EXPLOSION, //爆炸
            FROZEN, //冰冻

            //Hero skill Effects
            HEROINVINCIBLE = 500, //无敌
            HEROFLAMESLASH, //火焰斩
            HEROWHIRLWINDSLASH, //旋风斩
            HEROCRISISUNLEASHED, //危机降临
            HEROFROSTARROW, //寒冰箭
            HEROARROWRAIN, //箭雨
            HEROFROZENSEAL, //冰封
            HEROSTORMFURY, //风雨交加

            //other effects
            TRANSPORTIN = 1000, //进入传送
            TRANSPORTOUT, //传送出来
            SELFEXPLOSION, //自爆
            MAX,
        }

        //技能释放指示的类型
        public enum SkillIndicatorType
        {
            MIN = 0,
            AREA, //指示是一个区域
            DIRCTION, //指示是一个方向
            SINGLE, //指示的单个position
            MAX,
        }
    }

    //effect中_mainParticle结束时的回调 或者spine的通知
    public interface MainEffectFinishCb
    {
        public void MainEffectFinish(string effInfo = null);
    }

    //effect indicator
    public interface EffectIndicatorCb
    {
        public void GiveUpEffect(); //放弃释放effect
    }

    public interface AreaEffectIndicatorCb : EffectIndicatorCb
    {
        //center:区域Effect的中心点
        public void TriggerEffect(Vector2 center);
        //检查当前鼠标位置
        public void CheckPosition(Vector2 position);
    }

    public interface DirectionEffectIndicatorCb : EffectIndicatorCb
    {
        //angle: 与x轴正方向的转角
        public void TriggerEffect(float angle);
    }

    public interface SingleEffectIndicatorCb : EffectIndicatorCb
    {
        //鼠标的点击的位置
        public void TriggerEffect(Vector2 position);
        //检查当前鼠标位置
        public void CheckPosition(Vector2 position);
    }
}

