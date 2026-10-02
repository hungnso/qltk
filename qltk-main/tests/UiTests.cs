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
            if (!panel.Controls.OfType<Button>().Any(b => b.Text == "Sắp xếp tab")) throw new Exception("Tab arrangement button is missing.");
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
            var maxTabs = (NumericUpDown)config.Controls.Find("maxTabs", true).Single();
            var auto = (CheckBox)config.Controls.Find("autoLogin", true).Single();
            var save = (Button)config.Controls.Find("saveConfig", true).Single();
            var under8 = config.Controls.Find("showUnder8", true).SingleOrDefault() as CheckBox;
            var tracked = config.Controls.Find("showTrackedItems", true).SingleOrDefault() as CheckBox;
            var ids = config.Controls.Find("trackedItemIds", true).SingleOrDefault() as TextBox;
            if (under8 == null || tracked == null || ids == null) throw new Exception("Item statistics configuration is missing.");
            var statsGrid = manager.Controls.OfType<DataGridView>().Single();
            under8.Checked = true; tracked.Checked = true; ids.Text = "123,456,123";
            if (!statsGrid.Columns["under8"].Visible || !statsGrid.Columns["items"].Visible) throw new Exception("Statistics switches must show columns immediately.");
            if (game.Text != Path.Combine(fixture, "game.jar") || width.Value != 220 || height.Value != 240 || maxTabs.Value != 1 || !auto.Checked) throw new Exception("Configuration must show saved values and defaults.");
            string configPath = Path.Combine(fixture, "settings.xml");
            string original = File.ReadAllText(configPath);
            game.Text = Path.Combine(fixture, "missing.jar");
            Click(save);
            if (File.ReadAllText(configPath) != original) throw new Exception("Invalid path must not overwrite settings.");
            File.WriteAllText(configPath, original.Replace("</Settings>", "<AutoNst>false</AutoNst><Custom value='keep'/></Settings>"));
            File.WriteAllText(Path.Combine(fixture, "version two.jar"), "fixture");
            game.Text = Path.Combine(fixture, "version two.jar"); width.Value = 320; height.Value = 400; maxTabs.Value = 2; auto.Checked = false;
            Click(save);
            var saved = AppSettings.Load(fixture);
            if (saved.GamePath != game.Text || saved.Width != 320 || saved.Height != 400 || saved.MaxTab != 2 || saved.AutoLogin) throw new Exception("Configuration button must persist chosen version, dimensions, MaxTab and login flag.");
            if (!File.ReadAllText(configPath).Contains("Custom") || !File.ReadAllText(configPath).Contains("AutoNst")) throw new Exception("Saving must preserve unrelated settings.");
            if (!File.ReadAllText(configPath).Contains("<TrackedItemIds>123,456</TrackedItemIds>")) throw new Exception("Item IDs must be saved without duplicates.");
            original = File.ReadAllText(configPath); ids.Text = "123,wrong"; Click(save);
            if (File.ReadAllText(configPath) != original) throw new Exception("Invalid item ID must not overwrite any configuration.");
            ids.Text = "123,456"; maxTabs.Value = 1; Click(save);
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
            Console.WriteLine("PASS: configuration loads, rejects invalid paths, preserves other settings, and passes selected version/dimensions/login to new tabs.");
            typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            if (!table.Rows[0].Cells["character"].Value.ToString().Contains("fixture-character") || !table.Rows[0].Cells["character"].Value.ToString().Contains("Rương: 99")) throw new Exception("Character snapshot is not displayed in UI.");
            if (!table.Rows[0].Cells["under8"].Value.ToString().Contains("Áo +6") || !table.Rows[0].Cells["items"].Value.ToString().Contains("Đá: 25")) throw new Exception("Item statistics must be shown for the correct account.");
            under8.Checked = false; tracked.Checked = false;
            if (table.Columns["under8"].Visible || table.Columns["items"].Visible) throw new Exception("Statistics switches must hide columns immediately.");
            Click(buttons.Single(b => b.Text == "Mở tài khoản đã chọn"));
            if (manager.RunningCount != 1) throw new Exception("Second button press must not duplicate a tab.");
            Click(buttons.Single(b => b.Text == "Mở tất cả"));
            if (manager.RunningCount != 1) throw new Exception("Open all must respect MaxTab=1.");
            maxTabs.Value = 2; Click(save);
            Click(buttons.Single(b => b.Text == "Mở tất cả"));
            if (manager.RunningCount != 2) throw new Exception("Open all should launch remaining account after capacity increases.");
            WaitMarkers(fixture, 2);
            Console.WriteLine("PASS: selected/all buttons launch processes with correct credentials, prevent duplicates, and honor MaxTab.");
        } finally {
            if (manager != null) {
                var sessions = (IDictionary)typeof(AccountManager).GetField("running", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                foreach (DictionaryEntry entry in sessions) {
                    var process = (Process)entry.Value.GetType().GetField("Process").GetValue(entry.Value);
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
                }
                typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                manager.Dispose();
            }
            if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe fixture cleanup path.");
            Directory.Delete(fixture, true);
        }
    }
    static void Click(Button button) { typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty }); }
    static void WaitMarkers(string root, int expected) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            if (Directory.GetFiles(root, "launch.ok", SearchOption.AllDirectories).Length == expected) return;
            Thread.Sleep(50);
        }
        throw new Exception("Launched helper did not receive expected account arguments/environment.");
    }
}
