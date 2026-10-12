using Awavio.Core;

namespace Awavio.Windows;

static class Program
{

    [STAThread]
    static int Main(string[] args)
    {
        var paths = AppPaths.Default();
        using var owner = new Mutex(false, SessionOwnership.MutexName);
        var recoverOnly = args.Contains("--recover", StringComparer.OrdinalIgnoreCase);

        // The tray app waits a little, so a backstop run that fires at the same
        // moment as sign-in doesn't make the app give up.
        if (!TryAcquire(owner, recoverOnly ? TimeSpan.Zero : TimeSpan.FromSeconds(10)))
            return 0; // Another Awavio already owns the session; nothing to do.

        try
        {
            if (recoverOnly) return RecoverHeadless(paths);

            ApplicationConfiguration.Initialize();
            // A crash in a click handler must be visible and logged, not silent.
            var log = new Log(paths.LogFile);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                log.Write($"unhandled: {e.Exception}");
                MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}\n\nDetails are in {paths.LogFile}",
                    "Awavio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Write($"fatal: {e.ExceptionObject}");
            using var context = new TrayApplicationContext(paths);
            Application.Run(context);
            return 0;
        }
        finally
        {
            owner.ReleaseMutex();
        }
    }

    /// <summary>Backstop for the scheduled task: undo a session whose app is gone.
    /// Without the app, nothing holds the PC awake, so the lid setting goes back now.</summary>
    static int RecoverHeadless(AppPaths paths)
    {
        var log = new Log(paths.LogFile);
        var journal = new RecoveryJournal(paths.Journal);
        if (!journal.Exists) return 0;
        var controller = new SessionController(new WindowsPowerPlan(), new ExecutionStateKeepAwake(), journal, log);
        log.Write("backstop: found a journal with no running app");
        return controller.Recover().Ok ? 0 : 1;
    }

    static bool TryAcquire(Mutex mutex, TimeSpan timeout)
    {
        try
        {
            return mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner crashed. We own it now, and its journal is ours to recover.
            return true;
        }
    }
}
