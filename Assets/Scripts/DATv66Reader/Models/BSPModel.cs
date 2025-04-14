using LithFAQ;
using System;
using System.Collections.Generic;
using static LithFAQ.LTTypes;

public class BSPModel
{
    public Int16 WorldInfoFlags { get; set; }
    public string WorldName { get; set; }
    public int PointCount { get; set; }
    public int PlaneCount { get; set; }
    public int SurfaceCount { get; set; }
    public int UserPortalCount { get; set; }
    public int PolyCount { get; set; }
    public int LeafCount { get; set; }
    public int VertCount { get; set; }
    public int TotalVisListSize { get; set; }
    public int LeafListCount { get; set; }
    public int NodeCount { get; set; }
    public int SectionCount { get; set; }
    public LTVector MinBox { get; set; }
    public LTVector MaxBox { get; set; }
    public LTVector WorldTranslation { get; set; }
    public int NamesLen { get; set; }
    public int TextureCount { get; set; }
    public List<string> TextureNames { get; set; } = new List<string>();
    public List<WorldPolyModel> Polies { get; set; } = new List<WorldPolyModel>();
    public List<LeafListModel> Leafs { get; set; } = new List<LeafListModel>();
    public List<WorldPlane> Planes { get; set; } = new List<WorldPlane>();
    public List<WorldSurface> Surfaces { get; set; } = new List<WorldSurface>();
    public List<LTVector> Vertices { get; set; } = new List<LTVector>();
    public List<WorldTreeNode> m_pNodes { get; set; }
    public List<object> m_pUserPortals { get; set; } = new List<object>();
    public List<object> m_pPBlockTable { get; set; } = new List<object>();

    //shogo stuff
    public List<WVertex> wVertices { get; set; } = new List<WVertex>();

    public int Version { get; set; }
    public Game GameType { get; set; }
}