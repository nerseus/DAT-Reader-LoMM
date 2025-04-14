using System;
using System.Collections.Generic;

public class Leafs
{
    public short m_nNumLeafLists;
    public short m_nLeafListIndex;
    public List<LeafList> m_pLeafLists = new List<LeafList>();

    public int m_nPoliesCount;

    public short[] m_pPolies;

    public int m_nCardinal1;
}
