// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using UMI.Core.Services;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// I-021 Stufe 1: Die Prüfung "ist diese Datei der erwartete Installer?" (SHA256).
/// Erwartete Hashes sind feste, unabhängig bekannte Werte (NIST/FIPS-180-Testvektoren
/// bzw. <c>sha256sum</c>), nie über die geprüfte Funktion gebildet.
/// </summary>
public sealed class InstallerIntegrityTests : IDisposable
{
    // SHA256("abc") — NIST-Testvektor.
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    // SHA256("abd") — `printf abd | sha256sum`, ein Byte anders als "abc".
    private const string AbdSha256 = "a52d159f262b2c6ddb724a61840befc36eb30c88877a4030b65cbe86298449c9";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "umi_integrity_" + Guid.NewGuid().ToString("N"));

    public InstallerIntegrityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string WriteFile(string content)
    {
        var path = Path.Combine(_dir, "UMI_Setup_test.exe");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(content));
        return path;
    }

    // --- korrekte Datei ---

    [Fact]
    public async Task Check_CorrectFileAndDigest_IsValid()
    {
        var path = WriteFile("abc");

        using var check = await InstallerIntegrity.CheckAsync(path, "sha256:" + AbcSha256, null, CancellationToken.None);

        check.Verdict.Should().Be(InstallerVerdict.Valid);
        check.ActualSha256.Should().Be(AbcSha256);
        File.Exists(path).Should().BeTrue("eine gültige Datei bleibt liegen");
    }

    [Fact]
    public async Task Check_HexCaseDiffers_IsStillValid()
    {
        var path = WriteFile("abc");

        using var check = await InstallerIntegrity.CheckAsync(
            path, "sha256:" + AbcSha256.ToUpperInvariant(), null, CancellationToken.None);

        check.Verdict.Should().Be(InstallerVerdict.Valid);
    }

    // --- ein Byte geändert ---

    [Fact]
    public async Task Check_OneByteChanged_IsMismatch_AndFileIsDeleted()
    {
        var path = WriteFile("abd"); // erwartet wird der Hash von "abc"

        using var check = await InstallerIntegrity.CheckAsync(path, "sha256:" + AbcSha256, null, CancellationToken.None);

        check.Verdict.Should().Be(InstallerVerdict.Mismatch);
        check.ExpectedSha256.Should().Be(AbcSha256);
        check.ActualSha256.Should().Be(AbdSha256);
        File.Exists(path).Should().BeFalse("eine abweichende Datei wird sofort gelöscht");
    }

    [Fact]
    public async Task Check_Mismatch_LogsWarningWithExpectedAndActualHash()
    {
        var path = WriteFile("abd");
        var logger = new ListLogger();

        using var check = await InstallerIntegrity.CheckAsync(path, "sha256:" + AbcSha256, logger, CancellationToken.None);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains(AbcSha256)
            && e.Message.Contains(AbdSha256));
    }

    // --- kein Teilvergleich ---

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    public async Task Check_ExpectedDiffersOnlyInOneChar_IsMismatch(int index)
    {
        // Gleiche 63 Zeichen, ein Zeichen anders: ein Präfix-/Teilvergleich würde das durchwinken.
        var chars = AbcSha256.ToCharArray();
        chars[index] = chars[index] == '0' ? '1' : '0';
        var path = WriteFile("abc");

        using var check = await InstallerIntegrity.CheckAsync(
            path, "sha256:" + new string(chars), null, CancellationToken.None);

        check.Verdict.Should().Be(InstallerVerdict.Mismatch);
        File.Exists(path).Should().BeFalse();
    }

    [Theory]
    [InlineData(8)]
    [InlineData(63)]
    public async Task Check_PrefixOfCorrectHash_IsNotVerifiable_NotValid(int length)
    {
        var path = WriteFile("abc");

        using var check = await InstallerIntegrity.CheckAsync(
            path, "sha256:" + AbcSha256[..length], null, CancellationToken.None);

        check.Verdict.Should().Be(InstallerVerdict.NotVerifiable);
    }

    // --- Prüfung nicht möglich ---

    public static TheoryData<string?> UnusableDigests => new()
    {
        null,
        "",
        AbcSha256,                                  // Präfix fehlt
        "sha1:" + AbcSha256[..40],                  // falsches Präfix
        "SHA256:" + AbcSha256,                      // Präfix strikt klein
        "sha256:",                                  // leer
        "sha256:" + AbcSha256 + "0",                // 65 Zeichen
        "sha256:" + new string('g', 64),            // richtige Länge, kein Hex
    };

    [Theory]
    [MemberData(nameof(UnusableDigests))]
    public async Task Check_UnusableDigest_IsNotVerifiable_AndFileUntouched(string? digest)
    {
        var path = WriteFile("abc");

        using var check = await InstallerIntegrity.CheckAsync(path, digest, null, CancellationToken.None);

        check.Verdict.Should().Be(InstallerVerdict.NotVerifiable);
        check.Verdict.Should().NotBe(InstallerVerdict.Valid);
        File.Exists(path).Should().BeTrue("ohne Vergleich gibt es keinen Grund zu löschen");
    }

    // --- Austausch-Fenster: Handle bleibt offen ---

    [Fact]
    public async Task Check_Valid_HoldsFileOpen_UntilDisposed()
    {
        var path = WriteFile("abc");

        var check = await InstallerIntegrity.CheckAsync(path, "sha256:" + AbcSha256, null, CancellationToken.None);
        try
        {
            check.Verdict.Should().Be(InstallerVerdict.Valid);

            // Zwischen Prüfung und Start darf niemand schreiben, ersetzen oder löschen.
            // Windows meldet je nach Aufruf IOException (Sharing Violation) oder UnauthorizedAccessException.
            var write = () => File.WriteAllText(path, "evil");
            var delete = () => File.Delete(path);
            var replace = () => File.Move(WriteOther(), path, overwrite: true);
            foreach (var attack in new[] { write, delete, replace })
            {
                var thrown = Record.Exception(attack);
                (thrown is IOException or UnauthorizedAccessException).Should().BeTrue(
                    "der offene Handle muss Schreiben/Löschen/Ersetzen verhindern, war aber: {0}", thrown);
            }

            // Lesen (wie ShellExecute/CreateProcess) bleibt möglich.
            var read = () => { using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); };
            read.Should().NotThrow();
        }
        finally
        {
            check.Dispose();
        }

        var writeAfter = () => File.WriteAllText(path, "ok");
        writeAfter.Should().NotThrow("nach Dispose ist die Datei wieder frei");
    }

    private string WriteOther()
    {
        var other = Path.Combine(_dir, "other.exe");
        File.WriteAllText(other, "other");
        return other;
    }

    // --- Download + Digest-Pfad durch GitHubReleaseChecker ---

    [Fact]
    public async Task Download_CorrectDigest_ReturnsValid_AndKeepsFile()
    {
        var sut = CheckerServing("abc", out var handler);
        var target = Path.Combine(_dir, "dl.exe");

        var verdict = await sut.DownloadSetupAsync(Asset("sha256:" + AbcSha256), target, null, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.Valid);
        File.ReadAllText(target).Should().Be("abc");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Download_WrongContent_ReturnsMismatch_AndDeletesFile()
    {
        var sut = CheckerServing("abd", out _);
        var target = Path.Combine(_dir, "dl.exe");

        var verdict = await sut.DownloadSetupAsync(Asset("sha256:" + AbcSha256), target, null, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.Mismatch);
        File.Exists(target).Should().BeFalse();
    }

    [Fact]
    public async Task Download_NoDigest_ReturnsNotVerifiable_WithoutDownloading()
    {
        var sut = CheckerServing("abc", out var handler);
        var target = Path.Combine(_dir, "dl.exe");

        var verdict = await sut.DownloadSetupAsync(Asset(null), target, null, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.NotVerifiable);
        handler.Calls.Should().Be(0, "ohne Digest wird nichts geladen");
        File.Exists(target).Should().BeFalse();
    }

    // --- Teil-Datei bei Fehler/Abbruch ---

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(OperationCanceledException))]
    public async Task Download_FailsMidStream_DeletesPartialFile_AndRethrows(Type exceptionType)
    {
        var failure = (Exception)Activator.CreateInstance(exceptionType)!;
        var sut = new GitHubReleaseChecker(new HttpClient(new FailingMidStreamHandler(failure)));
        var target = Path.Combine(_dir, "dl.exe");

        var act = () => sut.DownloadSetupAsync(Asset("sha256:" + AbcSha256), target, null, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().Where(e => e.GetType() == exceptionType);
        File.Exists(target).Should().BeFalse("eine Teil-Datei darf nicht in %TEMP% liegen bleiben");
    }

    private static SetupAsset Asset(string? digest) => new("https://example.test/setup.exe", digest);

    private sealed class FailingMidStreamHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ThrowAfterFirstReadStream(failure))
            });
    }

    /// <summary>Liefert beim ersten Lesen 3 Bytes, beim zweiten die Exception — ein abgebrochener Download.</summary>
    private sealed class ThrowAfterFirstReadStream(Exception failure) : Stream
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

    private static GitHubReleaseChecker CheckerServing(string body, out CountingHandler handler)
    {
        handler = new CountingHandler(body);
        return new GitHubReleaseChecker(new HttpClient(handler));
    }

    private sealed class CountingHandler(string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.ASCII.GetBytes(body))
            });
        }
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
