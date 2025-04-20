using UnityEngine;
using UnityEditor;
using System.IO;

public class SaveOrganizedPrefab
{
    [MenuItem("Tools/Save Selected GameObjects with Meshes, Materials, and Textures (Organized)")]
    public static void Save()
    {
        string basePath = "Assets/Generated";
        string meshPath = $"{basePath}/Meshes";
        string texPath = $"{basePath}/Textures";
        string prefabPath = $"{basePath}/Prefabs";

        Directory.CreateDirectory(meshPath);
        Directory.CreateDirectory(texPath);
        Directory.CreateDirectory(prefabPath);

        foreach (var go in Selection.gameObjects)
        {
            string objName = go.name;

            // --- Save Mesh ---
            Mesh mesh = null;
            var mf = go.GetComponent<MeshFilter>();
            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (mf != null) mesh = mf.sharedMesh;
            if (smr != null) mesh = smr.sharedMesh;

            if (mesh != null)
            {
                string meshAssetPath = $"{meshPath}/{objName}_Mesh.asset";
                var meshCopy = Object.Instantiate(mesh);
                AssetDatabase.CreateAsset(meshCopy, meshAssetPath);
                AssetDatabase.SaveAssets();
                if (mf != null) mf.sharedMesh = meshCopy;
                if (smr != null) smr.sharedMesh = meshCopy;
            }

            // --- Save Materials and Textures ---
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material[] newMats = new Material[renderer.sharedMaterials.Length];

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material mat = renderer.sharedMaterials[i];
                    if (mat == null) continue;

                    // Save Texture
                    Texture2D tex = mat.mainTexture as Texture2D;
                    Texture2D savedTex = null;

                    if (tex != null)
                    {
                        string textureFilePath = $"{texPath}/{objName}_Tex_{i}.png";
                        byte[] pngData = tex.EncodeToPNG();
                        File.WriteAllBytes(textureFilePath, pngData);
                        AssetDatabase.ImportAsset(textureFilePath);

                        string assetTexPath = textureFilePath.Replace(Application.dataPath, "Assets");
                        savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetTexPath);
                    }

                    // Save Material
                    Material newMat = new Material(mat);
                    if (savedTex != null)
                        newMat.mainTexture = savedTex;

                    string matPath = $"{meshPath}/{objName}_Mat_{i}.mat";
                    AssetDatabase.CreateAsset(newMat, matPath);
                    newMats[i] = newMat;
                }

                renderer.sharedMaterials = newMats;
            }

            // --- Save Prefab ---
            string finalPrefabPath = $"{prefabPath}/{objName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, finalPrefabPath);
            Debug.Log($"✅ Saved prefab with mesh/material/texture: {finalPrefabPath}");
        }

        AssetDatabase.Refresh();
    }
}
