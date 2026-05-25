using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class WeaponDefines : MonoBehaviour
    {
        //投射物分类，子弹，炮弹
        public enum ProjectileTypes
        {
            MIN = 0,
            BULLET,
            SHELL,
            NOTARGETBULLET,//没有目标的投掷物
            MAX,
        }
        
    }
}

