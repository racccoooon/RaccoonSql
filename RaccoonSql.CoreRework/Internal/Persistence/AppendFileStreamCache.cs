using System.IO.Abstractions;

namespace RaccoonSql.CoreRework.Internal.Persistence;

public class AppendFileStreamCache
{
    private readonly Dictionary<string, Stream> _streams = new();

    ~AppendFileStreamCache()
    {
        foreach (var stream in _streams.Values)
        {
            stream.Flush();
            stream.Close();
        }
    }
    
    public Stream GetAppendStream(IFileSystem fileSystem, string path)
    {
        // ReSharper disable once InvertIf
        if (!_streams.TryGetValue(path, out var stream))
        {
            stream = _streams[path] = fileSystem.File.Open(path, FileMode.Append);
        }

        return stream;
    }

    public void DeleteFile(IFileSystem fileSystem, string path)
    {
        if (!_streams.Remove(path, out var stream)) 
            return;
        
        stream.Dispose();
        fileSystem.File.Delete(path);
    }
}