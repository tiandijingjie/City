using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class PopulationSystem : MonoBehaviour
    {
#region public parameters

        public static PopulationSystem Instance;
#endregion

#region private parameters

        [SerializeField] private int _maxPopulation = 500;

        private int _curMaxPopulation = 0; //当前的人口上限
        private int _curPopulation = 0;

        private bool _beInited;
#endregion

#region private parameters' get set

        public int gs_maxPopulation
        {
            get { return _curPopulation; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _beInited = false;
        }

#endregion

#region public functions

        public bool InitPopulationSystem()
        {
            if (_beInited == true)
                return false;

            _curMaxPopulation = 20;
            _curPopulation = 0;
            _beInited = true;
            return true;
        }
#endregion

#region private functions

#endregion
    }
}

