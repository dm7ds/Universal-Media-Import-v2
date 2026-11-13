// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later

using FluentAssertions;
using UMI.Core.Services;

namespace UMI.Core.Tests;

/// <summary>
/// I-028: Ablauf "laeuft ein Import? -> Rueckfrage -> abbrechen -> auslaufen lassen" beim Schliessen.
/// Die Fakes bilden nur die Ports nach; der Ablauf selbst ist die echte <see cref="CloseGuard"/>.
/// </summary>
public class CloseGuardTests
{
    private sealed class FakeImports : IImportActivity
    {
        private readonly TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasActiveImports { get; set; }
        public int CancelCalls { get; private set; }

        /// <summary>Wenn true, laeuft der Import nach CancelAllImports aus (wie die echte Cancellation).</summary>
        public bool StopsOnCancel { get; set; } = true;

        public void CancelAllImports()
        {
            CancelCalls++;
            if (!StopsOnCancel) return;
            BecomeIdle();
        }

        /// <summary>Der Import ist ausgelaufen (Signal fuer <see cref="WaitUntilIdleAsync"/>).</summary>
        public void BecomeIdle()
        {
            HasActiveImports = false;
            _idle.TrySetResult();
        }

        public async Task WaitUntilIdleAsync(CancellationToken ct)
        {
            if (!HasActiveImports) return;
            await _idle.Task.WaitAsync(ct);
        }
    }

    private sealed class FakeConfirmation(bool answer) : ICloseConfirmation
    {
        public int Asked { get; private set; }

        public Task<bool> ConfirmQuitWhileImportingAsync()
        {
            Asked++;
            return Task.FromResult(answer);
        }
    }

    [Fact]
    public async Task NoImportRunning_ClosesWithoutQuestion()
    {
        var imports = new FakeImports { HasActiveImports = false };
        var ask = new FakeConfirmation(answer: false);

        var decision = await new CloseGuard(imports, ask).RequestCloseAsync();

        decision.Should().Be(CloseDecision.NothingRunning);
        ask.Asked.Should().Be(0);
        imports.CancelCalls.Should().Be(0);
    }

    [Fact]
    public async Task ImportRunning_UserDeclines_KeepsImportRunning()
    {
        var imports = new FakeImports { HasActiveImports = true };
        var ask = new FakeConfirmation(answer: false);

        var decision = await new CloseGuard(imports, ask).RequestCloseAsync();

        decision.Should().Be(CloseDecision.Declined);
        ask.Asked.Should().Be(1);
        imports.CancelCalls.Should().Be(0);
        imports.HasActiveImports.Should().BeTrue();
    }

    [Fact]
    public async Task ImportRunning_UserConfirms_CancelsAndWaitsUntilIdle()
    {
        var imports = new FakeImports { HasActiveImports = true };
        var ask = new FakeConfirmation(answer: true);

        var decision = await new CloseGuard(imports, ask).RequestCloseAsync();

        decision.Should().Be(CloseDecision.ImportsStopped);
        ask.Asked.Should().Be(1);
        imports.CancelCalls.Should().Be(1);
        imports.HasActiveImports.Should().BeFalse();
    }

    [Fact]
    public async Task ImportRunning_UserConfirms_AnnouncesStoppingBeforeCancel()
    {
        var imports = new FakeImports { HasActiveImports = true };
        var guard = new CloseGuard(imports, new FakeConfirmation(answer: true));
        var cancelCallsWhenAnnounced = -1;
        guard.StoppingImports += (_, _) => cancelCallsWhenAnnounced = imports.CancelCalls;

        await guard.RequestCloseAsync();

        cancelCallsWhenAnnounced.Should().Be(0, "the wait feedback is announced right before the cancel");
    }

    [Fact]
    public async Task ImportRunning_UserDeclines_DoesNotAnnounceStopping()
    {
        var imports = new FakeImports { HasActiveImports = true };
        var guard = new CloseGuard(imports, new FakeConfirmation(answer: false));
        var announced = 0;
        guard.StoppingImports += (_, _) => announced++;

        await guard.RequestCloseAsync();

        announced.Should().Be(0);
    }

    [Fact]
    public async Task ImportRunning_UserConfirms_DoesNotCloseBeforeImportHasStopped()
    {
        var imports = new FakeImports { HasActiveImports = true, StopsOnCancel = false };
        var guard = new CloseGuard(imports, new FakeConfirmation(answer: true));

        // Bestaetigung und Cancel laufen synchron durch; der Wächter haengt dann am Idle-Signal.
        var decisionTask = guard.RequestCloseAsync();

        imports.CancelCalls.Should().Be(1);
        decisionTask.IsCompleted.Should().BeFalse("closing must wait until the import has actually stopped");

        imports.BecomeIdle();

        (await decisionTask).Should().Be(CloseDecision.ImportsStopped);
    }

    [Fact]
    public async Task ImportIgnoresCancel_ReportsTimeoutInsteadOfHanging()
    {
        var imports = new FakeImports { HasActiveImports = true, StopsOnCancel = false };
        var ask = new FakeConfirmation(answer: true);
        var guard = new CloseGuard(imports, ask, stopTimeout: TimeSpan.FromMilliseconds(100));

        var decision = await guard.RequestCloseAsync();

        decision.Should().Be(CloseDecision.StopTimedOut);
        imports.CancelCalls.Should().Be(1);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagated_NotMistakenForTimeout()
    {
        var imports = new FakeImports { HasActiveImports = true, StopsOnCancel = false };
        var ask = new FakeConfirmation(answer: true);
        using var cts = new CancellationTokenSource();

        var decisionTask = new CloseGuard(imports, ask).RequestCloseAsync(cts.Token);
        cts.Cancel();

        await decisionTask.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void SessionEnd_CancelsRunningImportsWithoutQuestion()
    {
        var imports = new FakeImports { HasActiveImports = true };
        var ask = new FakeConfirmation(answer: false);

        new CloseGuard(imports, ask).StopImportsForSessionEnd();

        imports.CancelCalls.Should().Be(1);
        ask.Asked.Should().Be(0);
    }

    [Fact]
    public void SessionEnd_WithoutImport_DoesNothing()
    {
        var imports = new FakeImports { HasActiveImports = false };

        new CloseGuard(imports, new FakeConfirmation(true)).StopImportsForSessionEnd();

        imports.CancelCalls.Should().Be(0);
    }

    [Fact]
    public void ImportsRunning_ReflectsTheImportActivityPort()
    {
        var imports = new FakeImports { HasActiveImports = true };
        var guard = new CloseGuard(imports, new FakeConfirmation(true));

        guard.ImportsRunning.Should().BeTrue();
        imports.HasActiveImports = false;
        guard.ImportsRunning.Should().BeFalse();
    }
}
