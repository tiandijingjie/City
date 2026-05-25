using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using PD = PropDefines;

    public class PropCtrl : MonoBehaviour
    {
#region public parameters

        public static PropCtrl Instance = null;
#endregion

#region private parameters

        private readonly Dictionary<PD.PropType, Type> _propType2Class = new Dictionary<PD.PropType, Type>
        {
            { PD.PropType.MINORFIRSTAIDKIT, typeof(MinorFirstAidKitProp) },
            { PD.PropType.SWIFTNESSPOTION, typeof(SwiftnessPotionProp) },
            { PD.PropType.REPAIRHAMMER, typeof(RepairHammerProp) },
            { PD.PropType.BLINDINGBOMB, typeof(BlindingBombProp) },
            { PD.PropType.LANDMINE, typeof(LandmineProp) },
            { PD.PropType.COINPOUCH, typeof(CoinPouchProp) },
            { PD.PropType.FIELDRATIONS, typeof(FieldRationsProp) },
            { PD.PropType.TARGETDUMMY, typeof(TargetDummyProp) },
            { PD.PropType.BASICOCULARSTONECOLLECTOR, typeof(BasicOcularStoneCollectorProp) },
            { PD.PropType.BASICBLOODBURNPOTION, typeof(BasicBloodBurnPotionProp) },
            { PD.PropType.STASISTRAP, typeof(StasisTrapProp) },
            { PD.PropType.AMPLIFIER, typeof(AmplifierProp) },
        };

        private float _propDurationChange = 1.0f; //所有道具持续时间的改变,百分比
        private float _propDamageChange = 1.0f; //所有伤害类型道具造成伤害的,百分比
        private float _propCureChange = 1.0f; //所有治疗类型的道具造成的治疗,百分比

        private AsyncDataPool<Prop> _updatePropList = new AsyncDataPool<Prop>();

        private bool _isPaused = false;
#endregion

#region private parameters' get set

        public float gs_propDurationChange
        {
            get { return _propDurationChange; }
        }

        public float gs_propDamageChange
        {
            get { return _propDamageChange; }
        }

        public float gs_propCureChange
        {
            get { return _propCureChange; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Update()
        {
            if(_isPaused == true)
                return;

            _updatePropList.ForEachAndFlush(static prop => prop.PropUpdate());
        }

        private void OnDestroy()
        {
            _updatePropList?.Dispose();
        }

#endregion

#region public functions
        //将道具加入update list
        public bool AddPropIntoUpdateList(Prop prop)
        {
            _updatePropList.AddItemAsync(prop);
            return true;
        }

        public void RemovePropFromUpdateList(Prop prop)
        {
            _updatePropList.RemoveItemAsync(prop);
        }

        //根据道具类型获取到道具的类
        public Type GetPropClassByType(PD.PropType propType)
        {
            return _propType2Class[propType];
        }

        //改变所有持续性道具的持续时间
        public void ChangePropDuration(float value)
        {
            _propDurationChange *= value;
        }

        public void ChangePropDamage(float value)
        {
            _propDamageChange *= value;
        }

        public void ChangePropCure(float value)
        {
            _propCureChange *= value;
        }

        public void Pause(bool isPaused)
        {
            _isPaused = isPaused;
        }
#endregion

#region private functions

#endregion
    }
}

