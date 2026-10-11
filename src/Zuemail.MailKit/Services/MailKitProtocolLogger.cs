using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zuemail.Core.Models;

namespace Zuemail.MailKit.Services;

/// <summary>Formats and asynchronously writes MailKit protocol traffic while redacting authentication secrets.</summary>
public sealed class MailKitProtocolLogger : IProtocolLogger, IDisposable
{
    private readonly ProtocolLoggerOptions _options;
    private readonly ILogger _logger;
    private readonly IProtocolLogger _secretDetector = new ProtocolLogger(Stream.Null);
    private readonly ConcurrentQueue<string> _pending = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly object _formatLock = new();
    private readonly object _stateLock = new();
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;
    private readonly Task _writerTask;
    private bool _clientMidline;
    private bool _serverMidline;
    private bool _completed;
    private bool _disposed;

    /// <summary>Creates a logger using the configured file path, stderr, or the supplied text writer.</summary>
    public MailKitProtocolLogger(ProtocolLoggerOptions options, ILogger<MailKitProtocolLogger> logger = null,
        TextWriter output = null)
    {
        _options = options?.Copy() ?? new ProtocolLoggerOptions();
        _logger = logger ?? NullLogger<MailKitProtocolLogger>.Instance;
        if (_options.TimestampFormat?.Equals("default", StringComparison.OrdinalIgnoreCase) == true)
            _options.TimestampFormat = ProtocolLoggerOptions.DefaultTimestampFormat;

        if (output != null)
        {
            _writer = output;
            _ownsWriter = false;
        }
        else if (_options.FilePath?.Equals("console", StringComparison.OrdinalIgnoreCase) == true)
        {
            _writer = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false), 1024, leaveOpen: true);
            _ownsWriter = true;
        }
        else if (!string.IsNullOrWhiteSpace(_options.FilePath))
        {
            var directory = Path.GetDirectoryName(_options.FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            _writer = new StreamWriter(_options.FilePath, _options.AppendToExisting, new UTF8Encoding(false));
            _ownsWriter = true;
        }
        else
        {
            _writer = TextWriter.Null;
            _ownsWriter = false;
        }

        _writerTask = Task.Run(WriteQueueAsync);
    }

    /// <summary>Gets or sets the detector MailKit uses to identify authentication secrets.</summary>
    public IAuthenticationSecretDetector AuthenticationSecretDetector
    {
        get => _secretDetector.AuthenticationSecretDetector;
        set => _secretDetector.AuthenticationSecretDetector = value;
    }

    /// <summary>Logs the connection endpoint.</summary>
    public void LogConnect(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));
        lock (_formatLock)
        {
            _clientMidline = false;
            _serverMidline = false;
            Enqueue(AddTimestamp(new StringBuilder()).Append("Connected to ").Append(uri).ToString());
        }
    }

    /// <summary>Logs bytes sent by the client after masking detected credentials.</summary>
    public void LogClient(byte[] buffer, int offset, int count) => Log(buffer, offset, count, isClient: true);

    /// <summary>Logs bytes received from the server.</summary>
    public void LogServer(byte[] buffer, int offset, int count) => Log(buffer, offset, count, isClient: false);

    private void Log(byte[] buffer, int offset, int count, bool isClient)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || count > buffer.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0)
            return;

        lock (_formatLock)
        {
            var end = offset + count;
            var lineStart = offset;
            for (var index = offset; index < end; index++)
            {
                if (buffer[index] != (byte)'\n')
                    continue;
                var hasPrefix = isClient ? _clientMidline : _serverMidline;
                FormatSegment(buffer, lineStart, index, isClient, hasPrefix, isFinal: true);
                if (isClient)
                    _clientMidline = false;
                else
                    _serverMidline = false;
                lineStart = index + 1;
            }

            if (lineStart < end)
            {
                var hasPrefix = isClient ? _clientMidline : _serverMidline;
                FormatSegment(buffer, lineStart, end, isClient, hasPrefix, isFinal: false);
                if (isClient)
                    _clientMidline = true;
                else
                    _serverMidline = true;
            }
        }
    }

    private void FormatSegment(byte[] buffer, int start, int end, bool isClient, bool hasPrefix, bool isFinal)
    {
        var textEnd = end;
        if (isFinal && textEnd > start && buffer[textEnd - 1] == (byte)'\r')
            textEnd--;
        var text = new StringBuilder();
        if (!hasPrefix)
        {
            AddTimestamp(text);
            text.Append(isClient ? _options.ClientPrefix : _options.ServerPrefix);
        }

        var cursor = start;
        if (isClient && AuthenticationSecretDetector != null)
        {
            var secrets = AuthenticationSecretDetector.DetectSecrets(buffer, start, end - start);
            if (secrets != null)
            {
                foreach (var secret in secrets)
                {
                    var secretStart = Math.Max(cursor, secret.StartIndex);
                    var secretEnd = Math.Min(textEnd, secret.StartIndex + secret.Length);
                    if (secretEnd <= secretStart)
                        continue;
                    AppendBytes(text, buffer, cursor, secretStart - cursor);
                    text.Append("********");
                    cursor = secretEnd;
                }
            }
        }
        AppendBytes(text, buffer, cursor, textEnd - cursor);
        Enqueue(text.ToString());
    }

    private static void AppendBytes(StringBuilder builder, byte[] buffer, int offset, int count)
    {
        if (count > 0)
            builder.Append(Encoding.UTF8.GetString(buffer, offset, count));
    }

    private StringBuilder AddTimestamp(StringBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(_options.TimestampFormat))
        {
            builder.Append(DateTimeOffset.Now.ToString(_options.TimestampFormat, CultureInfo.InvariantCulture));
            builder.Append(' ');
        }
        return builder;
    }

    private void Enqueue(string line)
    {
        lock (_stateLock)
        {
            if (_completed)
                return;
            _pending.Enqueue(line);
            _available.Release();
        }
    }

    private async Task WriteQueueAsync()
    {
        while (true)
        {
            await _available.WaitAsync().ConfigureAwait(false);
            while (_pending.TryDequeue(out var line))
            {
                try
                {
                    if (_writer != TextWriter.Null)
                    {
                        await _writer.WriteLineAsync(line).ConfigureAwait(false);
                        await _writer.FlushAsync().ConfigureAwait(false);
                    }
                    _logger.LogTrace("{ProtocolLine}", line);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to write a MailKit protocol log line.");
                }
            }
            lock (_stateLock)
            {
                if (_completed && _pending.IsEmpty)
                    return;
            }
        }
    }

    /// <summary>Stops accepting records, drains queued output, and releases owned writers.</summary>
    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_completed)
                return;
            _completed = true;
            _available.Release();
        }
        _writerTask.GetAwaiter().GetResult();
        if (_ownsWriter)
            _writer.Dispose();
        _secretDetector.Dispose();
        _available.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Indicates whether disposal has completed.</summary>
    public bool IsDisposed => _disposed;

    public override string ToString() => _options.ToString();
}