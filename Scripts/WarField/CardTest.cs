using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarField;

using CD = WarField.CardDefines;
using WRD = WarField.WarResDefine;
using PD = WarField.PropDefines;

public class CardTest : MonoBehaviour, IWarResListener
{
    [SerializeField] private Text _goldText;
    private bool _added = false;
    private int i = 0;

    private void Start()
    {
        Invoke("Registe", 0.1f);
    }

    private void Registe()
    {
        WarResCtrl.Instance.RegisterResListener(WRD.ResTypes.GOLDCOIN, this);
    }

    public void ButtonEvent()
    {
        //CardLowGoldMineAddGoldPerSecond.Instance.TakeAndActive(CardDefines.CardLevel.NORMAL, true);
        //CardDefenceAddArcTower.Instance.TakeAndActive(CardDefines.CardLevel.BASE);

        // WarBuildingCtrl.Instance.GetWarBuilding(WarFieldElements.RaceType.Human, WarBuildingDefines.BuildingMode.DEFENCE, out var list);
        // UIBuildingUpgradeBar.Instance.ShowBuildingUpgradeBar(list[0]);
        // SoldierCtrl.Instance.gs_curHero.TriggerActiveSkill("Frost Arrow");
        // Debug.Log("CardTest ");

        WarFieldBagSystem.Instance.ReceiveProp(PD.PropType.AMPLIFIER);
        Prop prop = WarFieldBagSystem.Instance.GetPropFromBag(PD.PropType.AMPLIFIER);
        //prop.UseProp();
    }

    public void ResChange(WarResDefine.ResTypes type, int deltaVlaue, int curTotal)
    {
        _goldText.text = curTotal.ToString();
    }
}
