/****************************************************
    文件：BundleEditor.cs
	作者：TravelerTD
    日期：2019/8/8 18:5:28
	功能：Nothing
*****************************************************/

using UnityEngine;
using UnityEditor;
using System.IO;

public class BundleEditor{
    [MenuItem("Tools/打包")]
    public static void Build() {
        // 构建 AssetBundle 包
        BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
        // 刷新编辑器，否则可能会看不见新生成的文件
        AssetDatabase.Refresh();
    }
}