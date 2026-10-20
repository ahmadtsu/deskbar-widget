using System.Runtime.InteropServices;
using DeskbarWidget.App.Taskbar;

namespace DeskbarWidget.App;

public static class Program
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    [STAThread]
    public static void Main(string[] args)
    {
        bool switched = NativeTaskbar.EnsureInteractiveDesktop();
        try
        {
            SetProcessDpiAwarenessContext((nint)DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
        }

        if (!switched)
        {
            Exception? threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    NativeTaskbar.EnsureInteractiveDesktop();
                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (threadEx != null)
            {
                throw threadEx;
            }
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
