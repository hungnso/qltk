using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QltkAccounts
{
    public sealed class AutoSettingsDialog : Form
    {
        readonly Dictionary<string,string> changes = new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string,CheckBox> checkboxes = new Dictionary<string,CheckBox>(StringComparer.Ordinal);
        readonly Label validation = new Label { AutoSize = true, ForeColor = Color.DarkRed, Margin = new Padding(10, 13, 0, 0) };
        bool loading;
        public IDictionary<string,string> ChangedFields { get { return changes; } }
        public AutoSettingsDialog(IEnumerable<AutoSnapshot> snapshots)
        {
            Text = "QLTK NST — Tự động trong game"; StartPosition = FormStartPosition.CenterParent;
            Size = new Size(650, 680); MinimumSize = new Size(500, 420); Font = new Font("Segoe UI", 9);
            var merged = AutoSelection.Merge(snapshots);
            var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8), WrapContents = false };
            var apply = new Button { Name = "autoApply", Text = "Áp dụng cho tab đã chọn", AutoSize = true, Height = 30 };
            var cancel = new Button { Text = "Đóng", AutoSize = true, Height = 30 };
            footer.Controls.Add(apply); footer.Controls.Add(cancel); footer.Controls.Add(validation);
            Controls.Add(rows); Controls.Add(footer);
            loading = true;
            for (int i = 0; i < AutoCatalog.Options.Length; i++) {
                if (i == 0 || i == 7 || i == 17 || i == 27) {
                    string title = i == 0 ? "Hồi phục và hỗ trợ" : i == 7 ? "Nhặt và lọc đồ" : i == 17 ? "Di chuyển và chiến đấu" : "Cộng điểm và nhóm";
                    rows.Controls.Add(new Label { Text = title, AutoSize = false, Width = 570, Height = 25, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 10, 0, 0) });
                }
                AutoOption option = AutoCatalog.Options[i]; string key = option.Key;
                var row = new Panel { Width = 570, Height = 30, Margin = new Padding(0) };
                string current = merged.Values[key];
                var box = new CheckBox { Name = "autoOption_" + key, Text = option.Label, Left = 6, Top = 5, Width = 380, ThreeState = true,
                    CheckState = current == null ? CheckState.Indeterminate : current == "true" ? CheckState.Checked : CheckState.Unchecked };
                checkboxes.Add(key, box);
                box.CheckStateChanged += delegate {
                    if (loading) return;
                    if (box.CheckState == CheckState.Indeterminate) changes.Remove(key);
                    else { changes[key] = box.CheckState == CheckState.Checked ? "true" : "false"; NormalizeChoices(key, box.CheckState == CheckState.Checked); }
                };
                row.Controls.Add(box);
                if (option.ValueKey != null) {
                    string numericKey = option.ValueKey;
                    var field = new TextBox { Name = "autoValue_" + numericKey, Text = merged.Values[numericKey] ?? "Khác nhau", Left = 402, Top = 3, Width = 90, MaxLength = 14 };
                    field.TextChanged += delegate { if (!loading) { if (field.Text == "Khác nhau") changes.Remove(numericKey); else changes[numericKey] = field.Text.Trim(); } };
                    field.Enter += delegate { if (field.Text == "Khác nhau") field.SelectAll(); };
                    row.Controls.Add(field);
                    row.Controls.Add(new Label { Left = 502, Top = 6, AutoSize = true, Text = numericKey == "ek" || numericKey == "el" ? "%" : "" });
                }
                rows.Controls.Add(row);
            }
            loading = false;
            apply.Click += delegate {
                validation.Text = "";
                if (changes.Count == 0) { validation.Text = "Chưa thay đổi mục nào."; return; }
                try { AutoEdit.Build("preview", 1, changes); DialogResult = DialogResult.OK; Close(); }
                catch (ArgumentException ex) { validation.Text = ex.Message; }
            };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        }
        void NormalizeChoices(string key, bool enabled)
        {
            if (!enabled) return;
            string[] others = key == "dt" ? new[] { "isAPickYHM", "isAPickYHMS", "dm", "doa", "dp", "dq", "dr", "ds", "weaponOnlyPickup" }
                : key == "weaponOnlyPickup" ? new[] { "doa", "dt" }
                : key == "doa" ? new[] { "weaponOnlyPickup", "dt" }
                : new[] { "isAPickYHM", "isAPickYHMS", "dm", "dp", "dq", "dr", "ds" }.Contains(key) ? new[] { "dt" } : new string[0];
            foreach (string other in others) checkboxes[other].CheckState = CheckState.Unchecked;
        }
    }
}
