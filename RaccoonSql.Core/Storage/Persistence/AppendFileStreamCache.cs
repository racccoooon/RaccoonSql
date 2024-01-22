using System.IO.Abstractions;

namespace RaccoonSql.Core.Storage.Persistence;

public class AppendFileStreamCache(IFileSystem fileSystem)
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
    
    public Stream GetAppendStream(string path)
    {
        if (!_streams.TryGetValue(path, out var stream))
        {
            stream = fileSystem.File.Open(path, FileMode.Append);
            _streams[path] = stream;
        }

        return stream;
    }

    public void DeleteFile(string path)
    {
        if (_streams.Remove(path, out var stream))
        {
            stream.Dispose();
        }
        fileSystem.File.Delete(path);
    }
}