using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ABCModelReader
{
    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadUInt16();
        byte[] stringBytes = reader.ReadBytes(length);
        return System.Text.Encoding.ASCII.GetString(stringBytes);
    }

    private static Weight ReadWeight(BinaryReader reader)
    {
        Weight weight = new Weight();
        weight.NodeIndex = reader.ReadUInt32();
        weight.Location = ReadVector3(reader);
        weight.Bias = reader.ReadSingle();
        return weight;
    }

    private static Vertex ReadVertex(BinaryReader reader)
    {
        Vertex vertex = new Vertex();
        int nWeightCount = reader.ReadUInt16();
        vertex.SublodVertexIndex = reader.ReadUInt16();
        vertex.Weights = new List<Weight>();
        for (int i = 0; i < nWeightCount; i++)
        {
            vertex.Weights.Add(ReadWeight(reader));
        }
        vertex.Location = ReadVector3(reader);
        vertex.Normal = ReadVector3(reader);
        return vertex;
    }

    private static FaceVertex ReadFaceVertex(BinaryReader reader)
    {
        FaceVertex faceVertex = new FaceVertex();
        faceVertex.Texcoord.x = reader.ReadSingle();
        faceVertex.Texcoord.y = reader.ReadSingle();
        faceVertex.VertexIndex = reader.ReadUInt16();
        return faceVertex;
    }

    private static Face ReadFace(BinaryReader reader)
    {
        Face face = new Face();
        face.Vertices = new List<FaceVertex>();
        for (int i = 0; i < 3; i++)
        {
            face.Vertices.Add(ReadFaceVertex(reader));
        }
        return face;
    }

    private static LOD ReadLOD(BinaryReader reader)
    {
        LOD lod = new LOD();
        int nFaceCount = reader.ReadInt32();
        lod.Faces = new List<Face>();
        for (int i = 0; i < nFaceCount; i++)
        {
            lod.Faces.Add(ReadFace(reader));
        }
        int nVertexCount = reader.ReadInt32();
        lod.Vertices = new List<Vertex>();
        for (int i = 0; i < nVertexCount; i++)
        {
            lod.Vertices.Add(ReadVertex(reader));
        }

        return lod;
    }

    private static Piece ReadPiece(int version, int lodCount, BinaryReader reader)
    {
        Piece piece = new Piece();
        piece.MaterialIndex = reader.ReadUInt16();
        piece.SpecularPower = reader.ReadSingle();
        piece.SpecularScale = reader.ReadSingle();
        if (version > 9)
        {
            piece.LodWeight = reader.ReadSingle();
        }
        piece.Padding = reader.ReadUInt16();
        piece.Name = ReadString(reader);
        piece.LODs = new List<LOD>();
        for (int i = 0; i < lodCount; i++)
        {
            piece.LODs.Add(ReadLOD(reader));
        }

        return piece;
    }

    public static ABCModel LoadABCModel(string filename, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            throw new Exception("No file selected");
        }

        if (!File.Exists(filename))
        {
            Debug.LogWarning($"ABCModelReader Error - File not found: {filename}");
            return null;
        }

        ABCModel model = new ABCModel();

        string fileExtension = Path.GetExtension(filename);
        model.Name = Path.GetFileNameWithoutExtension(filename);

        try
        {
            using BinaryReader reader = new BinaryReader(File.OpenRead(filename));
            int nextSectionOffset = 0;
            while (nextSectionOffset != -1)
            {
                reader.BaseStream.Seek(nextSectionOffset, SeekOrigin.Begin);

                if (fileExtension.Contains("ltb"))
                {
                    // Check if we have a pre-Jupiter LTB
                    string ltbHeader = ReadString(reader);

                    if (ltbHeader == "LTBHeader")
                    {
                        nextSectionOffset = reader.ReadInt32();
                        reader.BaseStream.Position = nextSectionOffset;
                    }
                }

                string sectionName = ReadString(reader);
                nextSectionOffset = reader.ReadInt32();
                if (sectionName == "Header")
                {
                    model.Version = reader.ReadInt32();
                    if (model.Version < 9 || model.Version > 13)
                    {
                        Debug.LogError($"Unsupported file version ({model.Version}).");
                        return null;
                    }

                    reader.BaseStream.Seek(8, SeekOrigin.Current);
                    var nodeCount = reader.ReadInt32();
                    reader.BaseStream.Seek(20, SeekOrigin.Current);
                    model.LODCount = reader.ReadInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    var weightSetCount = reader.ReadInt32();
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    // Unknown new value
                    if (model.Version >= 13)
                    {
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                    }

                    model.CommandString = ReadString(reader);
                    model.InternalRadius = reader.ReadSingle();
                    reader.BaseStream.Seek(64, SeekOrigin.Current);
                    model.LODDistances = new List<float>();
                    for (int i = 0; i < model.LODCount; i++)
                    {
                        model.LODDistances.Add(reader.ReadSingle());
                    }
                }
                else if (sectionName == "Pieces")
                {
                    int nWeightCount = reader.ReadInt32();
                    int nPiecesCount = reader.ReadInt32();
                    model.Pieces = new List<Piece>();
                    for (int i = 0; i < nPiecesCount; i++)
                    {
                        model.Pieces.Add(ReadPiece(model.Version, model.LODCount, reader));
                    }
                }
            }

            reader.Close();
        }
        catch (Exception ex)
        {
            // Debug.LogError($"Error while loading ABC file {filename}: {ex.Message}");   
            return null;
        }

        model.RelativePathToABCFileLowercase = Path.GetRelativePath(projectPath, filename).ConvertFolderSeperators().ToLower();
        return model;
    }
}
