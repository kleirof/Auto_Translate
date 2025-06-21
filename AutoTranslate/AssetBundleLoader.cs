using System;
using System.IO;
using UnityEngine;

namespace AutoTranslate
{
    class AssetBundleLoader
    {
        public static AssetBundle LoadAssetBundle(string filePath)
        {
            AssetBundle result = null;
            if (File.Exists(filePath))
            {
                try
                {
                    result = AssetBundle.LoadFromFile(filePath);
                    Debug.Log("已成功加载AssetBundle！Successfully loaded AssetBundle!");
                }
                catch (Exception ex)
                {
                    Debug.LogError("从文件加载AssetBundle失败。Failed loading AssetBundle from file.");
                    Debug.LogError(ex.ToString());
                }
            }
            else
            {
                Debug.LogError("AssetBundle不存在！AssetBundle does not exist!");
            }

            return result;
        }
    }
}
