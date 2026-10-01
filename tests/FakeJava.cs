using System;
using System.IO;
using System.Linq;
using System.Threading;
class FakeJava {
    static int Main(string[] args) {
        if (args.Length < 8 || Environment.GetEnvironmentVariable("QLTK_ACCOUNT_PASS") != " fixture-pass ") return 2;
        string user = Environment.GetEnvironmentVariable("QLTK_ACCOUNT_USER");
        if (user != "fixture-a" && user != "fixture-b") return 3;
        string status = args.Last();
        File.WriteAllText(status, "LOGIN_SUBMITTED");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(status), "launch.ok"), "PASS");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(status), "character.xml"), "<Character state='READY' name='fixture-character' level='51' xu='3456' luong='78' boxKnown='true' boxXu='99'/>");
        Thread.Sleep(30000); return 0;
    }
}
