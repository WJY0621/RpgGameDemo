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

public static class MonsterExcelToJsonTool
{
    private const string PreferredSheetName = "Monster";
    // 输出到 Resources，保证打包后能通过 Resources.Load 读取（与 Item JSON 一致）。
    private const string OutputRelativePath = "Assets/Resources/GameData/MonsterData/MonsterData.json";
    private const string DefaultExcelFolderRelativePath = "Assets/Resources/Excel";

    [MenuItem("Tools/Game Character/Import Monster Excel To Json")]
    public static void ImportMonsterExcelToJson()
    {
        string excelPath = EditorUtility.OpenFilePanel("Select Monster Excel File", GetDefaultExcelFolder(), "xlsx");
        if (string.IsNullOrEmpty(excelPath))
        {
            return;
        }

        try
        {
            List<Dictionary<string, string>> rows = XlsxReader.ReadSheetRows(excelPath, PreferredSheetName);
            MonsterJsonCollection collection = BuildCollection(rows);
            string json = JsonUtility.ToJson(collection, true);

            string outputFullPath = Path.Combine(Directory.GetCurrentDirectory(), OutputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath));
            File.WriteAllText(outputFullPath, json, new UTF8Encoding(false));

            AssetDatabase.Refresh();
            Debug.Log($"[MonsterExcelToJsonTool] Exported {collection.items.Count} monster records to {OutputRelativePath}");
            EditorUtility.DisplayDialog("Export Completed", $"������ѵ�����\n{OutputRelativePath}", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MonsterExcelToJsonTool] Failed to import monster excel.\n{ex}");
            EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
        }
    }

    private static string GetDefaultExcelFolder()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultExcelFolderRelativePath);
        return Directory.Exists(fullPath) ? fullPath : Application.dataPath;
    }

    private static MonsterJsonCollection BuildCollection(List<Dictionary<string, string>> rows)
    {
        MonsterJsonCollection collection = new MonsterJsonCollection();

        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, string> row = rows[i];
            if (row == null || row.Count == 0)
            {
                continue;
            }

            int monsterID = ReadInt(row, "monsterID", "monsterId");
            if (monsterID <= 0)
            {
                continue;
            }

            MonsterJsonRecord record = new MonsterJsonRecord
            {
                monsterID = monsterID,
                monsterName = ReadString(row, "monsterName"),
                modelName = ReadString(row, "modelName"),
                HP = ReadInt(row, "HP", "hp"),
                ATK = ReadInt(row, "ATK", "atk"),
                DEF = ReadInt(row, "DEF", "def"),
                moveSpeed = ReadFloat(row, "moveSpeed"),
                alertRange = ReadFloat(row, "alertRange"),
                chaseRange = ReadFloat(row, "chaseRange"),
                attackRange = ReadFloat(row, "attackRange"),
                loseTargetRange = ReadFloat(row, "loseTargetRange"),
                goldDrop = ReadString(row, "goldDrop"),
                dropItems = ParseDropItems(ReadString(row, "dropItems"))
            };

            collection.items.Add(record);
        }

        return collection;
    }

    private static List<MonsterDropItemData> ParseDropItems(string raw)
    {
        List<MonsterDropItemData> result = new List<MonsterDropItemData>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        string[] entries = raw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split('_');
            if (parts.Length < 2)
            {
                continue;
            }

            if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemID) || itemID <= 0)
            {
                continue;
            }

            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float dropChance))
            {
                continue;
            }

            int amount = 1;
            if (parts.Length >= 3)
            {
                int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
                amount = Mathf.Max(1, amount);
            }

            result.Add(new MonsterDropItemData
            {
                itemID = itemID,
                dropChance = Mathf.Clamp(dropChance, 0f, 100f),
                amount = amount
            });
        }

        return result;
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
    private class MonsterJsonCollection
    {
        public List<MonsterJsonRecord> items = new List<MonsterJsonRecord>();
    }

    [Serializable]
    private class MonsterJsonRecord
    {
        public int monsterID;
        public string monsterName;
        public string modelName;
        public int HP;
        public int ATK;
        public int DEF;
        public float moveSpeed;
        public float alertRange;
        public float chaseRange;
        public float attackRange;
        public float loseTargetRange;
        public string goldDrop;
        public List<MonsterDropItemData> dropItems = new List<MonsterDropItemData>();
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
