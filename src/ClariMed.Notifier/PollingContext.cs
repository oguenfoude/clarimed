using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Notifier
{
    public class PollingContext : ApplicationContext
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly System.Windows.Forms.Timer _timer;
        private AssignForm? _currentForm;

        public PollingContext(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 3000;
            _timer.Tick += async (s, e) => await CheckForUnassignedAsync();
            _timer.Start();

            var trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "ClariMed Virtual Printer Notifier"
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Exit Notifier", null, (s, e) => {
                trayIcon.Visible = false;
                Application.Exit();
            });
            trayIcon.ContextMenuStrip = menu;
        }

        private async Task CheckForUnassignedAsync()
        {
            if (_currentForm != null) return; 

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

            var unassigned = await db.InboxDocuments
                .Where(d => d.Status == InboxDocumentStatus.Unassigned)
                .OrderByDescending(d => d.ReceivedAt)
                .FirstOrDefaultAsync();

            if (unassigned != null)
            {
                _timer.Stop();
                _currentForm = new AssignForm(unassigned.Id, _scopeFactory);
                _currentForm.FormClosed += (s, e) => {
                    _currentForm = null;
                    _timer.Start();
                };
                _currentForm.Show();
            }
        }
    }
}
