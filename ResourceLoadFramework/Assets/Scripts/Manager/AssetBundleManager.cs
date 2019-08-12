/****************************************************
    文件：AssetBundleManager.cs
	作者：TravelerTD
    日期：2019/8/11 14:52:5
	功能：AssetBundle 管理
*****************************************************/

using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TDFramework;
using UnityEngine;

public class AssetBundleManager : Singleton<AssetBundleManager> {
    /// <summary>
    /// 资源关系依赖配表，可以根据 crc 来找到对应资源块
    /// </summary>
    protected Dictionary<uint, ResourceItem> resourceItemDic = new Dictionary<uint, ResourceItem>();
    /// <summary>
    /// 储存已加载的 AB包，防止重复加载和卸载，key：crc
    /// </summary>
    protected Dictionary<uint, AssetBundleItem> assetBundleItemDic = new Dictionary<uint, AssetBundleItem>();
    /// <summary>
    /// AssetBundleItem 类对象池
    /// </summary>
    protected ClassObjectPool<AssetBundleItem> assetBundleItemPool = ObjectManager.Instance.GetOrCreateClassPool<AssetBundleItem>(500);
    protected string abConfigABName = "AssetBundleConfig";
    protected string ABLoadPath {
        get {
            return Application.streamingAssetsPath + "/";
        }
    }

    /// <summary>
    /// 加载 AssetBundle 配置表
    /// </summary>
    /// <returns></returns>
    public bool LoadAssetBundleConfig() {
        resourceItemDic.Clear();
        string configPath = Application.streamingAssetsPath + "/abconfig";
        AssetBundle abConfig = AssetBundle.LoadFromFile(configPath);
        TextAsset textAsset = abConfig.LoadAsset<TextAsset>("AssetBundleConfig");
        if (textAsset == null) {
            Debug.LogError("AssetBundle is no exist!");
            return false;
        }
        MemoryStream ms = new MemoryStream(textAsset.bytes);
        BinaryFormatter bf = new BinaryFormatter();
        AssetBundleConfig config = (AssetBundleConfig)bf.Deserialize(ms);
        ms.Close();
        // 添加到字典里
        for (int i = 0; i < config.ABList.Count; i++) {
            ABBase abBase = config.ABList[i];
            ResourceItem item = new ResourceItem();
            item.crc = abBase.Crc;
            item.assetName = abBase.AssetName;
            item.abName = abBase.ABName;
            item.dependAB = abBase.ABDependce;
            if (resourceItemDic.ContainsKey(item.crc)) {
                Debug.LogError("重复的 crc 资源名:" + item.assetName + "，ab 包名：" + item.abName);
            }
            else {
                resourceItemDic.Add(item.crc, item);
            }
        }
        return true;
    }

    /// <summary>
    /// 根据路径的 crc 加载中间类 ResourceItem
    /// </summary>
    /// <param name="crc"></param>
    /// <returns></returns>
    public ResourceItem LoadResourceAssetBundle(uint crc) {
        ResourceItem item = null;
        if (!resourceItemDic.TryGetValue(crc, out item) || item == null) {
            Debug.LogError(string.Format("LoadResourceAssetBundle error: can not find crc {0} in AssetBundleConfig", crc.ToString()));
            return item;
        }
        if (item.ab != null) {
            return item;
        }
        item.ab = LoadAssetBundle(item.abName); // 加载 ab
        if (item.dependAB != null) { // 加载依赖项
            for (int i = 0; i < item.dependAB.Count; i++) {
                LoadAssetBundle(item.dependAB[i]);
            }
        }
        return item;
    }

    /// <summary>
    /// 加载单个 assetbundle，根据名字
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private AssetBundle LoadAssetBundle(string name) {
        AssetBundleItem item = null;
        uint crc = CRC32.GetCRC32(name);
        if (!assetBundleItemDic.TryGetValue(crc, out item)) {
            // 加载 ab
            AssetBundle assetBundle = null;
            string fullPath = ABLoadPath + name;
            assetBundle = AssetBundle.LoadFromFile(fullPath);
            if (assetBundle == null) {
                Debug.LogError(" Load AssetBundle Error:" + fullPath);
            }
            // 给 AssetBundleItem 赋值
            item = assetBundleItemPool.Spawn(true);
            item.assetBundle = assetBundle;
            item.RefCount++;
            assetBundleItemDic.Add(crc, item);
        }
        else {
            item.RefCount++; // 已经加载过了，此次加载就不需要重复加载 ab，而是把引用计数 +1
        }
        return item.assetBundle;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="item"></param>
    public void ReleaseAsset(ResourceItem item) {
        if (item == null) {
            return;
        }
        if (item.dependAB != null && item.dependAB.Count > 0) { // 有被引用
            // 先卸载依赖项
            for (int i = 0; i < item.dependAB.Count; i++) {
                UnLoadAssetBundle(item.dependAB[i]);
            }
            // 再卸载自己
            UnLoadAssetBundle(item.abName);
        }
    }

    /// <summary>
    /// 卸载 AB
    /// </summary>
    /// <param name="name"></param>
    private void UnLoadAssetBundle(string name) {
        AssetBundleItem item = null;
        uint crc = CRC32.GetCRC32(name);
        if (assetBundleItemDic.TryGetValue(crc, out item) && item != null) {
            item.RefCount--;
            if (item.RefCount <= 0 && item.assetBundle != null) { // 确保没有被引用了
                item.assetBundle.Unload(true); // 卸载
                item.Rest(); // 还原 类对象池
                assetBundleItemPool.Recycle(item);
                assetBundleItemDic.Remove(crc);
            }
        }
    }

    /// <summary>
    /// 根据 crc 查找 ResourceItem
    /// </summary>
    /// <param name="crc"></param>
    /// <returns></returns>
    public ResourceItem FindResourceItme(uint crc) {
        ResourceItem item = null;
        resourceItemDic.TryGetValue(crc, out item);
        return item;
    }
}

public class AssetBundleItem {
    public AssetBundle assetBundle = null;
    public int RefCount; // 引用计数，有一个依赖就+1，防止ab包的重复加载或卸载

    /// <summary>
    /// 还原 类对象池
    /// </summary>
    public void Rest() {
        assetBundle = null;
        RefCount = 0;
    }
}

/// <summary>
/// 资源块，中间类，缓存
/// </summary>
public class ResourceItem {
    // ab 相关
    public uint crc = 0; // 资源路径的 crc
    public string assetName = string.Empty; // 该资源的文件名
    public string abName = string.Empty; // 该资源所在的 ab名
    public List<string> dependAB = null; // 该资源所依赖的 AB
    public AssetBundle ab = null; // 该资源加载完的 ab
    // 资源相关
    public Object obj = null; // 加载出来的资源对象
    public int guid = 0; // 资源唯一标识
    public float lastUseTime = 0.0f; // 资源最后所使用的时间
    public bool clear = true; // 是否跳场景清掉
    protected int refCount = 0; // 资源引用计数
    public int RefCount {
        get { return refCount; }
        set {
            refCount = value;
            if (refCount < 0) {
                Debug.LogError("refcount < 0" + refCount + " ," + (obj != null ? obj.name : "name is null"));
            }
        }
    }
}