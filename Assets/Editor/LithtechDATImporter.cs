using UnityEngine;
using System.Collections;
using System.IO;
using UnityEditor;

public class LithtechDATImporter : Editor
{
	[MenuItem("Assets/Lithtech/Convert .DAT")]
	public static void ConvertDAT()
	{
		string mapName = AssetDatabase.GetAssetPath(Selection.activeObject);
		if(!mapName.ToLower().EndsWith(".dat"))
		{
			Debug.LogError("Wrong file formar :S, expected .DAT");
			return;
		}

		BinaryReader br = new BinaryReader(new FileStream(mapName, FileMode.Open));

		DATFormat dat = new DATFormat();
		//try {
			dat.LoadWorld(br, false, mapName);
		/*}
		catch
		{
			Debug.LogError("Something went terribly wrong!!");
			br.Close();
			return;
		}*/

		br.Close();
	}
	[MenuItem("Assets/Lithtech/Convert .DAT (Fix Transparency, it will reimport textures, will take a couple minutes)")]
	public static void ConvertDATT()
	{
		string mapName = AssetDatabase.GetAssetPath(Selection.activeObject);
		if (!mapName.ToLower().EndsWith(".dat"))
		{
			Debug.LogError("Wrong file formar :S, expected .DAT");
			return;
		}

		BinaryReader br = new BinaryReader(new FileStream(mapName, FileMode.Open));

		DATFormat dat = new DATFormat();
		//try {
		dat.LoadWorld(br, true, mapName);
		/*}
		catch
		{
			Debug.LogError("Something went terribly wrong!!");
			br.Close();
			return;
		}*/

		br.Close();
	}

	[MenuItem("GameObject/Create Material")]
	static void CreateMaterial()
	{
		// Create a simple material asset

		Material material = new Material(Shader.Find("Specular"));
		AssetDatabase.CreateAsset(material, "Assets/MyMaterial.mat");

		// Add an animation clip to it
		AnimationClip animationClip = new AnimationClip();
		animationClip.name = "My Clip";
		AssetDatabase.AddObjectToAsset(animationClip, material);

		// Reimport the asset after adding an object.
		// Otherwise the change only shows up when saving the project
		AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(animationClip));

		// Print the path of the created asset
		Debug.Log(AssetDatabase.GetAssetPath(material));
	}
}
