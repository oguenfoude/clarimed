using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ClariMed.Data.Repositories;
using ClariMed.Data.Services;

namespace ClariMed.VirtualPrinter;

public class WindowsPrinterRegistration
{
    private readonly ILogger<WindowsPrinterRegistration> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public WindowsPrinterRegistration(ILogger<WindowsPrinterRegistration> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task EnsureRegisteredAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();

        var settings = await settingsRepo.GetAsync();
        var watchFolder = settings.WatchFolderPath;
        var portName = Path.Combine(watchFolder, "VirtualPrint.pdf");
        var printerName = "ClariMed";
        var driverName = "Microsoft Print To PDF";

        if (await CheckPrinterExistsAsync(printerName))
        {
            _logger.LogInformation("Virtual printer already registered in Windows. Skipping setup.");
            return;
        }

        _logger.LogInformation($"Attempting to register ClariMed Virtual Printer '{printerName}' via PowerShell on port '{portName}'...");

        try
        {
            // Clean up existing printer and port if any (best-effort)
            _logger.LogInformation("Removing existing printer and port if any...");
            try { await RunPowerShellAsync($"Remove-Printer -Name '{printerName}'"); } catch { }
            try { await RunPowerShellAsync($"Remove-PrinterPort -Name '{portName}'"); } catch { }
            try { await RunPowerShellAsync("Remove-PrinterPort -Name 'ClariMed_IPP'"); } catch { }

            // Add printer port pointing to the file path
            var addPortCmd = $"Add-PrinterPort -Name '{portName}'";
            try 
            {
                await RunPowerShellAsync(addPortCmd);
            } 
            catch (Exception ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("ResourceExists")) 
            {
                _logger.LogInformation("Printer port already exists, continuing...");
            }

            // Add printer
            var addPrinterCmd = $"Add-Printer -Name '{printerName}' -DriverName '{driverName}' -PortName '{portName}'";
            try 
            {
                await RunPowerShellAsync(addPrinterCmd);
            } 
            catch (Exception ex) when (ex.Message.Contains("already exists") || ex.Message.Contains("ResourceExists")) 
            {
                _logger.LogInformation("Printer already exists, continuing...");
            }

            settings.PrinterRegistered = true;
            await settingsRepo.UpdateAsync(settings);
            
            _logger.LogInformation($"Successfully registered ClariMed Virtual Printer '{printerName}' on port '{portName}'.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register Windows printer. This usually requires Administrator privileges.");
        }
    }

    private async Task<bool> CheckPrinterExistsAsync(string printerName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-Printer -Name '{printerName}' -ErrorAction Stop\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return false;
            
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task RunPowerShellAsync(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Failed to start PowerShell process.");
        
        await process.WaitForExitAsync();
        
        var error = await process.StandardError.ReadToEndAsync();
        if (process.ExitCode != 0 || (!string.IsNullOrWhiteSpace(error) && error.Contains("Exception")))
        {
            throw new Exception($"PowerShell command failed with exit code {process.ExitCode}: {error}");
        }
    }
}
