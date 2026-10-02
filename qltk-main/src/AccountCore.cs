using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace QltkAccounts
{
    public static class TabGrid
    {
        public static Point Position(int tabId, int tabWidth, int tabHeight, Rectangle screen)
        {
            int width = tabWidth + 20, height = tabHeight + 90;
            int columns = Math.Max(1, screen.Width / width);
            int x = screen.Left + ((tabId - 1) % columns) * width;
            int y = screen.Top + (((tabId - 1) / columns) * height) % Math.Max(1, screen.Height - height + 1);
            return new Point(Math.Max(screen.Left, Math.Min(x, screen.Right - width)), Math.Max(screen.Top, Math.Min(y, screen.Bottom - height)));
        }
    }
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
        public static int[] ParseItemIds(string text)
        {
            var ids = new List<int>();
            if (string.IsNullOrWhiteSpace(text)) return ids.ToArray();
            foreach (string part in text.Split(',')) {
                int id;
                if (!int.TryParse(part.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id) || id > 32767)
                    throw new InvalidDataException("ID vật phẩm phải là số từ 0 đến 32767, ngăn cách bằng dấu phẩy.");
                if (!ids.Contains(id)) ids.Add(id);
                if (ids.Count > 128) throw new InvalidDataException("Chỉ theo dõi tối đa 128 ID vật phẩm.");
            }
            return ids.ToArray();
        }
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
        public bool ShowUnder8, ShowTrackedItems;
        public int[] TrackedItemIds = new int[0];
        public static void SaveLaunch(string root, string emulator, string game, int width, int height, bool autoLogin, bool? showUnder8 = null, bool? showTrackedItems = null, string trackedItemIds = null, int? maxTab = null)
        {
            int[] ids = trackedItemIds == null ? null : AccountParser.ParseItemIds(trackedItemIds);
            if (width < 100 || width > 2000 || height < 100 || height > 2000)
                throw new InvalidDataException("Kích thước tab phải nằm trong 100–2000.");
            if (maxTab.HasValue && (maxTab.Value < 1 || maxTab.Value > 200))
                throw new InvalidDataException("MaxTab phải nằm trong 1–200.");
            Func<string, string> resolve = p => {
                if (string.IsNullOrWhiteSpace(p)) throw new InvalidDataException("Chọn file Emulator và Game trước khi lưu.");
                string path = Path.GetFullPath(Path.IsPathRooted(p.Trim()) ? p.Trim() : Path.Combine(root, p.Trim()));
                if (!string.Equals(Path.GetExtension(path), ".jar", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                    throw new InvalidDataException("Không tìm thấy file JAR: " + path);
                return path;
            };
            string emulatorPath = resolve(emulator), gamePath = resolve(game);
            string target = Path.Combine(root, "settings.xml");
            var document = XDocument.Load(target);
            if (document.Root == null || document.Root.Name != "Settings") throw new InvalidDataException("settings.xml không có mục Settings.");
            document.Root.SetElementValue("EmulatorPath", emulatorPath);
            document.Root.SetElementValue("GamePath", gamePath);
            document.Root.SetElementValue("TabWidth", width);
            document.Root.SetElementValue("TabHeight", height);
            document.Root.SetElementValue("AutoLogin", autoLogin);
            if (maxTab.HasValue) document.Root.SetElementValue("MaxTab", maxTab.Value);
            if (showUnder8.HasValue) document.Root.SetElementValue("ShowUnder8", showUnder8.Value);
            if (showTrackedItems.HasValue) document.Root.SetElementValue("ShowTrackedItems", showTrackedItems.Value);
            if (ids != null) document.Root.SetElementValue("TrackedItemIds", string.Join(",", ids));
            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { document.Save(temp); File.Replace(temp, target, null); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
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
            bool under8, tracked;
            if (!bool.TryParse(value("ShowUnder8", "false"), out under8) || !bool.TryParse(value("ShowTrackedItems", "false"), out tracked)) throw new InvalidDataException("Công tắc thống kê phải là true hoặc false.");
            return new AppSettings { EmulatorPath = resolve(value("EmulatorPath", "MICRO_NST.jar")), GamePath = resolve(value("GamePath", "game.jar")), JavaPath = Path.Combine(root, "jre", "bin", "javaw.exe"), MaxTab = number("MaxTab", 5, 1, 200), Width = number("TabWidth", 220, 100, 2000), Height = number("TabHeight", 240, 100, 2000), AutoLogin = auto, ShowUnder8 = under8, ShowTrackedItems = tracked, TrackedItemIds = AccountParser.ParseItemIds(value("TrackedItemIds", "")) };
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
