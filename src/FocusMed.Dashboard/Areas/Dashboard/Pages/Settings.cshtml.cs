using FocusMed.Data;
using FocusMed.Data.Models;
using FocusMed.Data.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace FocusMed.Dashboard.Areas.Dashboard.Pages;

[IgnoreAntiforgeryToken]
public class SettingsModel : PageModel
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ClinicSettings? Settings { get; set; }
    public string Lang { get; set; } = "en";
    public string ActiveSection { get; set; } = "general";
    public IReadOnlyList<User>? Users { get; set; }
    public List<string> LocalIpAddresses { get; set; } = new();
    public bool IsAdmin => User.IsInRole("Admin");

    public SettingsModel(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task OnGetAsync([FromQuery] string? section = null)
    {
        if (section != null) ActiveSection = section;
        else ActiveSection = "general";

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
        Settings = await repo.GetAsync();
        Lang = Settings?.Language ?? "en";

        if (ActiveSection == "general" || ActiveSection == "dicom")
        {
            try
            {
                var ips = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                                 ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                                 (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ||
                                  ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211) &&
                                 !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                                 !ni.Description.Contains("vEthernet", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(ua => ua.Address.ToString())
                    .ToList();

                if (ips.Any())
                {
                    LocalIpAddresses = ips;
                }
                else
                {
                    var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                    LocalIpAddresses = host.AddressList
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.ToString())
                        .ToList();
                }
            }
            catch
            {
                LocalIpAddresses.Add("127.0.0.1");
            }

            if (!LocalIpAddresses.Any())
            {
                LocalIpAddresses.Add("127.0.0.1");
            }
        }

        if (ActiveSection == "users" && IsAdmin)
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            Users = await userRepo.GetAllAsync();
        }
    }

    // ── General (Language) ──
    public async Task<IActionResult> OnPostLanguageAsync()
    {
        var lang = Request.Form["Language"].FirstOrDefault() ?? "en";
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
        var settings = await repo.GetAsync();
        settings.Language = lang;
        await repo.UpdateAsync(settings);
        Response.Headers["HX-Refresh"] = "true";
        return Content("");
    }

    // ── DICOM Settings ──
    public async Task<IActionResult> OnPostDicomAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
        var settings = await repo.GetAsync();

        if (int.TryParse(Request.Form["DicomPort"].FirstOrDefault(), out var port) && port > 0)
            settings.DicomPort = port;

        var aeTitle = Request.Form["AETitle"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(aeTitle))
            settings.AETitle = aeTitle;

        settings.DicomSettingsPendingRestart = true;
        await repo.UpdateAsync(settings);
        return Content("<span class='text-amber-600 text-sm font-bold'>Settings saved. Server will restart when idle...</span>");
    }

    // ── Add User ──
    public async Task<IActionResult> OnPostAddUserAsync()
    {
        if (!IsAdmin) return Forbid();

        var username = Request.Form["Username"].FirstOrDefault()?.Trim();
        var displayName = Request.Form["DisplayName"].FirstOrDefault()?.Trim();
        var password = Request.Form["Password"].FirstOrDefault();
        var roleStr = Request.Form["Role"].FirstOrDefault() ?? "User";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
            return Content("<span class='text-red-600 text-sm font-medium'>Username, display name, and password are required.</span>");

        if (password.Length < 6)
            return Content("<span class='text-red-600 text-sm font-medium'>Password must be at least 6 characters.</span>");

        using var scope = _scopeFactory.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var existing = await userRepo.GetByUsernameAsync(username);
        if (existing != null)
            return Content("<span class='text-red-600 text-sm font-medium'>Username already exists.</span>");

        var role = roleStr == "Admin" ? UserRole.Admin : UserRole.User;
        await userRepo.AddAsync(new User
        {
            Username = username,
            DisplayName = displayName,
            Role = role,
            IsActive = true
        }, password);

        Users = await userRepo.GetAllAsync();
        return Partial("_UsersTable", Users);
    }

    // ── Toggle User Active ──
    public async Task<IActionResult> OnPostToggleUserAsync(int userId)
    {
        if (!IsAdmin) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepo.GetByIdAsync(userId);
        if (user == null) return NotFound();

        // Default admin cannot be deactivated by anyone
        if (user.Username == "admin")
            return Content("<span class='text-red-600 text-sm font-medium'>Default admin account cannot be modified.</span>");

        user.IsActive = !user.IsActive;
        await userRepo.UpdateAsync(user);

        Users = await userRepo.GetAllAsync();
        return Partial("_UsersTable", Users);
    }

    // ── Delete User ──
    public async Task<IActionResult> OnPostDeleteUserAsync(int userId)
    {
        if (!IsAdmin) return Forbid();

        using var scope = _scopeFactory.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepo.GetByIdAsync(userId);
        if (user == null) return NotFound();

        // Default admin cannot be deleted by anyone
        if (user.Username == "admin")
            return Content("<span class='text-red-600 text-sm font-medium'>Default admin account cannot be deleted.</span>");

        await userRepo.DeleteAsync(userId);

        Users = await userRepo.GetAllAsync();
        return Partial("_UsersTable", Users);
    }

    // ── Change Password ──
    public async Task<IActionResult> OnPostChangePasswordAsync(int userId)
    {
        if (!IsAdmin) return Forbid();

        var newPassword = Request.Form["NewPassword"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return Content("<span class='text-red-600 text-sm font-medium'>Password must be at least 6 characters.</span>");

        using var scope = _scopeFactory.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepo.GetByIdAsync(userId);
        if (user == null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await userRepo.UpdateAsync(user);

        return Content("<span class='text-emerald-600 text-sm font-bold'>Password updated</span>");
    }

    // ── DICOM Status (for polling) ──
    public async Task<IActionResult> OnGetDicomStatusAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClinicSettingsRepository>();
        var settings = await repo.GetAsync();

        if (settings?.DicomSettingsPendingRestart == true)
            return Content("<span class='inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-amber-50 text-amber-700 text-xs font-bold border border-amber-200'><span class='w-1.5 h-1.5 rounded-full bg-amber-500 animate-pulse'></span>Restart pending...</span>");

        return Content("<span class='inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-50 text-emerald-700 text-xs font-bold border border-emerald-200'><span class='w-1.5 h-1.5 rounded-full bg-emerald-500'></span>Active</span>");
    }
}
