/****************************************************
    文件：GameStart.cs
	作者：TravelerTD
    日期：2019/8/11 15:58:38
	功能：Nothing
*****************************************************/

using UnityEngine;

public class GameStart : MonoBehaviour {

    private void Awake() {
        AssetBundleManager.Instance.LoadAssetBundleConfig();
    }
}