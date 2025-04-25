using System;
using System.Collections.Generic;
using static LithFAQ.LTTypes;

public class WorldPolyModel
{
    public long IndexAndNumVerts { get; set; }
    public byte LoVerts { get; set; }
    public byte HiVerts{ get; set; }
    public LTVector Center { get; set; }
    public LTFloat Radius { get; set; }
    public Int16 LightmapWidth { get; set; }
    public Int16 LightmapHeight { get; set; }
    public Int16 UnknownNum { get; set; }
    public Int16[] UnknownList { get; set; }
    public LTVector O { get; set; }
    public LTVector P { get; set; }
    public LTVector Q { get; set; }
    public int SurfaceIndex { get; set; }
    public int PlaneIndex { get; set; }
    public List<VertexColor> VertexColorList { get; set; }
    public List<DiskRelVert> RelDiskVerts { get; set; }
    public int m_nLMFrameIndex { get; set; }

    public void FillRelVerts()
    {
        RelDiskVerts = new List<DiskRelVert>();
        for(int i = 0; i < LoVerts; i++)
        {
            RelDiskVerts.Add(new DiskRelVert());
            RelDiskVerts[i].nRelVerts = (short)i;
        }
    }
}