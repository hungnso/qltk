using System;
using System.IO;
using System.Linq;
using QltkAccounts;

class AccountTests
{
    static int assertions;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static void Main()
    {
        var servers = new[] { "Bokken", "Shuriken", "Tessen" };
        var parsed = AccountParser.Parse(new[] { "\uFEFFalice| secret |Bokken", "", "# comment", "bad", "bob|p|unknown", "alice|other|Bokken", "alice|p2|Shuriken", "empty||Tessen" }, servers);
        Check(parsed.Accounts.Count == 2, "Valid accounts and distinct servers must be retained.");
        Check(parsed.Errors.Count == 4, "Malformed, unknown, duplicate and empty password must be reported.");
        Check(parsed.Accounts[0].Password == " secret ", "Password spaces must not be changed.");
        Check(!string.Join(" ", parsed.Errors).Contains("secret"), "Errors must not reveal passwords.");
        Check(parsed.Accounts[1].ServerIndex == 1, "Server index must match servers.txt.");
        var caseSensitive = AccountParser.Parse(new[] { "Alice|p|bokken", "alice|p|Bokken" }, servers);
        Check(caseSensitive.Accounts.Count == 1 && caseSensitive.Errors.Count == 1, "Case variants must not launch the same game account twice.");
        Check(AccountParser.Parse(new[] { "x|p|1" }, servers).Errors.Count == 1, "Numeric unknown server must be rejected.");
        Check(AccountParser.Parse(new[] { "x|p|Bokken|extra" }, servers).Errors.Count == 1, "Extra fields must be rejected.");
        var info = CharacterSnapshot.Parse("<Character state='READY' name='First &amp; One' level='51' xu='3456' luong='78' boxKnown='false'/>");
        Check(info.Display.Contains("First & One") && info.Display.Contains("3.456") && info.Display.Contains("78"), "Character info must show decoded name and formatted balances.");
        Check(info.Display.Contains("Chưa đọc được"), "Unloaded chest must not appear as zero.");
        Check(!info.Display.Contains("\n") && !info.Display.Contains("\r"), "Character information must stay on a single line.");
        var armed = CharacterSnapshot.Parse("<Character state='READY' name='First' level='51' xu='600000' luong='901' boxKnown='true' boxXu='0' weaponState='EQUIPPED' weaponLevel='50' weaponUpgrade='12'/>");
        Check(armed.Display.EndsWith(" | VK: Lv 50 +12"), "Equipped weapon level and upgrade must appear after a pipe separator.");
        var unarmed = CharacterSnapshot.Parse("<Character state='READY' name='First' level='51' xu='0' luong='0' boxKnown='false' weaponState='NONE'/>");
        Check(unarmed.Display.Contains("VK: Chưa trang bị"), "An empty weapon slot must not report level zero.");
        var zero = CharacterSnapshot.Parse("<Character state='READY' name='First' level='51' xu='0' luong='0' boxKnown='true' boxXu='0'/>");
        Check(zero.Display.Contains("Rương: 0"), "Confirmed zero chest balance must be shown as zero.");
        bool invalidSnapshot = false;
        try { CharacterSnapshot.Parse("<Character state='READY' name='First' level='51' xu='bad' luong='78' boxKnown='false'/>"); } catch { invalidSnapshot = true; }
        Check(invalidSnapshot, "Malformed balance must not be silently converted to zero.");
        string baseInfo = "<Character state='READY' name='First' level='51' xu='0' luong='0' boxKnown='true' boxXu='0'>";
        var inventory = CharacterSnapshot.Parse(baseInfo + "<Equipment known='true'><Item name='Áo' upgrade='6'/><Item name='Giày' upgrade='7'/></Equipment><Inventory bagKnown='true' boxKnown='true'><Item id='123' name='Đá' quantity='25'/><Item id='456' name='Vật phẩm khác' quantity='0'/></Inventory></Character>");
        Check(inventory.EquipmentDisplay == "2 món | Áo +6 | Giày +7", "Under-eight gear must show count, names and upgrades.");
        Check(inventory.InventoryDisplay(new[] { 123, 456, 999 }) == "Đá: 25", "Tracking must show only requested, present items and hide absent IDs.");
        var partial = CharacterSnapshot.Parse(baseInfo + "<Equipment known='false'/><Inventory bagKnown='true' boxKnown='false'><Item id='123' name='Đá' quantity='3'/></Inventory></Character>");
        Check(partial.InventoryDisplay(new[] { 123 }).Contains("Đá: 3") && partial.InventoryDisplay(new[] { 123 }).Contains("Rương chưa tải"), "Partial inventory must not claim a complete count.");
        Check(partial.EquipmentDisplay == "Chưa đọc được trang bị", "Unknown equipment must not appear as no low-upgrade gear.");
        Check(AccountParser.ParseItemIds("123, 456,123").SequenceEqual(new[] { 123, 456 }), "Tracking IDs must normalize duplicates while retaining order.");
        bool invalidId = false; try { AccountParser.ParseItemIds("1,-2"); } catch (InvalidDataException) { invalidId = true; }
        Check(invalidId, "Negative template IDs must be rejected.");
        string dir = Path.Combine(Path.GetTempPath(), "qltk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try {
            var registry = new TabRegistry(Path.Combine(dir, "tabs.xml"));
            int first = registry.GetOrAdd(parsed.Accounts[0].Key);
            int second = registry.GetOrAdd(parsed.Accounts[1].Key);
            Check(first != second, "Accounts must have separate data folders.");
            var restored = new TabRegistry(Path.Combine(dir, "tabs.xml"));
            Check(restored.GetOrAdd(parsed.Accounts[1].Key) == second, "Order changes must preserve tab mapping.");
            Check(restored.GetOrAdd(parsed.Accounts[0].Key) == first, "Reload must preserve original tab mapping.");
            Check(!File.ReadAllText(Path.Combine(dir, "tabs.xml")).Contains("alice"), "Mapping should not store credentials.");
            var plan = LaunchPlanner.Plan(parsed.Accounts, new[] { parsed.Accounts[0].Key }, 5);
            Check(plan.Accounts.Count == 1 && plan.AlreadyRunning == 1, "Running accounts must not launch twice.");
            var limited = LaunchPlanner.Plan(parsed.Accounts, new string[0], 1);
            Check(limited.Accounts.Count == 1 && limited.OverLimit == 1, "MaxTab must cap launches.");
            Check(LaunchPlanner.Plan(parsed.Accounts, new[] { "unlisted-running" }, 1).Accounts.Count == 0, "Removed running rows still count toward MaxTab.");
            var third = TabGrid.Position(3, 220, 240, new System.Drawing.Rectangle(0, 0, 1000, 800));
            Check(third == new System.Drawing.Point(480, 0), "Tab arrangement must fill a row from left to right.");
            var fifth = TabGrid.Position(5, 220, 240, new System.Drawing.Rectangle(0, 0, 1000, 800));
            Check(fifth == new System.Drawing.Point(0, 330), "Tab arrangement must continue on the next row.");
            string quoted = LaunchArguments.Quote("C:\\a b\\");
            Check(quoted == "\"C:\\a b\\\\\"", "Windows quoting must preserve trailing slash.");
            Check(LaunchArguments.Quote("a\"b") == "\"a\\\"b\"", "Windows quoting must escape embedded quote.");
            File.WriteAllText(Path.Combine(dir, "settings.xml"), "<Settings><EmulatorPath>MICRO_NST.jar</EmulatorPath><GamePath>game.jar</GamePath><MaxTab>5</MaxTab><AutoLogin>true</AutoLogin><TabWidth>220</TabWidth><TabHeight>240</TabHeight></Settings>");
            var settings = AppSettings.Load(dir);
            Check(settings.GamePath == Path.Combine(dir, "game.jar"), "Relative paths must resolve against application folder.");
            Check(settings.MaxTab == 5 && settings.AutoLogin, "Existing settings must be honored.");
            File.WriteAllText(Path.Combine(dir, "settings.xml"), "<Settings><MaxTab>0</MaxTab></Settings>");
            bool rejected = false; try { AppSettings.Load(dir); } catch { rejected = true; }
            Check(rejected, "Invalid capacity must be rejected rather than launching without a limit.");
        } finally { Directory.Delete(dir, true); }
        Console.WriteLine("PASS: " + assertions + " account, capacity, mapping and settings assertions");
    }
}
