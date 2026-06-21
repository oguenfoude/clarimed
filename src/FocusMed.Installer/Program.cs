namespace FocusMed.Installer;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string exeName = System.IO.Path.GetFileNameWithoutExtension(Application.ExecutablePath).ToLower();

        if (args.Contains("--uninstall") || exeName.Contains("uninstall"))
        {
            Application.Run(new Form1(true));
            return;
        }
        
        if (args.Contains("--restart") || exeName.Contains("restart"))
        {
            Form1.RestartSystem();
            return;
        }

        Application.Run(new Form1(false));
    }
}