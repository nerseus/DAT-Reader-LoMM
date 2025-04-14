using System.Collections.Generic;

public class WorldObjectModel
{
    public Dictionary<string, object> options { get; set; } = new Dictionary<string, object>();
    public string objectName { get; set; }
    public string objectType { get; set; }
    public short dataLength { get; set; }
    public long dataOffset { get; set; }
    public int objectEntries { get; set; }
    public List<int> objectEntryFlag { get; set; } = new List<int>();
    public List<short> objectEntryStringDataLength { get; set; } = new List<short>();
    public List<short> objectEntryStringLength { get; set; } = new List<short>();
}
