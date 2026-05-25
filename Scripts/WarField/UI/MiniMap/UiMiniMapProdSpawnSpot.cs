using System;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;

namespace WarField
{
	using UD = UIDefines;
	using SD = SoldierDefines;
    using WE = WarFieldElements;

	public class UiMiniMapProdSpawnSpot : MonoBehaviour, UIMouthActivityIntf, ITask
	{
#region public parameters

#endregion

#region private parameters
	private UiMiniMapProdBarrack _uiMiniMapProdBarrack;
	private int _index; //spwanIndex
    private RectTransform _rectTransform;
    private Vector2 _lastPosition;
    private SoldierConf _sdConf;
    private FriendlyBarrack _barrackRef;

    private SkeletonGraphic _soldierAnim;

	private bool _beInited = false;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

		private void Awake()
		{
			_index = 0;
			_uiMiniMapProdBarrack = null;
			_beInited = false;

            _soldierAnim = transform.Find("SoldierAnim").GetComponent<SkeletonGraphic>();
		}

		private void Start()
		{
            WarFieldGameManager.Instance.RegisterTask(this, WE.TaskType.NORMAL);
            WarFieldGameManager.Instance.SuspendTask(this, WE.TaskType.NORMAL);
		}

#endregion

#region public functions

        public bool InitProdSpawnSpot(int index, UiMiniMapProdBarrack miniMapProdBarrack, FriendlyBarrack barrackRef, SoldierConf conf)
		{
			if (_beInited)
			{
				return false;
			}

			_uiMiniMapProdBarrack = miniMapProdBarrack;
			_index = index;
            GetComponent<UIMouthCheck>().RegisterReceiver(this);
            _rectTransform = GetComponent<RectTransform>();
            _lastPosition = _rectTransform.position;

            _barrackRef = barrackRef;
            _sdConf = conf;

            LoadSoldierAnim(_sdConf.p_name);

            _beInited = true;
			return true;
		}

        //spawn spot的士兵发生了变化
        public bool UpdateSpawnSpot(SoldierConf conf)
        {
            if(_sdConf == conf)
                return false;
            _sdConf = conf;
            LoadSoldierAnim(_sdConf.p_name);
            //tbd 更新item上的士兵显示
            return true;
        }

        public void RunNormalTask(float deltaTime)
		{
            if(_beInited == false)
                return;
            Vector2 mousePos = Input.mousePosition;
            if (Mathf.Abs(mousePos.y - _lastPosition.y) > 5) //5 pixel
            {
                _lastPosition = new Vector2(_rectTransform.position.x, mousePos.y);
                _rectTransform.position = _lastPosition;
                _uiMiniMapProdBarrack.SpawnSpotSelected(_index, _lastPosition.y);
            }
		}

		public void MouthEnter(string value, PointerEventData eventData) { }

		public void MouthExit(string value, PointerEventData eventData) { }

        public void MouthClick(string value, PointerEventData eventData)
        {
            _rectTransform.SetAsLastSibling(); //放置到最前面
            _uiMiniMapProdBarrack.SpawnSpotSelected(_index, _lastPosition.y);
            WarFieldGameManager.Instance.ActiveTask(this, WE.TaskType.NORMAL);
        }

        //选择结果会在CalculateTrail中通知到levelmap或者barrack
        public void MouthUp(string value, PointerEventData eventData)
        {
            WarFieldGameManager.Instance.SuspendTask(this, WE.TaskType.NORMAL);
            _uiMiniMapProdBarrack.SpawnSpotUnselected(_index, _lastPosition.y, _sdConf);
        }

        public void RunFixTask(float deltaTime)
        {
            throw new NotImplementedException();
        }
#endregion

#region private functions

        private void LoadSoldierAnim(string name)
        {
            Addressables.LoadAssetAsync<SkeletonDataAsset>(name.Replace(" ","")).Completed += handle =>
            {
                var skeleton = handle.Result;
                _soldierAnim.skeletonDataAsset = skeleton;
                _soldierAnim.Initialize(true);
                _soldierAnim.AnimationState.SetAnimation(0, "Move", true);
            };
        }
#endregion
	}
}

