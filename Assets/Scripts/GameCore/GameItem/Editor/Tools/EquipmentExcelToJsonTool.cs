#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

public static class EquipmentExcelToJsonTool
{
    private const string PreferredSheetName = "Equipment";
    private const string OutputRelativePath = "Assets/Resources/GameData/ItemData/EquipmentData.json";
    private const string DefaultExcelFolderRelativePath = "Assets/Resources/Excel";

    [MenuItem("Tools/Game Item/Import Equipment Excel To Json")]
    public static void ImportEquipmentExcelToJson()
    {
        string excelPath = EditorUtility.OpenFilePanel("Select Equipment Excel File", GetDefaultExcelFolder(), "xlsx");
        if (string.IsNullOrEmpty(excelPath))
        {
            return;
        }

        try
        {
            List<Dictionary<string, string>> rows = XlsxReader.ReadSheetRows(excelPath, PreferredSheetName);
            EquipmentJsonCollection collection = BuildCollection(rows);
            string json = JsonUtility.ToJson(collection, true);

            string outputFullPath = Path.Combine(Directory.GetCurrentDirectory(), OutputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath));
            File.WriteAllText(outputFullPath, json, new UTF8Encoding(false));

            AssetDatabase.Refresh();
            Debug.Log($"[EquipmentExcelToJsonTool] Exported {collection.items.Count} equipment records to {OutputRelativePath}");
            EditorUtility.DisplayDialog("Export Completed", $"装备表已导出到:\n{OutputRelativePath}", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EquipmentExcelToJsonTool] Failed to import equipment excel.\n{ex}");
            EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
        }
    }

    private static string GetDefaultExcelFolder()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultExcelFolderRelativePath);
        return Directory.Exists(fullPath) ? fullPath : Application.dataPath;
    }

    private static EquipmentJsonCollection BuildCollection(List<Dictionary<string, string>> rows)
    {
        EquipmentJsonCollection collection = new EquipmentJsonCollection();

        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, string> row = rows[i];
            if (row == null || row.Count == 0)
            {
                continue;
            }

            int itemId = ReadInt(row, "itemId");
            if (itemId <= 0)
            {
                continue;
            }

            string equipSlotCode = ReadString(row, "equipSlot").ToUpperInvariant();
            if (equipSlotCode == "A")
            {
                continue;
            }

            EquipmentJsonRecord record = new EquipmentJsonRecord
            {
                itemId = itemId,
                itemName = ReadString(row, "itemName"),
                itemType = "Equipment",
                quality = MapQuality(ReadInt(row, "quality")),
                qualityValue = ReadInt(row, "quality"),
                equipSlot = MapEquipSlot(equipSlotCode),
                equipSlotCode = equipSlotCode,
                toolType = MapToolType(equipSlotCode, ReadString(row, "toolType")),
                description = ReadString(row, "description"),
                iconName = ReadString(row, "iconName"),
                modelName = ReadString(row, "ModelName", "modelName"),
                buyPrice = ReadInt(row, "buyPrice"),
                sellPrice = ReadInt(row, "sellPrice"),
                atk = ReadInt(row, "atk"),
                def = ReadInt(row, "def"),
                hp = ReadInt(row, "hp"),
                critRate = ReadFloat(row, "critRate"),
                critDamage = ReadFloat(row, "critDamage"),
                attackSpeed = ReadFloat(row, "attackSpeed"),
                moveSpeed = ReadFloat(row, "moveSpeed")
            };

            if (!IsWeaponModelSlot(record.equipSlotCode))
            {
                record.modelName = string.Empty;
            }

            record.functionDescription = BuildFunctionDescription(record);
            collection.items.Add(record);
        }

        return collection;
    }

    private static string BuildFunctionDescription(EquipmentJsonRecord record)
    {
        List<string> parts = new List<string>();

        AppendFlatStat(parts, "攻击力", record.atk);
        AppendFlatStat(parts, "防御力", record.def);
        AppendFlatStat(parts, "生命值", record.hp);
        AppendPercentStat(parts, "暴击率", record.critRate);
        AppendPercentStat(parts, "暴击伤害", record.critDamage);
        AppendPercentStat(parts, "攻击速度", record.attackSpeed);
        AppendPercentStat(parts, "移动速度", record.moveSpeed);

        return parts.Count > 0 ? string.Join("，", parts) : string.Empty;
    }

    private static void AppendFlatStat(List<string> parts, string label, int value)
    {
        if (value == 0)
        {
            return;
        }

        string sign = value > 0 ? "+" : string.Empty;
        parts.Add($"{label}{sign}{value}");
    }

    private static void AppendPercentStat(List<string> parts, string label, float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return;
        }

        string sign = value > 0f ? "+" : string.Empty;
        parts.Add($"{label}{sign}{FormatNumber(value)}%");
    }

    private static string FormatNumber(float value)
    {
        if (Mathf.Approximately(value % 1f, 0f))
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string MapQuality(int qualityValue)
    {
        switch (qualityValue)
        {
            case 1: return "Common";
            case 2: return "Advanced";
            case 3: return "Rare";
            case 4: return "Epic";
            case 5: return "Legendary";
            default: return "Common";
        }
    }

    private static string MapEquipSlot(string equipSlotCode)
    {
        switch ((equipSlotCode ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "W": return "Weapon";
            case "WA": return "Weapon";
            case "C": return "Chest";
            case "H": return "Head";
            case "L": return "Leg";
            case "A": return "Accessory";
            case "T": return "Tool";
            case "TP": return "Tool";
            case "TA": return "Tool";
            case "TOOL": return "Tool";
            default: return "Unknown";
        }
    }

    private static string MapToolType(string equipSlotCode, string explicitToolType)
    {
        if (!string.IsNullOrWhiteSpace(explicitToolType))
        {
            return explicitToolType.Trim();
        }

        switch ((equipSlotCode ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "TP": return "Pickaxe";
            case "TA": return "Axe";
            default: return string.Empty;
        }
    }

    private static bool IsWeaponModelSlot(string equipSlotCode)
    {
        string normalizedCode = (equipSlotCode ?? string.Empty).Trim().ToUpperInvariant();
        return normalizedCode == "W" || normalizedCode == "WA" || normalizedCode == "T" || normalizedCode == "TP" || normalizedCode == "TA" || normalizedCode == "TOOL";
    }

    private static string ReadString(Dictionary<string, string> row, params string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (row.TryGetValue(keys[i], out string value))
            {
                return value?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static int ReadInt(Dictionary<string, string> row, params string[] keys)
    {
        string raw = ReadString(row, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
        {
            return intValue;
        }

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
        {
            return Mathf.RoundToInt(floatValue);
        }

        return 0;
    }

    private static float ReadFloat(Dictionary<string, string> row, params string[] keys)
    {
        string raw = ReadString(row, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0f;
        }

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
        {
            return floatValue;
        }

        return 0f;
    }

    [Serializable]
    private class EquipmentJsonCollection
    {
        public List<EquipmentJsonRecord> items = new List<EquipmentJsonRecord>();
    }

    [Serializable]
    private class EquipmentJsonRecord
    {
        public int itemId;
        public string itemName;
        public string itemType;
        public string quality;
        public int qualityValue;
        public string equipSlot;
        public string equipSlotCode;
        public string toolType;
        public string description;
        public string functionDescription;
        public string iconName;
        public string modelName;
        public int buyPrice;
        public int sellPrice;
        public int atk;
        public int def;
        public int hp;
        public float critRate;
        public float critDamage;
        public float attackSpeed;
        public float moveSpeed;
    }

    private static class XlsxReader
    {
        private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static List<Dictionary<string, string>> ReadSheetRows(string xlsxPath, string preferredSheetName)
        {
            using (FileStream stream = File.OpenRead(xlsxPath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                string workbookXml = ReadEntryText(archive, "xl/workbook.xml");
                string workbookRelsXml = ReadEntryText(archive, "xl/_rels/workbook.xml.rels");
                string sharedStringsXml = ReadEntryTextOrNull(archive, "xl/sharedStrings.xml");

                XDocument workbookDoc = XDocument.Parse(workbookXml);
                XDocument workbookRelsDoc = XDocument.Parse(workbookRelsXml);
                List<string> sharedStrings = ParseSharedStrings(sharedStringsXml);

                string sheetPath = ResolveWorksheetPath(workbookDoc, workbookRelsDoc, preferredSheetName);
                string sheetXml = ReadEntryText(archive, sheetPath);
                return ParseSheetRows(sheetXml, sharedStrings);
            }
        }

        private static string ResolveWorksheetPath(XDocument workbookDoc, XDocument workbookRelsDoc, string preferredSheetName)
        {
            XElement sheetsElement = workbookDoc.Root?.Element(SpreadsheetNs + "sheets");
            if (sheetsElement == null)
            {
                throw new Exception("Excel workbook does not contain any sheets.");
            }

            List<XElement> sheetElements = sheetsElement.Elements(SpreadsheetNs + "sheet").ToList();
            XElement targetSheet = sheetElements.FirstOrDefault(sheet =>
                string.Equals((string)sheet.Attribute("name"), preferredSheetName, StringComparison.OrdinalIgnoreCase))
                ?? sheetElements.FirstOrDefault();

            if (targetSheet == null)
            {
                throw new Exception("No worksheet found in the Excel file.");
            }

            string relationshipId = (string)targetSheet.Attribute(RelationshipNs + "id");
            if (string.IsNullOrEmpty(relationshipId))
            {
                throw new Exception("Worksheet relationship id is missing.");
            }

            XElement relationship = workbookRelsDoc.Root?
                .Elements(PackageRelationshipNs + "Relationship")
                .FirstOrDefault(rel => string.Equals((string)rel.Attribute("Id"), relationshipId, StringComparison.Ordinal));

            if (relationship == null)
            {
                throw new Exception($"Cannot resolve worksheet relationship: {relationshipId}");
            }

            string target = (string)relationship.Attribute("Target");
            if (string.IsNullOrEmpty(target))
            {
                throw new Exception("Worksheet target path is empty.");
            }

            return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : $"xl/{target}";
        }

        private static List<string> ParseSharedStrings(string sharedStringsXml)
        {
            List<string> sharedStrings = new List<string>();
            if (string.IsNullOrWhiteSpace(sharedStringsXml))
            {
                return sharedStrings;
            }

            XDocument sharedStringsDoc = XDocument.Parse(sharedStringsXml);
            foreach (XElement item in sharedStringsDoc.Descendants(SpreadsheetNs + "si"))
            {
                IEnumerable<string> textParts = item.Descendants(SpreadsheetNs + "t").Select(t => t.Value);
                sharedStrings.Add(string.Concat(textParts));
            }

            return sharedStrings;
        }

        private static List<Dictionary<string, string>> ParseSheetRows(string sheetXml, List<string> sharedStrings)
        {
            XDocument sheetDoc = XDocument.Parse(sheetXml);
            List<List<string>> rawRows = new List<List<string>>();

            foreach (XElement rowElement in sheetDoc.Descendants(SpreadsheetNs + "row"))
            {
                List<string> rowValues = new List<string>();

                foreach (XElement cellElement in rowElement.Elements(SpreadsheetNs + "c"))
                {
                    string reference = (string)cellElement.Attribute("r");
                    int columnIndex = GetColumnIndex(reference);
                    while (rowValues.Count <= columnIndex)
                    {
                        rowValues.Add(string.Empty);
                    }

                    rowValues[columnIndex] = ReadCellValue(cellElement, sharedStrings);
                }

                rawRows.Add(rowValues);
            }

            if (rawRows.Count == 0)
            {
                return new List<Dictionary<string, string>>();
            }

            List<string> headers = rawRows[0].Select(NormalizeHeader).ToList();
            List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();

            for (int rowIndex = 1; rowIndex < rawRows.Count; rowIndex++)
            {
                List<string> row = rawRows[rowIndex];
                Dictionary<string, string> rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int columnIndex = 0; columnIndex < headers.Count; columnIndex++)
                {
                    string header = headers[columnIndex];
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        continue;
                    }

                    string value = columnIndex < row.Count ? row[columnIndex] : string.Empty;
                    rowDict[header] = value?.Trim() ?? string.Empty;
                }

                result.Add(rowDict);
            }

            return result;
        }

        private static string ReadCellValue(XElement cellElement, List<string> sharedStrings)
        {
            string cellType = (string)cellElement.Attribute("t");
            XElement valueElement = cellElement.Element(SpreadsheetNs + "v");
            XElement inlineStringElement = cellElement.Element(SpreadsheetNs + "is");

            if (cellType == "inlineStr" && inlineStringElement != null)
            {
                return string.Concat(inlineStringElement.Descendants(SpreadsheetNs + "t").Select(t => t.Value));
            }

            string rawValue = valueElement?.Value ?? string.Empty;
            if (cellType == "s" && int.TryParse(rawValue, out int sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedIndex];
            }

            return rawValue;
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return 0;
            }

            int index = 0;
            for (int i = 0; i < cellReference.Length; i++)
            {
                char c = cellReference[i];
                if (!char.IsLetter(c))
                {
                    break;
                }

                index *= 26;
                index += char.ToUpperInvariant(c) - 'A' + 1;
            }

            return Mathf.Max(0, index - 1);
        }

        private static string NormalizeHeader(string header)
        {
            return string.IsNullOrWhiteSpace(header) ? string.Empty : header.Trim();
        }

        private static string ReadEntryText(ZipArchive archive, string entryPath)
        {
            string text = ReadEntryTextOrNull(archive, entryPath);
            if (text == null)
            {
                throw new FileNotFoundException($"Entry not found in xlsx archive: {entryPath}");
            }

            return text;
        }

        private static string ReadEntryTextOrNull(ZipArchive archive, string entryPath)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryPath);
            if (entry == null)
            {
                return null;
            }

            using (Stream entryStream = entry.Open())
            using (StreamReader reader = new StreamReader(entryStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
#endif
