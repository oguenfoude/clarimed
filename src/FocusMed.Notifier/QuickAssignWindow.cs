using System;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

#pragma warning disable CS8618

namespace FocusMed.Notifier;

public class QuickAssignWindow : Form
{
    private readonly HttpClient _http;
    private readonly int _inboxDocumentId;

    private TextBox _searchBox;
    private DataGridView _grid;

    private System.Windows.Forms.Timer _debounceTimer;
    private Label _lblStatus;
    private ProgressBar _progressBar;

    public QuickAssignWindow(int inboxDocumentId)
    {
        _inboxDocumentId = inboxDocumentId;
        _http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

        this.Text = "FocusMed - Assign Printed Document";
        this.Size = new Size(550, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.TopMost = true;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Font = new Font("Segoe UI", 10F);
        this.BackColor = Color.FromArgb(248, 250, 252); // slate-50

        InitializeComponents();

        this.Load += async (s, e) => await PerformSearch("");
    }

    private void InitializeComponents()
    {
        // 0. Header Instruction
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White };
        var lblHeader = new Label 
        { 
            Text = "A new document has been printed.\nPlease assign it to an existing patient.", 
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(15, 0, 0, 0),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59) // slate-800
        };
        
        var btnDismiss = new Button 
        { 
            Text = "Dismiss", 
            Dock = DockStyle.Right, 
            Width = 100, 
            BackColor = Color.FromArgb(239, 68, 68), // red-500
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnDismiss.FlatAppearance.BorderSize = 0;
        btnDismiss.Click += async (s, e) => 
        {
            SetLoading(true, "Dismissing...");
            await _http.DeleteAsync($"/api/quickassign/{_inboxDocumentId}");
            this.Close();
        };

        var headerRightPanel = new Panel { Dock = DockStyle.Right, Width = 120, Padding = new Padding(10) };
        headerRightPanel.Controls.Add(btnDismiss);

        headerPanel.Controls.Add(lblHeader);
        headerPanel.Controls.Add(headerRightPanel);
        
        var separator1 = new Label { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(226, 232, 240) }; // slate-200
        headerPanel.Controls.Add(separator1);

        // 1. Search Box
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15) };
        var lblSearch = new Label { Text = "Search Existing:", AutoSize = true, Location = new Point(15, 18), ForeColor = Color.FromArgb(71, 85, 105) };
        _searchBox = new TextBox { Location = new Point(130, 15), Width = 380, Font = new Font("Segoe UI", 11F) };
        searchPanel.Controls.Add(lblSearch);
        searchPanel.Controls.Add(_searchBox);

        _debounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _debounceTimer.Tick += async (s, e) =>
        {
            _debounceTimer.Stop();
            await PerformSearch(_searchBox.Text);
        };
        _searchBox.TextChanged += (s, e) =>
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        };

        // 2. Grid
        var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 15, 15) };
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(241, 245, 249),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            EnableHeadersVisualStyles = false,
            Cursor = Cursors.Hand
        };
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(241, 245, 249), ForeColor = Color.FromArgb(71, 85, 105), Font = new Font("Segoe UI", 9F, FontStyle.Bold), Padding = new Padding(5) };
        _grid.DefaultCellStyle = new DataGridViewCellStyle { SelectionBackColor = Color.FromArgb(224, 242, 254), SelectionForeColor = Color.FromArgb(15, 23, 42), Padding = new Padding(5) };
        
        var lblGridHint = new Label { Text = "Double-click a row below to assign", Dock = DockStyle.Top, Height = 25, ForeColor = Color.FromArgb(100, 116, 139), Font = new Font("Segoe UI", 9F, FontStyle.Italic) };
        gridPanel.Controls.Add(_grid);
        gridPanel.Controls.Add(lblGridHint);

        _grid.CellDoubleClick += async (s, e) =>
        {
            if (e.RowIndex >= 0)
            {
                var studyId = (int)_grid.Rows[e.RowIndex].Cells["StudyId"].Value;
                await AssignToStudy(studyId);
            }
        };

        // 3. Status Bar
        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(241, 245, 249) };
        _lblStatus = new Label { Text = "Ready", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0), Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(100, 116, 139) };
        _progressBar = new ProgressBar { Style = ProgressBarStyle.Marquee, Dock = DockStyle.Right, Width = 100, Visible = false };
        
        statusPanel.Controls.Add(_lblStatus);
        statusPanel.Controls.Add(_progressBar);

        // Layout
        this.Controls.Add(gridPanel);
        this.Controls.Add(searchPanel);
        this.Controls.Add(headerPanel);
        this.Controls.Add(statusPanel);
    }

    private void SetLoading(bool loading, string message)
    {
        _lblStatus.Text = message;
        _progressBar.Visible = loading;
        _searchBox.Enabled = !loading;
        _grid.Enabled = !loading;
    }

    private async Task PerformSearch(string query)
    {
        try
        {
            SetLoading(true, "Searching studies...");
            var results = await _http.GetFromJsonAsync<JsonElement[]>($"/api/quickassign/unassigned-studies?search={Uri.EscapeDataString(query)}");
            
            _grid.DataSource = null;
            _grid.Columns.Clear();
            _grid.Columns.Add("StudyId", "StudyId");
            _grid.Columns["StudyId"].Visible = false;
            _grid.Columns.Add("PatientName", "Name");
            _grid.Columns.Add("PatientCode", "ID");
            _grid.Columns.Add("Modality", "Modality");
            _grid.Columns.Add("StudyDate", "Date");

            if (results != null)
            {
                foreach (var r in results)
                {
                    var studyId = r.GetProperty("studyId").GetInt32();
                    var pName = r.GetProperty("patientName").GetString();
                    var pCode = r.GetProperty("patientCode").GetString();
                    var mod = r.GetProperty("modality").GetString();
                    var sDate = r.TryGetProperty("studyDate", out var dProp) && dProp.ValueKind != JsonValueKind.Null ? dProp.GetDateTime().ToShortDateString() : "";

                    _grid.Rows.Add(studyId, pName, pCode, mod, sDate);
                }
            }
            SetLoading(false, $"Found {results?.Length ?? 0} studies");
        }
        catch (Exception ex)
        {
            SetLoading(false, "Error searching studies");
            System.IO.File.AppendAllText(@"D:\ClariMed\notifier_error.log", $"PerformSearch Error: {ex}\n");
        }
    }

    private async Task AssignToStudy(int studyId)
    {
        try
        {
            SetLoading(true, "Assigning document...");
            var res = await _http.PostAsJsonAsync($"/api/quickassign/{_inboxDocumentId}/assign", new { StudyId = studyId });
            if (res.IsSuccessStatusCode)
            {
                MessageBox.Show("Document successfully assigned!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            else
            {
                MessageBox.Show("Failed to assign document.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetLoading(false, "Error assigning document");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetLoading(false, "Error assigning document");
        }
    }

}
