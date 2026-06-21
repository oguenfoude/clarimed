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
            
            // Open the browser to the dashboard regardless if it's already running or not
            try { Process.Start(new ProcessStartInfo { FileName = "http://localhost:5000", UseShellExecute = true }); } catch { }

            if (!createdNew)
            {
                // App is already running in tray, so just opening the browser is enough.
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new NotifierApplicationContext());
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"D:\ClariMed\crash.log", ex.ToString());
        }
        finally
        {
            _mutex?.ReleaseMutex();
        }
    }    
}