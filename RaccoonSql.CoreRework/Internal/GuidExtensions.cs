using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Internal;

public static class GuidExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint GetUint3(this Guid id)
    {
        return ((uint*)&id)[3];
    }
}