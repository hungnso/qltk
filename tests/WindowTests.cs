using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using QltkAccounts;

class WindowTests {
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    static Rectangle Bounds(Process process) { process.Refresh(); Rect r; if (!GetWindowRect(process.MainWindowHandle, out r)) throw new Exception("Missing fixture window."); return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom); }
    static void Move(Process process, int x, int y, int width, int height) { process.Refresh(); if (!SetWindowPos(process.MainWindowHandle, IntPtr.Zero, x, y, width, height, 0x4014)) throw new Exception("Cannot position fixture window."); Pump(200); }
    static void Pump(int ms) { var watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(20); } }
    static string Heartbeat(string path) { for (int retry = 0; retry < 10; retry++) { try { using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))) return reader.ReadToEnd(); } catch (IOException) { Thread.Sleep(15); } } throw new IOException("Cannot read fixture heartbeat."); }
    static Button Button(AccountManager manager, string title) { return manager.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().SingleOrDefault(b => b.Text == title); }
    static void Click(AccountManager manager, string title) { var button = Button(manager, title); if (button == null) throw new Exception("Missing arrange action: " + title); typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty }); }
    static Process[] Processes(AccountManager manager) { var sessions = (IDictionary)typeof(AccountManager).GetField("running", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager); return sessions.Values.Cast<object>().OrderBy(t => ((Account)t.GetType().GetField("Account").GetValue(t)).Username).Select(t => (Process)t.GetType().GetField("Process").GetValue(t)).ToArray(); }
    static void AwaitWindows(AccountManager manager, int count) { var watch = Stopwatch.StartNew(); while (watch.Elapsed.TotalSeconds < 8) { Pump(50); var processes = Processes(manager); if (processes.Length == count && processes.All(p => { p.Refresh(); return p.MainWindowHandle != IntPtr.Zero; })) { Pump(250); return; } } throw new Exception("Fixture game windows did not open."); }
    static void Stop(AccountManager manager) { if (manager == null) return; foreach (var p in Processes(manager)) if (!p.HasExited) { p.Kill(); p.WaitForExit(5000); } typeof(AccountManager).GetMethod("RefreshStatuses", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null); manager.Dispose(); }
    [STAThread] static void Main(string[] args) {
        try { Run(args); } catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); }
    }
    static void Run(string[] args) {
        Application.EnableVisualStyles();
        string root = Path.GetFullPath(args[0]), fixture = Path.Combine(Path.GetTempPath(), "qltk-window-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "jre", "bin"));
        File.Copy(Path.Combine(root, "tests", "FakeJava.exe"), Path.Combine(fixture, "jre", "bin", "javaw.exe"));
        foreach (var name in new[] { "game.jar", "MICRO_NST.jar", "account-bridge.jar", "with-window" }) File.WriteAllText(Path.Combine(fixture, name), "fixture");
        File.WriteAllText(Path.Combine(fixture, "servers.txt"), "Bokken\nShuriken\n");
        File.WriteAllText(Path.Combine(fixture, "accounts.txt"), "fixture-a| fixture-pass |Bokken\nfixture-b| fixture-pass |Shuriken\n");
        File.WriteAllText(Path.Combine(fixture, "settings.xml"), "<Settings><GamePath>game.jar</GamePath><MaxTab>2</MaxTab><VpsLight>false</VpsLight></Settings>");
        string legacy = "<Layout><Tab no='1' x='90' y='100'/></Layout>";
        File.WriteAllText(Path.Combine(fixture, "layout.xml"), legacy);
        AccountManager manager = null;
        try {
            manager = new AccountManager(fixture); manager.Show(); Pump(100);
            if (Button(manager, "Sắp xếp đã chọn") == null || Button(manager, "Sắp xếp tất cả") == null) throw new Exception("Selected/all arrangement buttons are missing.");
            Click(manager, "Mở tất cả"); AwaitWindows(manager, 2);
            var processes = Processes(manager); var screen = Screen.FromControl(manager).WorkingArea;
            Move(processes[0], screen.Left + 150, screen.Top + 60, 250, 160);
            Move(processes[1], screen.Left + 350, screen.Top + 90, 300, 180);
            var table = manager.Controls.OfType<DataGridView>().Single(); table.Rows[0].Cells["chosen"].Value = true;
            var untouched = Bounds(processes[1]); var firstSize = Bounds(processes[0]).Size;
            Click(manager, "Sắp xếp đã chọn"); Pump(250);
            if (Bounds(processes[0]).Location != screen.Location || Bounds(processes[0]).Size != firstSize || Bounds(processes[1]) != untouched) throw new Exception("Selected arrange must move only the chosen tab and preserve actual size.");
            Click(manager, "Sắp xếp tất cả"); Pump(250);
            var a = Bounds(processes[0]); var b = Bounds(processes[1]);
            if (a.Location != screen.Location || b.Left != a.Right || b.Top != a.Top || b.Size != untouched.Size) throw new Exception("Arrange all must pack real window sizes in displayed tab order.");
            int wide = screen.Width * 2 / 3;
            Move(processes[0], screen.Left + 80, screen.Top + 60, wide, 120);
            Move(processes[1], screen.Left + 110, screen.Top + 90, wide, 120);
            Click(manager, "Ngủ đã chọn"); Pump(200);
            string heartbeat = Directory.GetFiles(fixture, "heartbeat.txt", SearchOption.AllDirectories).OrderBy(p => p).First();
            string beat = Heartbeat(heartbeat);
            var elapsed = Stopwatch.StartNew(); Click(manager, "Sắp xếp tất cả"); elapsed.Stop(); Pump(250);
            if (elapsed.Elapsed.TotalSeconds > 2 || beat != Heartbeat(heartbeat) || !table.Rows[0].Cells["status"].Value.ToString().Contains("Đang ngủ")) throw new Exception("Arranging sleeping windows must return promptly without waking the process.");
            Click(manager, "Thức đã chọn"); Pump(350);
            a = Bounds(processes[0]); b = Bounds(processes[1]);
            if (a.Location != screen.Location || b.Left != screen.Left || b.Top != a.Bottom || a.Width != wide || b.Width != wide) throw new Exception("Windows must wrap to the next row and a sleeping tab must apply its queued move after wake.");
            if (File.ReadAllText(Path.Combine(fixture, "layout.xml")) != legacy) throw new Exception("Arranging new tabs must preserve the legacy tool layout.");
            var savedPosition = b.Location;
            Stop(manager); manager = null;
            File.WriteAllText(Path.Combine(fixture, "accounts.txt"), "fixture-b| fixture-pass |Shuriken\nfixture-a| fixture-pass |Bokken\n");
            manager = new AccountManager(fixture); manager.Show(); Pump(100);
            manager.Controls.OfType<DataGridView>().Single().Rows[0].Cells["chosen"].Value = true;
            Click(manager, "Mở tài khoản đã chọn"); AwaitWindows(manager, 1);
            var positionWait = Stopwatch.StartNew();
            while (Bounds(Processes(manager)[0]).Location != savedPosition && positionWait.ElapsedMilliseconds < 3000) Pump(50);
            if (Bounds(Processes(manager)[0]).Location != savedPosition) throw new Exception("Saved position must follow the account across restart and list reordering.");
            Console.WriteLine("PASS: selected/all arrange respects real sizes and order, wraps rows, leaves sleepers paused and restores saved account positions after restart.");
        } finally {
            Stop(manager);
            if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe fixture cleanup.");
            Directory.Delete(fixture, true);
        }
    }
}
