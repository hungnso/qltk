using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace QltkAccounts
{
    public sealed class LastLoginRecord
    {
        public CharacterSnapshot Character;
        public string Xml;
        public DateTime? LoginUtc;
        public DateTime UpdatedUtc;
        int[] storedIds;
        public string TimeDisplay {
            get {
                return "Login: " + (LoginUtc.HasValue ? LoginUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "Chưa ghi nhận") +
                    " | Cập nhật: " + UpdatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            }
        }
        public string InventoryDisplay(int[] ids) {
            if (storedIds == null) {
                var inventory = XElement.Parse(Xml).Element("Inventory");
                storedIds = inventory == null ? new int[0] : inventory.Elements("Item").Select(i => (int)i.Attribute("id")).ToArray();
            }
            var missing = ids.Except(storedIds).ToArray();
            return Character.InventoryDisplay(ids) + (missing.Length == 0 ? "" : " | Chưa lưu dữ liệu ID: " + string.Join(",", missing));
        }
    }
    public sealed class AccountHistoryStore
    {
        readonly string directory;
        public AccountHistoryStore(string root) { directory = Path.Combine(root, "data", "accounts", "history"); }
        string FilePath(string key) {
            if (key == null || key.Length != 64 || key.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Liên kết lịch sử không hợp lệ.");
            return Path.Combine(directory, key + ".xml");
        }
        static DateTime ReadTime(string text) {
            DateTime time;
            if (!DateTime.TryParseExact(text, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time)) throw new InvalidDataException("Thời điểm lịch sử không hợp lệ.");
            return time.ToUniversalTime();
        }
        public LastLoginRecord Load(string key) {
            string path = FilePath(key); if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("Lịch sử quá lớn.");
            var element = XElement.Load(path);
            if (element.Name != "AccountHistory" || (string)element.Attribute("key") != key || element.Element("Character") == null) throw new InvalidDataException("Sai liên kết lịch sử account.");
            string xml = element.Element("Character").ToString(SaveOptions.DisableFormatting);
            var character = CharacterSnapshot.Parse(xml);
            if (!character.IsReady) throw new InvalidDataException("Lịch sử không chứa thông tin NV thành công.");
            string login = (string)element.Attribute("loginUtc");
            return new LastLoginRecord { Xml = xml, Character = character, LoginUtc = login == null ? (DateTime?)null : ReadTime(login), UpdatedUtc = ReadTime((string)element.Attribute("updatedUtc")) };
        }
        public LastLoginRecord Save(string key, string xml, DateTime? loginUtc, DateTime updatedUtc, LastLoginRecord previous) {
            string path = FilePath(key);
            var snapshot = CharacterSnapshot.Parse(xml); if (!snapshot.IsReady) return previous;
            var character = XElement.Parse(xml);
            if (previous != null) {
                var prior = XElement.Parse(previous.Xml);
                foreach (string section in new[] { "Equipment", "Inventory" })
                    if (character.Element(section) == null && prior.Element(section) != null) character.Add(new XElement(prior.Element(section)));
            }
            string normalized = character.ToString(SaveOptions.DisableFormatting);
            if (previous != null && previous.Xml == normalized && previous.LoginUtc == loginUtc) return previous;
            var root = new XElement("AccountHistory", new XAttribute("key", key), new XAttribute("updatedUtc", updatedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)), character);
            if (loginUtc.HasValue) root.SetAttributeValue("loginUtc", loginUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { root.Save(temp); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return new LastLoginRecord { Xml = normalized, Character = CharacterSnapshot.Parse(normalized), LoginUtc = loginUtc, UpdatedUtc = updatedUtc.ToUniversalTime() };
        }
    }
}
