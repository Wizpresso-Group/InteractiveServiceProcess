using System.ServiceProcess;
using Cocona;
using Vanara.PInvoke;
using Wizpresso.InteractiveServiceProcess;


ServiceBase.Run(new ServiceBase[]
{
    new InteractiveProcess()
});

public class InteractiveProcess : ServiceBase
{
    private Kernel32.SafePROCESS_INFORMATION? _processInfo;
    private readonly CancellationTokenSource _tokenSource = new ();
    private static readonly object _processLock = new();
    protected override void OnStart(string[] _)
    {
        void Start([Argument] string app, [Argument] string cmdline, [Option] bool visible = false)
        {
            for (; !_tokenSource.IsCancellationRequested;)
            {
                lock (_processLock)
                {
                    ProcessExtensions.StartProcessAsCurrentUser(app, out _processInfo, cmdLine: cmdline,
                        visible: visible);
                    Kernel32.WaitForSingleObject(_processInfo?.hProcess, uint.MaxValue);
                }
            }
        }

        CoconaLiteApp.RunAsync(Start, Environment.GetCommandLineArgs()[1..], cancellationToken: _tokenSource.Token);
    }

    protected override void OnStop()
    {
        Task.WaitAll(
            Task.Run(_tokenSource.Cancel),
            Task.Run(() =>
            {
                lock (_processLock)
                {
                    using var process = _processInfo?.hProcess;
                    using var thread = _processInfo?.hThread;
                    if (process != null) Kernel32.TerminateProcess(process, 0);
                    if (thread != null) Kernel32.TerminateThread(thread, 0);
                }
            })
        );
    }
}