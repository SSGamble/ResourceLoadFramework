/****************************************************
    文件：ClassObjectPool.cs
	作者：TravelerTD
    日期：2019/8/11 15:1:23
	功能：类对象池
*****************************************************/

using System.Collections.Generic;

public class ClassObjectPool<T> where T : class, new() {
    protected Stack<T> pool = new Stack<T>(); // 池
    protected int mMaxCount = 0; // 最大对象个数，<=0 表示不限个数
    protected int noRecycleCount = 0; // 没有回收的个数

    public ClassObjectPool(int maxCount) {
        mMaxCount = maxCount;
        for (int i = 0; i < maxCount; i++) {
            pool.Push(new T());
        }
    }

    /// <summary>
    /// 从池里面取对象
    /// </summary>
    /// <param name="createIfPoolEmpty">如果池里没有对象了，需不需要 new 一个对象</param>
    /// <returns></returns>
    public T Spawn(bool createIfPoolEmpty) {
        if (pool.Count > 0) {
            T t = pool.Pop();
            if (t == null) {
                t = new T();
            }
            noRecycleCount++;
            return t;
        }
        else {
            if (createIfPoolEmpty) {
                T t = new T();
                noRecycleCount++;
                return t;
            }
        }
        return null;
    }

    /// <summary>
    /// 回收
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public bool Recycle(T obj) {
        if (obj == null) {
            return false;
        }
        if (pool.Count >= mMaxCount && mMaxCount > 0) { // 不属于池里的（直接 new 出来的），直接置为 null，之后会被 GC
            obj = null;
            return false;
        }
        pool.Push(obj);
        noRecycleCount--;
        return true;
    }
}