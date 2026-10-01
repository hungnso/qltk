using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace QltkAccounts
{
    public sealed class Account
    {
        public string Username, Password, Server, Key;
        public int ServerIndex, Line;
    }
    public sealed class ParseResult
    {
        public readonly List<Account> Accounts = new List<Account>();
        public readonly List<string> Errors = new List<string>();
    }
    public static class AccountParser
    {
        public static ParseResult Parse(IEnumerable<string> lines, string[] servers)
        {
            var result = new ParseResult(); var seen = new HashSet<string>(); int line = 0;
            foreach (string raw in lines) {
                line++; string text = raw.TrimStart('\uFEFF');
                if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("#")) continue;
                string[] fields = text.Split('|');
                if (fields.Length != 3 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]) || string.IsNullOrWhiteSpace(fields[2])) {
                    result.Errors.Add("Dòng " + line + ": cần tài_khoản|mật_khẩu|server, không để trống."); continue;
                }
                string username = fields[0].Trim(), server = fields[2].Trim();
                int index = Array.FindIndex(servers, s => string.Equals(s, server, StringComparison.OrdinalIgnoreCase));
                if (index < 0) { result.Errors.Add("Dòng " + line + ": server không có trong servers.txt."); continue; }
                string key;
                // The supplied game's LoginCredentialPolicy lowercases usernames.
                using (var sha = SHA256.Create()) { key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(username.ToLowerInvariant() + "\0" + servers[index].ToLowerInvariant()))).Replace("-", "").ToLowerInvariant(); }
                if (!seen.Add(key)) { result.Errors.Add("Dòng " + line + ": trùng tài khoản và server."); continue; }
                result.Accounts.Add(new Account { Username = username, Password = fields[1], Server = servers[index], ServerIndex = index, Line = line, Key = key });
            }
            return result;
        }
    }
    public sealed class LaunchPlan
    {
        public readonly List<Account> Accounts = new List<Account>();
        public int AlreadyRunning, OverLimit;
    }
    public static class LaunchPlanner
    {
        public static LaunchPlan Plan(IEnumerable<Account> accounts, IEnumerable<string> running, int maxTab)
        {
            if (maxTab < 1) throw new ArgumentOutOfRangeException("maxTab");
            var busy = new HashSet<string>(running); var plan = new LaunchPlan(); int slots = Math.Max(0, maxTab - busy.Count);
            foreach (var account in accounts) {
                if (busy.Contains(account.Key)) { plan.AlreadyRunning++; continue; }
                if (slots == 0) { plan.OverLimit++; continue; }
                busy.Add(account.Key); slots--; plan.Accounts.Add(account);
            }
            return plan;
        }
    }
    public sealed class TabRegistry
    {
        readonly string path;
        readonly Dictionary<string, int> tabs = new Dictionary<string, int>();
        public TabRegistry(string path)
        {
            this.path = path;
            if (!File.Exists(path)) return;
            var used = new HashSet<int>();
            foreach (var element in XDocument.Load(path).Root.Elements("Tab")) {
                string key = (string)element.Attribute("key"); int id = (int)element.Attribute("id");
                if (string.IsNullOrEmpty(key) || key.Length != 64 || key.Any(c => !Uri.IsHexDigit(c)) || id < 1 || !used.Add(id) || tabs.ContainsKey(key))
                    throw new InvalidDataException("File liên kết account/tab không hợp lệ.");
                tabs.Add(key, id);
            }
        }
        public int Find(string key) { int id; return tabs.TryGetValue(key, out id) ? id : 0; }
        public int GetOrAdd(string key)
        {
            int existing = Find(key); if (existing != 0) return existing;
            int next = tabs.Count == 0 ? 1 : tabs.Values.Max() + 1;
            tabs.Add(key, next);
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                new XDocument(new XElement("AccountTabs", tabs.OrderBy(t => t.Value).Select(t => new XElement("Tab", new XAttribute("key", t.Key), new XAttribute("id", t.Value))))).Save(temp);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            } catch { tabs.Remove(key); throw; }
            return next;
        }
    }
    public sealed class AppSettings
    {
        public string EmulatorPath, GamePath, JavaPath;
        public int MaxTab, Width, Height;
        public bool AutoLogin;
        public static AppSettings Load(string root)
        {
            var xml = XDocument.Load(Path.Combine(root, "settings.xml")).Root;
            Func<string, string, string> value = (name, fallback) => (string)xml.Element(name) ?? fallback;
            Func<string, int, int, int, int> number = (name, fallback, min, max) => {
                int parsed; if (!int.TryParse(value(name, fallback.ToString()), out parsed) || parsed < min || parsed > max)
                    throw new InvalidDataException(name + " phải nằm trong " + min + "–" + max + "."); return parsed;
            };
            Func<string, string> resolve = p => Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(root, p));
            bool auto; if (!bool.TryParse(value("AutoLogin", "true"), out auto)) throw new InvalidDataException("AutoLogin phải là true hoặc false.");
            return new AppSettings { EmulatorPath = resolve(value("EmulatorPath", "MICRO_NST.jar")), GamePath = resolve(value("GamePath", "game.jar")), JavaPath = Path.Combine(root, "jre", "bin", "javaw.exe"), MaxTab = number("MaxTab", 5, 1, 200), Width = number("TabWidth", 220, 100, 2000), Height = number("TabHeight", 240, 100, 2000), AutoLogin = auto };
        }
    }
    public static class LaunchArguments
    {
        public static string Quote(string text)
        {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in text) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') b.Append('\\', slashes * 2 + 1); else b.Append('\\', slashes);
                b.Append(c); slashes = 0;
            }
            return b.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
