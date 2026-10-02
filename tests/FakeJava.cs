using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
class FakeJava {
    [STAThread] static int Main(string[] args) {
        if (args.Length < 8 || Environment.GetEnvironmentVariable("QLTK_ACCOUNT_PASS") != " fixture-pass ") return 2;
        string user = Environment.GetEnvironmentVariable("QLTK_ACCOUNT_USER");
        if (user != "fixture-a" && user != "fixture-b") return 3;
        string status = args.Last();
        int bootstrap = Array.IndexOf(args, "AccountBootstrap");
        if (bootstrap < 0) return 4;
        string[] launch = args.Skip(bootstrap + 1).ToArray();
        File.WriteAllText(status, "LOGIN_SUBMITTED");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(status), "character.xml"), "<Character state='READY' name='fixture-character' level='51' xu='3456' luong='78' boxKnown='true' boxXu='99'><Equipment known='true'><Item name='Áo' upgrade='6'/></Equipment><Inventory bagKnown='true' boxKnown='true'><Item id='123' name='Đá' quantity='25'/></Inventory></Character>");
        string heartbeat = Path.Combine(Path.GetDirectoryName(status), "heartbeat.txt");
        File.WriteAllText(heartbeat, DateTime.UtcNow.Ticks.ToString());
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(status), "launch.ok"), launch[0] + "|" + launch[4] + "|" + launch[5] + "|" + launch[6] + "|" + string.Join(" ", args.Take(bootstrap)));
        string autoSession = Environment.GetEnvironmentVariable("QLTK_AUTO_SESSION");
        if (!string.IsNullOrEmpty(autoSession)) File.WriteAllText(Path.Combine(Path.GetDirectoryName(status), "auto-session.txt"), autoSession);
        if (File.Exists(Path.Combine(Path.GetDirectoryName(launch[0]), "with-window"))) {
            using (var form = new Form { Text = "Fixture window", Size = new Size(240, 330) })
            using (var timer = new System.Windows.Forms.Timer { Interval = 80 }) {
                timer.Tick += delegate { File.WriteAllText(heartbeat + ".tmp", DateTime.UtcNow.Ticks.ToString()); File.Replace(heartbeat + ".tmp", heartbeat, null); };
                timer.Start(); Application.Run(form);
            }
            return 0;
        }
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline) {
            File.WriteAllText(heartbeat + ".tmp", DateTime.UtcNow.Ticks.ToString());
            File.Replace(heartbeat + ".tmp", heartbeat, null);
            Thread.Sleep(80);
        }
        return 0;
    }
}
