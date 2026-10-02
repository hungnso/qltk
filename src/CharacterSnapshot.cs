using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;

namespace QltkAccounts
{
    public sealed class CharacterSnapshot
    {
        public string Display;
        public bool IsReady;
        public string EquipmentDisplay = "Chưa đọc được trang bị";
        readonly Dictionary<int, string> tracked = new Dictionary<int, string>();
        string inventoryWarning = "Chưa đọc được vật phẩm";
        public string InventoryDisplay(int[] ids)
        {
            string items = string.Join(" | ", ids.Where(tracked.ContainsKey).Select(id => tracked[id]));
            if (items.Length == 0) items = inventoryWarning.Length == 0 ? "Không có vật phẩm theo ID đã chọn" : "Chưa thấy vật phẩm theo ID đã chọn";
            return items + (inventoryWarning.Length == 0 ? "" : " | " + inventoryWarning);
        }
        static string Clean(string text) { return (text ?? "").Replace('\r', ' ').Replace('\n', ' '); }
        void ReadStatistics(XElement element)
        {
            var equipment = element.Element("Equipment");
            if (equipment != null && (string)equipment.Attribute("known") == "true") {
                var entries = equipment.Elements("Item").Select(i => Clean((string)i.Attribute("name")) + " +" + Number(i, "upgrade")).ToArray();
                EquipmentDisplay = entries.Length == 0 ? "Không có đồ dưới +8" : entries.Length + " món | " + string.Join(" | ", entries);
            }
            var inventory = element.Element("Inventory");
            if (inventory == null) return;
            bool bagKnown = (string)inventory.Attribute("bagKnown") == "true", boxKnown = (string)inventory.Attribute("boxKnown") == "true";
            inventoryWarning = bagKnown && boxKnown ? "" : !bagKnown && !boxKnown ? "Hành trang và rương chưa tải — số lượng chưa đầy đủ" :
                !bagKnown ? "Hành trang chưa tải — số lượng chưa đầy đủ" : "Rương chưa tải — số lượng chưa đầy đủ";
            foreach (var item in inventory.Elements("Item")) {
                long parsedId = Number(item, "id"); long quantity = Number(item, "quantity");
                if (parsedId > 32767) throw new InvalidDataException("ID vật phẩm không hợp lệ.");
                int id = (int)parsedId;
                if (quantity > 0) {
                    if (tracked.ContainsKey(id)) throw new InvalidDataException("ID vật phẩm trùng trong dữ liệu thống kê.");
                    string itemName = Clean((string)item.Attribute("name"));
                    tracked.Add(id, (string.IsNullOrWhiteSpace(itemName) ? "ID " + id : itemName) + ": " + quantity.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")));
                }
            }
        }
        static long Number(XElement element, string name)
        {
            long result;
            if (!long.TryParse((string)element.Attribute(name), NumberStyles.None, CultureInfo.InvariantCulture, out result))
                throw new InvalidDataException("Thông tin nhân vật không hợp lệ: " + name);
            return result;
        }
        public static CharacterSnapshot Parse(string xml)
        {
            if (xml.Length > 262144) throw new InvalidDataException("Thông tin nhân vật quá dài.");
            var element = XElement.Parse(xml);
            if (element.Name != "Character") throw new InvalidDataException("Sai định dạng thông tin nhân vật.");
            string state = (string)element.Attribute("state");
            if (state == "WAITING") return new CharacterSnapshot { Display = "Chưa vào game" };
            if (state == "EMPTY_FIRST") return new CharacterSnapshot { Display = "Ô nhân vật số 1 trống" };
            if (state == "ERROR") return new CharacterSnapshot { Display = "Chưa đọc được thông tin NV" };
            if (state != "READY") throw new InvalidDataException("Trạng thái nhân vật không hợp lệ.");
            string name = (string)element.Attribute("name");
            bool known;
            if (string.IsNullOrWhiteSpace(name) || !bool.TryParse((string)element.Attribute("boxKnown"), out known))
                throw new InvalidDataException("Thiếu thông tin nhân vật.");
            var culture = CultureInfo.GetCultureInfo("vi-VN");
            string chest = known ? Number(element, "boxXu").ToString("N0", culture) : "Chưa đọc được";
            string weaponState = (string)element.Attribute("weaponState");
            string weapon = weaponState == "EQUIPPED" ? "Lv " + Number(element, "weaponLevel") + " +" + Number(element, "weaponUpgrade") :
                weaponState == "NONE" ? "Chưa trang bị" : "Chưa đọc được";
            var result = new CharacterSnapshot { IsReady = true, Display = Clean(name) + " (Lv " + Number(element, "level") + ") | " +
                "Xu: " + Number(element, "xu").ToString("N0", culture) + " | Rương: " + chest + " | " +
                "Lượng: " + Number(element, "luong").ToString("N0", culture) + " | VK: " + weapon };
            result.ReadStatistics(element); return result;
        }
    }
}
