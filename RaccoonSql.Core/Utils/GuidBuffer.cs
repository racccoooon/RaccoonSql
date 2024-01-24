using System.Runtime.InteropServices;

namespace RaccoonSql.Core.Utils;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct GuidBuffer(Guid guid)
{
    [FieldOffset(0)]
    public fixed uint Uint[4];

    [FieldOffset(0)] 
    public Guid Guid = guid;
}


public static class Foo
{
    public static unsafe uint GetUint(this Guid id, int index)
    {
        return ((uint*)&id)[index];
    }
}