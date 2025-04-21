using UnityEngine;
using static LithFAQ.LTTypes;

[System.Serializable]
public class WorldObjectPropertyModel
{
    public string Name { get;set; }

    public PropType PropType { get;set; }
    
    public string StringValue { get; set; }

    public Vector3 VectorValue { get; set; }

    public float FloatValue { get; set; }

    public uint UIntValue { get; set; }

    public bool BoolValue { get; set; }

    public Quaternion QuatValue { get; set; }
}
