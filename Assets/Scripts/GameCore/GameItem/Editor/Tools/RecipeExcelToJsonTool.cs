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

public static class RecipeExcelToJsonTool
{
    private const string PreferredSheetName = "Recipe";
    // 输出到 Resources，保证打包后能通过 Resources.Load 读取（与 Item/Monster JSON 一致）。
    private const string OutputRelativePath = "Assets/Resources/GameData/RecipeData/RecipeData.json";
    private const string DefaultExcelFolderRelativePath = "Assets/Resources/Excel";

    [MenuItem("Tools/Game Item/Import Recipe Excel To Json")]
    public static void ImportRecipeExcelToJson()
    {
        string excelPath = EditorUtility.OpenFilePanel("Select Recipe Excel File", GetDefaultExcelFolder(), "xlsx");
        if (string.IsNullOrEmpty(excelPath))
        {
            return;
        }

        try
        {
            List<Dictionary<string, string>> rows = XlsxReader.ReadSheetRows(excelPath, PreferredSheetName);
            RecipeJsonCollection collection = BuildCollection(rows);
            string json = JsonUtility.ToJson(collection, true);

            string outputFullPath = Path.Combine(Directory.GetCurrentDirectory(), OutputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? string.Empty);
            File.WriteAllText(outputFullPath, json, new UTF8Encoding(false));

            AssetDatabase.Refresh();
            Debug.Log($"[RecipeExcelToJsonTool] Exported {collection.recipes.Count} recipe records to {OutputRelativePath}");
            EditorUtility.DisplayDialog("Export Completed", $"合成配方表已导出到\n{OutputRelativePath}", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecipeExcelToJsonTool] Failed to import recipe excel.\n{ex}");
            EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
        }
    }

    private static string GetDefaultExcelFolder()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultExcelFolderRelativePath);
        return Directory.Exists(fullPath) ? fullPath : Application.dataPath;
    }

    private static RecipeJsonCollection BuildCollection(List<Dictionary<string, string>> rows)
    {
        RecipeJsonCollection collection = new RecipeJsonCollection();

        for (int i = 0; i < rows.Count; i++)
        {
            Dictionary<string, string> row = rows[i];
            if (row == null || row.Count == 0)
            {
                continue;
            }

            int recipeId = ReadInt(row, "recipeId");
            int targetItemId = ReadInt(row, "targetItemId");
            if (recipeId <= 0 || targetItemId <= 0)
            {
                continue;
            }

            RecipeJsonRecord record = new RecipeJsonRecord
            {
                recipeId = recipeId,
                recipeName = SanitizeString(ReadString(row, "recipeName")),
                targetItemId = targetItemId,
                targetCount = Mathf.Max(1, ReadInt(row, "targetCount"))
            };

            string costItemsRaw = ReadString(row, "costItems");
            record.materials = ParseMaterials(costItemsRaw);
            collection.recipes.Add(record);
        }

        return collection;
    }

    private static List<RecipeMaterialRecord> ParseMaterials(string raw)
    {
        List<RecipeMaterialRecord> materials = new List<RecipeMaterialRecord>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return materials;
        }

        string[] entries = raw.Split(new[] { '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            string[] parts = entry.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Debug.LogWarning($"[RecipeExcelToJsonTool] Invalid costItems entry: {entry}");
                continue;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                Debug.LogWarning($"[RecipeExcelToJsonTool] Failed to parse material entry: {entry}");
                continue;
            }

            if (itemId <= 0 || count <= 0)
            {
                continue;
            }

            materials.Add(new RecipeMaterialRecord
            {
                itemId = itemId,
                count = count
            });
        }

        return materials;
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

    private static string SanitizeString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsControl(c) || c == '\r' || c == '\n' || c == '\t')
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
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

    [Serializable]
    private class RecipeJsonCollection
    {
        public List<RecipeJsonRecord> recipes = new List<RecipeJsonRecord>();
    }

    [Serializable]
    private class RecipeJsonRecord
    {
        public int recipeId;
        public string recipeName;
        public int targetItemId;
        public int targetCount;
        public List<RecipeMaterialRecord> materials = new List<RecipeMaterialRecord>();
    }

    [Serializable]
    private class RecipeMaterialRecord
    {
        public int itemId;
        public int count;
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
