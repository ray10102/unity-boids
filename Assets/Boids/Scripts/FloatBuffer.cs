using Unity.Entities;
using Unity.Mathematics;

public struct Float3BufferElement : IBufferElementData
{
    public float3 Value;

    // The following implicit conversions are optional, but can be convenient.
    public static implicit operator float3(Float3BufferElement e)
    {
        return e.Value;
    }

    public static implicit operator Float3BufferElement(float3 e)
    {
        return new Float3BufferElement { Value = e };
    }
}