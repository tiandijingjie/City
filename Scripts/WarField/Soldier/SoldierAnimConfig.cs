using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SD = SoldierDefines;

    public class SoldierAnimConfig : ScriptableObject
    {
        //记录所有soldier的动画寻址info
        public List<SD.SingleSoldierAnimBakedData> p_allSoldiersBakedList = new List<SD.SingleSoldierAnimBakedData>();

        // 运行时提供给 SoldierCtrl 的极速查询缓存
        private Dictionary<string, Dictionary<SD.SoldierAnimType, SD.FrameAnimClipOffsets>> _runtimeCache;

        public void InitializeCache()
        {
            _runtimeCache = new Dictionary<string, Dictionary<SD.SoldierAnimType, SD.FrameAnimClipOffsets>>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var soldier in p_allSoldiersBakedList)
            {
                var dict = new Dictionary<SD.SoldierAnimType, SD.FrameAnimClipOffsets>();
                for (int i = 0; i < soldier.p_animTypes.Count; i++)
                {
                    dict[soldier.p_animTypes[i]] = soldier.p_clipOffsets[i];
                }
                _runtimeCache[soldier.p_sdName] = dict;
            }
        }

        public Dictionary<SD.SoldierAnimType, SD.FrameAnimClipOffsets> GetClips(string soldierName)
        {
            if (_runtimeCache == null)
                InitializeCache();
            if (_runtimeCache.TryGetValue(soldierName, out var dict))
                return dict;
            return null;
        }
    }
}
