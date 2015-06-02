using UnityEditor;
using UnityEngine;
using System;
using System.IO;

public class ScriptableObjectUtil
{
	public static void  Save <T> (String path, T asset) where T : ScriptableObject
	{
		AssetDatabase.DeleteAsset (path);
		AssetDatabase.Refresh ();
		AssetDatabase.CreateAsset (asset, path);
		AssetDatabase.SaveAssets ();
		AssetDatabase.Refresh ();
	}
	
	public static T Load<T> (String path) where T : ScriptableObject
	{
		string assetPath = path;
		T asset = (T)AssetDatabase.LoadAssetAtPath (assetPath, typeof(T));
		/*
		if (asset == null) {
			asset = ScriptableObject.CreateInstance<T> ();
			Save<T> (path, asset);
		}
		*/
		return asset;
	}
}
