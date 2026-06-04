using System.Collections.Generic;
using UnityEngine;

namespace WarField.Anim
{
    [CreateAssetMenu(fileName = "GlobalAnimConfig", menuName = "WarField/Animation/GlobalAnimConfig")]
    public class GlobalAnimConfig : ScriptableObject
    {
        public List<ElementAnimBakedData> p_allElementsBakedList = new List<ElementAnimBakedData>();

        public ElementAnimBakedData GetElementData(string elementName)
        {
            if (p_allElementsBakedList == null || string.IsNullOrEmpty(elementName))
                return null;

            for (int i = 0; i < p_allElementsBakedList.Count; i++)
            {
                ElementAnimBakedData e = p_allElementsBakedList[i];
                if (e != null && e.p_elementName == elementName)
                    return e;
            }

            return null;
        }
    }
}
