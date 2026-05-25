using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField  //for the MapTest Scene
{
    public class MapTestScene : MonoBehaviour
    {
        [SerializeField]
        private int _level = 1;

        private void Start()
        {
            WarBuildingCtrl.Instance.InitWarBuildingCtrl();
            WarMapCtrl.Instance.InitMapCtrl(_level, WarFieldElements.Difficulty.NORMAL);
        }
    }
}

