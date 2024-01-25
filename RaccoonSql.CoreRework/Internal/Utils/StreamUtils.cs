namespace RaccoonSql.CoreRework.Internal.Utils;

public static class StreamUtils
{
    public static byte[] ToArray(this Stream stream)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var tempStream = new MemoryStream();
        stream.CopyTo(tempStream);
        return tempStream.ToArray();
    }
}