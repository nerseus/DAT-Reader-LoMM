using SFB;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using static ABCModelReader;
using static UnityEditor.PlayerSettings;

public class ModelToGameObject : MonoBehaviour
{
    public bool bAllLODs = false;

    public ABCModel model;

    public Importer importer;

    public void Start()
    {
        //always make sure we have a reference to the Importer, this is basically like a global object that holds important path info.
        importer = GameObject.Find("Level").GetComponent<Importer>();
    }

    private Mesh CombineMeshes(Mesh[] meshes)
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

    private Mesh CreateMesh(string modelName, PieceModel piece)
    {
        List<Mesh> individualMeshes = new List<Mesh>();

        // TTTT - Fix this:
        var faces = piece.LODs[0].Faces;

        //// Cot piece subset:
        //var faces = piece.LODs[0].Faces
        //    .Skip(20).Take(4) // pillow
        //    .Union(piece.LODs[0].Faces.Skip(10).Take(2)) // top of bed
        //    .ToList();

        //// Bookcase piece subset:
        //var faces = piece.LODs[0].Faces
        //    .Skip(10).Take(2) // front panel
        //    .ToList();


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

    public GameObject LoadABC(ModelDefinition mDef, Transform parentTransform, bool hasGravity = false)
    {
        if (mDef == null)
        {
            return null;
        }

        if (importer == null)
        {
            importer = GameObject.FindObjectOfType<Importer>();
        }

        ABCModel model = ABCModelReader.ReadABCModel(mDef.szModelFilePath, importer.szProjectPath);
        if (model == null)
        {
            return null;
        }

        //load dtx textures
        foreach (var tex in mDef.szModelTextureName)
        {
            if (DTX.LoadDTXIntoLibrary(tex, importer.dtxMaterialList, importer.szProjectPath) == DTXReturn.FAILED)
            {
                Debug.LogError("Could not load texture: " + tex);
            }
        }

        mDef.model = model;

        mDef.rootObject = new GameObject(model.Name);
        mDef.rootObject.transform.position = Vector3.zero;
        mDef.rootObject.transform.rotation = Quaternion.identity;
        mDef.rootObject.transform.localScale = Vector3.one;

        foreach (var m in model.Pieces)
        {
            GameObject modelInGameObject = new GameObject(m.Name);
            modelInGameObject.transform.parent = mDef.rootObject.transform;
            modelInGameObject.AddComponent<MeshFilter>();
            modelInGameObject.AddComponent<MeshRenderer>();
            modelInGameObject.GetComponent<MeshFilter>().mesh = CreateMesh(model.Name, m);

            modelInGameObject.GetComponent<MeshFilter>().mesh.RecalculateBounds();

            //Sometimes people don't specify a second, third or fourth texture... so we need to check if the index is out of bounds
            if (m.MaterialIndex > mDef.szModelTextureName.Count - 1)
                m.MaterialIndex = (ushort)(mDef.szModelTextureName.Count - 1);

            modelInGameObject.GetComponent<MeshRenderer>().material = importer.dtxMaterialList.materials[mDef.szModelTextureName[m.MaterialIndex]];

            if (mDef.bChromakey)
            {
                var mr = modelInGameObject.GetComponent<MeshRenderer>();
                mr.material.shader = Shader.Find("Shader Graphs/Lithtech Vertex Transparent");
                mr.material.SetInt("_Chromakey", 1);

            }
        }

        //combine
        mDef.rootObject.MeshCombine(true);

        mDef.rootObject.tag = LithtechTags.NoRayCast;

        //if (mDef.bMoveToFloor ||
        //    mDef.modelType == ModelType.Pickup ||
        //    mDef.modelType == ModelType.Character ||
        //    mDef.modelType == ModelType.Weapon ||
        //    mDef.szModelFileName.Contains("Tree02")
        //    )
        //{
        //    var c = mDef.rootObject.AddComponent<DebugLines>();
        //    c.MoveToFloor = mDef.bMoveToFloor;
        //    c.ModelType = mDef.modelType;
        //    c.ModelFilename = mDef.szModelFileName;
        //}

        mDef.rootObject.transform.SetParent(parentTransform);

        var mDefComponent = parentTransform.gameObject.AddComponent<ModelDefinitionComponent>();
        mDefComponent.ModelDef = mDef;
        mDefComponent.HasGravity = hasGravity;

        return mDef.rootObject;
    }
}
