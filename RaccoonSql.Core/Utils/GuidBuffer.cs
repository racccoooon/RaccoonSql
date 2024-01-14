using System.Runtime.InteropServices;

namespace RaccoonSql.Core.Utils;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct GuidBuffer(Guid guid)
{
    [FieldOffset(0)]
    public fixed uint Int[4];

    [FieldOffset(0)] 
    public Guid Guid = guid;
}