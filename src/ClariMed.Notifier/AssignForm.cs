using System;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using ClariMed.Data;
using ClariMed.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClariMed.Notifier
{
    public partial class AssignForm : Form
    {
        private readonly int _inboxDocId;
        private readonly IServiceScopeFactory _scopeFactory;
        private Label _lblTitle = null!;
        private CheckBox _chkShowAll = null!;
        private FlowLayoutPanel _flpStudies = null!;
        private Button _btnDismiss = null!;

        public AssignForm(int inboxDocId, IServiceScopeFactory scopeFactory)
        {
            _inboxDocId = inboxDocId;
            _scopeFactory = scopeFactory;
            
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "New Incoming Document";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true; 
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            _lblTitle = new Label {
                Text = "A new document was received.\nPlease click a Patient Study below to assign it:",
                AutoSize = false,
                Size = new Size(460, 40),
                Location = new Point(10, 10),
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };

            _chkShowAll = new CheckBox {
                Text = "Show All Studies (including assigned)",
                AutoSize = true,
                Location = new Point(10, 55),
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Checked = false
            };
            _chkShowAll.CheckedChanged += (s, e) => LoadData();

            _flpStudies = new FlowLayoutPanel {
                Location = new Point(10, 85),
                Size = new Size(460, 270),
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.WhiteSmoke
            };

            _btnDismiss = new Button {
                Text = "Dismiss",
                Location = new Point(200, 365),
                Size = new Size(100, 35),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.White
            };
            _btnDismiss.Click += (s,e) => this.Close();

            this.Controls.Add(_lblTitle);
            this.Controls.Add(_chkShowAll);
            this.Controls.Add(_flpStudies);
            this.Controls.Add(_btnDismiss);
        }

        private async void LoadData()
        {
            _flpStudies.Controls.Clear();
            _flpStudies.Controls.Add(new Label { Text = "Loading...", AutoSize = true, Margin = new Padding(10) });

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

            var studiesQuery = db.Studies
                .Include(s => s.Patient)
                .OrderByDescending(s => s.StudyDate)
                .AsQueryable();

            if (!_chkShowAll.Checked)
            {
                var assignedStudyIds = await db.Documents
                    .Where(d => d.StudyId != null)
                    .Select(d => d.StudyId ?? 0)
                    .ToListAsync();
                
                studiesQuery = studiesQuery.Where(s => !assignedStudyIds.Contains(s.Id));
            }

            var studies = await studiesQuery.Take(50).ToListAsync();

            _flpStudies.Controls.Clear();

            if (studies.Count == 0)
            {
                _flpStudies.Controls.Add(new Label { Text = "No studies found.", AutoSize = true, Margin = new Padding(10) });
                return;
            }

            foreach(var s in studies)
            {
                var btn = new Button
                {
                    Text = $"{s.Patient?.Name}\nID: {s.Patient?.PatientId} | {s.Modality}\n{s.StudyDescription}",
                    Size = new Size(430, 60),
                    Margin = new Padding(5),
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    BackColor = Color.White,
                    Cursor = Cursors.Hand,
                    Tag = s.Id
                };
                btn.FlatAppearance.BorderColor = Color.LightGray;
                btn.Click += async (sender, e) => {
                    if (sender is Button button && button.Tag is int studyId)
                    {
                        await AssignToStudy(studyId);
                    }
                };

                _flpStudies.Controls.Add(btn);
            }
        }

        private async System.Threading.Tasks.Task AssignToStudy(int studyId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ClariMedDbContext>();

                var doc = await db.InboxDocuments.FindAsync(_inboxDocId);
                var study = await db.Studies.FindAsync(studyId);

                if (doc != null && study != null)
                {
                    doc.AssignedToStudyId = study.Id;
                    doc.Status = InboxDocumentStatus.Assigned;
                    doc.AssignedAt = DateTime.UtcNow;

                    var newDoc = new Document
                    {
                        OriginalFileName = doc.FileName,
                        OriginalFilePath = doc.PdfPath,
                        PdfFilePath = doc.PdfPath,
                        Status = DocumentStatus.Converted,
                        ReceivedAt = doc.ReceivedAt,
                        ConvertedAt = DateTime.UtcNow,
                        StudyId = study.Id
                    };
                    db.Documents.Add(newDoc);
                    await db.SaveChangesAsync();

                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = $"http://localhost:5000/preview/{study.Id}",
                            UseShellExecute = true
                        });
                    }
                    catch { /* Best effort */ }

                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to assign document: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
