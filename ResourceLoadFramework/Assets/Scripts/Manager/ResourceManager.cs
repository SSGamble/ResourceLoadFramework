/****************************************************
    文件：ResourceManager.cs
	作者：TravelerTD
    日期：2019/8/11 14:52:42
	功能：基于 AssetBundle 的资源管理
*****************************************************/

using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TDFramework;
using UnityEngine;

public class ResourceObj {
    //路径对应CRC
    public uint crc = 0;
    //存ResourceItem
    public ResourceItem resItem = null;
    //实例化出来的GameObject
    public GameObject cloneObj = null;
    //是否跳场景清除
    public bool clear = true;
    //储存GUID
    public long guid = 0;
    //是否已经放回对象池
    public bool already = false;
    //--------------------------------
    //是否放到场景节点下面
    public bool setSceneParent = false;
    //实例化资源加载完成回调
    public OnAsyncObjFinish dealFinish = null;
    //异步参数
    public object param1, param2, param3 = null;
    //离线数据
    //public OfflineData m_OfflineData = null;

    public void Reset() {
        crc = 0;
        cloneObj = null;
        clear = true;
        guid = 0;
        resItem = null;
        already = false;
        setSceneParent = false;
        dealFinish = null;
        param1 = param2 = param3 = null;
        //m_OfflineData = null;
    }
}

/// <summary>
/// 资源加载完成回调
/// </summary>
/// <param name="path"></param>
/// <param name="obj"></param>
/// <param name="param1"></param>
/// <param name="param2"></param>
/// <param name="param3"></param>
public delegate void OnAsyncObjFinish(string path, Object obj, object param1 = null, object param2 = null, object param3 = null);
/// <summary>
/// 实例化对象加载完成回调
/// </summary>
/// <param name="path"></param>
/// <param name="resObj"></param>
/// <param name="param1"></param>
/// <param name="param2"></param>
/// <param name="param3"></param>
public delegate void OnAsyncFinsih(string path, ResourceObj resObj, object param1 = null, object param2 = null, object param3 = null);

public class ResourceManager : Singleton<ResourceManager> {
    /// <summary>
    /// 最长连续卡着加载资源的时间，单位微妙
    /// </summary>
    private const long MAXLOADRESTIME = 200000;
    /// <summary>
    /// 最大缓存个数
    /// </summary>
    private const int MAXCACHECOUNT = 500;
    /// <summary>
    /// 是否从 AB 加载
    /// </summary>
    public bool loadFormAssetBundle = true;
    /// <summary>
    /// 缓存使用的资源列表
    /// </summary>
    public Dictionary<uint, ResourceItem> AssetDic { get; set; } = new Dictionary<uint, ResourceItem>();
    /// <summary>
    /// 缓存引用计数为零的资源列表，即目前暂时没有使用的资源，达到缓存最大的时候就会释放这个列表里面最早没用的资源
    /// </summary>
    protected CMapList<ResourceItem> noRefrenceAssetMapList = new CMapList<ResourceItem>();

    /// <summary>
    /// 同步资源加载，外部直接调用，仅加载不需要实例化的资源，例如 Texture,音频 等等
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="path"></param>
    /// <returns></returns>
    public T LoadResource<T>(string path) where T : UnityEngine.Object {
        if (string.IsNullOrEmpty(path)) {
            return null;
        }
        uint crc = CRC32.GetCRC32(path);
        ResourceItem item = GetCacheResourceItem(crc); // 从资源池获取缓存资源
        if (item != null) {
            return item.obj as T;
        }
        T obj = null;
        // 编辑器下运行
#if UNITY_EDITOR
        if (!loadFormAssetBundle) {
            item = AssetBundleManager.Instance.FindResourceItme(crc);
            if (item != null && item.ab != null) {
                if (item.obj != null) {
                    obj = (T)item.obj;
                }
                else {
                    obj = item.ab.LoadAsset<T>(item.assetName);
                }
            }
            else {
                if (item == null) {
                    item = new ResourceItem();
                    item.crc = crc;
                }
                obj = LoadAssetByEditor<T>(path);
            }
        }
#endif
        // 其他情况运行
        if (obj == null) {
            item = AssetBundleManager.Instance.LoadResourceAssetBundle(crc);
            if (item != null && item.ab != null) {
                if (item.obj != null) {
                    obj = item.obj as T;
                }
                else {
                    obj = item.ab.LoadAsset<T>(item.assetName);
                }
            }
        }
        CacheResource(path, ref item, crc, obj); // 缓存
        return obj;
    }

    /// <summary>
    /// 从资源池获取缓存资源
    /// </summary>
    /// <param name="crc"></param>
    /// <param name="addRefcount">增加的引用计数</param>
    /// <returns></returns>
    ResourceItem GetCacheResourceItem(uint crc, int addRefcount = 1) {
        ResourceItem item = null;
        if (AssetDic.TryGetValue(crc, out item)) {
            if (item != null) {
                item.RefCount += addRefcount;
                item.lastUseTime = Time.realtimeSinceStartup;
            }
        }
        return item;
    }

#if UNITY_EDITOR
    protected T LoadAssetByEditor<T>(string path) where T : UnityEngine.Object {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
    }
#endif

    /// <summary>
    /// 缓存加载的资源
    /// </summary>
    /// <param name="path"></param>
    /// <param name="item"></param>
    /// <param name="crc"></param>
    /// <param name="obj"></param>
    /// <param name="addRefcount"></param>
    void CacheResource(string path, ref ResourceItem item, uint crc, Object obj, int addRefcount = 1) {
        // 缓存太多，清除最早没有使用的资源
        WashOut();

        if (item == null) {
            Debug.LogError("CacheResource-ResourceItem is null, path: " + path);
        }

        if (obj == null) {
            Debug.LogError("CacheResource-ResouceLoad Fail :  " + path);
        }

        item.obj = obj;
        item.guid = obj.GetInstanceID();
        item.lastUseTime = Time.realtimeSinceStartup;
        item.RefCount += addRefcount;
        ResourceItem oldItme = null;
        if (AssetDic.TryGetValue(item.crc, out oldItme)) {
            AssetDic[item.crc] = item; // 已经包含，替换更新
        }
        else {
            AssetDic.Add(item.crc, item);
        }
    }

    /// <summary>
    /// 缓存太多，清除最早没有使用的资源
    /// </summary>
    protected void WashOut() {
        // 当大于缓存个数时，释放前一半的资源
        while (noRefrenceAssetMapList.Size() >= MAXCACHECOUNT) {
            for (int i = 0; i < MAXCACHECOUNT / 2; i++) {
                ResourceItem item = noRefrenceAssetMapList.Back();
                DestoryResouceItme(item, true);
            }
        }
    }

    /// <summary>
    /// 回收一个资源
    /// </summary>
    /// <param name="item"></param>
    /// <param name="destroy">是否删除缓存</param>
    protected void DestoryResouceItme(ResourceItem item, bool destroyCache = false) {
        if (item == null || item.RefCount > 0) {
            return;
        }
        // 不删除缓存的情况
        if (!destroyCache) {
            noRefrenceAssetMapList.InsertToHead(item);
            return;
        }
        // 删除缓存的情况
        if (!AssetDic.Remove(item.crc)) {
            return;
        }
        noRefrenceAssetMapList.Remove(item);
        AssetBundleManager.Instance.ReleaseAsset(item); // 释放 assetbundle 引用
        // 清空资源对应的对象池
        //ObjectManager.Instance.ClearPoolObject(item.crc);
        if (item.obj != null) {
            item.obj = null;
#if UNITY_EDITOR
            Resources.UnloadUnusedAssets();
#endif
        }
    }

    /// <summary>
    ///  不需要实例化的资源的卸载，根据 ResouceObj
    /// </summary>
    /// <param name="resObj"></param>
    /// <param name="destoryObj"></param>
    /// <returns></returns>
    public bool ReleaseResouce(ResourceObj resObj, bool destoryObj = false) {
        if (resObj == null)
            return false;
        ResourceItem item = null;
        if (!AssetDic.TryGetValue(resObj.crc, out item) || null == item) {
            Debug.LogError("AssetDic 里不存在该资源：" + resObj.cloneObj.name + "  可能释放了多次");
        }
        GameObject.Destroy(resObj.cloneObj);
        item.RefCount--;
        DestoryResouceItme(item, destoryObj);
        return true;
    }

    /// <summary>
    /// 不需要实例化的资源的卸载，根据对象
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="destoryObj"></param>
    /// <returns></returns>
    public bool ReleaseResouce(Object obj, bool destoryObj = false) {
        if (obj == null) {
            return false;
        }
        ResourceItem item = null;
        foreach (ResourceItem res in AssetDic.Values) {
            if (res.guid == obj.GetInstanceID()) {
                item = res;
            }
        }
        if (item == null) {
            Debug.LogError("AssetDic 里不存在改资源：" + obj.name + "  可能释放了多次");
            return false;
        }
        item.RefCount--;
        DestoryResouceItme(item, destoryObj); // 回收
        return true;
    }

    /// <summary>
    /// 不需要实例化的资源卸载，根据路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="destoryObj"></param>
    /// <returns></returns>
    public bool ReleaseResouce(string path, bool destoryObj = false) {
        if (string.IsNullOrEmpty(path)) {
            return false;
        }

        uint crc = CRC32.GetCRC32(path);
        ResourceItem item = null;
        if (!AssetDic.TryGetValue(crc, out item) || null == item) {
            Debug.LogError("AssetDic里不存在改资源：" + path + "  可能释放了多次");
        }

        item.RefCount--;

        DestoryResouceItme(item, destoryObj);
        return true;
    }
}

/// <summary>
/// 双向链表结构节点
/// </summary>
/// <typeparam name="T"></typeparam>
public class DoubleLinkedListNode<T> where T : class, new() {
    public DoubleLinkedListNode<T> prev = null; // 前一个节点
    public DoubleLinkedListNode<T> next = null; // 后一个节点
    public T t = null; // 当前节点
}

/// <summary>
/// 双向链表结构
/// </summary>
/// <typeparam name="T"></typeparam>
public class DoubleLinedList<T> where T : class, new() {
    public DoubleLinkedListNode<T> Head = null; // 表头
    public DoubleLinkedListNode<T> Tail = null; // 表尾
    protected int count = 0; // 个数
    public int Count {
        get { return count; }
    }
    /// <summary>
    /// 双向链表结构类对象池
    /// </summary>
    protected ClassObjectPool<DoubleLinkedListNode<T>> doubleLinkNodePool = ObjectManager.Instance.GetOrCreateClassPool<DoubleLinkedListNode<T>>(500);

    /// <summary>
    /// 添加一个节点到头部
    /// </summary>
    /// <param name="t"></param>
    /// <returns>返回这个节点</returns>
    public DoubleLinkedListNode<T> AddToHeader(T t) {
        DoubleLinkedListNode<T> pList = doubleLinkNodePool.Spawn(true);
        pList.next = null;
        pList.prev = null;
        pList.t = t;
        return AddToHeader(pList);
    }

    /// <summary>
    /// 添加一个节点到头部
    /// </summary>
    /// <param name="pNode"></param>
    /// <returns>返回这个节点</returns>
    public DoubleLinkedListNode<T> AddToHeader(DoubleLinkedListNode<T> pNode) {
        if (pNode == null)
            return null;
        pNode.prev = null;
        if (Head == null) {
            Head = Tail = pNode;
        }
        else {
            pNode.next = Head;
            Head.prev = pNode;
            Head = pNode;
        }
        count++;
        return Head;
    }

    /// <summary>
    /// 添加节点到尾部
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public DoubleLinkedListNode<T> AddToTail(T t) {
        DoubleLinkedListNode<T> pList = doubleLinkNodePool.Spawn(true);
        pList.next = null;
        pList.prev = null;
        pList.t = t;
        return AddToTail(pList);
    }

    /// <summary>
    /// 添加节点到尾部
    /// </summary>
    /// <param name="pNode"></param>
    /// <returns></returns>
    public DoubleLinkedListNode<T> AddToTail(DoubleLinkedListNode<T> pNode) {
        if (pNode == null)
            return null;
        pNode.next = null;
        if (Tail == null) {
            Head = Tail = pNode;
        }
        else {
            pNode.prev = Tail;
            Tail.next = pNode;
            Tail = pNode;
        }
        count++;
        return Tail;
    }

    /// <summary>
    /// 移除某个节点
    /// </summary>
    /// <param name="pNode"></param>
    public void RemoveNode(DoubleLinkedListNode<T> pNode) {
        if (pNode == null)
            return;
        if (pNode == Head)
            Head = pNode.next;
        if (pNode == Tail)
            Tail = pNode.prev;
        if (pNode.prev != null)
            pNode.prev.next = pNode.next;
        if (pNode.next != null)
            pNode.next.prev = pNode.prev;
        pNode.next = pNode.prev = null;
        pNode.t = null;
        doubleLinkNodePool.Recycle(pNode);
        count--;
    }

    /// <summary>
    /// 把某个节点移动到头部
    /// </summary>
    /// <param name="pNode"></param>
    public void MoveToHead(DoubleLinkedListNode<T> pNode) {
        if (pNode == null || pNode == Head)
            return;
        if (pNode.prev == null && pNode.next == null)
            return;
        if (pNode == Tail)
            Tail = pNode.prev;
        if (pNode.prev != null)
            pNode.prev.next = pNode.next;
        if (pNode.next != null)
            pNode.next.prev = pNode.prev;
        pNode.prev = null;
        pNode.next = Head;
        Head.prev = pNode;
        Head = pNode;
        if (Tail == null) {
            Tail = Head;
        }
    }
}

/// <summary>
/// 封装双向链表
/// </summary>
/// <typeparam name="T"></typeparam>
public class CMapList<T> where T : class, new() {
    DoubleLinedList<T> m_DLink = new DoubleLinedList<T>();
    Dictionary<T, DoubleLinkedListNode<T>> m_FindMap = new Dictionary<T, DoubleLinkedListNode<T>>();

    /// <summary>
    /// 析构函数
    /// </summary>
    ~CMapList() {
        Clear();
    }

    /// <summary>
    /// 情况列表
    /// </summary>
    public void Clear() {
        while (m_DLink.Tail != null) {
            Remove(m_DLink.Tail.t);
        }
    }

    /// <summary>
    /// 插入一个节点到表头
    /// </summary>
    /// <param name="t"></param>
    public void InsertToHead(T t) {
        DoubleLinkedListNode<T> node = null;
        if (m_FindMap.TryGetValue(t, out node) && node != null) {
            m_DLink.AddToHeader(node);
            return;
        }
        m_DLink.AddToHeader(t);
        m_FindMap.Add(t, m_DLink.Head);
    }

    /// <summary>
    /// 从表尾弹出一个结点
    /// </summary>
    public void Pop() {
        if (m_DLink.Tail != null) {
            Remove(m_DLink.Tail.t);
        }
    }

    /// <summary>
    /// 删除某个节点
    /// </summary>
    /// <param name="t"></param>
    public void Remove(T t) {
        DoubleLinkedListNode<T> node = null;
        if (!m_FindMap.TryGetValue(t, out node) || node == null) {
            return;
        }
        m_DLink.RemoveNode(node);
        m_FindMap.Remove(t);
    }

    /// <summary>
    /// 获取到尾部节点
    /// </summary>
    /// <returns></returns>
    public T Back() {
        return m_DLink.Tail == null ? null : m_DLink.Tail.t;
    }

    /// <summary>
    /// 返回节点个数
    /// </summary>
    /// <returns></returns>
    public int Size() {
        return m_FindMap.Count;
    }

    /// <summary>
    /// 查找是否存在该节点
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public bool Find(T t) {
        DoubleLinkedListNode<T> node = null;
        if (!m_FindMap.TryGetValue(t, out node) || node == null)
            return false;
        return true;
    }

    /// <summary>
    /// 刷新某个节点，把节点移动到头部
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public bool Reflesh(T t) {
        DoubleLinkedListNode<T> node = null;
        if (!m_FindMap.TryGetValue(t, out node) || node == null)
            return false;
        m_DLink.MoveToHead(node);
        return true;
    }
}
