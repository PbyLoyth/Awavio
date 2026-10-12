using System.Diagnostics;
using Awavio.Core;
using Awavio.Windows;

namespace Awavio.Windows.Smoke;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--ui-only"))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { UiChecks.Run(); return 0; }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        // Explicit opt-in: test a real power plan; --isolated-plan keeps it inactive.
        if (!args.Contains("--real-power"))
        {
            Console.Error.WriteLine("Pass --real-power to test and temporarily change the current power plan.");
            return 2;
        }
        var paths = AppPaths.Default();
        using var owner = new Mutex(false, SessionOwnership.MutexName);
        try
        {
            if (!owner.WaitOne(0)) throw new InvalidOperationException("Quit Awavio before the smoke test.");
        }
        catch (AbandonedMutexException) { }
        var nativePower = new WindowsPowerPlan();
        Guid? isolatedScheme = null;
        IPowerPlan power = nativePower;
        if (args.Contains("--isolated-plan"))
        {
            var originalScheme = nativePower.ActiveScheme();
            var error = NativeMethods.PowerDuplicateScheme(IntPtr.Zero, originalScheme, out var pointer);
            if (error != 0) throw new System.ComponentModel.Win32Exception((int)error);
            try { isolatedScheme = System.Runtime.InteropServices.Marshal.PtrToStructure<Guid>(pointer); }
            finally { NativeMethods.LocalFree(pointer); }
            // The active plan may already say Do nothing because another app
            // has a session. Give only our inactive clone a nonzero baseline.
            nativePower.WriteLid(isolatedScheme.Value, new LidActions(1, 1));
            Require(nativePower.ReadLid(isolatedScheme.Value) == new LidActions(1, 1), "isolated baseline is Sleep for AC/DC");
            power = new IsolatedPowerPlan(nativePower, isolatedScheme.Value);
        }
        var journal = new RecoveryJournal(paths.Journal);
        var controller = new SessionController(power, new ExecutionStateKeepAwake(), journal, new Log(paths.LogFile));
        try
        {
            Require(controller.Recover().Ok, "initial recovery");
            var scheme = power.ActiveScheme();
            var before = power.ReadLid(scheme);
            Directory.CreateDirectory(paths.Home);
            File.WriteAllText(Path.Combine(paths.Home, "before.json"),
                System.Text.Json.JsonSerializer.Serialize(new { Scheme = scheme, Lid = before }));
            Console.WriteLine($"BEFORE {scheme} AC={before.Ac} DC={before.Dc}");
            controller.Start(TimeSpan.FromMinutes(2), new AppConfig(), DateTimeOffset.Now);
            Require(power.ReadLid(scheme) == new LidActions(0, 0), "native lid setting is Do nothing");
            Require(journal.Exists, "recovery journal exists");
            var previous = NativeMethods.SetThreadExecutionState(
                NativeMethods.ExecutionState.Continuous | NativeMethods.ExecutionState.SystemRequired);
            Require((previous & NativeMethods.ExecutionState.SystemRequired) != 0, "thread has real keep-awake request");
            Console.WriteLine("ACTIVE AC=0 DC=0, native keep-awake request confirmed");
            if (args.Contains("--hold"))
            {
                File.WriteAllText(Path.Combine(paths.Home, "ready"), "ready");
                Thread.Sleep(Timeout.Infinite); // Parent kills us to test the published --recover path.
            }
            var elapsed = Stopwatch.StartNew();
            while (controller.IsActive)
            {
                Thread.Sleep(250);
                controller.Tick(DateTimeOffset.Now, PowerSnapshot.PluggedIn, new AppConfig());
            }
            Require(elapsed.Elapsed >= TimeSpan.FromSeconds(119), "two-minute real timer");
            Require(power.ReadLid(scheme) == before, "native lid settings restored after timer");
            Require(!journal.Exists, "journal removed after restore");
            previous = NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.Continuous);
            Require((previous & NativeMethods.ExecutionState.SystemRequired) == 0, "awake request released");
            Console.WriteLine($"RESTORED AC={before.Ac} DC={before.Dc}, awake request released");

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new DurationForm("Smoke test", "Start", TimeSpan.FromMinutes(2)))
            {
                _ = form.Handle;
                Require(form.Controls.Count > 0, "duration form creates real Windows controls");
            }
            using (var tray = new TrayApplicationContext(paths))
            {
                var buildMenu = typeof(TrayApplicationContext).GetMethod("BuildMenu",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                for (var i = 0; i < 100; i++) buildMenu.Invoke(tray, null);
                Application.DoEvents();
                Require(!journal.Exists, "idle tray does not create a session");
            }
            Console.WriteLine("PASS: native timer, lid restore, awake release, WinForms controls and tray menus");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            if (controller.IsActive) controller.End(EndReason.Quit);
            if (isolatedScheme is { } isolated && nativePower.ActiveScheme() != isolated)
                NativeMethods.PowerDeleteScheme(IntPtr.Zero, isolated);
            owner.ReleaseMutex();
        }
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("FAILED: " + message);
    }

    sealed class IsolatedPowerPlan(WindowsPowerPlan native, Guid scheme) : IPowerPlan
    {
        public Guid ActiveScheme() => scheme;
        public LidActions ReadLid(Guid requested) => native.ReadLid(requested);
        public void WriteLid(Guid requested, LidActions actions) => native.WriteLid(requested, actions);
    }
}
