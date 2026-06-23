using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarField.Anim
{
    public class AnimDefines
    {
        public enum AnimEntityType
        {
            SOLDIER,
            BUILDING,
            FARMER,
            VFX
        }
    }

    // Entity id layout:
    // soldier:  [other 25-31][sdtype 8-24][troop 4-7][race 2-3][AnimEntityType 0-1]
    // building: [other 25-31][bdtype 8-24][bdmode 4-7][race 2-3][AnimEntityType 0-1]
    public struct AnimEneityId
    {
        private readonly uint _idValue;

        public AnimEneityId(uint value)
        {
            _idValue = value;
        }

        public static implicit operator uint(AnimEneityId id) => id._idValue;
        public static implicit operator AnimEneityId(uint value) => new AnimEneityId(value);

        public uint GetBits(int startBit, int length)
        {
            uint mask = length == 32 ? uint.MaxValue : (1u << length) - 1;
            return (_idValue >> startBit) & mask;
        }

        public bool Equals(AnimEneityId other) => _idValue == other._idValue;
        public override bool Equals(object obj) => obj is AnimEneityId other && Equals(other);
        public override int GetHashCode() => (int)_idValue;
    }

    [Serializable]
    public class VariationAnimData
    {
        public int p_eventFrame;
        // 变体级播放速率倍率，1.0 = 正常速度，与 AnimCtrl._animFPS 配合使用
        // (已烘焙数据；运行时实体级倍率见 AnimationRuntimeState.p_animRate)
        public float p_frameRate;
        public int p_animFrameCount;
        public List<int> p_animStartOffset = new List<int>();
    }

    [Serializable]
    public class StateAnimData
    {
        public string p_stateName;
        public bool p_isLoop;
        public List<VariationAnimData> p_variations = new List<VariationAnimData>();
    }

    [Serializable]
    public class ElementAnimBakedData //用于读取配置文件,然后转成blob
    {
        public string p_elementName;
        public List<StateAnimData> p_stateAnim = new List<StateAnimData>();
        public Mesh p_bakedMesh;

        public Texture2DArray p_hdColorArray;
        public Texture2DArray p_mdColorArray;
        public Texture2DArray p_ldColorArray;

        public Texture2DArray p_hdNormalArray;
        public Texture2DArray p_mdNormalArray;
        public Texture2DArray p_ldNormalArray;

        public StateAnimData GetStateAnim(string stateName)
        {
            if (p_stateAnim == null || string.IsNullOrEmpty(stateName))
                return null;

            for (int i = 0; i < p_stateAnim.Count; i++)
            {
                StateAnimData e = p_stateAnim[i];
                if (e != null && string.Equals(e.p_stateName, stateName, StringComparison.OrdinalIgnoreCase))
                    return e;
            }

            return null;
        }
    }

    // 运行时从 JSON 文件（{name}_AnimData.json）反序列化 p_stateAnim 数据的包装类。
    // Frame2TextureArray 烘焙工具写入，AnimCtrl.Awake 通过 Resources.Load<TextAsset> 读取。
    [Serializable]
    public class AnimDataWrapper
    {
        public List<StateAnimData> p_stateAnim = new List<StateAnimData>();
    }

    //用来获取animation需要的id
    public interface IAnimInfo
    {
        public uint IAnimInfo_GetEleAnimId();
        public Dictionary<string, uint> IAnimInfo_GetStateId(); //获取到所有state name->state id的dictionary
        public void IAnimInfo_OnAnimEvent(int stateId);//事件通知, -1 表示动画结束 Finish, 否者就是对应的stateId
    }
}
