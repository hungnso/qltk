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
using System.Xml;
using System.Globalization;
using System.Text;

namespace QltkAccounts
{
    sealed class RunningTab
    {
        public Account Account; public int Id, Width, Height; public Process Process;
        public string Home, Status = "Đang mở…", CharacterInfo = "Chưa vào game";
        public bool Positioned;
        public CharacterSnapshot Snapshot;
        public long SnapshotStamp, SnapshotLength, StatusStamp, StatusLength;
        public string SnapshotXml;
        public string WindowTitle;
        public DateTime? LoginUtc;
        public bool HistoryDirty;
        public DateTime HistoryUpdatedUtc;
        public ProcessSleep Power;
        public string AutoSession, AutoStatus = "Chưa đọc Auto";
        public AutoSnapshot AutoSnapshot;
        public long AutoStateStamp, AutoStateLength, AutoSequence, AutoPending;
        public DateTime AutoPendingUtc;
    }
    sealed class PowerRequest { public RunningTab Tab; public bool Sleep; }
    public sealed class AccountManager : Form
    {
        readonly string root;
        readonly DataGridView grid = new DataGridView();
        readonly TextBox errors = new TextBox();
        readonly Label summary = new Label();
        readonly Button openSelected = new Button(), openAll = new Button(), reload = new Button();
        readonly Button autoSettings = new Button { Name = "autoSettings", Text = "Xem / chỉnh Auto", AutoSize = true, Height = 28 };
        readonly Label autoSummary = new Label { AutoSize = true, Text = "Chọn tab rồi xem cấu hình Auto", Margin = new Padding(12, 6, 0, 0) };
        readonly TableLayoutPanel launchConfig = new TableLayoutPanel();
        readonly TextBox emulatorPath = new TextBox { Name = "emulatorPath" }, gamePath = new TextBox { Name = "gamePath" };
        readonly NumericUpDown tabWidth = new NumericUpDown { Name = "tabWidth", Minimum = 100, Maximum = 2000, Value = 220, Width = 70 };
        readonly NumericUpDown tabHeight = new NumericUpDown { Name = "tabHeight", Minimum = 100, Maximum = 2000, Value = 240, Width = 70 };
        readonly NumericUpDown maxTab = new NumericUpDown { Name = "maxTab", Minimum = 1, Maximum = 200, Value = 5, Width = 70 };
        readonly CheckBox autoLogin = new CheckBox { Name = "autoLogin", Text = "Auto login", AutoSize = true, Checked = true };
        readonly CheckBox showUnder8 = new CheckBox { Name = "showUnder8", Text = "Hiện đồ dưới +8", AutoSize = true };
        readonly CheckBox showTrackedItems = new CheckBox { Name = "showTrackedItems", Text = "Hiện vật phẩm theo ID", AutoSize = true };
        readonly TextBox trackedItemIds = new TextBox { Name = "trackedItemIds", Width = 240, MaxLength = 2048 };
        readonly CheckBox vpsLight = new CheckBox { Name = "vpsLight", Text = "VPS nhẹ", AutoSize = true };
        readonly NumericUpDown javaHeapMb = new NumericUpDown { Name = "javaHeapMb", Minimum = 64, Maximum = 512, Value = 128, Width = 70 };
        readonly Dictionary<string, DataGridViewRow> rowsByKey = new Dictionary<string, DataGridViewRow>();
        readonly Dictionary<string, LastLoginRecord> saved = new Dictionary<string, LastLoginRecord>();
        readonly AccountHistoryStore historyStore;
        readonly Dictionary<string, RunningTab> running = new Dictionary<string, RunningTab>();
        readonly Queue<Account> pending = new Queue<Account>();
        readonly Queue<PowerRequest> powerQueue = new Queue<PowerRequest>();
        readonly List<Button> powerButtons = new List<Button>();
        readonly List<Button> arrangeButtons = new List<Button>();
        Dictionary<string, Point> windowPositions;
        readonly System.Windows.Forms.Timer powerTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer launchTimer = new System.Windows.Forms.Timer();
        readonly List<Account> accounts = new List<Account>();
        TabRegistry registry;
        AppSettings settings;
        bool failedConfig;

        public AccountManager(string root)
        {
            this.root = root;
            historyStore = new AccountHistoryStore(root);
            Text = "QLTK NST — Quản lý accounts.txt";
            StartPosition = FormStartPosition.CenterScreen; Size = new Size(1280, 640); MinimumSize = new Size(900, 480);
            Font = new Font("Segoe UI", 9);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(8), WrapContents = true };
            AddButton(toolbar, reload, "Đọc lại danh sách", delegate { LoadAccounts(); });
            AddButton(toolbar, openSelected, "Mở tài khoản đã chọn", delegate { QueueLaunch(SelectedAccounts()); });
            AddButton(toolbar, openAll, "Mở tất cả", delegate { QueueLaunch(accounts.ToList()); });
            var show = new Button(); AddButton(toolbar, show, "Hiện tab", delegate { ShowSelected(); });
            var close = new Button(); AddButton(toolbar, close, "Đóng tab đã chọn", delegate { CloseSelected(); });
            foreach (bool all in new[] { false, true }) {
                bool arrangeAll = all; var button = new Button();
                AddButton(toolbar, button, all ? "Sắp xếp tất cả" : "Sắp xếp đã chọn", delegate { ArrangeTabs(arrangeAll); }); arrangeButtons.Add(button);
            }
            foreach (string title in new[] { "Ngủ đã chọn", "Thức đã chọn", "Ngủ tất cả", "Thức tất cả" }) {
                string action = title; var button = new Button();
                AddButton(toolbar, button, action, delegate { QueuePower(action.StartsWith("Ngủ"), action.EndsWith("tất cả")); }); powerButtons.Add(button);
            }
            BuildConfiguration();
            grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White; grid.AllowUserToOrderColumns = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.Height = 24;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "chosen", HeaderText = "Chọn", FillWeight = 35, MinimumWidth = 55 });
            foreach (string[] column in new[] { new[] { "username", "Tài khoản" }, new[] { "server", "Server" }, new[] { "tab", "Tab" }, new[] { "status", "Trạng thái" }, new[] { "character", "Thông tin NV" } }) {
                var c = new DataGridViewTextBoxColumn { Name = column[0], HeaderText = column[1], ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable };
                if (column[0] == "username") c.MinimumWidth = 120;
                if (column[0] == "server") c.MinimumWidth = 160;
                if (column[0] == "tab") { c.FillWeight = 35; c.MinimumWidth = 50; }
                if (column[0] == "status") { c.FillWeight = 130; c.MinimumWidth = 180; }
                if (column[0] == "character") { c.FillWeight = 350; c.MinimumWidth = 500; c.DefaultCellStyle.WrapMode = DataGridViewTriState.False; }
                grid.Columns.Add(c);
            }
            foreach (string[] column in new[] { new[] { "under8", "Đồ đang mặc dưới +8" }, new[] { "items", "Vật phẩm theo ID" } })
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = column[0], HeaderText = column[1], ReadOnly = true, Visible = false, MinimumWidth = 260, FillWeight = 190, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "historyTime", HeaderText = "Lần đăng nhập / cập nhật", ReadOnly = true, MinimumWidth = 340, FillWeight = 220, SortMode = DataGridViewColumnSortMode.NotSortable });
            var autoColumn = new DataGridViewTextBoxColumn { Name = "auto", HeaderText = "Auto", ReadOnly = true, MinimumWidth = 220, FillWeight = 150, SortMode = DataGridViewColumnSortMode.NotSortable };
            grid.Columns.Add(autoColumn); autoColumn.DisplayIndex = grid.Columns["status"].DisplayIndex + 1;
            showUnder8.CheckedChanged += delegate { ApplyStatisticsVisibility(); };
            showTrackedItems.CheckedChanged += delegate { ApplyStatisticsVisibility(); };
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) ShowSelected(); };
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            errors.Dock = DockStyle.Bottom; errors.Height = 88; errors.Multiline = true; errors.ReadOnly = true; errors.ScrollBars = ScrollBars.Vertical;
            summary.Dock = DockStyle.Bottom; summary.Height = 32; summary.Padding = new Padding(8, 5, 0, 0);
            Controls.Add(grid); Controls.Add(errors); Controls.Add(summary); Controls.Add(toolbar); Controls.Add(launchConfig);
            launchTimer.Interval = 1200; launchTimer.Tick += delegate { LaunchNext(); };
            poll.Interval = 800; poll.Tick += delegate { RefreshStatuses(); };
            powerTimer.Interval = 120; powerTimer.Tick += delegate { ApplyNextPower(); };
            LoadAccounts(); poll.Start();
            FormClosing += OnClosing;
        }
        static void AddButton(FlowLayoutPanel panel, Button button, string title, EventHandler action)
        {
            button.Text = title; button.AutoSize = true; button.Height = 34; button.Click += action; panel.Controls.Add(button);
        }
        void BuildConfiguration()
        {
            launchConfig.Name = "launchConfig"; launchConfig.Dock = DockStyle.Top; launchConfig.Height = 208;
            launchConfig.Padding = new Padding(8); launchConfig.ColumnCount = 3; launchConfig.RowCount = 6;
            launchConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            launchConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            launchConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            for (int row = 0; row < 6; row++) launchConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            AddPathRow("Emulator:", emulatorPath, 0); AddPathRow("Game:", gamePath, 1);
            launchConfig.Controls.Add(new Label { Text = "Kích thước tab:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            var sizes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            sizes.Controls.Add(tabWidth); sizes.Controls.Add(new Label { Text = "×", AutoSize = true, Margin = new Padding(4, 5, 4, 0) });
            sizes.Controls.Add(tabHeight);
            sizes.Controls.Add(new Label { Text = "MaxTab:", AutoSize = true, Margin = new Padding(16, 5, 3, 0) }); sizes.Controls.Add(maxTab);
            autoLogin.Margin = new Padding(20, 5, 0, 0); sizes.Controls.Add(autoLogin);
            sizes.Controls.Add(new Label { Text = "Áp dụng khi mở tab mới", AutoSize = true, Margin = new Padding(20, 5, 0, 0) });
            launchConfig.Controls.Add(sizes, 1, 2);
            var save = new Button { Name = "saveConfig", Text = "Lưu cấu hình", Dock = DockStyle.Fill, AutoSize = true };
            save.Click += delegate { SaveConfiguration(true); }; launchConfig.Controls.Add(save, 2, 2);
            launchConfig.Controls.Add(new Label { Text = "Thống kê:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
            var stats = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            showUnder8.Margin = new Padding(3, 5, 12, 0); showTrackedItems.Margin = new Padding(3, 5, 12, 0);
            stats.Controls.Add(showUnder8); stats.Controls.Add(showTrackedItems);
            stats.Controls.Add(new Label { Text = "ID:", AutoSize = true, Margin = new Padding(0, 5, 3, 0) }); stats.Controls.Add(trackedItemIds);
            stats.Controls.Add(new Label { Text = "Ví dụ: 123,456", AutoSize = true, Margin = new Padding(8, 5, 0, 0) });
            launchConfig.Controls.Add(stats, 1, 3); launchConfig.SetColumnSpan(stats, 2);
            launchConfig.Controls.Add(new Label { Text = "Hiệu năng:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
            var performance = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            vpsLight.Margin = new Padding(3, 5, 18, 0); performance.Controls.Add(vpsLight);
            performance.Controls.Add(new Label { Text = "Heap Java/tab (MB):", AutoSize = true, Margin = new Padding(0, 5, 3, 0) }); performance.Controls.Add(javaHeapMb);
            performance.Controls.Add(new Label { Text = "VPS nhẹ: mở cách 5 giây, thống kê mỗi 5 giây", AutoSize = true, Margin = new Padding(12, 5, 0, 0) });
            launchConfig.Controls.Add(performance, 1, 4); launchConfig.SetColumnSpan(performance, 2);
            launchConfig.Controls.Add(new Label { Text = "Auto:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
            var autoRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            autoSettings.Click += delegate { ShowAutoSettings(); };
            autoRow.Controls.Add(autoSettings); autoRow.Controls.Add(autoSummary);
            launchConfig.Controls.Add(autoRow, 1, 5); launchConfig.SetColumnSpan(autoRow, 2);
        }
        void ApplyStatisticsVisibility()
        {
            grid.Columns["under8"].Visible = showUnder8.Checked; grid.Columns["items"].Visible = showTrackedItems.Checked;
        }
        void AddPathRow(string title, TextBox input, int row)
        {
            launchConfig.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            input.Dock = DockStyle.Fill; launchConfig.Controls.Add(input, 1, row);
            var browse = new Button { Text = "Duyệt", Dock = DockStyle.Fill };
            browse.Click += delegate {
                using (var dialog = new OpenFileDialog { Title = "Chọn " + title.TrimEnd(':'), Filter = "File Java (*.jar)|*.jar", CheckFileExists = true }) {
                    if (File.Exists(input.Text)) { dialog.InitialDirectory = Path.GetDirectoryName(input.Text); dialog.FileName = Path.GetFileName(input.Text); }
                    else dialog.InitialDirectory = root;
                    if (dialog.ShowDialog(this) == DialogResult.OK) input.Text = dialog.FileName;
                }
            };
            launchConfig.Controls.Add(browse, 2, row);
        }
        void ShowConfiguration()
        {
            emulatorPath.Text = settings.EmulatorPath; gamePath.Text = settings.GamePath;
            tabWidth.Value = settings.Width; tabHeight.Value = settings.Height; autoLogin.Checked = settings.AutoLogin;
            maxTab.Value = settings.MaxTab;
            vpsLight.Checked = settings.VpsLight; javaHeapMb.Value = settings.JavaHeapMb;
            launchTimer.Interval = settings.LaunchDelayMs; poll.Interval = settings.PollIntervalMs;
            showUnder8.Checked = settings.ShowUnder8; showTrackedItems.Checked = settings.ShowTrackedItems;
            trackedItemIds.Text = string.Join(",", settings.TrackedItemIds); ApplyStatisticsVisibility();
        }
        bool SaveConfiguration(bool showMessage)
        {
            if (pending.Count > 0) return false;
            try {
                AppSettings.SaveLaunch(root, emulatorPath.Text, gamePath.Text, (int)tabWidth.Value, (int)tabHeight.Value, autoLogin.Checked, showUnder8.Checked, showTrackedItems.Checked, trackedItemIds.Text, (int)maxTab.Value, vpsLight.Checked, (int)javaHeapMb.Value);
                settings = AppSettings.Load(root); ShowConfiguration(); RefreshStatuses();
                if (showMessage) errors.Text = "Đã lưu cấu hình. Phiên bản và kích thước áp dụng cho tab mở mới; đóng rồi mở lại tab cũ để áp dụng.";
                return true;
            } catch (Exception ex) { errors.Text = "Không lưu được cấu hình: " + ex.Message; return false; }
        }
        void LoadAccounts()
        {
            if (pending.Count > 0) return;
            try {
                settings = AppSettings.Load(root);
                ShowConfiguration();
                if (registry == null) registry = new TabRegistry(Path.Combine(root, "data", "accounts", "tabs.xml"));
                string[] servers = File.ReadAllLines(Path.Combine(root, "servers.txt"), System.Text.Encoding.UTF8).Select(s => s.Trim().TrimStart('\uFEFF')).Where(s => s.Length > 0 && !s.StartsWith("#")).ToArray();
                if (servers.Length == 0 || servers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != servers.Length) throw new InvalidDataException("servers.txt trống hoặc có server trùng nhau.");
                var result = AccountParser.Parse(File.ReadAllLines(Path.Combine(root, "accounts.txt"), System.Text.Encoding.UTF8), servers);
                // Removed accounts with a live window remain visible so they can be closed.
                accounts.Clear(); accounts.AddRange(result.Accounts);
                foreach (var tab in running.Values) if (!accounts.Any(a => a.Key == tab.Account.Key)) accounts.Add(tab.Account);
                grid.Rows.Clear(); rowsByKey.Clear();
                foreach (var account in accounts) {
                    int id = registry.Find(account.Key);
                    int row = grid.Rows.Add(false, account.Username, account.Server, grid.Rows.Count.ToString(), "Chưa mở", "Chưa vào game");
                    SetCell(grid.Rows[row], "auto", "Chưa mở");
                    grid.Rows[row].Tag = account;
                    rowsByKey.Add(account.Key, grid.Rows[row]);
                    LoadSaved(account, id); RenderInformation(account.Key, null, true);
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
            if (!SaveConfiguration(false)) return;
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
                var options = new List<string> { "-Xmx" + settings.JavaHeapMb + "m" };
                if (settings.VpsLight) options.AddRange(new[] { "-Xms16m", "-XX:+UseSerialGC" });
                var arguments = options.Concat(new[] { "-cp", Path.Combine(root, "account-bridge.jar") + ";" + settings.EmulatorPath, "AccountBootstrap", settings.GamePath, home, account.ServerIndex.ToString(), id.ToString(), settings.Width.ToString(), settings.Height.ToString(), settings.AutoLogin.ToString().ToLowerInvariant(), status });
                var start = new ProcessStartInfo(settings.JavaPath, string.Join(" ", arguments.Select(LaunchArguments.Quote))) {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                start.EnvironmentVariables["QLTK_ACCOUNT_USER"] = account.Username;
                start.EnvironmentVariables["QLTK_ACCOUNT_PASS"] = account.Password;
                start.EnvironmentVariables["QLTK_STATISTICS_FILE"] = Path.Combine(root, "settings.xml");
                start.EnvironmentVariables["QLTK_VPS_LIGHT"] = settings.VpsLight.ToString().ToLowerInvariant();
                string autoSession = Guid.NewGuid().ToString("N");
                start.EnvironmentVariables["QLTK_AUTO_SESSION"] = autoSession;
                var process = new Process { StartInfo = start };
                // Discard emulator output, which may include sensitive game messages.
                process.OutputDataReceived += delegate { }; process.ErrorDataReceived += delegate { };
                try { if (!process.Start()) throw new IOException("Java không khởi động."); process.BeginOutputReadLine(); process.BeginErrorReadLine(); }
                catch { process.Dispose(); throw; }
                running.Add(account.Key, new RunningTab { Account = account, Id = id, Process = process, Home = home, Width = settings.Width, Height = settings.Height, Power = new ProcessSleep(process), AutoSession = autoSession });
            } catch (Exception ex) {
                errors.Text += Environment.NewLine + "Dòng " + account.Line + ": không mở được tab — " + ex.Message;
                SetRowStatus(account.Key, "Lỗi mở tab");
            } finally { if (pending.Count == 0) launchTimer.Stop(); SetButtons(); RefreshStatuses(); }
        }
        void SetButtons() {
            bool idle = pending.Count == 0 && powerQueue.Count == 0;
            bool ready = !failedConfig && idle; openAll.Enabled = ready; openSelected.Enabled = ready; reload.Enabled = idle; launchConfig.Enabled = idle;
            foreach (var button in powerButtons) button.Enabled = idle && running.Count > 0;
            foreach (var button in arrangeButtons) button.Enabled = idle && running.Count > 0;
            autoSettings.Enabled = idle && running.Count > 0;
        }
        void QueuePower(bool sleep, bool all)
        {
            if (powerQueue.Count > 0 || pending.Count > 0) { errors.Text = "Đang mở tab hoặc xử lý ngủ/thức; đợi hoàn tất rồi thử lại."; return; }
            RefreshStatuses();
            var keys = all ? running.Keys.ToList() : SelectedAccounts().Select(a => a.Key).ToList();
            foreach (string key in keys) { RunningTab tab; if (running.TryGetValue(key, out tab)) powerQueue.Enqueue(new PowerRequest { Tab = tab, Sleep = sleep }); }
            if (powerQueue.Count == 0) { errors.Text = "Không có tab đang chạy phù hợp với lựa chọn."; return; }
            errors.Text = (sleep ? "Đang cho ngủ " : "Đang đánh thức ") + powerQueue.Count + " tab…";
            SetButtons(); ApplyNextPower(); if (powerQueue.Count > 0) powerTimer.Start();
        }
        void ApplyNextPower()
        {
            if (powerQueue.Count == 0) { powerTimer.Stop(); SetButtons(); return; }
            var request = powerQueue.Dequeue(); var tab = request.Tab;
            try {
                RunningTab current;
                if (!running.TryGetValue(tab.Account.Key, out current) || !object.ReferenceEquals(current, tab) || tab.Process.HasExited) return;
                if (request.Sleep && !tab.Power.IsSleeping) { try { CaptureSnapshot(tab); } catch (IOException) { } }
                string error;
                bool success = request.Sleep ? tab.Power.Sleep(out error) : tab.Power.Wake(out error);
                if (!success) errors.AppendText(Environment.NewLine + "Tab " + DisplayNumber(tab.Account) + ": " + error);
            } catch (Exception ex) { errors.AppendText(Environment.NewLine + "Tab " + DisplayNumber(tab.Account) + ": " + ex.Message); }
            finally { if (powerQueue.Count == 0) powerTimer.Stop(); RefreshStatuses(); SetButtons(); }
        }
        static void SetCell(DataGridViewRow row, string column, string value) { if (!object.Equals(row.Cells[column].Value, value)) row.Cells[column].Value = value; }
        void SetRowStatus(string key, string value) { DataGridViewRow row; if (rowsByKey.TryGetValue(key, out row)) SetCell(row, "status", value); }
        void SetRowAuto(string key, string value) { DataGridViewRow row; if (rowsByKey.TryGetValue(key, out row)) SetCell(row, "auto", value); }
        static XElement ReadAutoXml(string xml)
        {
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 }))
                return XElement.Load(reader);
        }
        void RefreshAuto(RunningTab tab)
        {
            try {
                var state = new FileInfo(Path.Combine(tab.Home, "auto-state.xml"));
                if (state.Exists && state.Length <= 262144 && (tab.AutoStateStamp != state.LastWriteTimeUtc.Ticks || tab.AutoStateLength != state.Length)) {
                    string xml = File.ReadAllText(state.FullName, Encoding.UTF8);
                    var root = ReadAutoXml(xml);
                    if ((string)root.Attribute("session") == tab.AutoSession) {
                        if ((string)root.Attribute("state") == "UNSUPPORTED") { tab.AutoSnapshot = null; tab.AutoStatus = "Phiên bản game chưa hỗ trợ Auto"; }
                        else if ((string)root.Attribute("state") != "READY") { tab.AutoSnapshot = null; tab.AutoStatus = "Chưa vào game"; }
                        else {
                            tab.AutoSnapshot = AutoSnapshot.Parse(xml);
                            if (tab.AutoStatus == "Chưa đọc Auto" || tab.AutoStatus == "Lỗi đọc Auto") tab.AutoStatus = "Đã đọc Auto";
                        }
                    }
                    tab.AutoStateStamp = state.LastWriteTimeUtc.Ticks; tab.AutoStateLength = state.Length;
                }
                if (tab.AutoPending > 0) {
                    string path = Path.Combine(tab.Home, "auto-result.xml");
                    if (File.Exists(path) && new FileInfo(path).Length <= 65536) {
                        var result = ReadAutoXml(File.ReadAllText(path, Encoding.UTF8));
                        if (result.Name == "AutoResult" && (string)result.Attribute("session") == tab.AutoSession && (string)result.Attribute("id") == tab.AutoPending.ToString(CultureInfo.InvariantCulture)) {
                            tab.AutoStatus = (string)result.Attribute("state") == "OK" ? "Đã áp dụng Auto" : "Lỗi Auto: " + ((string)result.Attribute("message") ?? "game từ chối");
                            tab.AutoPending = 0;
                            autoSummary.Text = tab.Account.Username + ": " + tab.AutoStatus;
                        }
                    }
                    if (tab.AutoPending > 0) {
                        if (tab.Power.IsSleeping) { tab.AutoPendingUtc = DateTime.UtcNow; tab.AutoStatus = "Auto chờ thức"; }
                        else if (DateTime.UtcNow - tab.AutoPendingUtc > TimeSpan.FromSeconds(30)) { tab.AutoStatus = "Auto chưa xác nhận — thử lại"; tab.AutoPending = 0; autoSummary.Text = tab.Account.Username + ": " + tab.AutoStatus; }
                        else tab.AutoStatus = "Đang áp dụng Auto…";
                    }
                }
            } catch (IOException) { tab.AutoStatus = "Lỗi đọc Auto"; }
            catch (UnauthorizedAccessException) { tab.AutoStatus = "Lỗi đọc Auto"; }
            catch (System.Xml.XmlException) { tab.AutoStatus = "Lỗi đọc Auto"; }
            catch (InvalidDataException) { tab.AutoStatus = "Lỗi đọc Auto"; }
            SetRowAuto(tab.Account.Key, tab.AutoStatus);
        }
        void ShowAutoSettings()
        {
            RefreshStatuses();
            var selected = SelectedAccounts();
            if (selected.Count == 0) { errors.Text = "Chọn ít nhất một tab để xem Auto."; return; }
            var ready = new List<Account>(); var snapshots = new List<AutoSnapshot>();
            foreach (var account in selected) {
                RunningTab tab;
                if (running.TryGetValue(account.Key, out tab) && tab.AutoSnapshot != null && !tab.Process.HasExited) { ready.Add(account); snapshots.Add(tab.AutoSnapshot); }
                else SetRowAuto(account.Key, running.ContainsKey(account.Key) ? "Chưa đọc được Auto" : "Tab chưa mở");
            }
            if (ready.Count == 0) { errors.Text = "Các tab đã chọn chưa có trạng thái Auto. Chờ game vào nhân vật rồi thử lại."; return; }
            using (var dialog = new AutoSettingsDialog(snapshots)) {
                dialog.Text += " — " + ready.Count + " tab";
                if (dialog.ShowDialog(this) == DialogResult.OK) ApplyAuto(ready, dialog.ChangedFields);
            }
            if (ready.Count < selected.Count) errors.AppendText(Environment.NewLine + (selected.Count - ready.Count) + " tab chưa sẵn sàng nên chưa nhận lệnh Auto.");
        }
        void ApplyAuto(List<Account> selected, IDictionary<string,string> changes)
        {
            if (changes == null || changes.Count == 0) return;
            int sent = 0, skipped = 0;
            foreach (var account in selected) {
                RunningTab tab;
                if (!running.TryGetValue(account.Key, out tab) || tab.Process.HasExited || tab.AutoSnapshot == null || tab.AutoPending > 0) { skipped++; continue; }
                try {
                    long id = ++tab.AutoSequence;
                    string xml = AutoEdit.Build(tab.AutoSession, id, changes);
                    string target = Path.Combine(tab.Home, "auto-command.xml");
                    string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try {
                        File.WriteAllText(temporary, xml, new UTF8Encoding(false));
                        if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
                    } finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    tab.AutoPending = id; tab.AutoPendingUtc = DateTime.UtcNow;
                    tab.AutoStatus = tab.Power.IsSleeping ? "Auto chờ thức" : "Đang áp dụng Auto…";
                    SetRowAuto(account.Key, tab.AutoStatus); sent++;
                } catch (ArgumentException ex) { tab.AutoStatus = "Auto không hợp lệ: " + ex.Message; SetRowAuto(account.Key, tab.AutoStatus); skipped++; }
                catch (IOException) { tab.AutoStatus = "Không gửi được Auto"; SetRowAuto(account.Key, tab.AutoStatus); skipped++; }
                catch (UnauthorizedAccessException) { tab.AutoStatus = "Không gửi được Auto"; SetRowAuto(account.Key, tab.AutoStatus); skipped++; }
            }
            errors.Text = "Đã gửi Auto tới " + sent + " tab; " + skipped + " tab chưa nhận lệnh. Chờ game xác nhận ở cột Auto.";
            autoSummary.Text = sent + " tab đang áp dụng • " + skipped + " tab bỏ qua";
        }
        void LoadSaved(Account account, int id)
        {
            if (saved.ContainsKey(account.Key)) return;
            try {
                LastLoginRecord record = historyStore.Load(account.Key);
                if (record == null && id > 0 && !running.ContainsKey(account.Key)) {
                    string legacy = Path.Combine(root, "data", "accounts", "tab_" + id, "character.xml");
                    if (File.Exists(legacy) && new FileInfo(legacy).Length <= 1048576) {
                        string xml = File.ReadAllText(legacy);
                        if (CharacterSnapshot.Parse(xml).IsReady) record = historyStore.Save(account.Key, xml, LoginTime(xml), File.GetLastWriteTimeUtc(legacy), null);
                    }
                }
                if (record != null) saved[account.Key] = record;
            } catch (IOException) { } catch (System.Xml.XmlException) { } catch (UnauthorizedAccessException) { }
        }
        static DateTime? LoginTime(string xml)
        {
            string value = (string)XElement.Parse(xml).Attribute("loginUtc");
            if (value == null) return null;
            DateTime time;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time)) throw new InvalidDataException("Thời điểm đăng nhập không hợp lệ.");
            return time;
        }
        void CaptureSnapshot(RunningTab tab)
        {
            var info = new FileInfo(Path.Combine(tab.Home, "character.xml"));
            if (info.Exists && (tab.SnapshotStamp != info.LastWriteTimeUtc.Ticks || tab.SnapshotLength != info.Length)) {
                try {
                    string xml = File.ReadAllText(info.FullName);
                    var snapshot = CharacterSnapshot.Parse(xml);
                    tab.Snapshot = snapshot; tab.CharacterInfo = snapshot.Display;
                    tab.SnapshotStamp = info.LastWriteTimeUtc.Ticks; tab.SnapshotLength = info.Length;
                    if (snapshot.IsReady) {
                        DateTime? login = LoginTime(xml);
                        if (login.HasValue) tab.LoginUtc = login;
                        else if (!tab.LoginUtc.HasValue) tab.LoginUtc = DateTime.UtcNow;
                        tab.SnapshotXml = xml; tab.HistoryDirty = true; tab.HistoryUpdatedUtc = info.LastWriteTimeUtc;
                    }
                } catch (Exception ex) {
                    if (ex is System.Xml.XmlException || ex is InvalidDataException) { tab.Snapshot = null; tab.CharacterInfo = "Chưa đọc được thông tin NV"; }
                    else throw;
                }
            }
            if (tab.HistoryDirty) {
                LastLoginRecord previous; saved.TryGetValue(tab.Account.Key, out previous);
                try {
                    saved[tab.Account.Key] = historyStore.Save(tab.Account.Key, tab.SnapshotXml, tab.LoginUtc, tab.HistoryUpdatedUtc, previous);
                    tab.HistoryDirty = false;
                } catch (IOException) { errors.Text = "Không lưu được lịch sử account. Kiểm tra quyền ghi và dung lượng ổ đĩa."; }
                catch (UnauthorizedAccessException) { errors.Text = "Không có quyền ghi lịch sử account."; }
            }
        }
        void RenderInformation(string key, CharacterSnapshot current, bool useSaved)
        {
            DataGridViewRow row; if (!rowsByKey.TryGetValue(key, out row)) return;
            LastLoginRecord record; saved.TryGetValue(key, out record);
            bool stored = useSaved && record != null;
            var snapshot = stored ? record.Character : current;
            string prefix = stored ? "Đã lưu | " : "";
            SetCell(row, "character", prefix + (snapshot == null ? "Chưa vào game" : snapshot.Display));
            if (showUnder8.Checked) SetCell(row, "under8", prefix + (snapshot == null ? "Chưa đọc được trang bị" : snapshot.EquipmentDisplay));
            if (showTrackedItems.Checked) SetCell(row, "items", prefix + (settings.TrackedItemIds.Length == 0 ? "Nhập ID rồi lưu cấu hình" : stored ? record.InventoryDisplay(settings.TrackedItemIds) : snapshot == null ? "Chưa đọc được vật phẩm" : snapshot.InventoryDisplay(settings.TrackedItemIds)));
            SetCell(row, "historyTime", record == null ? "Chưa có lịch sử" : record.TimeDisplay);
        }
        void RefreshStatuses()
        {
            foreach (var pair in running.ToList()) {
                var tab = pair.Value;
                try {
                    if (!tab.Power.IsSleeping) CaptureSnapshot(tab);
                    if (tab.Process.HasExited) { SetRowStatus(pair.Key, tab.Process.ExitCode == 0 ? "Đã đóng" : "Lỗi khởi động (Java " + tab.Process.ExitCode + ")"); SetRowAuto(pair.Key, "Tab đã đóng"); RenderInformation(pair.Key, null, true); tab.Power.Dispose(); tab.Process.Dispose(); running.Remove(pair.Key); continue; }
                    RefreshAuto(tab);
                    if (tab.Power.IsSleeping) { SetRowStatus(pair.Key, tab.Power.FullySleeping ? "Đang ngủ" : "Ngủ một phần — bấm Thức lại"); RenderInformation(pair.Key, null, true); continue; }
                    if (!tab.Positioned) { tab.Process.Refresh(); if (tab.Process.MainWindowHandle != IntPtr.Zero) { Position(tab); tab.Positioned = true; } }
                    else UpdateWindowTitle(tab);
                    string status = Path.Combine(tab.Home, "bridge.status");
                    var statusInfo = new FileInfo(status);
                    if (statusInfo.Exists && (tab.StatusStamp != statusInfo.LastWriteTimeUtc.Ticks || tab.StatusLength != statusInfo.Length)) {
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
                        tab.StatusStamp = statusInfo.LastWriteTimeUtc.Ticks; tab.StatusLength = statusInfo.Length;
                    }
                    SetRowStatus(pair.Key, tab.Status);
                    string characterFile = Path.Combine(tab.Home, "character.xml");
                    var characterInfo = new FileInfo(characterFile);
                    bool stale = !characterInfo.Exists || DateTime.UtcNow - characterInfo.LastWriteTimeUtc > TimeSpan.FromSeconds(15);
                    RenderInformation(pair.Key, tab.Snapshot, stale || tab.Snapshot == null || !tab.Snapshot.IsReady);
                } catch (IOException) { } catch (InvalidOperationException) { }
            }
            foreach (var account in accounts) if (!running.ContainsKey(account.Key)) RenderInformation(account.Key, null, true);
            foreach (var account in pending) SetRowStatus(account.Key, "Đang chờ mở…");
            foreach (DataGridViewRow row in grid.Rows) SetCell(row, "tab", row.Index.ToString());
            string summaryText = accounts.Count + " tài khoản • " + running.Count + " tab đang chạy • " + running.Values.Count(t => t.Power.IsSleeping) + " đang ngủ • " + pending.Count + " đang chờ • MaxTab: " + (settings == null ? "—" : settings.MaxTab.ToString());
            if (summary.Text != summaryText) summary.Text = summaryText;
            SetButtons();
        }
        int DisplayNumber(Account account)
        {
            DataGridViewRow row;
            return rowsByKey.TryGetValue(account.Key, out row) ? row.Index : accounts.FindIndex(a => a.Key == account.Key);
        }
        Dictionary<string, Point> WindowPositions()
        {
            if (windowPositions != null) return windowPositions;
            windowPositions = new Dictionary<string, Point>();
            string path = Path.Combine(root, "data", "accounts", "layout.xml");
            try {
                if (!File.Exists(path) || new FileInfo(path).Length > 1048576) return windowPositions;
                var document = XDocument.Load(path);
                if (document.Root == null || document.Root.Name != "Layout") return windowPositions;
                foreach (var entry in document.Root.Elements("Tab")) {
                    string key = (string)entry.Attribute("key"); int x, y;
                    if (key != null && key.Length == 64 && key.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) && int.TryParse((string)entry.Attribute("x"), out x) && int.TryParse((string)entry.Attribute("y"), out y) && Math.Abs((long)x) <= 100000 && Math.Abs((long)y) <= 100000) windowPositions[key] = new Point(x, y);
                }
            } catch (IOException) { } catch (System.Xml.XmlException) { } catch (UnauthorizedAccessException) { }
            return windowPositions;
        }
        void SaveWindowPositions()
        {
            string path = Path.Combine(root, "data", "accounts", "layout.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var document = new XDocument(new XElement("Layout", WindowPositions().OrderBy(p => p.Key).Select(p => new XElement("Tab", new XAttribute("key", p.Key), new XAttribute("x", p.Value.X), new XAttribute("y", p.Value.Y)))));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { document.Save(temporary); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        void ArrangeTabs(bool all)
        {
            if (pending.Count > 0 || powerQueue.Count > 0) return;
            RefreshStatuses();
            var selected = all ? accounts.ToList() : SelectedAccounts();
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            int x = screen.Left, y = screen.Top, rowHeight = 0, moved = 0, unavailable = 0;
            bool overlap = false;
            foreach (var account in selected.OrderBy(DisplayNumber)) {
                RunningTab tab; if (!running.TryGetValue(account.Key, out tab)) continue;
                try {
                    tab.Process.Refresh(); IntPtr handle = tab.Process.MainWindowHandle; NativeRect bounds;
                    if (handle == IntPtr.Zero || !GetWindowRect(handle, out bounds) || IsIconic(handle)) { unavailable++; continue; }
                    int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
                    if (width <= 0 || height <= 0) { unavailable++; continue; }
                    if (x > screen.Left && (long)x + width > screen.Right) { x = screen.Left; y += rowHeight; rowHeight = 0; }
                    int placedX = Math.Max(screen.Left, Math.Min(x, screen.Right - width));
                    int placedY = Math.Max(screen.Top, Math.Min(y, screen.Bottom - height));
                    if (placedY != y || width > screen.Width || height > screen.Height) overlap = true;
                    // NOSIZE, NOZORDER, NOACTIVATE, ASYNCWINDOWPOS: paused UI threads stay paused.
                    if (!SetWindowPos(handle, IntPtr.Zero, placedX, placedY, 0, 0, 0x4015)) { unavailable++; continue; }
                    WindowPositions()[account.Key] = new Point(placedX, placedY); tab.Positioned = true; moved++;
                    x = placedX + width; rowHeight = Math.Max(rowHeight, height);
                } catch (InvalidOperationException) { unavailable++; }
            }
            errors.Text = moved == 0 ? "Không có cửa sổ tab phù hợp để sắp xếp." : "Đã gửi sắp xếp " + moved + " tab theo thứ tự hiển thị. Tab đang ngủ nhận vị trí khi được đánh thức.";
            if (unavailable > 0) errors.AppendText(Environment.NewLine + unavailable + " cửa sổ chưa sẵn sàng hoặc đang thu nhỏ; hiện tab rồi thử lại.");
            if (overlap) errors.AppendText(Environment.NewLine + "Màn hình không đủ chỗ; một số cửa sổ có thể chồng nhau.");
            if (moved > 0) {
                try { SaveWindowPositions(); }
                catch (IOException ex) { errors.AppendText(Environment.NewLine + "Không lưu được vị trí tab: " + ex.Message); }
                catch (UnauthorizedAccessException ex) { errors.AppendText(Environment.NewLine + "Không lưu được vị trí tab: " + ex.Message); }
            }
        }
        void UpdateWindowTitle(RunningTab tab)
        {
            string title = "Tab " + DisplayNumber(tab.Account) + " — " + tab.Account.Username + " — " + tab.Account.Server;
            if (title == tab.WindowTitle) return;
            IntPtr handle = tab.Process.MainWindowHandle;
            if (handle != IntPtr.Zero && SetWindowText(handle, title)) tab.WindowTitle = title;
        }
        void Position(RunningTab tab)
        {
            IntPtr handle = tab.Process.MainWindowHandle;
            UpdateWindowTitle(tab);
            int number = DisplayNumber(tab.Account);
            int width = tab.Width + 20, height = tab.Height + 90;
            Rectangle screen = Screen.PrimaryScreen.WorkingArea; int columns = Math.Max(1, screen.Width / width);
            int x = screen.Left + (number % columns) * width;
            int y = screen.Top + ((number / columns) * height) % Math.Max(1, screen.Height - height + 1);
            string layout = Path.Combine(root, "layout.xml");
            if (File.Exists(layout)) {
                try { var element = XDocument.Load(layout).Root.Elements("Tab").FirstOrDefault(e => (int?)e.Attribute("no") == tab.Id); if (element != null) { x = (int)element.Attribute("x"); y = (int)element.Attribute("y"); } } catch { }
            }
            Point savedPosition;
            if (WindowPositions().TryGetValue(tab.Account.Key, out savedPosition)) { x = savedPosition.X; y = savedPosition.Y; screen = Screen.FromPoint(savedPosition).WorkingArea; }
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - width)); y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - height));
            SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x4014);
        }
        void ShowSelected()
        {
            foreach (var account in SelectedAccounts()) { RunningTab tab; if (running.TryGetValue(account.Key, out tab) && !tab.Process.HasExited) { tab.Process.Refresh(); IntPtr handle = tab.Process.MainWindowHandle; if (handle == IntPtr.Zero) continue; ShowWindowAsync(handle, 9); if (!tab.Power.IsSleeping) SetForegroundWindow(handle); } }
        }
        void CloseSelected()
        {
            foreach (var account in SelectedAccounts()) {
                RunningTab tab; if (!running.TryGetValue(account.Key, out tab) || tab.Process.HasExited) continue;
                var remaining = powerQueue.Where(request => !object.ReferenceEquals(request.Tab, tab)).ToArray();
                powerQueue.Clear(); foreach (var request in remaining) powerQueue.Enqueue(request);
                string error;
                if (!tab.Power.Wake(out error)) { errors.AppendText(Environment.NewLine + "Tab " + DisplayNumber(tab.Account) + ": " + error); continue; }
                tab.Process.CloseMainWindow();
            }
            if (powerQueue.Count == 0) powerTimer.Stop(); RefreshStatuses();
        }
        void OnClosing(object sender, FormClosingEventArgs args)
        {
            if (running.Count > 0 && MessageBox.Show("Đóng QLTK và các tab game đang chạy?", "QLTK NST", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { args.Cancel = true; return; }
            launchTimer.Stop(); poll.Stop(); powerTimer.Stop(); pending.Clear(); powerQueue.Clear();
            foreach (var tab in running.Values) {
                string error; tab.Power.Wake(out error);
                try { CaptureSnapshot(tab); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { if (!tab.Process.HasExited) { tab.Process.CloseMainWindow(); if (!tab.Process.WaitForExit(1500)) tab.Process.Kill(); } } catch { }
                tab.Power.Dispose(); tab.Process.Dispose();
            }
        }
        public int LoadedAccountCount { get { return accounts.Count; } }
        public int RunningCount { get { return running.Count; } }
        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                launchTimer.Stop(); poll.Stop(); powerTimer.Stop(); launchTimer.Dispose(); poll.Dispose(); powerTimer.Dispose();
                foreach (var tab in running.Values) { try { tab.Power.Dispose(); } catch (InvalidOperationException) { } }
            }
            base.Dispose(disposing);
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SetWindowText(IntPtr handle, string title);
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr handle, int command);
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
