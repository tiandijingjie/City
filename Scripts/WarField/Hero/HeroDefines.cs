using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class HeroDefines
    {
        public enum HeroManipulateSrc
        {
            MIN = 0,
            KEYDIRDOWN,//按下方向键
            KEYDIRUP,//抬起方向键
            MOUSEPRESS, //鼠标选择移动目标
            HOLDPRESS, //站住不动
            UNSELECT, //取消选中
            MAX,
        }
    }
}

