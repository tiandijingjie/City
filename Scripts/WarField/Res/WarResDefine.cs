using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class WarResDefine
    {
        public enum ResTypes
        {
            MIN = 0,
            GOLDCOIN, //金币
            OCULARSTONE, //曈石 用于抽卡
            GEM, //宝石 用于在游戏外的升级
            MAX,
        }

        //资源的丰度
        public enum ResContainLevel
        {
            MIN = 0,
            LOW,
            MID,
            HIGH,
            MAX,
        }
    }

    //监听资源总量的变化
    public interface IWarResListener
    {
        //when res amount change notify
        public void ResChange(WarResDefine.ResTypes type, int deltaVlaue, int curTotal);
    }

    //排队等待资源
    public interface IResWaiter
    {
        //get enough res
        //return 是否真的消耗资源
        public bool ResReady(object indexer, WarResDefine.ResTypes type, int amount);
    }

    // 查询可采集的资源,并进行监听
    public interface ICollectResListener
    {
        // 成功找到可用未锁定的资源
        // 返回true 才能锁定资源
        public bool ICollectResListener_OnResourceFound(int poolIndex, Vector2 pos);

        // 当前场上没有可用的资源
        public void ICollectResListener_OnResourceNotFound();

        // 目标资源在农民走过去的过程中因故（如超时）意外消失了
        public void ICollectResListener_OnResourceDisappeared(int poolIndex);
    }
}
