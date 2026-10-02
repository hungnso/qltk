using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using QltkAccounts;
class UiTests {
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles();
        string root = Path.GetFullPath(args[0]);
        var servers = File.ReadAllLines(Path.Combine(root, "servers.txt"));
        int expected = AccountParser.Parse(File.ReadAllLines(Path.Combine(root, "accounts.txt")), servers).Accounts.Count;
        using (var form = new AccountManager(root)) {
            if (form.LoadedAccountCount != expected) throw new Exception("UI did not load accounts.txt.");
            if (form.RunningCount != 0) throw new Exception("Startup must not open game tabs.");
            var grid = form.Controls.OfType<DataGridView>().Single();
            if (!grid.Columns.Contains("character") || grid.Columns["character"].HeaderText != "Thông tin NV") throw new Exception("Character information column is missing.");
            if (grid.Columns.Cast<DataGridViewColumn>().Any(c => c.Name.ToLowerInvariant().Contains("pass"))) throw new Exception("UI exposes passwords.");
            var panel = form.Controls.OfType<FlowLayoutPanel>().Single();
            var reload = panel.Controls.OfType<Button>().Single(b => b.Text == "Đọc lại danh sách");
            Click(reload);
            if (form.LoadedAccountCount != expected || form.RunningCount != 0) throw new Exception("Reload should preserve list and not launch games.");
            Console.WriteLine("PASS: UI loads " + expected + " account(s), hides passwords, and starts zero tabs until a button is pressed.");
        }
        string fixture = Path.Combine(Path.GetTempPath(), "qltk-ui-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "jre", "bin"));
        File.Copy(Path.Combine(root, "tests", "FakeJava.exe"), Path.Combine(fixture, "jre", "bin", "javaw.exe"));
        File.WriteAllText(Path.Combine(fixture, "accounts.txt"), "fixture-a| fixture-pass |Bokken\nfixture-b| fixture-pass |Shuriken\n");
        File.WriteAllText(Path.Combine(fixture, "servers.txt"), "Bokken\nShuriken\n");
        foreach (string name in new[] { "game.jar", "MICRO_NST.jar", "account-bridge.jar" }) File.WriteAllText(Path.Combine(fixture, name), "fixture");
        Action<int> capacity = max => File.WriteAllText(Path.Combine(fixture, "settings.xml"), "<Settings><GamePath>game.jar</GamePath><MaxTab>" + max + "</MaxTab></Settings>");
        capacity(1);
        AccountManager manager = null;
        try {
            manager = new AccountManager(fixture);
            var config = manager.Controls.Find("launchConfig", true).SingleOrDefault();
            if (config == null) throw new Exception("Launch configuration controls are missing.");
            var game = (TextBox)config.Controls.Find("gamePath", true).Single();
            var width = (NumericUpDown)config.Controls.Find("tabWidth", true).Single();
            var height = (NumericUpDown)config.Controls.Find("tabHeight", true).Single();
            var auto = (CheckBox)config.Controls.Find("autoLogin", true).Single();
            var save = (Button)config.Controls.Find("saveConfig", true).Single();
            var maxTab = config.Controls.Find("maxTab", true).SingleOrDefault() as NumericUpDown;
            if (maxTab == null) throw new Exception("MaxTab editing control is missing.");
            if (maxTab.Value != 1) throw new Exception("MaxTab must load the saved capacity.");
            var light = config.Controls.Find("vpsLight", true).SingleOrDefault() as CheckBox;
            var heap = config.Controls.Find("javaHeapMb", true).SingleOrDefault() as NumericUpDown;
            if (light == null || heap == null) throw new Exception("VPS performance configuration is missing.");
            light.Checked = true; heap.Value = 128;
            var under8 = config.Controls.Find("showUnder8", true).SingleOrDefault() as CheckBox;
            var tracked = config.Controls.Find("showTrackedItems", true).SingleOrDefault() as CheckBox;
            var ids = config.Controls.Find("trackedItemIds", true).SingleOrDefault() as TextBox;
            if (under8 == null || tracked == null || ids == null) throw new Exception("Item statistics configuration is missing.");
            var statsGrid = manager.Controls.OfType<DataGridView>().Single();
            if (statsGrid.Rows[0].Cells["tab"].Value.ToString() != "0" || statsGrid.Rows[1].Cells["tab"].Value.ToString() != "1") throw new Exception("Account tab labels must start at zero before launching.");
            under8.Checked = true; tracked.Checked = true; ids.Text = "123,456,123";
            if (!statsGrid.Columns["under8"].Visible || !statsGrid.Columns["items"].Visible) throw new Exception("Statistics switches must show columns immediately.");
            if (game.Text != Path.Combine(fixture, "game.jar") || width.Value != 220 || height.Value != 240 || !auto.Checked) throw new Exception("Configuration must show saved values and defaults.");
            string configPath = Path.Combine(fixture, "settings.xml");
            string original = File.ReadAllText(configPath);
            game.Text = Path.Combine(fixture, "missing.jar");
            Click(save);
            if (File.ReadAllText(configPath) != original) throw new Exception("Invalid path must not overwrite settings.");
            File.WriteAllText(configPath, original.Replace("</Settings>", "<AutoNst>false</AutoNst><Custom value='keep'/></Settings>"));
            File.WriteAllText(Path.Combine(fixture, "version two.jar"), "fixture");
            game.Text = Path.Combine(fixture, "version two.jar"); width.Value = 320; height.Value = 400; auto.Checked = false;
            Click(save);
            var saved = AppSettings.Load(fixture);
            if (saved.GamePath != game.Text || saved.Width != 320 || saved.Height != 400 || saved.AutoLogin) throw new Exception("Configuration button must persist chosen version, dimensions and login flag.");
            if (!(bool)typeof(AppSettings).GetField("VpsLight").GetValue(saved) || (int)typeof(AppSettings).GetField("JavaHeapMb").GetValue(saved) != 128) throw new Exception("VPS mode and Java heap must be saved.");
            if (!File.ReadAllText(configPath).Contains("Custom") || !File.ReadAllText(configPath).Contains("AutoNst")) throw new Exception("Saving must preserve unrelated settings.");
            if (!File.ReadAllText(configPath).Contains("<TrackedItemIds>123,456</TrackedItemIds>")) throw new Exception("Item IDs must be saved without duplicates.");
            original = File.ReadAllText(configPath); ids.Text = "123,wrong"; Click(save);
            if (File.ReadAllText(configPath) != original) throw new Exception("Invalid item ID must not overwrite any configuration.");
            ids.Text = "123,456";
            manager.Show(); Application.DoEvents();
            using (var preview = new System.Drawing.Bitmap(manager.Width, manager.Height)) {
                manager.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, manager.Width, manager.Height));
                preview.Save(Path.Combine(root, "build", "launch-config-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            manager.Hide();
            var table = manager.Controls.OfType<DataGridView>().Single();
            var buttons = manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().ToList();
            table.Rows[0].Cells["chosen"].Value = true;
            Click(buttons.Single(b => b.Text == "Mở tài khoản đã chọn"));
            if (manager.RunningCount != 1) throw new Exception("Selected button must launch exactly one tab.");
            WaitMarkers(fixture, 1);
            if (!Directory.GetFiles(fixture, "launch.ok", SearchOption.AllDirectories).All(p => File.ReadAllText(p).Contains("version two.jar|320|400|false"))) throw new Exception("Saved configuration was not passed to launched game.");
            if (!Directory.GetFiles(fixture, "launch.ok", SearchOption.AllDirectories).All(p => File.ReadAllText(p).Contains("-Xmx128m") && File.ReadAllText(p).Contains("-XX:+UseSerialGC"))) throw new Exception("Light mode must pass chosen heap and Serial GC to Java.");
            Console.WriteLine("PASS: configuration loads, rejects invalid paths, preserves other settings, and passes selected version/dimensions/login to new tabs.");
            typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            var sessionsForCache = (IDictionary)typeof(AccountManager).GetField("running", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            object cachedTab = sessionsForCache.Values.Cast<object>().First();
            object firstSnapshot = cachedTab.GetType().GetField("Snapshot").GetValue(cachedTab);
            typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            if (!object.ReferenceEquals(firstSnapshot, cachedTab.GetType().GetField("Snapshot").GetValue(cachedTab))) throw new Exception("Unchanged snapshot must not be reparsed each UI poll.");
            if (!table.Rows[0].Cells["character"].Value.ToString().Contains("fixture-character") || !table.Rows[0].Cells["character"].Value.ToString().Contains("Rương: 99")) throw new Exception("Character snapshot is not displayed in UI.");
            if (!table.Rows[0].Cells["under8"].Value.ToString().Contains("Áo +6") || !table.Rows[0].Cells["items"].Value.ToString().Contains("Đá: 25")) throw new Exception("Item statistics must be shown for the correct account.");
            string snapshotFile = Directory.GetFiles(fixture, "character.xml", SearchOption.AllDirectories).Single();
            File.WriteAllText(snapshotFile, File.ReadAllText(snapshotFile).Replace("fixture-character", "fixture-next"));
            typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            if (!table.Rows[0].Cells["character"].Value.ToString().Contains("fixture-next")) throw new Exception("Changed snapshot must invalidate the cache.");
            var firstAccount = (Account)table.Rows[0].Tag;
            string historyFile = Path.Combine(fixture, "data", "accounts", "history", firstAccount.Key + ".xml");
            if (!File.Exists(historyFile)) throw new Exception("Last successful character information must be saved durably.");
            string historyContent = File.ReadAllText(historyFile), readyContent = File.ReadAllText(snapshotFile);
            if (historyContent.Contains("fixture-pass") || !historyContent.Contains("fixture-next")) throw new Exception("History must store character information without credentials.");
            File.WriteAllText(snapshotFile, "<Character state='WAITING'/>");
            typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            if (File.ReadAllText(historyFile) != historyContent || !table.Rows[0].Cells["character"].Value.ToString().Contains("Đã lưu") || !table.Rows[0].Cells["character"].Value.ToString().Contains("fixture-next")) throw new Exception("Waiting login must preserve and display last good history.");
            File.WriteAllText(snapshotFile, readyContent);
            under8.Checked = false; tracked.Checked = false;
            if (table.Columns["under8"].Visible || table.Columns["items"].Visible) throw new Exception("Statistics switches must hide columns immediately.");
            Click(buttons.Single(b => b.Text == "Mở tài khoản đã chọn"));
            if (manager.RunningCount != 1) throw new Exception("Second button press must not duplicate a tab.");
            Click(buttons.Single(b => b.Text == "Mở tất cả"));
            if (manager.RunningCount != 1) throw new Exception("Open all must respect MaxTab=1.");
            maxTab.Value = 2; Click(save);
            if (AppSettings.Load(fixture).MaxTab != 2 || !manager.Controls.OfType<Label>().Single().Text.Contains("MaxTab: 2")) throw new Exception("Saving MaxTab must update settings and summary immediately.");
            Click(buttons.Single(b => b.Text == "Mở tất cả"));
            if (manager.RunningCount != 2) throw new Exception("Open all should launch remaining account after capacity increases.");
            WaitMarkers(fixture, 2);
            var originalTabs = table.Rows.Cast<DataGridViewRow>().Select(r => r.Cells["tab"].Value.ToString()).ToArray();
            maxTab.Value = 1; Click(save);
            if (manager.RunningCount != 2 || !table.Rows.Cast<DataGridViewRow>().Select(r => r.Cells["tab"].Value.ToString()).SequenceEqual(originalTabs)) throw new Exception("Reducing MaxTab must preserve running tabs and their IDs.");
            Click(buttons.Single(b => b.Text == "Mở tất cả"));
            if (manager.RunningCount != 2) throw new Exception("Reducing MaxTab must not close or duplicate running tabs.");
            Console.WriteLine("PASS: MaxTab can be edited and saved in UI; summary updates immediately and existing tabs survive a reduced limit.");
            Console.WriteLine("PASS: selected/all buttons launch processes with correct credentials, prevent duplicates, and honor MaxTab.");
            var sleepSelected = buttons.SingleOrDefault(b => b.Text == "Ngủ đã chọn");
            var wakeSelected = buttons.SingleOrDefault(b => b.Text == "Thức đã chọn");
            var sleepAll = buttons.SingleOrDefault(b => b.Text == "Ngủ tất cả");
            var wakeAll = buttons.SingleOrDefault(b => b.Text == "Thức tất cả");
            if (sleepSelected == null || wakeSelected == null || sleepAll == null || wakeAll == null) throw new Exception("Bulk sleep/wake actions are missing.");
            var heartbeats = Directory.GetFiles(fixture, "heartbeat.txt", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
            if (heartbeats.Length != 2) throw new Exception("Heartbeat process fixtures are not ready.");
            Click(sleepSelected); Pump(500);
            if (!table.Rows[0].Cells["status"].Value.ToString().Contains("Đang ngủ") || table.Rows[1].Cells["status"].Value.ToString().Contains("Đang ngủ")) throw new Exception("Sleep selected must pause only the chosen account.");
            string sleepingBeat = ReadHeartbeat(heartbeats[0]), awakeBeat = ReadHeartbeat(heartbeats[1]); Pump(400);
            if (sleepingBeat != ReadHeartbeat(heartbeats[0]) || awakeBeat == ReadHeartbeat(heartbeats[1])) throw new Exception("Selected game process must freeze while unselected game continues.");
            Click(sleepSelected); Pump(200); Click(wakeSelected); Pump(500);
            if (sleepingBeat == ReadHeartbeat(heartbeats[0])) throw new Exception("One wake must resume game after repeated sleep requests.");
            Click(sleepAll); Pump(500);
            var allSleepingBeats = heartbeats.Select(ReadHeartbeat).ToArray(); Pump(400);
            if (!allSleepingBeats.SequenceEqual(heartbeats.Select(ReadHeartbeat))) throw new Exception("Sleep all must freeze all managed game processes.");
            Click(wakeAll); Pump(500);
            if (allSleepingBeats.Where((beat, i) => beat == ReadHeartbeat(heartbeats[i])).Any()) throw new Exception("Wake all must resume all managed game processes. Status: " + string.Join(" / ", table.Rows.Cast<DataGridViewRow>().Select(r => r.Cells["status"].Value.ToString())) + "; " + manager.Controls.OfType<TextBox>().Single().Text + "; beats " + string.Join(" / ", heartbeats.Select(ReadHeartbeat)) + "; before " + string.Join(" / ", allSleepingBeats));
            if (!table.Rows[0].Cells["character"].Value.ToString().Contains("fixture-next")) throw new Exception("Sleep/wake must preserve character history.");
            Click(sleepSelected); Pump(300); sleepingBeat = ReadHeartbeat(heartbeats[0]);
            Click(buttons.Single(b => b.Text == "Đóng tab đã chọn")); Pump(400);
            if (sleepingBeat == ReadHeartbeat(heartbeats[0])) throw new Exception("Closing a sleeping tab must wake it before requesting window close.");
            Console.WriteLine("PASS: selected/all sleep freezes owned processes, repeated sleep is safe, wake resumes heartbeats and history is retained.");
            StopTabs(manager); manager.Dispose(); manager = new AccountManager(fixture);
            var restoredGrid = manager.Controls.OfType<DataGridView>().Single();
            string registryPath = Path.Combine(fixture, "data", "accounts", "tabs.xml");
            string registryBefore = File.ReadAllText(registryPath);
            File.WriteAllText(Path.Combine(fixture, "accounts.txt"), "fixture-b| fixture-pass |Shuriken\nfixture-a| fixture-pass |Bokken\n");
            Click(manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b => b.Text == "Đọc lại danh sách"));
            if (restoredGrid.Rows[0].Cells["tab"].Value.ToString() != "0" || restoredGrid.Rows[1].Cells["tab"].Value.ToString() != "1" || !restoredGrid.Rows[1].Cells["character"].Value.ToString().Contains("fixture-next") || File.ReadAllText(registryPath) != registryBefore) throw new Exception("Reordering zero-based labels must preserve account history and persistent data IDs.");
            File.WriteAllText(Path.Combine(fixture, "accounts.txt"), "fixture-a| fixture-pass |Bokken\nfixture-b| fixture-pass |Shuriken\n");
            Click(manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b => b.Text == "Đọc lại danh sách"));
            Console.WriteLine("PASS: tab labels start at zero and reordering preserves history and persistent data IDs.");
            if (!restoredGrid.Rows[0].Cells["character"].Value.ToString().Contains("fixture-next") || !restoredGrid.Rows[0].Cells["character"].Value.ToString().Contains("Đã lưu") || !restoredGrid.Rows[0].Cells["historyTime"].Value.ToString().Contains("Login:")) throw new Exception("History and login/update times must survive closed tabs and restarting manager.");
            Console.WriteLine("PASS: last good history survives waiting login, closing game tabs and restarting manager without storing passwords.");
            var maxForQueue = (NumericUpDown)manager.Controls.Find("maxTab", true).Single(); maxForQueue.Value = 2;
            manager.Show(); Application.DoEvents();
            var allForQueue = manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(b => b.Text == "Mở tất cả");
            var elapsed = Stopwatch.StartNew(); Click(allForQueue);
            while (manager.RunningCount < 2 && elapsed.Elapsed.TotalSeconds < 12) { Application.DoEvents(); Thread.Sleep(25); }
            if (manager.RunningCount != 2 || elapsed.Elapsed.TotalSeconds < 4.5) throw new Exception("VPS mode must spread actual queued launches at least five seconds apart.");
            manager.Hide(); Console.WriteLine("PASS: VPS mode limits heap, caches unchanged snapshots, detects updated files and spreads queued launches over five seconds.");
        } finally {
            if (manager != null) {
                StopTabs(manager);
                manager.Dispose();
            }
            if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe fixture cleanup path.");
            Directory.Delete(fixture, true);
        }
    }
    static void StopTabs(AccountManager manager) {
        var sessions = (IDictionary)typeof(AccountManager).GetField("running", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
        foreach (DictionaryEntry entry in sessions) {
            var process = (Process)entry.Value.GetType().GetField("Process").GetValue(entry.Value);
            if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
        }
        typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
    }
    static void Click(Button button) { typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty }); }
    static void Pump(int milliseconds) { var watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(20); } }
    static string ReadHeartbeat(string path) { for (int attempt = 0; attempt < 10; attempt++) { try { using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))) return reader.ReadToEnd(); } catch (IOException) { Thread.Sleep(15); } } throw new Exception("Cannot read fixture heartbeat."); }
    static void WaitMarkers(string root, int expected) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            if (Directory.GetFiles(root, "launch.ok", SearchOption.AllDirectories).Length == expected) return;
            Thread.Sleep(50);
        }
        throw new Exception("Launched helper did not receive expected account arguments/environment.");
    }
}
