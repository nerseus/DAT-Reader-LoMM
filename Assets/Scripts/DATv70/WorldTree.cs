using System.Collections.Generic;
using System.IO;
using static LithFAQ.LTTypes;
using static LithFAQ.LTUtils;

public class WorldTree
{
    public int nNumNode { get; set; }
    public WorldTreeNode pRootNode { get; set; }
    public List<WorldTreeNode> pNodes { get; set; }

    public WorldTree()
    {
        pNodes = new List<WorldTreeNode>();
        pRootNode = new WorldTreeNode(pNodes);
    }

    public void ReadWorldTree(ref BinaryReader b)
    {
        int nDummyTerrainDepth, nCurOffset, i;
        LTVector vBoxMin, vBoxMax;
        byte nCurByte, nCurBit;
        WorldTreeNode pNewNode;

        nDummyTerrainDepth = 0;
        vBoxMin = ReadLTVector(ref b);
        vBoxMax = ReadLTVector(ref b);

        nNumNode = b.ReadInt32();
        nDummyTerrainDepth = b.ReadInt32();

        i = 0;

        if (nNumNode > 1)
        {
            while(i < nNumNode - 1)
            {
                pNewNode = new WorldTreeNode(pNodes);
                pNodes.Add(pNewNode);
                i++;
            }
        }

        nCurByte = 0;
        nCurBit = 8;

        pRootNode.SetBB(vBoxMin, vBoxMax);

        nCurOffset = 0;

        pRootNode.LoadLayout(ref b, ref nCurByte, ref nCurBit, pNodes, ref nCurOffset);
    }

}