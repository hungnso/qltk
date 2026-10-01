using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

namespace QltkAccounts
{
    sealed class RunningTab
    {
        public Account Account; public int Id; public Process Process;
        public string Home, Status = "Đang mở…", CharacterInfo = "Chưa vào game";
        public bool Positioned;
    }
    public sealed class AccountManager : Form
    {
        readonly string root;
        readonly DataGridView grid = new DataGridView();
        readonly TextBox errors = new TextBox();
        readonly Label summary = new Label();
        readonly Button openSelected = new Button(), openAll = new Button(), reload = new Button();
        readonly Dictionary<string, RunningTab> running = new Dictionary<string, RunningTab>();
        readonly Queue<Account> pending = new Queue<Account>();
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer launchTimer = new System.Windows.Forms.Timer();
        readonly List<Account> accounts = new List<Account>();
        TabRegistry registry;
        AppSettings settings;
        bool failedConfig;

        public AccountManager(string root)
        {
            this.root = root;
            Text = "QLTK NST — Quản lý accounts.txt";
            StartPosition = FormStartPosition.CenterScreen; Size = new Size(1180, 570); MinimumSize = new Size(900, 450);
            Font = new Font("Segoe UI", 10);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(8), WrapContents = false };
            AddButton(toolbar, reload, "Đọc lại danh sách", delegate { LoadAccounts(); });
            AddButton(toolbar, openSelected, "Mở tài khoản đã chọn", delegate { QueueLaunch(SelectedAccounts()); });
            AddButton(toolbar, openAll, "Mở tất cả", delegate { QueueLaunch(accounts.ToList()); });
            var show = new Button(); AddButton(toolbar, show, "Hiện tab", delegate { ShowSelected(); });
            var close = new Button(); AddButton(toolbar, close, "Đóng tab đã chọn", delegate { CloseSelected(); });
            grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White; grid.AllowUserToOrderColumns = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "chosen", HeaderText = "Chọn", FillWeight = 35 });
            foreach (string[] column in new[] { new[] { "username", "Tài khoản" }, new[] { "server", "Server" }, new[] { "tab", "Tab" }, new[] { "status", "Trạng thái" }, new[] { "character", "Thông tin NV" } }) {
                var c = new DataGridViewTextBoxColumn { Name = column[0], HeaderText = column[1], ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
                if (column[0] == "tab") c.FillWeight = 35;
                if (column[0] == "status") c.FillWeight = 130;
                if (column[0] == "character") { c.FillWeight = 220; c.DefaultCellStyle.WrapMode = DataGridViewTriState.True; }
                grid.Columns.Add(c);
            }
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) ShowSelected(); };
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            errors.Dock = DockStyle.Bottom; errors.Height = 88; errors.Multiline = true; errors.ReadOnly = true; errors.ScrollBars = ScrollBars.Vertical;
            summary.Dock = DockStyle.Bottom; summary.Height = 32; summary.Padding = new Padding(8, 5, 0, 0);
            Controls.Add(grid); Controls.Add(errors); Controls.Add(summary); Controls.Add(toolbar);
            launchTimer.Interval = 1200; launchTimer.Tick += delegate { LaunchNext(); };
            poll.Interval = 800; poll.Tick += delegate { RefreshStatuses(); };
            LoadAccounts(); poll.Start();
            FormClosing += OnClosing;
        }
        static void AddButton(FlowLayoutPanel panel, Button button, string title, EventHandler action)
        {
            button.Text = title; button.AutoSize = true; button.Height = 34; button.Click += action; panel.Controls.Add(button);
        }
        void LoadAccounts()
        {
            if (pending.Count > 0) return;
            try {
                settings = AppSettings.Load(root);
                if (registry == null) registry = new TabRegistry(Path.Combine(root, "data", "accounts", "tabs.xml"));
                string[] servers = File.ReadAllLines(Path.Combine(root, "servers.txt"), System.Text.Encoding.UTF8).Select(s => s.Trim().TrimStart('\uFEFF')).Where(s => s.Length > 0 && !s.StartsWith("#")).ToArray();
                if (servers.Length == 0 || servers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != servers.Length) throw new InvalidDataException("servers.txt trống hoặc có server trùng nhau.");
                var result = AccountParser.Parse(File.ReadAllLines(Path.Combine(root, "accounts.txt"), System.Text.Encoding.UTF8), servers);
                // Removed accounts with a live window remain visible so they can be closed.
                accounts.Clear(); accounts.AddRange(result.Accounts);
                foreach (var tab in running.Values) if (!accounts.Any(a => a.Key == tab.Account.Key)) accounts.Add(tab.Account);
                grid.Rows.Clear();
                foreach (var account in accounts) {
                    int id = registry.Find(account.Key);
                    int row = grid.Rows.Add(false, account.Username, account.Server, id == 0 ? "—" : id.ToString(), "Chưa mở", "Chưa vào game");
                    grid.Rows[row].Tag = account;
                }
                errors.Text = result.Errors.Count > 0 ? string.Join(Environment.NewLine, result.Errors) : "Danh sách đã tải. Chọn account rồi bấm mở; QLTK không tự mở tab khi khởi động.";
                failedConfig = false; SetButtons(); RefreshStatuses();
            } catch (Exception ex) {
                failedConfig = true; errors.Text = "Không đọc được danh sách/cấu hình: " + ex.Message; SetButtons();
            }
        }
        List<Account> SelectedAccounts()
        {
            grid.EndEdit();
            var checkedRows = grid.Rows.Cast<DataGridViewRow>().Where(r => object.Equals(r.Cells["chosen"].Value, true)).ToList();
            var rows = checkedRows.Count > 0 ? checkedRows : grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList();
            return rows.Select(r => (Account)r.Tag).ToList();
        }
        void QueueLaunch(List<Account> selected)
        {
            if (pending.Count > 0 || failedConfig) return;
            if (selected.Count == 0) { errors.Text = "Chọn ít nhất một tài khoản để mở."; return; }
            try {
                settings = AppSettings.Load(root);
                foreach (string path in new[] { settings.JavaPath, settings.EmulatorPath, settings.GamePath, Path.Combine(root, "account-bridge.jar") })
                    if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy: " + Path.GetFileName(path));
                RefreshStatuses();
                var plan = LaunchPlanner.Plan(selected, running.Keys, settings.MaxTab);
                foreach (var account in plan.Accounts) pending.Enqueue(account);
                errors.Text = "Đang mở " + plan.Accounts.Count + " tài khoản. " + plan.AlreadyRunning + " tab đã chạy; " + plan.OverLimit + " account chưa mở do giới hạn MaxTab=" + settings.MaxTab + ".";
                SetButtons(); RefreshStatuses();
                if (pending.Count > 0) { LaunchNext(); if (pending.Count > 0) launchTimer.Start(); }
            } catch (Exception ex) { errors.Text = "Không mở được tab: " + ex.Message; }
        }
        void LaunchNext()
        {
            if (pending.Count == 0) { launchTimer.Stop(); SetButtons(); return; }
            Account account = pending.Dequeue();
            try {
                if (running.ContainsKey(account.Key) || running.Count >= settings.MaxTab) return;
                int id = registry.GetOrAdd(account.Key);
                string home = Path.Combine(root, "data", "accounts", "tab_" + id);
                Directory.CreateDirectory(home);
                string status = Path.Combine(home, "bridge.status"); if (File.Exists(status)) File.Delete(status);
                string characterFile = Path.Combine(home, "character.xml"); if (File.Exists(characterFile)) File.Delete(characterFile);
                var arguments = new[] { "-Xmx256m", "-cp", Path.Combine(root, "account-bridge.jar") + ";" + settings.EmulatorPath, "AccountBootstrap", settings.GamePath, home, account.ServerIndex.ToString(), id.ToString(), settings.Width.ToString(), settings.Height.ToString(), settings.AutoLogin.ToString().ToLowerInvariant(), status };
                var start = new ProcessStartInfo(settings.JavaPath, string.Join(" ", arguments.Select(LaunchArguments.Quote))) {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.EnvironmentVariables["QLTK_ACCOUNT_USER"] = account.Username;
                start.EnvironmentVariables["QLTK_ACCOUNT_PASS"] = account.Password;
                var process = new Process { StartInfo = start };
                // Discard emulator output, which may include sensitive game messages.
                process.OutputDataReceived += delegate { }; process.ErrorDataReceived += delegate { };
                try { if (!process.Start()) throw new IOException("Java không khởi động."); process.BeginOutputReadLine(); process.BeginErrorReadLine(); }
                catch { process.Dispose(); throw; }
                running.Add(account.Key, new RunningTab { Account = account, Id = id, Process = process, Home = home });
            } catch (Exception ex) {
                errors.Text += Environment.NewLine + "Dòng " + account.Line + ": không mở được tab — " + ex.Message;
                SetRowStatus(account.Key, "Lỗi mở tab");
            } finally { if (pending.Count == 0) launchTimer.Stop(); SetButtons(); RefreshStatuses(); }
        }
        void SetButtons() { bool ready = !failedConfig && pending.Count == 0; openAll.Enabled = ready; openSelected.Enabled = ready; reload.Enabled = pending.Count == 0; }
        void SetRowStatus(string key, string value) { foreach (DataGridViewRow row in grid.Rows) if (((Account)row.Tag).Key == key) row.Cells["status"].Value = value; }
        void RefreshStatuses()
        {
            foreach (var pair in running.ToList()) {
                var tab = pair.Value;
                try {
                    if (tab.Process.HasExited) { SetRowStatus(pair.Key, tab.Process.ExitCode == 0 ? "Đã đóng" : "Lỗi khởi động (Java " + tab.Process.ExitCode + ")"); tab.Process.Dispose(); running.Remove(pair.Key); continue; }
                    tab.Process.Refresh();
                    if (!tab.Positioned && tab.Process.MainWindowHandle != IntPtr.Zero) { Position(tab); tab.Positioned = true; }
                    string status = Path.Combine(tab.Home, "bridge.status");
                    if (File.Exists(status)) {
                        switch (File.ReadAllText(status).Trim()) {
                            case "STARTING": tab.Status = "Đang tải game…"; break;
                            case "READY_MANUAL": tab.Status = "Đã mở — đăng nhập thủ công"; break;
                            case "LOGIN_SUBMITTED": tab.Status = "Đã gửi đăng nhập"; break;
                            case "SELECTING_CHARACTER": tab.Status = "Đang chọn NV số 1…"; break;
                            case "IN_GAME": tab.Status = "Đã vào game"; break;
                            case "CHARACTER_SLOT_EMPTY": tab.Status = "Ô NV số 1 trống — chọn thủ công"; break;
                            case "ERROR_CHARACTER": tab.Status = "Lỗi đọc/chọn NV — kiểm tra tab"; break;
                            case "ERROR_LOGIN_TIMEOUT": tab.Status = "Game chưa sẵn sàng — kiểm tra tab"; break;
                            case "ERROR_LOGIN": tab.Status = "Lỗi truyền account — kiểm tra tab"; break;
                            case "ERROR_STARTUP": tab.Status = "Lỗi khởi động game"; break;
                        }
                    }
                    SetRowStatus(pair.Key, tab.Status);
                    string characterFile = Path.Combine(tab.Home, "character.xml");
                    if (File.Exists(characterFile)) {
                        try {
                            string display = CharacterSnapshot.Parse(File.ReadAllText(characterFile)).Display;
                            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(characterFile) > TimeSpan.FromSeconds(15)) display = "Thông tin chưa cập nhật" + Environment.NewLine + display;
                            tab.CharacterInfo = display;
                        } catch (Exception ex) { if (ex is System.Xml.XmlException || ex is InvalidDataException) tab.CharacterInfo = "Chưa đọc được thông tin NV"; else throw; }
                    }
                    foreach (DataGridViewRow row in grid.Rows) if (((Account)row.Tag).Key == pair.Key) row.Cells["character"].Value = tab.CharacterInfo;
                } catch (IOException) { } catch (InvalidOperationException) { }
            }
            foreach (var account in pending) SetRowStatus(account.Key, "Đang chờ mở…");
            foreach (DataGridViewRow row in grid.Rows) { int id = registry == null ? 0 : registry.Find(((Account)row.Tag).Key); if (id > 0) row.Cells["tab"].Value = id.ToString(); }
            summary.Text = accounts.Count + " tài khoản • " + running.Count + " tab đang chạy • " + pending.Count + " đang chờ • MaxTab: " + (settings == null ? "—" : settings.MaxTab.ToString());
        }
        void Position(RunningTab tab)
        {
            IntPtr handle = tab.Process.MainWindowHandle;
            SetWindowText(handle, "Tab " + tab.Id + " — " + tab.Account.Username + " — " + tab.Account.Server);
            int width = settings.Width + 20, height = settings.Height + 90;
            Rectangle screen = Screen.PrimaryScreen.WorkingArea; int columns = Math.Max(1, screen.Width / width);
            int x = screen.Left + ((tab.Id - 1) % columns) * width;
            int y = screen.Top + (((tab.Id - 1) / columns) * height) % Math.Max(1, screen.Height - height + 1);
            string layout = Path.Combine(root, "layout.xml");
            if (File.Exists(layout)) {
                try { var element = XDocument.Load(layout).Root.Elements("Tab").FirstOrDefault(e => (int?)e.Attribute("no") == tab.Id); if (element != null) { x = (int)element.Attribute("x"); y = (int)element.Attribute("y"); } } catch { }
            }
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - width)); y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - height));
            SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x0004);
        }
        void ShowSelected()
        {
            foreach (var account in SelectedAccounts()) { RunningTab tab; if (running.TryGetValue(account.Key, out tab) && !tab.Process.HasExited) { tab.Process.Refresh(); ShowWindow(tab.Process.MainWindowHandle, 9); SetForegroundWindow(tab.Process.MainWindowHandle); } }
        }
        void CloseSelected()
        {
            foreach (var account in SelectedAccounts()) { RunningTab tab; if (running.TryGetValue(account.Key, out tab) && !tab.Process.HasExited) tab.Process.CloseMainWindow(); }
        }
        void OnClosing(object sender, FormClosingEventArgs args)
        {
            if (running.Count > 0 && MessageBox.Show("Đóng QLTK và các tab game đang chạy?", "QLTK NST", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { args.Cancel = true; return; }
            launchTimer.Stop(); poll.Stop(); pending.Clear();
            foreach (var tab in running.Values) {
                try { if (!tab.Process.HasExited) { tab.Process.CloseMainWindow(); if (!tab.Process.WaitForExit(1500)) tab.Process.Kill(); } } catch { }
                tab.Process.Dispose();
            }
        }
        public int LoadedAccountCount { get { return accounts.Count; } }
        public int RunningCount { get { return running.Count; } }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { launchTimer.Stop(); poll.Stop(); launchTimer.Dispose(); poll.Dispose(); }
            base.Dispose(disposing);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetWindowText(IntPtr handle, string title);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    }
    static class Program
    {
        [STAThread] static void Main()
        {
            bool created;
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string lockName = "Local\\QLTK_Accounts_" + root.GetHashCode().ToString("X");
            using (var mutex = new Mutex(true, lockName, out created)) {
                if (!created) { MessageBox.Show("QLTK Accounts đã mở. Hãy dùng cửa sổ đang chạy."); return; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AccountManager(root));
            }
        }
    }
}
