using System.Collections.Generic;

public class DATModel
{
    public int Version { get; set; }
    public WorldModel WorldModel { get; set; }
    public List<WorldObjectModel> WorldObjects { get; set; }
    public List<BSPModel> BSPModels { get; set; }

    public DATModel(int version)
    {
        Version = version;
        BSPModels = new List<BSPModel>();
    }
}