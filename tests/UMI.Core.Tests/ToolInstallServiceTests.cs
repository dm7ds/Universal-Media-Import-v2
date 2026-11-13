// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using UMI.Core.Constants;
using UMI.Core.Services;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// I-022: Werkzeug-Archive (Gyroflow, FFprobe) werden vor dem Entpacken gegen den veröffentlichten
/// SHA256 geprüft. Die Archive sind feste Testvektoren (Base64 einer mit Python erzeugten ZIP, der
/// SHA256 mit <c>sha256sum</c> gemessen) — nie über die geprüfte Funktion gebildet.
/// </summary>
public sealed class ToolInstallServiceTests : IDisposable
{
    // ZIP mit "ffmpeg-9.9.9-essentials_build/bin/ffprobe.exe" = "FAKE-FFPROBE-BINARY".
    private const string FfprobeZipBase64 =
        "UEsDBBQAAAAAAAAAIVzEXoH4EwAAABMAAAAtAAAAZmZtcGVnLTkuOS45LWVzc2VudGlhbHNfYnVpbGQvYmluL2ZmcHJvYmUuZXhlRkFLRS1GRlBST0JFLUJJTkFSWVBLAQIUABQAAAAAAAAAIVzEXoH4EwAAABMAAAAtAAAAAAAAAAAAAACkAQAAAABmZm1wZWctOS45LjktZXNzZW50aWFsc19idWlsZC9iaW4vZmZwcm9iZS5leGVQSwUGAAAAAAEAAQBbAAAAXgAAAAAA";
    private const string FfprobeZipSha256 = "d095b5f0bb9172a928203303ab46d63cdfa90894ffc46907e4332dbb6e3293fa";
    private const int FfprobePayloadOffset = 75; // erstes Byte von "FAKE-FFPROBE-BINARY" in der ZIP

    // ZIP mit "Gyroflow.exe" = "FAKE-GYROFLOW-BINARY".
    private const string GyroflowZipBase64 =
        "UEsDBBQAAAAAAAAAIVyaRqbqFAAAABQAAAAMAAAAR3lyb2Zsb3cuZXhlRkFLRS1HWVJPRkxPVy1CSU5BUllQSwECFAAUAAAAAAAAACFcmkam6hQAAAAUAAAADAAAAAAAAAAAAAAApAEAAAAAR3lyb2Zsb3cuZXhlUEsFBgAAAAABAAEAOgAAAD4AAAAAAA==";
    private const string GyroflowZipSha256 = "6f2c470c4e0fee345a01d543a873a0b6b4afd5ecd69fa863b11e55bb9e2ce44d";
    private const int GyroflowPayloadOffset = 42;
    // Dasselbe Archiv, Byte 42 von 'F' auf 'G' (mit Python hashlib gemessen).
    private const string GyroflowTamperedZipSha256 = "5fde963313578443b9980a9acab8ffd4bd2c0a4c9401a6a545d461d4047fa9c2";

    private const string GitHubApiUrl = "https://api.github.com/repos/gyroflow/gyroflow/releases/latest";
    private const string GyroflowArchiveUrl = "https://example.test/gyroflow/Gyroflow-windows64.zip";
    private const string FfmpegChecksumRequestUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256";
    private const string FfmpegChecksumResolvedUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.9.9-essentials_build.zip.sha256";
    private const string FfmpegArchiveUrl = "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.9.9-essentials_build.zip";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "umi_toolinstall_" + Guid.NewGuid().ToString("N"));
    private string ToolsRoot => Path.Combine(_dir, "tools");
    private string DownloadDir => Path.Combine(_dir, "dl");

    public ToolInstallServiceTests() => Directory.CreateDirectory(DownloadDir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // --- Gyroflow: Digest aus dem GitHub-Release-Asset ---

    [Fact]
    public async Task Gyroflow_CorrectArchive_IsExtracted()
    {
        var sut = Sut(GyroflowServer(GyroflowZipBase64, "sha256:" + GyroflowZipSha256), out _);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Installed);
        result.ExecutablePath.Should().Be(Path.Combine(ToolsRoot, "gyroflow", "Gyroflow.exe"));
        File.ReadAllText(result.ExecutablePath!).Should().Be("FAKE-GYROFLOW-BINARY");
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Gyroflow_OneByteChanged_IsNotExtracted_AndArchiveIsDeleted()
    {
        var sut = Sut(GyroflowServer(Tampered(GyroflowZipBase64, GyroflowPayloadOffset), "sha256:" + GyroflowZipSha256), out _);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Mismatch);
        result.ExecutablePath.Should().BeNull();
        Directory.Exists(Path.Combine(ToolsRoot, "gyroflow")).Should().BeFalse("bei Abweichung wird nichts entpackt");
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Gyroflow_Mismatch_LeavesExistingInstallationUntouched()
    {
        var existing = WriteExistingTool("gyroflow", "Gyroflow.exe", "OLD");
        var sut = Sut(GyroflowServer(Tampered(GyroflowZipBase64, GyroflowPayloadOffset), "sha256:" + GyroflowZipSha256), out _);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Mismatch);
        File.ReadAllText(existing).Should().Be("OLD");
    }

    [Fact]
    public async Task Gyroflow_Mismatch_LogsExpectedAndMeasuredHash()
    {
        var logger = new ListLogger<ToolInstallService>();
        var sut = Sut(GyroflowServer(Tampered(GyroflowZipBase64, GyroflowPayloadOffset), "sha256:" + GyroflowZipSha256), out _, logger);

        await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains(GyroflowZipSha256)
            && e.Message.Contains(GyroflowTamperedZipSha256));
    }

    public static TheoryData<string?> UnusableDigests => new()
    {
        null,
        "",
        GyroflowZipSha256,                  // Präfix fehlt
        "sha256:" + GyroflowZipSha256[..63],
    };

    [Theory]
    [MemberData(nameof(UnusableDigests))]
    public async Task Gyroflow_NoUsableDigest_NothingDownloadedOrInstalled(string? digest)
    {
        var sut = Sut(GyroflowServer(GyroflowZipBase64, digest), out var handler);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.NotVerifiable);
        handler.Requested.Should().NotContain(GyroflowArchiveUrl, "ohne Prüfwert wird das Archiv gar nicht erst geladen");
        Directory.Exists(ToolsRoot).Should().BeFalse();
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Gyroflow_AssetMissingInRelease_IsNotVerifiable()
    {
        // Das einzige Asset gehört zu einer anderen Plattform, ist aber vollständig und prüfbar (Hash passt zum ausgelieferten Archiv):
        // wer das erstbeste Asset nimmt, würde es installieren.
        var release = $$"""{"tag_name":"v1","assets":[{"name":"Gyroflow-linux64.tar.gz","browser_download_url":"https://example.test/x","digest":"sha256:{{GyroflowZipSha256}}"}]}""";
        var handler = new RoutingHandler(request => request.RequestUri!.ToString() == GitHubApiUrl ? Json(release) : Bytes(GyroflowZipBase64));
        var sut = new ToolInstallService(new HttpClient(handler), ToolsRoot, null, DownloadDir);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.NotVerifiable);
        handler.Requested.Should().ContainSingle().Which.Should().Be(GitHubApiUrl);
    }

    // --- FFprobe: veröffentlichte .sha256-Datei, aufgelöst auf die konkrete Version ---

    [Fact]
    public async Task FFprobe_CorrectArchive_IsExtracted_FromTheVersionTheChecksumBelongsTo()
    {
        var sut = Sut(FfmpegServer(FfprobeZipBase64, FfprobeZipSha256 + "\n"), out var handler);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Installed);
        result.ExecutablePath.Should().Be(Path.Combine(ToolsRoot, "ffmpeg", "ffmpeg-9.9.9-essentials_build", "bin", "ffprobe.exe"));
        File.ReadAllText(result.ExecutablePath!).Should().Be("FAKE-FFPROBE-BINARY");
        handler.Requested.Should().Equal(FfmpegChecksumRequestUrl, FfmpegArchiveUrl);
        AssertNoLeftovers();
    }

    [Fact]
    public async Task FFprobe_OneByteChanged_IsNotExtracted_AndArchiveIsDeleted()
    {
        var existing = WriteExistingTool("ffmpeg", "ffprobe.exe", "OLD");
        var sut = Sut(FfmpegServer(Tampered(FfprobeZipBase64, FfprobePayloadOffset), FfprobeZipSha256), out _);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Mismatch);
        File.ReadAllText(existing).Should().Be("OLD");
        Directory.GetDirectories(Path.Combine(ToolsRoot, "ffmpeg")).Should().BeEmpty("nichts wurde entpackt");
        AssertNoLeftovers();
    }

    [Theory]
    [InlineData("<html><body>404 Not Found</body></html>")]
    [InlineData("")]
    [InlineData("d095b5f0bb9172a928203303ab46d63cdfa90894ffc46907e4332dbb6e3293fa *ffmpeg.zip")]
    public async Task FFprobe_ChecksumFileUnusable_NothingDownloadedOrInstalled(string checksumFile)
    {
        var sut = Sut(FfmpegServer(FfprobeZipBase64, checksumFile), out var handler);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.NotVerifiable);
        handler.Requested.Should().NotContain(FfmpegArchiveUrl);
        Directory.Exists(ToolsRoot).Should().BeFalse();
    }

    [Fact]
    public async Task FFprobe_ChecksumResolvesToNonChecksumTarget_IsNotVerifiable()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.ToString() == FfmpegChecksumRequestUrl
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FfprobeZipSha256),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.gyan.dev/ffmpeg/builds/packages/evil.exe"),
            }
            : Bytes(FfprobeZipBase64));
        var sut = new ToolInstallService(new HttpClient(handler), ToolsRoot, null, DownloadDir);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.NotVerifiable);
        handler.Requested.Should().ContainSingle();
    }

    [Fact]
    public async Task FFprobe_ChecksumResolvesToForeignHost_IsNotVerifiable_AndNothingIsDownloaded()
    {
        var handler = new RoutingHandler(request => request.RequestUri!.ToString() == FfmpegChecksumRequestUrl
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FfprobeZipSha256),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.example/builds/ffmpeg.zip.sha256"),
            }
            : Bytes(FfprobeZipBase64));
        var sut = new ToolInstallService(new HttpClient(handler), ToolsRoot, null, DownloadDir);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.NotVerifiable);
        handler.Requested.Should().ContainSingle("das Archiv eines fremden Hosts wird nicht geladen");
    }

    [Fact]
    public async Task FFprobe_HugeChecksumResponse_IsNotReadBeyondTheLimit()
    {
        const int hugeBytes = 10_000_000;
        var served = new CountingStream(hugeBytes);
        var handler = new RoutingHandler(request => request.RequestUri!.ToString() == FfmpegChecksumRequestUrl
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(served),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, FfmpegChecksumResolvedUrl),
            }
            : Bytes(FfprobeZipBase64));
        var sut = new ToolInstallService(new HttpClient(handler), ToolsRoot, null, DownloadDir);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.NotVerifiable);
        served.BytesRead.Should().BeLessThan(1024, "eine .sha256-Datei ist 64 Zeichen; mehr als das Limit wird nicht aus dem Netz gelesen");
        handler.Requested.Should().ContainSingle();
    }

    // --- Archiv ohne die erwartete Datei ---

    [Fact]
    public async Task ValidHashButExecutableMissing_KeepsExistingInstallation_AndLeavesNoStaging()
    {
        var existing = WriteExistingTool("ffmpeg", "ffprobe.exe", "OLD");
        // Das Gyroflow-Archiv hat einen korrekten Hash, enthält aber keine ffprobe.exe.
        var sut = Sut(FfmpegServer(GyroflowZipBase64, GyroflowZipSha256), out _);

        var result = await sut.InstallAsync(ToolKeys.FFprobe, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.ExecutableNotFound);
        File.ReadAllText(existing).Should().Be("OLD");
        Directory.GetDirectories(ToolsRoot).Should().ContainSingle("kein Staging-Ordner bleibt liegen")
            .Which.Should().Be(Path.Combine(ToolsRoot, "ffmpeg"));
        AssertNoLeftovers();
    }

    [Fact]
    public async Task Reinstall_ReplacesExistingInstallation()
    {
        var existing = WriteExistingTool("gyroflow", "Gyroflow.exe", "OLD");
        var sut = Sut(GyroflowServer(GyroflowZipBase64, "sha256:" + GyroflowZipSha256), out _);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Installed);
        File.ReadAllText(existing).Should().Be("FAKE-GYROFLOW-BINARY");
    }

    [Fact]
    public async Task Gyroflow_ArmAssetListedBeforeTheRealOne_InstallsTheExactNamedAsset()
    {
        // Das echte Release listet Gyroflow-windows-arm64.zip vor Gyroflow-windows64.zip. Der ARM64-Eintrag ist
        // vollständig und prüfbar: wer "irgendein windows-zip" nimmt, würde ihn installieren.
        const string armUrl = "https://example.test/gyroflow/Gyroflow-windows-arm64.zip";
        var release = $$"""
            {"tag_name":"v9.9.9","assets":[
              {"name":"Gyroflow-windows-arm64.zip","browser_download_url":"{{armUrl}}","digest":"sha256:{{FfprobeZipSha256}}"},
              {"name":"Gyroflow-windows64.zip","browser_download_url":"{{GyroflowArchiveUrl}}","digest":"sha256:{{GyroflowZipSha256}}"}]}
            """;
        var handler = new RoutingHandler(request => request.RequestUri!.ToString() switch
        {
            GitHubApiUrl => Json(release),
            armUrl => Bytes(FfprobeZipBase64),
            _ => Bytes(GyroflowZipBase64),
        });
        var sut = new ToolInstallService(new HttpClient(handler), ToolsRoot, null, DownloadDir);

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Installed);
        File.ReadAllText(result.ExecutablePath!).Should().Be("FAKE-GYROFLOW-BINARY");
        handler.Requested.Should().Equal(GitHubApiUrl, GyroflowArchiveUrl);
    }

    // --- Archiv bleibt während des Entpackens gesperrt ---

    [Fact]
    public async Task Archive_CannotBeChangedReplacedOrDeleted_WhileExtracting()
    {
        var attempts = new List<Exception?>();
        var sut = Sut(GyroflowServer(GyroflowZipBase64, "sha256:" + GyroflowZipSha256), out _);
        sut.TestHooks = new ToolInstallService.InstallTestHooks
        {
            BeforeExtract = archive =>
            {
                attempts.Add(Record.Exception(() => File.WriteAllText(archive, "evil")));
                attempts.Add(Record.Exception(() => File.Delete(archive)));
                attempts.Add(Record.Exception(() => File.Move(WriteOther(), archive, overwrite: true)));
            },
        };

        var result = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        result.Outcome.Should().Be(ToolInstallOutcome.Installed);
        attempts.Should().HaveCount(3);
        attempts.Should().OnlyContain(e => e is IOException || e is UnauthorizedAccessException,
            "zwischen Prüfung und Entpacken darf niemand das Archiv ändern, ersetzen oder löschen: {0}", attempts);
        File.ReadAllText(result.ExecutablePath!).Should().Be("FAKE-GYROFLOW-BINARY");
    }

    // --- Ersetzen ohne Zwischenzustand ---

    [Fact]
    public async Task Reinstall_WhileToolFilesAreLocked_ReportsInUse_AndKeepsOldInstallationComplete()
    {
        var exe = WriteExistingTool("gyroflow", "Gyroflow.exe", "OLD-EXE");
        var dll = WriteExistingTool("gyroflow", "lib.dll", "OLD-DLL");
        var sut = Sut(GyroflowServer(GyroflowZipBase64, "sha256:" + GyroflowZipSha256), out _);

        ToolInstallResult locked;
        using (new FileStream(exe, FileMode.Open, FileAccess.Read, FileShare.Read)) // läuft: EXE ohne FileShare.Delete gesperrt
        {
            locked = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

            locked.Outcome.Should().Be(ToolInstallOutcome.InUse);
            File.ReadAllText(exe).Should().Be("OLD-EXE");
            File.ReadAllText(dll).Should().Be("OLD-DLL", "die alte Installation bleibt vollständig, nicht halb gelöscht");
            Directory.GetDirectories(ToolsRoot).Should().ContainSingle().Which.Should().Be(Path.Combine(ToolsRoot, "gyroflow"));
            AssertNoLeftovers();
        }

        var retry = await sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        retry.Outcome.Should().Be(ToolInstallOutcome.Installed, "nach dem Schließen des Werkzeugs gelingt das Ersetzen");
        File.ReadAllText(exe).Should().Be("FAKE-GYROFLOW-BINARY");
        File.Exists(dll).Should().BeFalse("die neue Installation ersetzt die alte vollständig");
        Directory.GetDirectories(ToolsRoot).Should().ContainSingle("kein .old- oder Staging-Ordner bleibt liegen");
    }

    [Fact]
    public async Task ActivationFails_RestoresOldInstallation_AndRethrows()
    {
        var exe = WriteExistingTool("gyroflow", "Gyroflow.exe", "OLD-EXE");
        var dll = WriteExistingTool("gyroflow", "lib.dll", "OLD-DLL");
        var sut = Sut(GyroflowServer(GyroflowZipBase64, "sha256:" + GyroflowZipSha256), out _);
        sut.TestHooks = new ToolInstallService.InstallTestHooks { BeforeActivate = () => throw new IOException("simulated") };

        var act = () => sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        await act.Should().ThrowAsync<IOException>().WithMessage("simulated");
        File.ReadAllText(exe).Should().Be("OLD-EXE");
        File.ReadAllText(dll).Should().Be("OLD-DLL");
        Directory.GetDirectories(ToolsRoot).Should().ContainSingle().Which.Should().Be(Path.Combine(ToolsRoot, "gyroflow"));
        AssertNoLeftovers();
    }

    // --- Abbruch/Fehler: keine Teil-Datei ---

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(OperationCanceledException))]
    public async Task DownloadFailsMidStream_DeletesPartialArchive_AndRethrows(Type exceptionType)
    {
        var failure = (Exception)Activator.CreateInstance(exceptionType)!;
        var archiveHandler = new FailingMidStreamHandler(failure);
        var sut = new ToolInstallService(new HttpClient(new SplitHandler(GyroflowServer(GyroflowZipBase64, "sha256:" + GyroflowZipSha256), archiveHandler)),
            ToolsRoot, null, DownloadDir);

        var act = () => sut.InstallAsync(ToolKeys.Gyroflow, null, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().Where(e => e.GetType() == exceptionType);
        AssertNoLeftovers();
        Directory.Exists(Path.Combine(ToolsRoot, "gyroflow")).Should().BeFalse();
    }

    // --- Verwaltung ---

    [Fact]
    public void FindInstalledExecutable_ReturnsPathOnlyWhenInstalled()
    {
        var sut = Sut(GyroflowServer(GyroflowZipBase64, null), out _);
        sut.FindInstalledExecutable(ToolKeys.Gyroflow).Should().BeNull();

        var existing = WriteExistingTool("gyroflow", "Gyroflow.exe", "x");

        sut.FindInstalledExecutable(ToolKeys.Gyroflow).Should().Be(existing);
    }

    [Fact]
    public async Task UnknownToolKey_Throws()
    {
        var sut = Sut(GyroflowServer(GyroflowZipBase64, null), out _);

        sut.Invoking(s => s.FindInstalledExecutable(ToolKeys.ExifTool)).Should().Throw<ArgumentException>();
        await sut.Invoking(s => s.InstallAsync(ToolKeys.ExifTool, null, CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
    }

    // --- Hilfen ---

    private ToolInstallService Sut(RoutingHandler handler, out RoutingHandler handlerOut, ILogger<ToolInstallService>? logger = null)
    {
        handlerOut = handler;
        return new ToolInstallService(new HttpClient(handler), ToolsRoot, logger, DownloadDir);
    }

    /// <summary>GitHub-API liefert das Release mit dem Asset; das Archiv kommt von seiner <c>browser_download_url</c>.</summary>
    private static RoutingHandler GyroflowServer(string archiveBase64, string? digest)
    {
        var digestJson = digest is null ? string.Empty : $",\"digest\":\"{digest}\"";
        var release = $$"""{"tag_name":"v9.9.9","assets":[{"name":"Gyroflow-windows64.zip","browser_download_url":"{{GyroflowArchiveUrl}}"{{digestJson}}}]}""";
        return new RoutingHandler(request => request.RequestUri!.ToString() == GitHubApiUrl ? Json(release) : Bytes(archiveBase64));
    }

    /// <summary>gyan.dev: die Prüfsummen-Adresse leitet auf die konkrete Version um (RequestMessage = aufgelöste Adresse).</summary>
    private static RoutingHandler FfmpegServer(string archiveBase64, string checksumFile)
        => new(request => request.RequestUri!.ToString() == FfmpegChecksumRequestUrl
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(checksumFile),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, FfmpegChecksumResolvedUrl),
            }
            : Bytes(archiveBase64));

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(string base64)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Convert.FromBase64String(base64)) };

    /// <summary>Dasselbe Archiv mit genau einem geänderten Byte im Nutzdatenbereich.</summary>
    private static string Tampered(string base64, int offset)
    {
        var bytes = Convert.FromBase64String(base64);
        bytes[offset] ^= 0x01;
        return Convert.ToBase64String(bytes);
    }

    private string WriteExistingTool(string dirName, string exeName, string content)
    {
        var dir = Path.Combine(ToolsRoot, dirName);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, exeName);
        File.WriteAllText(path, content);
        return path;
    }

    private void AssertNoLeftovers()
    {
        Directory.GetFileSystemEntries(DownloadDir).Should().BeEmpty("das Archiv liegt nach jedem Ausgang nicht mehr im Download-Ordner");
        if (Directory.Exists(ToolsRoot))
            Directory.GetDirectories(ToolsRoot, "*.staging-*").Should().BeEmpty();
    }

    private string WriteOther()
    {
        var other = Path.Combine(_dir, "other.zip");
        File.WriteAllText(other, "other");
        return other;
    }

    /// <summary>Liefert <c>length</c> Mal 'a' und zählt, wie viele Bytes tatsächlich gelesen wurden.</summary>
    private sealed class CountingStream(int length) : Stream
    {
        public int BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(count, length - BytesRead);
            Array.Fill(buffer, (byte)'a', offset, n);
            BytesRead += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>API-Anfragen an den ersten Handler, Archiv-Anfragen an den zweiten.</summary>
    private sealed class SplitHandler(HttpMessageHandler api, HttpMessageHandler archive) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _api = new(api);
        private readonly HttpMessageInvoker _archive = new(archive);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => (request.RequestUri!.ToString() == GitHubApiUrl ? _api : _archive).SendAsync(request, ct);
    }
}
