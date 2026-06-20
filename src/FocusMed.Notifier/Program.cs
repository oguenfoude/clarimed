namespace FocusMed.Notifier;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new NotifierApplicationContext());
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(@"D:\ClariMed\crash.log", ex.ToString());
        }
    }    
}