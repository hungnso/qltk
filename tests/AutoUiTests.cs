using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using QltkAccounts;

class AutoUiTests {
    static void Check(bool yes, string why) { if (!yes) throw new Exception(why); }
    static void Click(Button button) { typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty }); }
    static void Pump(int ms) { var watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(20); } }
    static string State(string session, bool food, int hp) { return new XElement("AutoState", new XAttribute("session", session), new XAttribute("state", "READY"), AutoCatalog.Options.Select(o => new XElement("Option", new XAttribute("key", o.Key), new XAttribute("enabled", o.Key == "isAFood" ? food : false), o.ValueKey == null ? null : new XAttribute("value", o.ValueKey == "ek" ? hp : o.Minimum)))).ToString(); }
    static void Refresh(AccountManager manager) { typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null); }
    static void Stop(AccountManager manager) {
        if (manager == null) return;
        var sessions = (IDictionary)typeof(AccountManager).GetField("running", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
        foreach (DictionaryEntry entry in sessions) {
            object tab = entry.Value; object power = tab.GetType().GetField("Power").GetValue(tab);
            power.GetType().GetMethod("Wake").Invoke(power, new object[] { null });
            var process = (Process)tab.GetType().GetField("Process").GetValue(tab); if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
        }
        Refresh(manager); manager.Dispose();
    }
    [STAThread] static void Main(string[] args) { try { Run(args); } catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); } }
    static void Run(string[] args) {
        Application.EnableVisualStyles();
        var a = AutoSnapshot.Parse(State("one", true, 50)); var b = AutoSnapshot.Parse(State("two", false, 60));
        using (var dialog = new AutoSettingsDialog(new[] { a, b })) {
            Check(AutoCatalog.Options.All(o => dialog.Controls.Find("autoOption_" + o.Key, true).Length == 1), "All visible game checkboxes must be accessible in the Auto dialog.");
            Check(AutoCatalog.Options.Where(o => o.ValueKey != null).All(o => dialog.Controls.Find("autoValue_" + o.ValueKey, true).Length == 1), "All seven numeric fields must be present.");
            var food = (CheckBox)dialog.Controls.Find("autoOption_isAFood", true).Single();
            Check(food.CheckState == CheckState.Indeterminate, "Different tab settings must appear mixed.");
            Check(((TextBox)dialog.Controls.Find("autoValue_ek", true).Single()).Text == "Khác nhau", "Different numeric values must appear mixed.");
            food.CheckState = CheckState.Checked;
            Click((Button)dialog.Controls.Find("autoApply", true).Single());
            Check(dialog.DialogResult == DialogResult.OK && dialog.ChangedFields.Count == 1 && dialog.ChangedFields["isAFood"] == "true", "Applying a mixed dialog must contain only touched settings.");
        }
        string root = Path.GetFullPath(args[0]), fixture = Path.Combine(Path.GetTempPath(), "qltk-auto-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "jre", "bin"));
        File.Copy(Path.Combine(root, "tests", "FakeJava.exe"), Path.Combine(fixture, "jre", "bin", "javaw.exe"));
        foreach (var name in new[] { "game.jar", "MICRO_NST.jar", "account-bridge.jar" }) File.WriteAllText(Path.Combine(fixture, name), "fixture");
        File.WriteAllText(Path.Combine(fixture, "servers.txt"), "Bokken\nShuriken\n");
        File.WriteAllText(Path.Combine(fixture, "accounts.txt"), "fixture-a| fixture-pass |Bokken\nfixture-b| fixture-pass |Shuriken\n");
        File.WriteAllText(Path.Combine(fixture, "settings.xml"), "<Settings><GamePath>game.jar</GamePath><MaxTab>2</MaxTab><VpsLight>false</VpsLight></Settings>");
        AccountManager manager = null;
        try {
            manager = new AccountManager(fixture); manager.Show(); Pump(100);
            var autoButton = manager.Controls.Find("autoSettings", true).SingleOrDefault() as Button;
            Check(autoButton != null, "QLTK must show an Auto row under the performance settings.");
            Click(manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(x => x.Text == "Mở tất cả"));
            var wait = Stopwatch.StartNew(); while (manager.RunningCount < 2 && wait.ElapsedMilliseconds < 7000) Pump(50);
            Check(manager.RunningCount == 2, "Both fixture tabs must launch.");
            var table = manager.Controls.OfType<DataGridView>().Single();
            foreach (DataGridViewRow row in table.Rows) {
                var account = (Account)row.Tag;
                var id = (int)typeof(AccountManager).GetField("registry", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager).GetType().GetMethod("Find").Invoke(typeof(AccountManager).GetField("registry", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager), new object[] { account.Key });
                string home = Path.Combine(fixture, "data", "accounts", "tab_" + id);
                var sessionFile = Path.Combine(home, "auto-session.txt");
                wait.Restart(); while (!File.Exists(sessionFile) && wait.ElapsedMilliseconds < 3000) Pump(50);
                Check(File.Exists(sessionFile), "Each launched game needs a session token for Auto.");
                File.WriteAllText(Path.Combine(home, "auto-state.xml"), State(File.ReadAllText(sessionFile), account.Username == "fixture-a", 50));
            }
            Refresh(manager);
            table.Rows[0].Cells["chosen"].Value = true;
            var selected = new List<Account> { (Account)table.Rows[0].Tag };
            var unselected = (Account)table.Rows[1].Tag;
            var edits = new Dictionary<string,string> { { "isAFood", "true" } };
            typeof(AccountManager).GetMethod("ApplyAuto", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, new object[] { selected, edits });
            var commandFiles = Directory.GetFiles(fixture, "auto-command.xml", SearchOption.AllDirectories);
            Check(commandFiles.Length == 1 && File.ReadAllText(commandFiles[0]).Contains("isAFood"), "Auto changes must target only chosen tabs. Files=" + commandFiles.Length + "; row=" + table.Rows[0].Cells["auto"].Value + "; message=" + manager.Controls.OfType<TextBox>().Single().Text);
            Check(!commandFiles[0].Contains(unselected.Key), "Unselected account must not receive Auto command.");
            var command = XElement.Load(commandFiles[0]);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(commandFiles[0]), "auto-result.xml"), new XElement("AutoResult", new XAttribute("session", (string)command.Attribute("session")), new XAttribute("id", (string)command.Attribute("id")), new XAttribute("state", "OK"), new XAttribute("message", "")).ToString());
            Refresh(manager);
            Check(table.Rows[0].Cells["auto"].Value.ToString().Contains("Đã áp dụng"), "QLTK must mark Auto success only after the game acknowledges the matching command.");
            Check(((Label)typeof(AccountManager).GetField("autoSummary", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager)).Text.Contains("Đã áp dụng"), "Auto row summary must show the latest confirmed result.");
            Click(manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(x => x.Text == "Ngủ đã chọn")); Pump(250);
            typeof(AccountManager).GetMethod("ApplyAuto", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, new object[] { selected, new Dictionary<string,string> { { "ek", "60" } } });
            Check(table.Rows[0].Cells["auto"].Value.ToString().Contains("chờ thức"), "Selected sleeping tab must retain pending Auto request without waking.");
            var running = (IDictionary)typeof(AccountManager).GetField("running", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            var sleepingTab = running[selected[0].Key];
            sleepingTab.GetType().GetField("AutoPendingUtc").SetValue(sleepingTab, DateTime.UtcNow.AddMinutes(-1));
            Refresh(manager);
            Check((long)sleepingTab.GetType().GetField("AutoPending").GetValue(sleepingTab) > 0, "A sleeping tab must retain its Auto request beyond the active-tab timeout.");
            Click(manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().Single(x => x.Text == "Thức đã chọn")); Pump(300);
            Refresh(manager);
            Check((long)sleepingTab.GetType().GetField("AutoPending").GetValue(sleepingTab) > 0, "Waking a tab must allow time for the game to acknowledge its queued Auto request.");
            var otherState = Directory.GetFiles(fixture, "auto-state.xml", SearchOption.AllDirectories).Single(path => Path.GetDirectoryName(path) != Path.GetDirectoryName(commandFiles[0]));
            var otherSession = (string)XElement.Load(otherState).Attribute("session");
            File.WriteAllText(otherState, new XElement("AutoState", new XAttribute("session", otherSession), new XAttribute("state", "WAITING")).ToString());
            Refresh(manager);
            Check(table.Rows[1].Cells["auto"].Value.ToString().Contains("Chưa vào game"), "Leaving gameplay must invalidate stale Auto settings in QLTK.");
            typeof(AccountManager).GetMethod("ApplyAuto", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, new object[] { new List<Account> { unselected }, new Dictionary<string,string> { { "ek", "70" } } });
            Check(!File.Exists(Path.Combine(Path.GetDirectoryName(otherState), "auto-command.xml")), "An unready tab must not receive Auto changes based on a stale snapshot.");
            Console.WriteLine("PASS: full Auto dialog shows mixed state and applies only touched fields to chosen tabs, confirming result and queuing sleepers.");
        } finally {
            Stop(manager);
            if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe fixture cleanup.");
            Directory.Delete(fixture, true);
        }
    }
}
