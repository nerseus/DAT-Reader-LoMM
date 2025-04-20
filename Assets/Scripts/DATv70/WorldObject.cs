using System;
using System.Collections.Generic;
public class WorldObject
{
    public Dictionary<string, object> options { get; set; }
    public string objectName;
    public string objectType;
    public short dataLength;
    public long dataOffset;
    public int objectEntries;
    public List<int> objectEntryFlag { get; set; } = new List<int>();
    public List<short> objectEntryStringDataLength = new List<short>();
    public List<short> objectEntryStringLength = new List<short>();
}
