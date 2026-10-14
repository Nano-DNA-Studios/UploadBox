using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Tests;

internal sealed class TestBrowserFile : IBrowserFile
{
    private readonly byte[]? content;
    private readonly Func<Stream>? streamFactory;

    public TestBrowserFile(string name, byte[] content, string contentType = "application/octet-stream")
    {
        Name = name;
        this.content = content;
        ContentType = contentType;
        LastModified = DateTimeOffset.UtcNow;
        Size = content.LongLength;
    }

    public TestBrowserFile(string name, long size, Func<Stream> streamFactory, string contentType = "application/octet-stream")
    {
        Name = name;
        Size = size;
        this.streamFactory = streamFactory;
        ContentType = contentType;
        LastModified = DateTimeOffset.UtcNow;
    }

    public string Name { get; }
    public DateTimeOffset LastModified { get; }
    public long Size { get; }
    public string ContentType { get; }

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Size > maxAllowedSize)
            throw new IOException("File exceeds the allowed stream size.");

        cancellationToken.ThrowIfCancellationRequested();
        return streamFactory?.Invoke() ?? new MemoryStream(content!, writable: false);
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"UploadBox.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}

internal sealed class CancellationGateStream : Stream
{
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => started.Task;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
