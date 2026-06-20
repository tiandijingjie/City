using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;

    [CreateAssetMenu(fileName = "GlobalWeaponConfig", menuName = "WarField/Weapon/GlobalWeaponConfig")]
    public class GlobalWeaponConfig : ScriptableObject //增加一个可创建的文件,作为全局的weapon的配置
    {
        public List<WD.ElementWeaponBakedData> p_allElementsBakedList = new List<WD.ElementWeaponBakedData>();

        public WD.ElementWeaponBakedData GetElementData(uint weaponId)
        {
            if (p_allElementsBakedList == null || weaponId <= 0)
                return null;

            for (int i = 0; i < p_allElementsBakedList.Count; i++)
            {
                WD.ElementWeaponBakedData e = p_allElementsBakedList[i];
                if (e != null && e.p_weaponId == weaponId)
                    return e;
            }

            return null;
        }
    }
}
