public class DTXModel
{
    public DTXHeader Header { get; set; }
    public string RelativePathToDTX { get; set; }
    public byte[] Data { get; set; }
}