using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

//检查鼠标的活动
public class UIMouthCheck : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private string _value;
    [SerializeField] private bool _isEnabled = true;
    private UIMouthActivityIntf _receiver = null;

    //一般在动态添加UIMouthCheck componecnt时调用
    public void SetValue(string value)
    {
        _value = value;
    }

    public void RegisterReceiver(UIMouthActivityIntf receiver)
    {
        _receiver = receiver;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(_isEnabled == false)
            return;
        if(_receiver != null)
            _receiver.MouthEnter(_value, eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if(_isEnabled == false)
            return;
        if(_receiver != null)
            _receiver.MouthExit(_value, eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(_isEnabled == false)
            return;
        if(_receiver != null)
            _receiver.MouthClick(_value, eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if(_isEnabled == false)
            return;
        if(_receiver != null)
            _receiver.MouthUp(_value, eventData);
    }

    public void SetEnable(bool value)
    {
        _isEnabled = value;
    }

}

public interface UIMouthActivityIntf
{
    public void MouthEnter(string value, PointerEventData eventData);
    public void MouthExit(string value, PointerEventData eventData);
    public void MouthClick(string value, PointerEventData eventData);
    public void MouthUp(string value, PointerEventData eventData);
}


