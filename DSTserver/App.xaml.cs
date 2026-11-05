using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace DSTserver;

public partial class App : Application
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    protected override void OnStartup(StartupEventArgs e)
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        base.OnStartup(e);
    }
}
