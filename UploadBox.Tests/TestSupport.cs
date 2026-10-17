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

/// <summary>
/// Shared immutable browser-file examples for routine upload scenarios.
/// Each call to <see cref="IBrowserFile.OpenReadStream(long, CancellationToken)"/> creates a fresh stream.
/// </summary>
internal static class TestUpload
{
    public static readonly TestBrowserFile ValidIso = new("disk.iso", [1, 2, 3]);
    public static readonly TestBrowserFile SourceIso = new("source.iso", [1, 2, 3]);
    public static readonly TestBrowserFile SourceOneByteIso = new("source.iso", [1]);
    public static readonly TestBrowserFile OneByteIso = new("one.iso", [1]);
    public static readonly TestBrowserFile TwoByteIso = new("two.iso", [2]);
    public static readonly TestBrowserFile ThreeByteIso = new("three.iso", [3]);
    public static readonly TestBrowserFile NineByteIso = new("a.iso", [9]);
    public static readonly TestBrowserFile UppercaseIso = new("one.ISO", [1]);
    public static readonly TestBrowserFile WrongExtension = new("disk.bin", [1]);
    public static readonly TestBrowserFile FirstWrongExtension = new("one.bin", [1]);
    public static readonly TestBrowserFile SecondWrongExtension = new("two.bin", [2]);
    public static readonly TestBrowserFile ExactLimitIso = new("exact.iso", [1, 2]);
    public static readonly TestBrowserFile OversizedIso = new("large.iso", [1, 2, 3]);
    public static readonly TestBrowserFile EmptyIso = new("empty.iso", []);
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
