using System.Diagnostics;

namespace FocusMed.Notifier;

static class Program
{
    static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        try
        {
            _mutex = new Mutex(true, "FocusMedApp", out bool createdNew);

            if (!createdNew)
            {
                // App is already running in tray, so just opening the browser is enough.
                try { Process.Start(new ProcessStartInfo { FileName = "http://localhost:5000", UseShellExecute = true }); } catch { }
                return;
            }

            ApplicationConfiguration.Initialize();
            
            // Show Splash Screen first
            using (var splash = new SplashForm())
            {
                Application.Run(splash);
                if (!splash.LaunchSuccess)
                {
                    return; // Exit if failed to launch
                }
            }

            Application.Run(new NotifierApplicationContext());
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("crash.log", ex.ToString());
        }
        finally
        {
            _mutex?.ReleaseMutex();
        }
    }    
}