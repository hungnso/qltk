using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace QltkAccounts
{
    public sealed class CharacterSnapshot
    {
        public string Display;
        static long Number(XElement element, string name)
        {
            long result;
            if (!long.TryParse((string)element.Attribute(name), NumberStyles.None, CultureInfo.InvariantCulture, out result))
                throw new InvalidDataException("Thông tin nhân vật không hợp lệ: " + name);
            return result;
        }
        public static CharacterSnapshot Parse(string xml)
        {
            if (xml.Length > 16384) throw new InvalidDataException("Thông tin nhân vật quá dài.");
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
            return new CharacterSnapshot { Display = name.Replace('\r', ' ').Replace('\n', ' ') + " (Lv " + Number(element, "level") + ")" + Environment.NewLine +
                "Xu: " + Number(element, "xu").ToString("N0", culture) + " | Rương: " + chest + Environment.NewLine +
                "Lượng: " + Number(element, "luong").ToString("N0", culture) };
        }
    }
}
