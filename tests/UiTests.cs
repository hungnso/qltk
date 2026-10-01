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
            var table = manager.Controls.OfType<DataGridView>().Single();
            var buttons = manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().ToList();
            table.Rows[0].Cells["chosen"].Value = true;
            Click(buttons.Single(b => b.Text == "Mở tài khoản đã chọn"));
            if (manager.RunningCount != 1) throw new Exception("Selected button must launch exactly one tab.");
            WaitMarkers(fixture, 1);
            typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            if (!table.Rows[0].Cells["character"].Value.ToString().Contains("fixture-character") || !table.Rows[0].Cells["character"].Value.ToString().Contains("Rương: 99")) throw new Exception("Character snapshot is not displayed in UI.");
            Click(buttons.Single(b => b.Text == "Mở tài khoản đã chọn"));
            if (manager.RunningCount != 1) throw new Exception("Second button press must not duplicate a tab.");
            Click(buttons.Single(b => b.Text == "Mở tất cả"));
            if (manager.RunningCount != 1) throw new Exception("Open all must respect MaxTab=1.");
            capacity(2);
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
