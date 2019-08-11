/****************************************************
    文件：ResourcesTest.cs
	作者：TravelerTD
    日期：2019/8/8 17:54:29
	功能：资源加载测试
*****************************************************/

using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using TDFramework;
using UnityEngine;

public class ResourcesTest : MonoBehaviour {

    private void Start() {
        AssetBundle abConfig = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/abconfig");
        TextAsset textAsset = abConfig.LoadAsset<TextAsset>("AssetBundleConfig");
        MemoryStream ms = new MemoryStream(textAsset.bytes);
        BinaryFormatter bf = new BinaryFormatter();
        AssetBundleConfig config = (AssetBundleConfig)bf.Deserialize(ms);
        ms.Close();
        string path = "Assets/GameData/Prefabs/Attack.prefab";
        uint crc = CRC32.GetCRC32(path);
        ABBase abBase = null;
        for (int i = 0; i < config.ABList.Count; i++) {
            if (config.ABList[i].Crc == crc) {
                abBase = config.ABList[i];

            }
        }
        for (int i = 0; i < abBase.ABDependce.Count; i++) { // 加载依赖项
            AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/" + abBase.ABDependce[i]);
        }
        AssetBundle ab = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/" + abBase.ABName); // 加载 ab
        GameObject go = GameObject.Instantiate(ab.LoadAsset<GameObject>(abBase.AssetName)); // 实例化资源
    }

}