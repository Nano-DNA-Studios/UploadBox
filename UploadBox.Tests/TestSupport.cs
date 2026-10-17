using Microsoft.AspNetCore.Components.Forms;

namespace UploadBox.Tests;

/// <summary>
/// Provides an in-memory browser file for upload tests.
/// </summary>
internal sealed class TestBrowserFile : IBrowserFile
{
    /// <summary>
    /// Stores in-memory file content when no stream factory is provided.
    /// </summary>
    private readonly byte[]? content;

    /// <summary>
    /// Creates a content stream when the file uses deferred content.
    /// </summary>
    private readonly Func<Stream>? streamFactory;

    /// <summary>
    /// Initializes a browser file backed by the specified byte content.
    /// </summary>
    /// <param name="name">Name reported for the browser file.</param>
    /// <param name="content">Bytes returned by the browser file stream.</param>
    /// <param name="contentType">Content type reported for the browser file.</param>
    public TestBrowserFile(string name, byte[] content, string contentType = "application/octet-stream")
    {
        Name = name;
        this.content = content;
        ContentType = contentType;
        LastModified = DateTimeOffset.UtcNow;
        Size = content.LongLength;
    }

    /// <summary>
    /// Initializes a browser file that creates its content stream on demand.
    /// </summary>
    /// <param name="name">Name reported for the browser file.</param>
    /// <param name="size">Declared file size in bytes.</param>
    /// <param name="streamFactory">Factory that creates the content stream.</param>
    /// <param name="contentType">Content type reported for the browser file.</param>
    public TestBrowserFile(string name, long size, Func<Stream> streamFactory, string contentType = "application/octet-stream")
    {
        Name = name;
        Size = size;
        this.streamFactory = streamFactory;
        ContentType = contentType;
        LastModified = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the name reported for the browser file.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the last-modified time reported for the browser file.
    /// </summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>
    /// Gets the declared file size in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets the content type reported for the browser file.
    /// </summary>
    public string ContentType { get; }

    /// <inheritdoc cref="IBrowserFile.OpenReadStream(long, CancellationToken)"/>
    /// <exception cref="IOException">Thrown when the declared file size exceeds <paramref name="maxAllowedSize"/>.</exception>
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
    /// <summary>
    /// Browser file with a valid lowercase ISO extension.
    /// </summary>
    public static readonly TestBrowserFile ValidIso = new("disk.iso", [1, 2, 3]);

    /// <summary>
    /// Browser file used as a normal upload source.
    /// </summary>
    public static readonly TestBrowserFile SourceIso = new("source.iso", [1, 2, 3]);

    /// <summary>
    /// One-byte browser file used for source-stream scenarios.
    /// </summary>
    public static readonly TestBrowserFile SourceOneByteIso = new("source.iso", [1]);

    /// <summary>
    /// One-byte browser file fixture.
    /// </summary>
    public static readonly TestBrowserFile OneByteIso = new("one.iso", [1]);

    /// <summary>
    /// Two-byte browser file fixture.
    /// </summary>
    public static readonly TestBrowserFile TwoByteIso = new("two.iso", [2]);

    /// <summary>
    /// Three-byte browser file fixture.
    /// </summary>
    public static readonly TestBrowserFile ThreeByteIso = new("three.iso", [3]);

    /// <summary>
    /// Nine-byte browser file fixture.
    /// </summary>
    public static readonly TestBrowserFile NineByteIso = new("a.iso", [9]);

    /// <summary>
    /// Browser file with an uppercase ISO extension.
    /// </summary>
    public static readonly TestBrowserFile UppercaseIso = new("one.ISO", [1]);

    /// <summary>
    /// Browser file with an unsupported extension.
    /// </summary>
    public static readonly TestBrowserFile WrongExtension = new("disk.bin", [1]);

    /// <summary>
    /// First browser file with an unsupported extension.
    /// </summary>
    public static readonly TestBrowserFile FirstWrongExtension = new("one.bin", [1]);

    /// <summary>
    /// Second browser file with an unsupported extension.
    /// </summary>
    public static readonly TestBrowserFile SecondWrongExtension = new("two.bin", [2]);

    /// <summary>
    /// Browser file whose size matches a configured limit.
    /// </summary>
    public static readonly TestBrowserFile ExactLimitIso = new("exact.iso", [1, 2]);

    /// <summary>
    /// Browser file larger than the configured test limit.
    /// </summary>
    public static readonly TestBrowserFile OversizedIso = new("large.iso", [1, 2, 3]);

    /// <summary>
    /// Zero-byte browser file fixture.
    /// </summary>
    public static readonly TestBrowserFile EmptyIso = new("empty.iso", []);
}

/// <summary>
/// Creates a temporary directory and removes it when disposed.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    /// Creates a uniquely named temporary directory.
    /// </summary>
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"UploadBox.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Gets the path of the temporary directory.
    /// </summary>
    public string Path { get; }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    /// <remarks>Deletes the temporary directory when it still exists.</remarks>
    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}

/// <summary>
/// Provides a readable stream that waits until cancellation is requested.
/// </summary>
internal sealed class CancellationGateStream : Stream
{
    /// <summary>
    /// Signals when a read operation begins.
    /// </summary>
    private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets a task that completes when a read operation begins.
    /// </summary>
    public Task Started => started.Task;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <inheritdoc/>
    /// <remarks>Signals <see cref="Started"/> when reading begins, then waits for cancellation.</remarks>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
