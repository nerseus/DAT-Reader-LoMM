using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ABCMeshLookupUtility : DataExtractor
{
    private static bool TrySetExistingLookups()
    {
        var assetFilenames = Directory.GetFiles(ABCMeshPath, "*.asset", SearchOption.AllDirectories);
        if (assetFilenames.Length == 0)
        {
            return false;
        }

        // Update UnityPathAndFilenameToPrefab on each model
        foreach (var assetFilename in assetFilenames)
        {
            string relativePathToABC = Path.ChangeExtension(Path.GetRelativePath(ABCMeshPath, assetFilename), "abc");
            UnityLookups.ABCMeshLookups.Add(relativePathToABC, assetFilename);
        }

        return true;
    }

    private static float GetMinXValue(ABCModel abcModel)
    {
        return abcModel.Pieces.Min(piece => piece.LODs[0].Vertices.Min(vert => vert.Location.x)) * UnityScaleFactor;
    }

    private static float GetMaxXValue(ABCModel abcModel)
    {
        return abcModel.Pieces.Max(piece => piece.LODs[0].Vertices.Max(vert => vert.Location.x)) * UnityScaleFactor;
    }

    private static float GetMinYValue(ABCModel abcModel)
    {
        return abcModel.Pieces.Min(piece => piece.LODs[0].Vertices.Min(vert => vert.Location.y)) * UnityScaleFactor;
    }

    private static float GetMinZValue(ABCModel abcModel)
    {
        return abcModel.Pieces.Min(piece => piece.LODs[0].Vertices.Min(vert => vert.Location.z)) * UnityScaleFactor;
    }

    private static float GetMaxZValue(ABCModel abcModel)
    {
        return abcModel.Pieces.Max(piece => piece.LODs[0].Vertices.Max(vert => vert.Location.z)) * UnityScaleFactor;
    }

    private static Mesh CombineMeshPieces(List<Mesh> meshes, bool mergeSubMeshes)
    {
        CombineInstance[] combineInstances = new CombineInstance[meshes.Count];

        for (int i = 0; i < meshes.Count; i++)
        {
            combineInstances[i].mesh = meshes[i];
            combineInstances[i].subMeshIndex = 0;
            combineInstances[i].transform = Matrix4x4.identity;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combineInstances, mergeSubMeshes, useMatrices:true);

        return combinedMesh;
    }

    private static Mesh CreateMesh(PieceModel piece, Vector3 vertOffset)
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
                var vert = piece.LODs[0].Vertices[originalVertexIndex].Location * UnityScaleFactor;
                faceVertices.Add(vert + vertOffset);
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
        Mesh combinedMesh = CombineMeshPieces(individualMeshes, true);

        return combinedMesh;
    }

    public static Mesh CombineMeshesByMaterial(List<List<Mesh>> meshesByMaterial)
    {
        var allCombineInstances = new List<CombineInstance>();

        List<Mesh> combinedMeshes = new List<Mesh>();
        foreach (var materialMeshes in meshesByMaterial)
        {
            var combinedMaterialMesh = CombineMeshPieces(materialMeshes, true);
            combinedMeshes.Add(combinedMaterialMesh);
        }

        var finalMesh = CombineMeshPieces(combinedMeshes, false);
        return finalMesh;
    }


    private static Mesh CreateMesh(ABCModel abcModel)
    {
        var xVertOffset = -((GetMinXValue(abcModel) + GetMaxXValue(abcModel)) / 2f);
        var yVertOffset = -GetMinYValue(abcModel);
        var zVertOffset = -((GetMinZValue(abcModel) + GetMaxZValue(abcModel)) / 2f);
        var vertOffset = new Vector3(xVertOffset, yVertOffset, 0);

        // Initialize meshes so there is one list per material.
        List<List<Mesh>> meshes = new List<List<Mesh>>();
        for (int i = 0; i <= abcModel.GetMaxMaterialIndex(); i++)
        {
            meshes.Add(new List<Mesh>());
        }


        foreach (var piece in abcModel.Pieces.Where(x => x.LODs[0].Faces.Count > 1))
        {
            var meshPiece = CreateMesh(piece, vertOffset);
            meshes[piece.MaterialIndex].Add(meshPiece);
        }

        var mesh = CombineMeshesByMaterial(meshes);

        return mesh;
    }

    private static void CreateABCMeshes(List<ABCModel> abcModels)
    {
        AssetDatabase.StartAssetEditing();

        int i = 0;
        foreach (var abcModel in abcModels)
        {
            i++;
            float progress = (float)i / abcModels.Count;
            EditorUtility.DisplayProgressBar("Creating ABC Meshes", $"Item {i} of {abcModels.Count}", progress);

            var mesh = CreateMesh(abcModel);
            string unityPathAndFilenameToMesh = Path.ChangeExtension(Path.Combine(ABCMeshPath, abcModel.RelativePathToABCFileLowercase), "asset").ConvertFolderSeperators();
            Directory.CreateDirectory(Path.GetDirectoryName(unityPathAndFilenameToMesh));
            AssetDatabase.CreateAsset(mesh, unityPathAndFilenameToMesh);

            UnityLookups.ABCMeshLookups.Add(abcModel.RelativePathToABCFileLowercase, unityPathAndFilenameToMesh);
        }

        EditorUtility.ClearProgressBar();

        RefreshAssetDatabase();
    }

    public static void SetLookups(bool alwaysCreate, List<ABCModel> abcModels)
    {
        UnityLookups.ABCMeshLookups.Clear();

        if (!alwaysCreate)
        {
            if (TrySetExistingLookups())
            {
                return;
            }
        }

        CreateABCMeshes(abcModels);
    }
}
