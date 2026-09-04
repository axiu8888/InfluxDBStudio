using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CymaticLabs.InfluxDB.Studio.Controls
{
    /// <summary>
    /// Writes a simple .xlsx workbook without third-party Excel libraries.
    /// </summary>
    static class ExcelExport
    {
        public static void WriteXlsx(string path, string sheetName, IList<string> headers, IEnumerable<IList<string>> rows)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException("path");
            if (headers == null) throw new ArgumentNullException("headers");

            sheetName = SanitizeSheetName(sheetName);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "[Content_Types].xml", ContentTypesXml);
                WriteEntry(zip, "_rels/.rels", RelsXml);
                WriteEntry(zip, "xl/workbook.xml", WorkbookXml(sheetName));
                WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
                WriteEntry(zip, "xl/styles.xml", StylesXml);
                WriteEntry(zip, "xl/worksheets/sheet1.xml", BuildSheetXml(headers, rows));
            }
        }

        static string BuildSheetXml(IList<string> headers, IEnumerable<IList<string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetData>");

            sb.Append("<row r=\"1\">");
            for (var c = 0; c < headers.Count; c++)
                AppendCell(sb, 1, c, headers[c], header: true);
            sb.Append("</row>");

            var rowIndex = 2;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    sb.Append("<row r=\"").Append(rowIndex).Append("\">");
                    var count = row != null ? row.Count : 0;
                    for (var c = 0; c < headers.Count; c++)
                    {
                        var value = c < count && row[c] != null ? row[c] : string.Empty;
                        AppendCell(sb, rowIndex, c, value, header: false);
                    }
                    sb.Append("</row>");
                    rowIndex++;
                }
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        static void AppendCell(StringBuilder sb, int row, int column, string value, bool header)
        {
            var @ref = ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
            sb.Append("<c r=\"").Append(@ref).Append("\" t=\"inlineStr\"");
            if (header) sb.Append(" s=\"1\"");
            sb.Append("><is><t xml:space=\"preserve\">");
            sb.Append(EscapeXml(value));
            sb.Append("</t></is></c>");
        }

        static string ColumnName(int zeroBasedIndex)
        {
            var name = string.Empty;
            var index = zeroBasedIndex + 1;
            while (index > 0)
            {
                var remainder = (index - 1) % 26;
                name = ((char)('A' + remainder)) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        static string SanitizeSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Sheet1";
            var cleaned = name.Replace(":", " ").Replace("\\", " ").Replace("/", " ")
                .Replace("?", " ").Replace("*", " ").Replace("[", " ").Replace("]", " ");
            if (cleaned.Length > 31) cleaned = cleaned.Substring(0, 31);
            return cleaned.Trim().Length == 0 ? "Sheet1" : cleaned.Trim();
        }

        static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch == '&') sb.Append("&amp;");
                else if (ch == '<') sb.Append("&lt;");
                else if (ch == '>') sb.Append("&gt;");
                else if (ch == '"') sb.Append("&quot;");
                else if (ch == '\'') sb.Append("&apos;");
                else if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= 32)
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        static void WriteEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
        }

        const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        const string RelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        const string WorkbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"2\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "</fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf/></cellStyleXfs>" +
            "<cellXfs count=\"2\">" +
            "<xf xfId=\"0\"/>" +
            "<xf fontId=\"1\" xfId=\"0\" applyFont=\"1\"/>" +
            "</cellXfs>" +
            "</styleSheet>";

        static string WorkbookXml(string sheetName)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets><sheet name=\"" + EscapeXml(sheetName) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                "</workbook>";
        }
    }
}
