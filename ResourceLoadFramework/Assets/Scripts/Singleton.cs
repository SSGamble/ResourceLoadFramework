/****************************************************
    文件：Singleton.cs
	作者：TravelerTD
    日期：2019/8/11 14:55:21
	功能：单例基类
*****************************************************/

using UnityEngine;

public class Singleton<T> where T : new() {
    private static T instance;
    public static T Instance {
        get {
            if (instance == null) {
                instance = new T();
            }
            return instance;
        }
    }
}