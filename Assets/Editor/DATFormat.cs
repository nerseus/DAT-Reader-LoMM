#region imports
using UnityEngine;
using System.Collections;
using System.IO;
using System;

using uint8 = System.Byte;
using int8 = System.SByte;

using uint32 = System.UInt32;
using int32 = System.Int32;

using uint16 = System.UInt16;
using int16 = System.Int16;

using int64 = System.Int64;
using System.Collections.Generic;
using UnityEditor;
#endregion

public class DATFormat
{
	BinaryReader br;

	//Usefull for jumping around the file
	uint32 object_data_pos;
	uint32 lightgrid_pos;
	uint32 blind_object_data_pos;
	uint32 collision_data_pos;
	uint32 particle_blocker_data_pos;
	uint32 render_data_pos;

	//Contains ambient color and some other jazz
	string world_info_string;

	//World border information
	Vector3 world_extents_min;
	Vector3 world_extents_max;
	Vector3 world_offset;
	Vector3 world_extents_diff_inv;

	public GameObject p;
	public void LoadWorld(BinaryReader br, bool fixTransparency, string fileName)
	{
		string[] ar = fileName.Split('/');
		string mapName = ar[ar.Length-1];
		mapName = mapName.Replace(".", "_");
		this.br = br;

		uint32 file_version = br.ReadUInt32();

		if (file_version != 85)
		{
			Debug.LogError("Wrong file version, got " + file_version + " expected " + 85 + ".");
		}
		ReadWorldHeader();

		//read the length of the world info string.
		uint32 str_length = br.ReadUInt32();
		world_info_string = ReadString(br, str_length);

		world_extents_min = ReadVector(br);
		world_extents_max = ReadVector(br);
		world_offset = ReadVector(br);

		//compute the inverse of the world size.
		world_extents_diff_inv.x = 1.0f / (world_extents_max.x - world_extents_min.x);
		world_extents_diff_inv.y = 1.0f / (world_extents_max.y - world_extents_min.y);
		world_extents_diff_inv.z = 1.0f / (world_extents_max.z - world_extents_min.z);

		//read in the world tree.
		LoadLayout();

		GameObject[] gos = GameObject.FindGameObjectsWithTag("Untagged");
		foreach (GameObject g in gos)
		{
			if (g.name.Contains("GO"))
				GameObject.DestroyImmediate(g);
		}

		if (p != null)
			GameObject.DestroyImmediate(p);
		p = new GameObject(mapName);

		if (!Directory.Exists("Assets/LithTech")) Directory.CreateDirectory("Assets/LithTech");
		if (!Directory.Exists("Assets/LithTech/" + mapName)) Directory.CreateDirectory("Assets/LithTech/" + mapName);
		if (!Directory.Exists("Assets/LithTech/" + mapName + "/Materials")) Directory.CreateDirectory("Assets/LithTech/" + mapName + "/Materials");
		if (!Directory.Exists("Assets/LithTech/" + mapName + "/Meshes")) Directory.CreateDirectory("Assets/LithTech/" + mapName + "/Meshes");

		for (int i = 0; i < models.Count; i++)
		{
			GameObject go = new GameObject("GO" + i);
			go.transform.parent = p.transform;
			go.transform.localScale = Vector3.one * 0.01f;
			MeshRenderer mr = go.AddComponent<MeshRenderer>();
			MeshFilter mf = go.AddComponent<MeshFilter>();

			Mesh m = new Mesh();

			m.vertices = models[i].m_vPos.ToArray();
			m.normals = models[i].m_vNormal.ToArray();
			m.colors = models[i].m_nColor.ToArray();
			m.tangents = models[i].m_vTangent.ToArray();

			m.subMeshCount = models[i].tris.Length;
			for (int s = 0; s < m.subMeshCount; s++)
			{
				m.SetTriangles(models[i].tris[s], s);
			}

			m.uv = models[i].m_fUV0.ToArray();
			m.uv2 = models[i].m_fUV1.ToArray();

			mf.mesh = m;

			//AssetDatabase.CreateAsset(m, "Assets/LithTech/" + mapName + "/Meshes/mesh" + i + ".asset");

			Material[] mats = new Material[models[i].tris.Length];
			int lightmap = 0;
			for (int mc = 0; mc < mats.Length; mc++)
			{
				string filePath = models[i].names[mc];
				string newFilePath = "";
				bool copy = false;
				for (int c = filePath.Length - 1; c >= 0; c--)
				{
					if (copy)
					{
						newFilePath = filePath[c] + newFilePath;
					}
					if (filePath[c] == '.') copy = true;
				}
				if (!copy)
					newFilePath = filePath;
				newFilePath = newFilePath.Replace("\\", "/");
				string[] r = newFilePath.Split('/');
				string rawName = r[r.Length - 1];

				if (fixTransparency)
				{
					TextureImporter asset = (TextureImporter)TextureImporter.GetAtPath("Assets/Resources/" + newFilePath + ".tga");
					if (asset == null)
						asset = (TextureImporter)TextureImporter.GetAtPath("Assets/Resources/" + rawName + ".tga");
					if (asset != null) if (!asset.isReadable) asset.isReadable = true;

					AssetDatabase.ImportAsset("Assets/Resources/" + newFilePath + ".tga", ImportAssetOptions.ForceUpdate);
				}

				Texture2D t = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/Resources/" + newFilePath + ".tga", typeof(Texture2D));
				if (t == null)
					t = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/Resources/" + rawName + ".tga", typeof(Texture2D));
				if (t != null)
				{
					if (fixTransparency && t.GetPixel(0, 0).a != 1)
					{
						mats[mc] = new Material(Shader.Find("Unlit/Transparent Cutout"));
						mats[mc].name = models[i].names[mc];
					}
					else
					{
						mats[mc] = new Material(Shader.Find("Vertex"));
						mats[mc].name = models[i].names[mc];
					}
					mats[mc].mainTexture = t;
				}

				else if (filePath.Contains("Light"))
				{
					mats[mc] = new Material(Shader.Find("Particles/Standard Surface"));
					mats[mc].name = models[i].names[mc];

					mats[mc].mainTexture = models[i].lightmaps[lightmap];
					lightmap++;
				}

				if(mats[mc] != null)
				{
					//AssetDatabase.CreateAsset(mats[mc], "Assets/LithTech/" + mapName + "/Materials/" + rawName + mc + ".asset");
				}
				//mats[mc].name = rawName;
			}

			mr.materials = mats;

			//mr.material = new Material(Shader.Find("Standard"));
			//mr.material.name = models[i].names[]
		}

		AssetDatabase.SaveAssets();
		PrefabUtility.CreatePrefab("Assets/LithTech/" + mapName + "/" + mapName + ".prefab", p);
	}

	void ReadWorldHeader()
	{
		object_data_pos = br.ReadUInt32();
		blind_object_data_pos = br.ReadUInt32();
		lightgrid_pos = br.ReadUInt32();
		collision_data_pos = br.ReadUInt32();
		particle_blocker_data_pos = br.ReadUInt32();
		render_data_pos = br.ReadUInt32();

		uint packertype = br.ReadUInt32();
		uint packerversion = br.ReadUInt32();

		//Left overs for supposed future versions
		for (int i = 0; i < 6; i++)
			br.ReadUInt32();
	}
	List<WorldModel> models = new List<WorldModel>();

	void LoadLayout()
	{
		br.BaseStream.Position = render_data_pos;
		//LoadWorldData{	// Load the render data
		uint32 m_nRenderBlockCount = br.ReadUInt32();
		bool bResult = true;
		// Load the blocks
		for (uint32 nReadLoop = 0; nReadLoop < m_nRenderBlockCount; ++nReadLoop)
		{
			//long newPos = FindNextString();
			/*if (newPos != 0)
			{
				br.BaseStream.Position = newPos - 28;
			}*/

			Vector3 m_vCenter = ReadVector(br);
			Vector3 m_vHalfDims = ReadVector(br);

			// Read in the section list
			uint32 nSectionCount = br.ReadUInt32();
			uint32 nIndexOffset = 0;
			WorldModel model = new WorldModel();

			model.tris = new List<int>[nSectionCount];
			List<int> vertCount = new List<int>();
			if (nSectionCount != 0)
			{
				for (uint32 nSectionLoop = 0; nSectionLoop < nSectionCount; ++nSectionLoop)
				{
					string[] sTextureName = new string[2];
					uint32 nCurrTex;
					for (nCurrTex = 0; nCurrTex < 2; nCurrTex++)
					{
						sTextureName[nCurrTex] = ReadString(br);
						if (nCurrTex == 0)
						{
							model.names.Add(sTextureName[nCurrTex]);
							//Debug.Log(sTextureName[nCurrTex]);
						}
						//Debug.Log(sTextureName[nCurrTex]);
					}

					uint8 nShaderCode = br.ReadByte();

					uint32 nTriCount = br.ReadUInt32();
					vertCount.Add((int)nTriCount);
					//Debug.Log(nTriCount);
					model.tris[nSectionLoop] = new List<int>();
					if (nTriCount == 0)
					{
						Debug.LogError("Triangle count is 0.");
					}

					string sTextureEffect = ReadString(br);
					//Debug.Log(sTextureEffect);

					for (nCurrTex = 0; nCurrTex < 2; nCurrTex++)
					{
						if (IsSpriteTexture(sTextureName[nCurrTex]))
						{
							Debug.LogError("Sprite Detected. Sprite loading isn't a feature yet.");
						}
					}

					uint32 m_nLightmapWidth = br.ReadUInt32();
					uint32 m_nLightmapHeight = br.ReadUInt32();
					uint32 m_nLightmapSize = br.ReadUInt32();

					if (m_nLightmapSize != 0)
					{
						byte[] m_pLightmapData = new uint8[m_nLightmapSize];
						for (int i = 0; i < m_nLightmapSize; i++)
						{
							m_pLightmapData[i] = br.ReadByte();
						}

						List<Color> uncompressedData;
						DecompressLMData(m_pLightmapData, m_nLightmapSize, (int)m_nLightmapWidth, (int)m_nLightmapHeight, out uncompressedData);

						Texture2D lightmap = new Texture2D((int)m_nLightmapWidth, (int)m_nLightmapHeight);
						lightmap.SetPixels(uncompressedData.ToArray());

						model.lightmaps.Add(lightmap);
					}
				}
			}

			uint32 m_nVertexCount = br.ReadUInt32();
			if (m_nVertexCount != 0)
			{
				for (int i = 0; i < m_nVertexCount; i++)
				{
					model.m_vPos.Add(ReadVector(br));
					Vector2 uv0 = ReadVector2(br);
					Vector2 uv1 = ReadVector2(br);

					uv0.y *= -1;
					uv1.y *= -1;

					model.m_fUV0.Add(uv0);
					model.m_fUV1.Add(uv1);

					model.m_nColor.Add(ToColor(br.ReadInt32()));

					model.m_vNormal.Add(ReadVector(br));
					model.m_vTangent.Add(ReadVector(br));
					model.m_vBinormal.Add(ReadVector(br));
				}
			}
			uint32 m_nTriCount = br.ReadUInt32();
			if (m_nTriCount != 0)
			{
				int nSectionLoop = 0;
				int vertC = 0;
				for (uint32 nTriLoop = 0; nTriLoop < m_nTriCount; ++nTriLoop)
				{
					int t0 = (int)br.ReadUInt32();
					int t1 = (int)br.ReadUInt32();
					int t2 = (int)br.ReadUInt32();
					model.m_nTris.Add(t0);
					model.m_nTris.Add(t1);
					model.m_nTris.Add(t2);

					if (vertCount[nSectionLoop] == vertC)
					{
						nSectionLoop++;
						vertC = 0;
					}

					model.tris[nSectionLoop].Add(t0);
					model.tris[nSectionLoop].Add(t1);
					model.tris[nSectionLoop].Add(t2);

					vertC++;

					uint32 nPolyIndex = br.ReadUInt32();
					//Debug.Log(nPolyIndex);
					model.m_nPolyIndex.Add((int)nPolyIndex);
				}
			}

			uint32 nSkyPortalCount = br.ReadUInt32();
			for (int o = 0; o < nSkyPortalCount; o++)
			{
				uint8 nVertCount = br.ReadByte();

				for (; nVertCount != 0; --nVertCount)
				{
					ReadVector(br);
				}
				Vector3 normal = ReadVector(br);
				float dist = br.ReadSingle();

				//uint32 m_nID = br.ReadUInt32();
			}

			uint32 nOccluderCount = br.ReadUInt32();
			for (int o = 0; o < nOccluderCount; o++)
			{
				uint8 nVertCount = br.ReadByte();

				for (int i = 0; i < nVertCount; i++)
				{
					ReadVector(br);
				}
				Vector3 normal = ReadVector(br);
				float dist = br.ReadSingle();

				br.ReadUInt32();
			}

			uint32 nLightGroupCount = br.ReadUInt32();
			for (int o = 0; o < nLightGroupCount; o++)
			{
				uint16 nLength = br.ReadUInt16();
				for (int i = 0; i < nLength; i++)
				{
					uint8 nNextChar = br.ReadByte();
				}
				Vector3 m_vColor = ReadVector(br);
				uint32 nDataLength = br.ReadUInt32();
				br.BaseStream.Position += nDataLength;

				uint32 nSectionLMSize = br.ReadUInt32();
				for (int i = 0; i < nSectionLMSize; i++)
				{
					uint32 nSubLMSize = br.ReadUInt32();
					for (int ni = 0; ni < nSubLMSize; ni++)
					{
						uint32 m_nLeft = br.ReadUInt32();
						uint32 m_nTop = br.ReadUInt32();
						uint32 m_nWidth = br.ReadUInt32();
						uint32 m_nHeight = br.ReadUInt32();

						uint32 nDataSize = br.ReadUInt32();

						br.BaseStream.Position += nDataSize;
					}
				}
			}

			uint8 nChildFlags = br.ReadByte();
			for (uint32 nChildReadLoop = 0; nChildReadLoop < 2; ++nChildReadLoop)
			{
				uint32 nIndex = br.ReadUInt32();
			}

			models.Add(model);
		}
	}
	public static Color32 ToColor(int HexVal)
	{
		byte R = (byte)((HexVal >> 16) & 0xFF);
		byte G = (byte)((HexVal >> 8) & 0xFF);
		byte B = (byte)((HexVal) & 0xFF);
		return new Color32(R, G, B, 255);
	}

	public bool IsSpriteTexture(string pFilename)
	{
		int nTexNameLen = pFilename.Length;
		if (nTexNameLen < 4)
			return false;
		return pFilename.EndsWith(".spr");
	}

	public static string ReadString(BinaryReader br, uint count, bool safeCheck = false)
	{
		string output = "";
		for (int i = 0; i < count; i++)
		{
			char c = br.ReadChar();
			if (safeCheck)
			{
				if (!Char.IsLetterOrDigit(c) || Char.IsSymbol(c))
				{
					br.BaseStream.Position--;
					break;
				}
			}
			output += c;
		}
		return output;
	}
	public static string ReadString(BinaryReader br)
	{
		string output = "";
		int count = br.ReadUInt16();
		for (int i = 0; i < count; i++)
		{
			char c = br.ReadChar();
			output += c;
		}
		return output;
	}
	public static Vector3 ReadVector(BinaryReader br)
	{
		return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
	}
	public static Vector2 ReadVector2(BinaryReader br)
	{
		return new Vector2(br.ReadSingle(), br.ReadSingle());
	}

	bool DecompressLMData(uint8[] pCompressed, uint32 dataLen, int width, int height, out List<Color> pCurrOut)
	{	
		//the index into the input buffer
		uint32 nCurrPos = 0;

		//cursor in the output buffer
		pCurrOut = new List<Color>();

		//run through the input buffer
		for(; nCurrPos < dataLen; )
		{
			//read in the tag
			uint8 nTag = pCompressed[nCurrPos];
			nCurrPos++;

			//see if it is a run or a span
			bool bIsRun = (nTag & 0x80) != 0 ? true : false;
		
			//blit the color span
			uint32 nRunLen = (uint32)(nTag & 0x7F) + 1;
			for (uint32 nCurrPel = 0; nCurrPel < nRunLen; nCurrPel++)
			{
				//set the color
				Color c = new Color(pCompressed[nCurrPos + 0] / 255f, pCompressed[nCurrPos + 1] / 255f, pCompressed[nCurrPos + 2] / 255f);
				pCurrOut.Add(c);
				//if it isn't a run, we need to move on to the next input color
				if (!bIsRun)
				{
					//update the input position
					nCurrPos += 3;
				}

			}

			//if this was a run, we need to move onto the next byte now
			if (bIsRun)
			{
				//update the input position
				nCurrPos += 3;
			}		
		}
		pCurrOut.Reverse();

		for(int y = 0; y < height; y++)
		{
			pCurrOut.Reverse(y * width, width);
		}
		return true;
	}
}

class WorldModel
{
	//Vert info
	public List<Vector3> m_vPos = new List<Vector3>();
	public List<Vector3> m_vNormal = new List<Vector3>();
	public List<Vector4> m_vTangent = new List<Vector4>();
	public List<Vector3> m_vBinormal = new List<Vector3>();
	public List<Vector2> m_fUV0 = new List<Vector2>();
	public List<Vector2> m_fUV1 = new List<Vector2>();
	public List<Color> m_nColor = new List<Color>();

	//Triangles info
	public List<int>[] tris;
	public List<int> m_nTris = new List<int>();
	public List<int> m_nPolyIndex = new List<int>();

	//Texture paths
	public List<string> names = new List<string>();
	public List<Texture2D> lightmaps = new List<Texture2D>();
}
