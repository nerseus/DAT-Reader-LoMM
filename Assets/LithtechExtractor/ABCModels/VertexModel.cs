using System.Collections.Generic;
using UnityEngine;

public class VertexModel
{
    public ushort SublodVertexIndex { get; set; }
    public List<WeightModel> Weights { get; set; }
    public Vector3 Location { get; set; }
    public Vector3 Normal { get; set; }
}
