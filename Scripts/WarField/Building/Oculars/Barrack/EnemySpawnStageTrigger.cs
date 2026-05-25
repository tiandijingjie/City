using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WBD = WarBuildingDefines;

    public class EnemySpawnStageTrigger : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] private WBD.BarrackTriggerStage _triggerIndex = WBD.BarrackTriggerStage.MIN;

        private EnemyBarrack _barrack = null;
        private EdgeCollider2D _edgeCollider = null;
#endregion

#region private parameters' get set

        public WBD.BarrackTriggerStage gs_triggerIndex
        {
            get { return _triggerIndex; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            _barrack = GetComponentInParent<EnemyBarrack>();
            if (_barrack == null)
            {
                GameLogger.LogError("Failt to get Barrack in trigger");
            }
            _edgeCollider = GetComponent<EdgeCollider2D>();
            if (_edgeCollider == null)
            {
                GameLogger.LogError("Failt to get Edge collider in trigger");
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _barrack.OnSpawnStageTrigger(_triggerIndex);
            //after be triggered, then disable self
            DisableTrigger();
        }
#endregion

#region public functions

        public void InitTrigger(Vector2[] points)
        {
            _edgeCollider.points = points;
        }

        public void DisableTrigger()
        {
            gameObject.SetActive(false);
        }

#endregion

#region private functions

#endregion
    }
}

