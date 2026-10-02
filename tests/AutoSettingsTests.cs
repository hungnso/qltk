using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using QltkAccounts;

class AutoSettingsTests {
    static int count;
    static void Check(bool condition, string message) { count++; if (!condition) throw new Exception(message); }
    static string State(string session, bool food, int hp) {
        var options = AutoCatalog.Options.Select(option => new XElement("Option",
            new XAttribute("key", option.Key), new XAttribute("enabled", option.Key == "isAFood" ? food : false),
            option.ValueKey == null ? null : new XAttribute("value", option.ValueKey == "ek" ? hp : option.Minimum)));
        return new XDocument(new XElement("AutoState", new XAttribute("session", session), options)).ToString();
    }
    static void Main() {
        string[] keys = { "timeStartBlink", "isAHP", "isAMP", "isAFood", "isABuff", "isAResuscitate", "isAPickYen", "isAPickYHM", "isAPickYHMS", "dm", "dn", "doa", "dp", "dq", "dr", "ds", "dt", "du", "dv", "dw", "dx", "dy", "dz", "ea", "eb", "ec", "ed", "ee", "ef", "eg", "eh", "weaponOnlyPickup" };
        Check(AutoCatalog.Options.Select(o => o.Key).SequenceEqual(keys), "The catalog must match the 32 visible game options in display order.");
        Check(AutoCatalog.Options[0].Label == "Dùng HP khi còn dưới" && AutoCatalog.Options[31].Label == "Chỉ nhặt vũ khí", "Labels must match the game menu.");
        Check(AutoCatalog.Options.Count(o => o.ValueKey != null) == 7, "Seven settings have a numeric value.");
        var first = AutoSnapshot.Parse(State("session-a", true, 50));
        var second = AutoSnapshot.Parse(State("session-b", false, 60));
        var merged = AutoSelection.Merge(new[] { first, second });
        Check(merged.Values["isAFood"] == null && merged.Values["ek"] == null, "Different values must be shown as mixed.");
        Check(merged.Values["isABuff"] == "false", "Equal values must retain their common value.");
        var edits = new Dictionary<string,string> { { "isAFood", "true" } };
        XElement command = XElement.Parse(AutoEdit.Build("session-a", 7, edits));
        Check((string)command.Attribute("session") == "session-a" && (string)command.Attribute("id") == "7", "Command must identify the tab session and request.");
        Check(command.Elements("Set").Count() == 1 && (string)command.Element("Set").Attribute("key") == "isAFood" && (string)command.Element("Set").Attribute("enabled") == "true", "Only touched fields may be sent.");
        Check(!command.ToString().Contains("ek") && !command.ToString().Contains("isABuff"), "Unedited settings must not be copied from one tab to another.");
        command = XElement.Parse(AutoEdit.Build("session-a", 8, new Dictionary<string,string> { { "dt", "true" } }));
        Check(command.Elements("Set").Any(e => (string)e.Attribute("key") == "doa" && (string)e.Attribute("enabled") == "false") && command.Elements("Set").Any(e => (string)e.Attribute("key") == "weaponOnlyPickup" && (string)e.Attribute("enabled") == "false"), "No-pick mode must clear conflicting pickup options.");
        command = XElement.Parse(AutoEdit.Build("session-a", 9, new Dictionary<string,string> { { "weaponOnlyPickup", "true" } }));
        Check(command.Elements("Set").Any(e => (string)e.Attribute("key") == "doa" && (string)e.Attribute("enabled") == "false"), "Weapon-only pickup must clear general equipment pickup.");
        foreach (var invalid in new[] { new Dictionary<string,string> { { "ek", "100" } }, new Dictionary<string,string> { { "el", "9" } }, new Dictionary<string,string> { { "em", "25" } }, new Dictionary<string,string> { { "unknown", "true" } }, new Dictionary<string,string> { { "isAFood", "maybe" } } }) {
            bool rejected = false; try { AutoEdit.Build("session-a", 10, invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid Auto value must be rejected before writing a command.");
        }
        bool tooLarge = false; try { AutoSnapshot.Parse(new string('X', 300000)); } catch (Exception ex) { tooLarge = ex is ArgumentException || ex is System.IO.InvalidDataException; }
        Check(tooLarge, "Oversized Auto state must be rejected before XML parsing.");
        Console.WriteLine("PASS: " + count + " Auto catalog, mixed-state, partial-update and validation assertions");
    }
}
