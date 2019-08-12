/****************************************************
    文件：ObjectManager.cs
	作者：TravelerTD
    日期：2019/8/11 14:53:2
	功能：基于 ResourceManager 的对象管理
*****************************************************/

using UnityEngine;
using System;
using System.Collections.Generic;

public class ObjectManager : Singleton<ObjectManager> {

    #region 类对象池的使用
    // 类对象池的字典，key：类型，val：ClassObjectPool
    protected Dictionary<Type, object> classPoolDic = new Dictionary<Type, object>();

    /// <summary>
    /// 创建类对象池，创建完成后外面可以保存 ClassObjectPool<T> ，然后调用 Spawn 和 Recycle 来创建和回收对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="maxCount"></param>
    /// <returns></returns>
    public ClassObjectPool<T> GetOrCreateClassPool<T>(int maxCount) where T : class, new() {
        Type type = typeof(T);
        object outObj = null;
        if (!classPoolDic.TryGetValue(type, out outObj) || outObj == null) {
            ClassObjectPool<T> newPool = new ClassObjectPool<T>(maxCount);
            classPoolDic.Add(type, newPool);
        }
        return outObj as ClassObjectPool<T>;
    }
    #endregion
}