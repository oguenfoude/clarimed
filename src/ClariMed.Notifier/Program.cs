using System;
using System.IO;
using System.Windows.Forms;
using ClariMed.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClariMed.Notifier
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Point to the database used by the worker
            var dbPath = @"D:\ClariMed\db\clarimed.db";

            var services = new ServiceCollection();
            services.AddClariMedData(dbPath);
            services.AddSingleton<PollingContext>();
            var provider = services.BuildServiceProvider();

            Application.Run(provider.GetRequiredService<PollingContext>());
        }
    }
}