/****************************************************
    文件：BundleEditor.cs
	作者：TravelerTD
    日期：2019/8/9 15:59:44
	功能：生成 AB 包
*****************************************************/
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using TDFramework;
using UnityEditor;
using UnityEngine;

public class BundleEditor : MonoBehaviour {
    /// <summary>
    /// 打包配置表路径
    /// </summary>
    private static string ABCONFIGPATH = "Assets/Scripts/Editor/ABConfig.asset";
    /// <summary>
    /// 打包路径
    /// </summary>
    private static string bundleTargetPath = Application.streamingAssetsPath;
    /// <summary>
    /// 过滤后的 ab 包，保存的是剔除冗余AB包后的资源路径
    /// </summary>
    private static List<string> allFileAB = new List<string>();
    /// <summary>
    /// 所有文件夹 ab 包，用于对指定文件夹进行打包，key：ab包名，val：路径
    /// </summary>
    private static Dictionary<string, string> allFileDir = new Dictionary<string, string>();
    /// <summary>
    /// 单个 Prefab 的 ab 包，用于对指定文件夹下所有单个文件进行打包，key：ab包名，val：路径
    /// </summary>
    private static Dictionary<string, List<string>> allPrefabDir = new Dictionary<string, List<string>>();
    /// <summary>
    /// 存储所有有效路径，相比较 allFileAB 而言过滤掉了一些不需要动态加载的资源
    /// </summary>
    private static List<string> configFile = new List<string>();

    [MenuItem("Tools/打包")]
    public static void Build() {
        allFileDir.Clear();
        allFileAB.Clear();
        allPrefabDir.Clear();
        configFile.Clear();
        ABConfig abConfig = AssetDatabase.LoadAssetAtPath<ABConfig>(ABCONFIGPATH); // 加载配置表
        // 先处理文件夹
        foreach (ABConfig.FileDirABName fileDir in abConfig.allFileDirAB) { // 遍历配置表中的文件夹
            if (allFileDir.ContainsKey(fileDir.ABName)) {
                Debug.LogError("AB包配置名字重复，请检查！");
            }
            else {
                allFileDir.Add(fileDir.ABName, fileDir.Path);
                allFileAB.Add(fileDir.Path);
                configFile.Add(fileDir.Path);
            }
        }
        // 再处理单个文件，Prefab
        string[] allStr = AssetDatabase.FindAssets("t:Prefab", abConfig.allPrefabPath.ToArray()); // 找到所有的 Prefab，返回的是 guid 数组
        for (int i = 0; i < allStr.Length; i++) { // 遍历 Prefab
            string path = AssetDatabase.GUIDToAssetPath(allStr[i]); // 根据 guid 获取资源路径
            EditorUtility.DisplayProgressBar("查找 Prefab", "Prefab:" + path, i * 1.0f / allStr.Length); // 进度条
            configFile.Add(path);
            if (!ContainAllFileAB(path)) { // 判断过滤
                // 一个 Prefab 其实包含了很多的东西，如贴图，shader 等，如果 shader 已经打包了，那么这里就不需要打包了
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                string[] allDepend = AssetDatabase.GetDependencies(path); // 获取 Prefab 的依赖项，包含 Prefab 自己，还包含脚本
                List<string> allDependPath = new List<string>(); // 一个 Prefab 的所有依赖项路径
                for (int j = 0; j < allDepend.Length; j++) { // 遍历依赖项路径
                    if (!ContainAllFileAB(allDepend[j]) && !allDepend[j].EndsWith(".cs")) { // 过滤依赖项，剔除脚本
                        allFileAB.Add(allDepend[j]);
                        allDependPath.Add(allDepend[j]);
                    }
                }
                if (allPrefabDir.ContainsKey(obj.name)) {
                    Debug.LogError("存在相同名字的 Prefab :" + obj.name);
                }
                else {
                    allPrefabDir.Add(obj.name, allDependPath); // 一个 Prefab 过滤后的依赖
                }
            }
        }
        // 设置文件夹打包的 ab名
        foreach (string name in allFileDir.Keys) {
            SetABName(name, allFileDir[name]);
        }
        // 设置单个文件 Prefab 的 ab名
        foreach (string name in allPrefabDir.Keys) {
            SetABName(name, allPrefabDir[name]);
        }
        // 打包
        BunildAssetBundle();

        // 清除所有的 AB包 名字
        string[] oldABName = AssetDatabase.GetAllAssetBundleNames(); // 设置好的所有 AB包 的名字，eg：attack，shader，sound
        for (int i = 0; i < oldABName.Length; i++) {
            AssetDatabase.RemoveAssetBundleName(oldABName[i], true);
            EditorUtility.DisplayProgressBar("清除AB包名", "名字:" + oldABName[i], i * 1.0f / oldABName.Length);
        }
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    /// <summary>
    /// 构建 AB 包
    /// </summary>
    private static void BunildAssetBundle() {
        string[] allBundlesName = AssetDatabase.GetAllAssetBundleNames(); // 设置好的所有 AB包 的名字，eg：attack，shader，sound
        Dictionary<string, string> resPathDic = new Dictionary<string, string>(); // 过滤后的资源信息，key: 全路径，val: 包名
        for (int i = 0; i < allBundlesName.Length; i++) {
            string[] allBundlesPath = AssetDatabase.GetAssetPathsFromAssetBundle(allBundlesName[i]); // 获取指定包名内所包含资源的全路径
            for (int j = 0; j < allBundlesPath.Length; j++) {
                if (allBundlesPath[j].EndsWith(".cs")) { // 过滤脚本文件
                    continue;
                }
                Debug.Log("此 AB 包：" + allBundlesName[i] + "下面包含的资源文件路径：" + allBundlesPath[j]);
                if (ValidPath(allBundlesPath[j])) { // 是否是有效路径
                    resPathDic.Add(allBundlesPath[j], allBundlesName[i]);
                }
            }
        }
        // 删除没用的 AB 包
        DeleteAB();
        // 生成自己的配置表
        WriteData(resPathDic);
        // 构建 AssetBundle 包
        BuildPipeline.BuildAssetBundles(bundleTargetPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
    }

    /// <summary>
    /// 生成自己的配置表
    /// </summary>
    /// <param name="resPathDic">过滤后的资源信息，key: 全路径，val: 包名</param>
    private static void WriteData(Dictionary<string, string> resPathDic) {
        // 设置配置表信息
        AssetBundleConfig config = new AssetBundleConfig();
        config.ABList = new List<ABBase>();
        foreach (string path in resPathDic.Keys) {
            ABBase abBase = new ABBase();
            abBase.Path = path;
            abBase.Crc = CRC32.GetCRC32(path);
            abBase.ABName = resPathDic[path];
            abBase.AssetName = path.Remove(0, path.LastIndexOf("/") + 1);
            abBase.ABDependce = new List<string>();
            string[] resDependce = AssetDatabase.GetDependencies(path); // 获取依赖项
            for (int i = 0; i < resDependce.Length; i++) { // 过滤，看依赖项在哪一个 ab 里面
                string tempPath = resDependce[i];
                if (tempPath == path || path.EndsWith(".cs")) { // 过滤自己和脚本
                    continue;
                }
                string abName = "";
                if (resPathDic.TryGetValue(tempPath, out abName)) { // 存在在其他 ab 里
                    if (abName == resPathDic[path]) { // 忽略自己
                        continue;
                    }
                    if (!abBase.ABDependce.Contains(abName)) { // 可能一个 prefab 依赖了 一个ab包里面的多个文件，只添加一次依赖就行了
                        abBase.ABDependce.Add(abName);
                    }
                }
            }
            config.ABList.Add(abBase);
        }
        // 写入 xml，方便自己可以看见
        string xmlPath = Application.dataPath + "/AssetBundleConfig.xml";
        if (File.Exists(xmlPath)) {
            File.Delete(xmlPath);
        }
        FileStream fsXml = new FileStream(xmlPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite); // 文件流
        StreamWriter sw = new StreamWriter(fsXml, System.Text.Encoding.UTF8); // 写入流
        XmlSerializer xml = new XmlSerializer(config.GetType()); // 需要序列化的类型
        xml.Serialize(sw, config); // 将 config 序列化到 sw 去
        sw.Close();
        fsXml.Close();
        // 写入 二进制
        foreach (ABBase abBase in config.ABList) { // 清除掉 path 属性，因为这只是为了在 xml 方便自己查看，是不需要存在在二进制文件里的，CRC 才是唯一标识
            abBase.Path = "";
        }
        string bytePath = "Assets/GameData/Data/ABData/AssetBundleConfig.bytes";
        FileStream fsByte = new FileStream(bytePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite); // 文件流
        BinaryFormatter bf = new BinaryFormatter(); // 二进制流
        bf.Serialize(fsByte, config);
        fsByte.Close();
    }

    /// <summary>
    /// 删除没用的 AB 包
    /// </summary>
    private static void DeleteAB() {
        string[] allBundlesName = AssetDatabase.GetAllAssetBundleNames(); // 设置好的所有 AB包 的名字，eg：attack，shader，sound
        DirectoryInfo directoryInfo = new DirectoryInfo(bundleTargetPath);
        FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.AllDirectories); // 获取文件夹下所有的文件
        for (int i = 0; i < files.Length; i++) {
            if (ContainABName(files[i].Name, allBundlesName) || files[i].Name.EndsWith(".meta")) { // 如果文件包含在将要打包的列表里，就不用删了，meta 文件也不用管
                continue;
            }
            else {
                Debug.Log("此ab包已经被删除或改名了：" + files[i].Name);
                if (File.Exists(files[i].FullName)) {
                    File.Delete(files[i].FullName);
                }
            }
        }
    }

    /// <summary>
    /// 指定 ab 包是否已存在
    /// </summary>
    /// <param name="name"></param>
    /// <param name="strs"></param>
    /// <returns></returns>
    private static bool ContainABName(string name, string[] allBundlesName) {
        // 遍历文件夹里的文件名与设置的所有 AB包 进行检查判断
        for (int i = 0; i < allBundlesName.Length; i++) {
            if (name == allBundlesName[i]) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 设置 ab 包的名字
    /// </summary>
    /// <param name="name"></param>
    /// <param name="path"></param>
    private static void SetABName(string name, string path) {
        AssetImporter assetImporter = AssetImporter.GetAtPath(path);
        if (assetImporter == null) {
            Debug.LogError("不存在此路径文件:" + path);
        }
        else {
            assetImporter.assetBundleName = name;
        }
    }

    /// <summary>
    /// 设置 ab 包的名字
    /// </summary>
    /// <param name="name"></param>
    /// <param name="pathList"></param>
    private static void SetABName(string name, List<string> pathList) {
        for (int i = 0; i < pathList.Count; i++) {
            SetABName(name, pathList[i]);
        }
    }

    /// <summary>
    /// 指定路径是否已经在过滤的AB包里面，用于剔除冗余 AB 包
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool ContainAllFileAB(string path) {
        for (int i = 0; i < allFileAB.Count; i++) {
            if (path == allFileAB[i] || (path.Contains(allFileAB[i]) && (path.Replace(allFileAB[i], "")[0] == '/'))) { // eg: 1.xxx/Test  2.xxx/TestTT/a.prefab
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 是否是有效路径 - configFile
    /// </summary>
    /// <param name="path"></param>
    private static bool ValidPath(string path) {
        for (int i = 0; i < configFile.Count; i++) {
            if (path.Contains(configFile[i])) {
                return true;
            }
        }
        return false;
    }
}