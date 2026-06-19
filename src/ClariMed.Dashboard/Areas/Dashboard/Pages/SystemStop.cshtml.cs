using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;

namespace ClariMed.Dashboard.Areas.Dashboard.Pages;

public class SystemStopModel : PageModel
{
    private readonly IHostApplicationLifetime _appLifetime;

    public SystemStopModel(IHostApplicationLifetime appLifetime)
    {
        _appLifetime = appLifetime;
    }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        _appLifetime.StopApplication();
        return Page();
    }
}
