using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace QltkAccounts
{
    public sealed class AutoOption
    {
        public readonly string Key, Label, ValueKey;
        public readonly int Minimum, Maximum;
        public AutoOption(string key, string label, string valueKey = null, int minimum = 0, int maximum = 0)
        { Key = key; Label = label; ValueKey = valueKey; Minimum = minimum; Maximum = maximum; }
    }
    public static class AutoCatalog
    {
        public static readonly AutoOption[] Options = {
            new AutoOption("timeStartBlink", "Dùng HP khi còn dưới", "ek", 1, 99),
            new AutoOption("isAHP", "Dùng MP khi còn dưới", "el", 10, 90),
            new AutoOption("isAMP", "Dùng thức ăn cấp", "em", 1, 70),
            new AutoOption("isAFood", "Dùng chiêu hỗ trợ"),
            new AutoOption("isABuff", "Dùng khiên mana"),
            new AutoOption("isAResuscitate", "Dùng đốt quái & ẩn thân"),
            new AutoOption("isAPickYen", "Dùng phân thân"),
            new AutoOption("isAPickYHM", "Nhặt yên"),
            new AutoOption("isAPickYHMS", "Nhặt HP, MP", "en", 1, 70),
            new AutoOption("dm", "Nhặt N.Liệu(Đá)", "eo", 1, 7),
            new AutoOption("dn", "Luyện đá Max", "ep", 1, 12),
            new AutoOption("doa", "Nhặt Trang Bị", "eq", 1, 70),
            new AutoOption("dp", "Nhặt VP Nhiệm Vụ"),
            new AutoOption("dq", "Nhặt VP Sự Kiện"),
            new AutoOption("dr", "Nhặt All"),
            new AutoOption("ds", "Nhặt SVC"),
            new AutoOption("dt", "Không nhặt gì cả"),
            new AutoOption("du", "ReMap"),
            new AutoOption("dv", "Tàn sát map trống"),
            new AutoOption("dw", "Auto Mua Thức Ăn"),
            new AutoOption("dx", "TS khi hết MP"),
            new AutoOption("dy", "Auto Reconnect"),
            new AutoOption("dz", "Chuyển Map Hết Boss"),
            new AutoOption("ea", "Săn TATL"),
            new AutoOption("eb", "Đánh Quái Thường"),
            new AutoOption("ec", "Đánh Tinh Anh"),
            new AutoOption("ed", "Đánh Thủ Lĩnh"),
            new AutoOption("ee", "Cộng tiềm năng"),
            new AutoOption("ef", "Cộng kĩ năng"),
            new AutoOption("eg", "Đánh theo nhóm"),
            new AutoOption("eh", "Né PK"),
            new AutoOption("weaponOnlyPickup", "Chỉ nhặt vũ khí")
        };
        public static AutoOption Find(string key) { return Options.FirstOrDefault(o => o.Key == key || o.ValueKey == key); }
        public static bool ValidNumber(AutoOption option, int value)
        {
            if (value < option.Minimum || value > option.Maximum) return false;
            return option.ValueKey != "em" && option.ValueKey != "en" && option.ValueKey != "eq" || value == 1 || value % 10 == 0;
        }
    }
    public sealed class AutoSnapshot
    {
        public readonly string Session;
        public readonly Dictionary<string,string> Values;
        AutoSnapshot(string session, Dictionary<string,string> values) { Session = session; Values = values; }
        public static AutoSnapshot Parse(string xml)
        {
            if (xml == null || xml.Length > 262144) throw new InvalidDataException("Dữ liệu Auto quá lớn hoặc trống.");
            XElement root;
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 })) root = XElement.Load(reader);
            if (root.Name != "AutoState") throw new InvalidDataException("Dữ liệu Auto không hợp lệ.");
            string session = (string)root.Attribute("session");
            if (string.IsNullOrEmpty(session) || session.Length > 128) throw new InvalidDataException("Phiên Auto không hợp lệ.");
            var values = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var element in root.Elements("Option")) {
                string key = (string)element.Attribute("key"), enabled = (string)element.Attribute("enabled");
                AutoOption option = AutoCatalog.Options.FirstOrDefault(o => o.Key == key);
                if (option == null || !values.TryAddCompat(key, enabled)) throw new InvalidDataException("Mục Auto không hợp lệ hoặc trùng.");
                if (enabled != "true" && enabled != "false") throw new InvalidDataException("Checkbox Auto không hợp lệ.");
                if (option.ValueKey != null) {
                    string value = (string)element.Attribute("value"); int parsed;
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || !AutoCatalog.ValidNumber(option, parsed)) throw new InvalidDataException("Giá trị Auto không hợp lệ.");
                    values.Add(option.ValueKey, parsed.ToString(CultureInfo.InvariantCulture));
                }
            }
            if (values.Count != AutoCatalog.Options.Length + 7) throw new InvalidDataException("Trạng thái Auto thiếu mục.");
            return new AutoSnapshot(session, values);
        }
    }
    static class AutoDictionaryExtension
    {
        public static bool TryAddCompat(this Dictionary<string,string> values, string key, string value)
        {
            if (key == null || values.ContainsKey(key)) return false;
            values.Add(key, value); return true;
        }
    }
    public sealed class AutoSelection
    {
        public readonly Dictionary<string,string> Values;
        AutoSelection(Dictionary<string,string> values) { Values = values; }
        public static AutoSelection Merge(IEnumerable<AutoSnapshot> snapshots)
        {
            var source = snapshots.ToList(); if (source.Count == 0) throw new ArgumentException("Chưa chọn tab có Auto.");
            var values = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (string key in source[0].Values.Keys) {
                string first = source[0].Values[key];
                values[key] = source.All(s => s.Values.ContainsKey(key) && s.Values[key] == first) ? first : null;
            }
            return new AutoSelection(values);
        }
    }
    public static class AutoEdit
    {
        public static string Build(string session, long requestId, IDictionary<string,string> changedFields)
        {
            if (string.IsNullOrEmpty(session) || session.Length > 128 || requestId <= 0 || changedFields == null || changedFields.Count == 0) throw new ArgumentException("Yêu cầu Auto không hợp lệ.");
            var changes = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (var pair in changedFields) {
                AutoOption option = AutoCatalog.Find(pair.Key);
                if (option == null) throw new ArgumentException("Mục Auto không hợp lệ: " + pair.Key);
                if (option.Key == pair.Key) { if (pair.Value != "true" && pair.Value != "false") throw new ArgumentException("Checkbox Auto không hợp lệ."); }
                else { int numeric; if (!int.TryParse(pair.Value, NumberStyles.None, CultureInfo.InvariantCulture, out numeric) || !AutoCatalog.ValidNumber(option, numeric)) throw new ArgumentException("Giá trị Auto không hợp lệ: " + pair.Key); }
                changes[pair.Key] = pair.Value;
            }
            string value;
            if (changes.TryGetValue("dt", out value) && value == "true") {
                foreach (string key in new[] { "isAPickYHM", "isAPickYHMS", "dm", "doa", "dp", "dq", "dr", "ds", "weaponOnlyPickup" }) changes[key] = "false";
            } else {
                if (changes.TryGetValue("weaponOnlyPickup", out value) && value == "true") { changes["doa"] = "false"; changes["dt"] = "false"; }
                else if (changes.TryGetValue("doa", out value) && value == "true") { changes["weaponOnlyPickup"] = "false"; changes["dt"] = "false"; }
                if (new[] { "isAPickYHM", "isAPickYHMS", "dm", "dp", "dq", "dr", "ds" }.Any(key => changes.TryGetValue(key, out value) && value == "true")) changes["dt"] = "false";
            }
            var nodes = AutoCatalog.Options.SelectMany(option => new[] { option.Key, option.ValueKey }.Where(key => key != null && changes.ContainsKey(key)).Select(key => new XElement("Set", new XAttribute("key", key), new XAttribute(key == option.Key ? "enabled" : "value", changes[key]))));
            return new XElement("AutoCommand", new XAttribute("session", session), new XAttribute("id", requestId), nodes).ToString(SaveOptions.DisableFormatting);
        }
    }
}
