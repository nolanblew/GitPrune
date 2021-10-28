using System.Runtime.InteropServices;

static class RootChecker
{
    [DllImport("libc")]
    public static extern uint getuid();

    public static bool IsRoot()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return getuid() == 0;
        }
        else
        {
            return false;
        }
    }
}