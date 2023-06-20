using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.WTSApi32;
using static Vanara.PInvoke.WTSApi32.WTS_CONNECTSTATE_CLASS;

namespace Wizpresso.InteractiveServiceProcess;

// Credits: https://github.com/murrayju/CreateProcessAsUser/tree/master
public static class ProcessExtensions
{
    private const uint INVALID_SESSION_ID = 0xFFFFFFFF;

    // Gets the user token from the currently active session
    private static bool GetSessionUserToken(out AdvApi32.SafeHTOKEN? phUserToken)
    {
        var activeSessionId = INVALID_SESSION_ID;

        // Get a handle to the user access token for the current active session.
        if (WTSEnumerateSessionsEx(WTS_CURRENT_SERVER_HANDLE, out var sessions))
        {
            // TODO: use a priority queue to rank possible sessions (including affinity to certain user name)
            activeSessionId = sessions
                .Where(si => si.State is WTSActive or WTSConnected or WTSDisconnected)
                .LastOrDefault(si => si.State is WTSActive)
                .SessionId;
        }

        // If enumerating did not work, fall back to the old method
        if (activeSessionId == INVALID_SESSION_ID)
        {
            activeSessionId = Kernel32.WTSGetActiveConsoleSessionId();
        }

        if (!WTSQueryUserToken(activeSessionId, out var hImpersonationToken))
        {
            phUserToken = null;
            return false;
        }

        // "Service providers must use caution that they do not leak user tokens when calling this function".
        // And we are directly duplicating the user token. How ironic isn't it?
        var ret = AdvApi32.DuplicateTokenEx(hImpersonationToken, 0, null,
            AdvApi32.SECURITY_IMPERSONATION_LEVEL.SecurityDelegation, AdvApi32.TOKEN_TYPE.TokenPrimary,
            out phUserToken);

        // "Service providers must close token handles after they have finished using them", although it doesn't matter
        Kernel32.CloseHandle(hImpersonationToken);
        return ret;
    }

    public static bool StartProcessAsCurrentUser(string appPath, out Kernel32.SafePROCESS_INFORMATION procInfo, string? cmdLine = null, string? workDir = null, bool visible = true)
    {
        if (!GetSessionUserToken(out var hUserToken))
        {
            throw new("StartProcessAsCurrentUser: GetSessionUserToken failed.");
        }

        using (hUserToken)
        {
            if (!UserEnv.CreateEnvironmentBlock(out string[] pEnv, hUserToken, false))
            {
                throw new("StartProcessAsCurrentUser: CreateEnvironmentBlock failed.");
            }

            if (!AdvApi32.CreateProcessAsUser(hUserToken,
                    appPath, // Application Name
                    new($@"""{appPath}"" {cmdLine}"), // Command Line
                    null,
                    null,
                    false,
                    Kernel32.CREATE_PROCESS.CREATE_UNICODE_ENVIRONMENT | (visible
                        ? Kernel32.CREATE_PROCESS.CREATE_NEW_CONSOLE
                        : Kernel32.CREATE_PROCESS.CREATE_NO_WINDOW),
                    pEnv,
                    workDir, // Working directory
                    new Kernel32.STARTUPINFO
                    {
                        wShowWindow = (ushort)(visible ? ShowWindowCommand.SW_SHOW : ShowWindowCommand.SW_HIDE),
                        lpDesktop = @"winsta0\default"
                    },
                    out procInfo))
            {
                throw new("StartProcessAsCurrentUser: CreateProcessAsUser failed.  Error Code -" +
                          Marshal.GetLastWin32Error());
            }

            return true;
        }

    }

}