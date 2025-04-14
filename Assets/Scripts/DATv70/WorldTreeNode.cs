using System.Collections.Generic;
using System.IO;
using static LithFAQ.LTTypes;
using static LithFAQ.LTUtils;

public class WorldTreeNode
{
    public WorldTreeNode(List<WorldTreeNode> nodeList)
    {
        pNodeList = nodeList;
    }

    public WorldTreeNode()
    {
    }

    public LTVector vBBoxMin { get; set; }
    public LTVector vBBoxMax { get; set; }
    public LTFloat fCenterX { get; set; }
    public LTFloat fCenterZ { get; set; }
    public LTFloat fSmallestDim { get; set; }
    public WorldTreeNode pParent { get; set; }
    public int nChildren { get; set; }
    public List<WorldTreeNode> pNodeList { get; set; }

    public void SetBB(LTVector a, LTVector b)
    {
        vBBoxMin = a;
        vBBoxMax = b;

        fCenterX = (b.X + a.X) * 0.5f;
        fCenterZ = (b.Z + a.Z) * 0.5f;
        fSmallestDim = (LTFloat)System.Math.Min(b.X - a.X, b.Z - a.Z);
    }

    public WorldTreeNode GetChild(int nX, int nZ)
    {
        return pNodeList[nChildren + (nX * 2 + nZ)];
    }

    public WorldTreeNode GetChild(int nNode)
    {
        return pNodeList[nChildren + nNode];
    }

    public void Subdivide(List<WorldTreeNode> pNodeList, ref int nCurOffset)
    {
        LTVector vMin, vMax;

        nChildren = nCurOffset;
        nCurOffset = nCurOffset + 4;

        for(int i = 0; i < 4 ; i++)
        {
            GetChild(i).pParent = this;
        }

        vMin = new LTVector(vBBoxMin.X, vBBoxMin.Y, vBBoxMin.Z);
        vMax = new LTVector(fCenterX, vBBoxMax.Y, fCenterZ);
        GetChild(0, 0).SetBB(vMin, vMax);

        vMin = new LTVector(vBBoxMin.X, vBBoxMin.Y, fCenterZ);
        vMax = new LTVector(fCenterX, vBBoxMax.Y, vBBoxMax.Z);
        GetChild(0, 1).SetBB(vMin, vMax);

        vMin = new LTVector(fCenterX, vBBoxMin.Y, vBBoxMin.Z);
        vMax = new LTVector(vBBoxMax.X, vBBoxMax.Y, fCenterZ);
        GetChild(1, 0).SetBB(vMin, vMax);

        vMin = new LTVector(fCenterX, vBBoxMin.Y, fCenterZ);
        vMax = new LTVector(vBBoxMax.X, vBBoxMax.Y, vBBoxMax.Z);
        GetChild(1, 1).SetBB(vMin, vMax);


    }
    public void LoadLayout(ref BinaryReader b, ref byte flags, ref byte bitFlagToCheck, List<WorldTreeNode> pNodeList, ref int nCurOffset)
    {
        bool bSubdivide;

        if(bitFlagToCheck == 8)
        {
            flags = (byte)b.ReadByte();
            bitFlagToCheck = 0;
        }

        bSubdivide = (flags & (1 << bitFlagToCheck)) > 0;
        bitFlagToCheck++;

        if (bSubdivide)
        {
            Subdivide(pNodeList, ref nCurOffset);

            for (int i = 0; i < 4; i++)
            {
                GetChild(i).LoadLayout(ref b, ref flags, ref bitFlagToCheck, pNodeList, ref nCurOffset);
            }
        }
    }
}
