// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UMI.Core.Tests;

/// <summary>Gemeinsame Test-Doubles der Download-Prüfungen (Installer I-021, Werkzeuge I-022).</summary>
internal class ListLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}

/// <summary><see cref="ListLogger"/> für Konstruktoren, die einen <c>ILogger&lt;T&gt;</c> verlangen.</summary>
internal sealed class ListLogger<T> : ListLogger, ILogger<T>;

/// <summary>Antwortet auf jede Anfrage über <paramref name="respond"/> und merkt sich die angefragten URLs.</summary>
internal sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requested { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requested.Add(request.RequestUri!.ToString());
        var response = respond(request);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }
}

/// <summary>Antwortet auf jede Anfrage mit einem Body, der beim zweiten Lesen die Exception wirft — ein abgebrochener Download.</summary>
internal sealed class FailingMidStreamHandler(Exception failure) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ThrowAfterFirstReadStream(failure))
        });
}

/// <summary>Liefert beim ersten Lesen 3 Bytes, beim zweiten die Exception.</summary>
internal sealed class ThrowAfterFirstReadStream(Exception failure) : Stream
{
    private bool _served;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_served)
            throw failure;
        _served = true;
        Encoding.ASCII.GetBytes("abc").CopyTo(buffer, offset);
        return 3;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
