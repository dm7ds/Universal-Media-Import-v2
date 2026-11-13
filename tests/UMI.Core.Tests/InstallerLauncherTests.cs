// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using FluentAssertions;
using UMI.Core.Services;
using Xunit;

namespace UMI.Core.Tests;

/// <summary>
/// I-021: Die eine Start-Sequenz (erneut prüfen -> bei Valid starten -> erst danach Handle freigeben).
/// Der Start ist ein Fake-Delegat; getestet wird, WAS die Sequenz um ihn herum tut.
/// Erwartete Hashes: NIST-Testvektor SHA256("abc"), nie über den Produktionscode gebildet.
/// </summary>
public sealed class InstallerLauncherTests : IDisposable
{
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "umi_launcher_" + Guid.NewGuid().ToString("N"));

    public InstallerLauncherTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string WriteInstaller(string content)
    {
        var path = Path.Combine(_dir, "UMI_Setup_test.exe");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(content));
        return path;
    }

    private static SetupAsset Asset(string? digest) => new("https://example.test/setup.exe", digest);

    private static Exception? TryAlter(Action alter) => Record.Exception(alter);

    [Fact]
    public async Task Valid_StartsWhileFileIsLocked_AgainstWriteDeleteAndReplace()
    {
        var path = WriteInstaller("abc");
        var other = Path.Combine(_dir, "other.exe");
        File.WriteAllText(other, "other");
        var startCalls = new List<string>();
        var attacks = new List<(string Name, Exception? Thrown)>();

        var verdict = await InstallerLauncher.RunVerifiedAsync(
            Asset("sha256:" + AbcSha256), path,
            start: p =>
            {
                // Genau jetzt, mitten im Start, versucht ein fremder Prozess die Datei zu ändern.
                startCalls.Add(p);
                attacks.Add(("write", TryAlter(() => File.WriteAllText(p, "evil"))));
                attacks.Add(("delete", TryAlter(() => File.Delete(p))));
                attacks.Add(("replace", TryAlter(() => File.Move(other, p, overwrite: true))));
            },
            logger: null, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.Valid);
        startCalls.Should().Equal(path);
        attacks.Should().HaveCount(3);
        foreach (var (name, thrown) in attacks)
        {
            (thrown is IOException or UnauthorizedAccessException).Should().BeTrue(
                "{0} muss während des Starts scheitern, war aber: {1}", name, thrown);
        }

        File.ReadAllText(path).Should().Be("abc", "der Installer blieb unverändert");
        var writeAfter = () => File.WriteAllText(path, "ok");
        writeAfter.Should().NotThrow("nach dem Start ist der Handle freigegeben");
    }

    [Fact]
    public async Task Mismatch_DoesNotCallStart_AndDeletesFile()
    {
        var path = WriteInstaller("abd"); // im Staging-Fenster ausgetauscht
        var startCalls = 0;

        var verdict = await InstallerLauncher.RunVerifiedAsync(
            Asset("sha256:" + AbcSha256), path, _ => startCalls++, null, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.Mismatch);
        startCalls.Should().Be(0);
        File.Exists(path).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1:da39a3ee5e6b4b0d3255bfef95601890afd80709")]
    [InlineData("sha256:abc")]
    public async Task NotVerifiable_DoesNotCallStart(string? digest)
    {
        var path = WriteInstaller("abc");
        var startCalls = 0;

        var verdict = await InstallerLauncher.RunVerifiedAsync(
            Asset(digest), path, _ => startCalls++, null, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.NotVerifiable);
        startCalls.Should().Be(0);
    }

    [Fact]
    public async Task StartThrows_IsPropagated_NoSecondStart_AndHandleIsReleased()
    {
        var path = WriteInstaller("abc");
        var startCalls = 0;
        var failure = new InvalidOperationException("UAC abgelehnt");

        var act = () => InstallerLauncher.RunVerifiedAsync(
            Asset("sha256:" + AbcSha256), path, _ => { startCalls++; throw failure; }, null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        startCalls.Should().Be(1, "es gibt keinen zweiten Startversuch");
        var writeAfter = () => File.WriteAllText(path, "ok");
        writeAfter.Should().NotThrow("auch bei werfendem Start wird der Handle freigegeben");
    }

    // --- Verdrahtung der öffentlichen Methode (Digest kommt aus dem SetupAsset) ---

    /// <summary>
    /// Nur der Mismatch-Fall läuft über die öffentliche Methode: sie startet echt per Shell, und bei
    /// Mismatch wird garantiert nichts gestartet. Der Valid-Fall ist über <c>RunVerifiedAsync</c> mit
    /// Fake-Delegat abgedeckt (ein echter Start gehört nicht in einen Test).
    /// </summary>
    [Fact]
    public async Task PublicLaunch_SwappedFile_IsMismatch_AndFileDeleted()
    {
        var path = WriteInstaller("abd");
        var sut = new GitHubReleaseChecker(new HttpClient());

        var verdict = await sut.LaunchVerifiedInstallerAsync(Asset("sha256:" + AbcSha256), path, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.Mismatch, "der Digest des SetupAsset muss bis zur Prüfung durchgereicht werden");
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task PublicLaunch_NoDigest_IsNotVerifiable()
    {
        var path = WriteInstaller("abc");
        var sut = new GitHubReleaseChecker(new HttpClient());

        var verdict = await sut.LaunchVerifiedInstallerAsync(Asset(null), path, CancellationToken.None);

        verdict.Should().Be(InstallerVerdict.NotVerifiable);
        File.Exists(path).Should().BeTrue();
    }
}
