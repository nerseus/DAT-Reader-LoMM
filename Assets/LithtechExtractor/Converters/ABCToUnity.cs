using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Utility;

public static class ABCToUnity
{
    private static Mesh CombineMeshes(Mesh[] meshes)
    {
        CombineInstance[] combineInstances = new CombineInstance[meshes.Length];

        for (int i = 0; i < meshes.Length; i++)
        {
            combineInstances[i].mesh = meshes[i];
            combineInstances[i].transform = Matrix4x4.identity;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combineInstances, true, true);

        return combinedMesh;
    }

    private static Mesh CreateMesh(string modelName, PieceModel piece)
    {
        List<Mesh> individualMeshes = new List<Mesh>();

        var faces = piece.LODs[0].Faces;

        // only use the first LOD, we don't care about the rest.
        int faceIndex = 0;
        foreach (FaceModel face in faces)
        {
            Mesh faceMesh = new Mesh();
            List<Vector3> faceVertices = new List<Vector3>();
            List<Vector3> faceNormals = new List<Vector3>();
            List<Vector2> faceUV = new List<Vector2>();
            List<int> faceTriangles = new List<int>();

            foreach (FaceVertexModel faceVertex in face.Vertices)
            {
                int originalVertexIndex = faceVertex.VertexIndex;

                // Add vertices, normals, and UVs for the current face
                faceVertices.Add(piece.LODs[0].Vertices[originalVertexIndex].Location * 0.01f);
                faceNormals.Add(piece.LODs[0].Vertices[originalVertexIndex].Normal);

                Vector2 uv = new Vector2(faceVertex.Texcoord.x, faceVertex.Texcoord.y);
                // Flip UV upside down - difference between Lithtech and Unity systems.
                uv.y = 1f - uv.y;

                faceUV.Add(uv);
                faceTriangles.Add(faceVertices.Count - 1);
            }

            faceMesh.vertices = faceVertices.ToArray();
            faceMesh.normals = faceNormals.ToArray();
            faceMesh.uv = faceUV.ToArray();
            faceMesh.triangles = faceTriangles.ToArray();

            individualMeshes.Add(faceMesh);
            faceIndex++;
        }

        // Combine all individual meshes into a single mesh
        Mesh combinedMesh = CombineMeshes(individualMeshes.ToArray());

        return combinedMesh;
    }

    private static Dictionary<string, UnityDTXModel> LoadSkins(List<string> skinTextures, string projectPath)
    {
        Dictionary<string, UnityDTXModel> unityDTXModels = new Dictionary<string, UnityDTXModel>();
        foreach (var skin in skinTextures)
        {
            string filenameAndPathToTexture = Path.Combine(projectPath, skin);
            var dtxModel = DTXModelReader.ReadDTXModel(filenameAndPathToTexture, skin);
            if (dtxModel == null)
            {
                Debug.LogWarning($"Could not find skin {skin} while loading an ABC file.");
                return null;
            }

            var unityDTX = DTXConverter.ConvertDTX(dtxModel);
            if (unityDTX == null)
            {
                Debug.LogWarning($"Could not create unityDTX for skin {skin} while loading an ABC file.");
                return null;
            }

            unityDTXModels.Add(skin, unityDTX);
        }

        return unityDTXModels;
    }

    public static GameObject CreateObjectFromABC(ABCModel model, List<string> skinTextures, string projectPath, Dictionary<string, UnityDTXModel> unityDTXModels = null)
    {
        if (unityDTXModels == null)
        {
            unityDTXModels = LoadSkins(skinTextures, projectPath);
            if (unityDTXModels == null)
            {
                return null;
            }
        }

        var rootObject = new GameObject(model.Name);
        rootObject.transform.position = Vector3.zero;
        rootObject.transform.rotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;

        foreach (var piece in model.Pieces)
        {
            GameObject modelInGameObject = new GameObject(piece.Name);
            modelInGameObject.transform.parent = rootObject.transform;
            modelInGameObject.AddComponent<MeshFilter>();
            modelInGameObject.AddComponent<MeshRenderer>();
            modelInGameObject.GetComponent<MeshFilter>().mesh = CreateMesh(model.Name, piece);

            modelInGameObject.GetComponent<MeshFilter>().mesh.RecalculateBounds();

            // Sometimes people don't specify a second, third or fourth texture... so we need to check if the index is out of bounds
            if (piece.MaterialIndex > skinTextures.Count - 1)
            {
                piece.MaterialIndex = (ushort)(skinTextures.Count - 1);
            }

            var skinName = skinTextures[piece.MaterialIndex];
            var unityDTX = unityDTXModels[skinName];
            modelInGameObject.GetComponent<MeshRenderer>().material = DTXConverter.CreateDefaultMaterial(unityDTX.DTXModel.RelativePathToDTX, unityDTX.Texture2D);
        }

        //combine
        rootObject.MeshCombine(true);
        rootObject.tag = LithtechTags.NoRayCast;

        return rootObject;
    }

    public static GameObject CreateObjectFromABC(string filenameAndPathToABC, List<string> skinTextures, string projectPath)
    {
        ABCModel model = ABCModelReader.ReadABCModel(filenameAndPathToABC, projectPath);
        if (model == null)
        {
            return null;
        }

        return CreateObjectFromABC(model, skinTextures, projectPath);
    }
}
