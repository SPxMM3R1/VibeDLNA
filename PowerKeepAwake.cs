using System.Runtime.InteropServices;

namespace FolderDlnaServer;

internal static class PowerKeepAwake
{
    private const ExecutionState Continuous = (ExecutionState)0x80000000;
    private const ExecutionState SystemRequired = (ExecutionState)0x00000001;
    private const ExecutionState AwayModeRequired = (ExecutionState)0x00000040;

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            SetThreadExecutionState(Continuous | SystemRequired | AwayModeRequired);
        }
        else
        {
            SetThreadExecutionState(Continuous);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [Flags]
    private enum ExecutionState : uint
    {
    }
}
