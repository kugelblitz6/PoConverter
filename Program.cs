using System.Text;

var modeIndex = Array.IndexOf(args, "-mode");
var modeValue = modeIndex >= 0 && modeIndex + 1 < args.Length ? args[modeIndex + 1] : "csv";

if (modeValue != "csv" && modeValue != "po")
{
    Console.Error.WriteLine($"Error: -mode の値が不正です: {modeValue}（csv または po を指定してください）");
    return 1;
}

var poMode = modeValue == "po";

if (!TryParseArgs(args, out var inputDir, out var outputFile))
{
    Console.Error.WriteLine("Usage: PoConverter.exe [-mode csv] -input <po-directory> -output <csv-filepath>");
    Console.Error.WriteLine("       PoConverter.exe -mode po -input <csv-filepath> -output <po-directory>");
    return 1;
}

if (poMode)
    return CsvToPo(inputDir, outputFile);

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"Input directory not found: {inputDir}");
    return 1;
}

var columnOrder = new[] { "ja", "en", "zh-Hans", "zh-Hant", "de", "ru", "fr", "es", "pt", "it", "ko" };
var headerLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["ja"]      = "日本語",
    ["en"]      = "英語",
    ["zh-Hans"] = "中国語（簡体字）",
    ["zh-Hant"] = "中国語（繁体字）",
    ["de"]      = "ドイツ語",
    ["ru"]      = "ロシア語",
    ["fr"]      = "フランス語",
    ["es"]      = "スペイン語",
    ["pt"]      = "ポルトガル語",
    ["it"]      = "イタリア語",
    ["ko"]      = "韓国語",
};

// ディレクトリ名 → パス のマップ（大文字小文字を無視）
var langDirMap = Directory.GetDirectories(inputDir)
    .Where(d => Directory.GetFiles(d, "*.po").Length > 0)
    .ToDictionary(d => Path.GetFileName(d) ?? "", StringComparer.OrdinalIgnoreCase);

if (langDirMap.Count == 0)
{
    Console.Error.WriteLine("No PO files found in any subdirectory.");
    return 1;
}

// 固定順で存在する言語を並べ、固定順にない言語は末尾に追加
var languages = columnOrder
    .Where(langDirMap.ContainsKey)
    .Concat(langDirMap.Keys.Where(k => !columnOrder.Any(c => string.Equals(c, k, StringComparison.OrdinalIgnoreCase))))
    .ToList();

var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
var msgidOrder = new List<string>();

foreach (var lang in languages)
{
    translations[lang] = [];

    foreach (var poFile in Directory.GetFiles(langDirMap[lang], "*.po"))
    {
        foreach (var (msgid, msgstr) in ParsePoFile(poFile))
        {
            if (string.IsNullOrEmpty(msgid)) continue;
            translations[lang].TryAdd(msgid, msgstr);
            if (!msgidOrder.Contains(msgid))
                msgidOrder.Add(msgid);
        }
    }
}

using var writer = new StreamWriter(outputFile, false, new UTF8Encoding(true));

var headerCols = languages.Select(l => QuoteCsv(headerLabels.TryGetValue(l, out var label) ? label : l));
writer.Write(QuoteCsv("msgid") + "," + string.Join(",", headerCols));
writer.Write("\r\n");

foreach (var msgid in msgidOrder)
{
    var row = languages.Select(lang =>
        translations[lang].TryGetValue(msgid, out var val) ? QuoteCsv(val) : "\"\"");
    writer.Write(QuoteCsv(msgid) + "," + string.Join(",", row));
    writer.Write("\r\n");
}

Console.WriteLine($"CSV written to: {outputFile}");
return 0;

static bool TryParseArgs(string[] args, out string inputDir, out string outputFile)
{
    inputDir = string.Empty;
    outputFile = string.Empty;
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "-input") inputDir = args[i + 1];
        else if (args[i] == "-output") outputFile = args[i + 1];
    }
    return !string.IsNullOrEmpty(inputDir) && !string.IsNullOrEmpty(outputFile);
}

static IEnumerable<(string msgid, string msgstr)> ParsePoFile(string filePath)
{
    var lines = File.ReadAllLines(filePath, Encoding.UTF8);
    string? msgid = null;
    string? msgstr = null;
    bool inMsgid = false;
    bool inMsgstr = false;

    foreach (var line in lines)
    {
        if (line.StartsWith("msgid "))
        {
            if (msgid != null && msgstr != null)
                yield return (msgid, msgstr);

            msgid = DecodePoString(line[6..].Trim());
            msgstr = null;
            inMsgid = true;
            inMsgstr = false;
        }
        else if (line.StartsWith("msgstr "))
        {
            msgstr = DecodePoString(line[7..].Trim());
            inMsgid = false;
            inMsgstr = true;
        }
        else if (line.StartsWith('"'))
        {
            if (inMsgid && msgid != null)
                msgid += DecodePoString(line.Trim());
            else if (inMsgstr && msgstr != null)
                msgstr += DecodePoString(line.Trim());
        }
        else if (string.IsNullOrWhiteSpace(line))
        {
            if (msgid != null && msgstr != null)
                yield return (msgid, msgstr);
            msgid = null;
            msgstr = null;
            inMsgid = false;
            inMsgstr = false;
        }
    }

    if (msgid != null && msgstr != null)
        yield return (msgid, msgstr);
}

static string DecodePoString(string s)
{
    if (s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"'))
        s = s[1..^1];
    return s
        .Replace("\\\\", "\x00BACKSLASH\x00")
        .Replace("\\n", "\n")
        .Replace("\\r", "\r")
        .Replace("\\t", "\t")
        .Replace("\\\"", "\"")
        .Replace("\x00BACKSLASH\x00", "\\");
}

static string QuoteCsv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

static int CsvToPo(string csvPath, string outputDir)
{
    if (!File.Exists(csvPath))
    {
        Console.Error.WriteLine($"CSV file not found: {csvPath}");
        return 1;
    }

    var labelToCode = new Dictionary<string, string>
    {
        ["日本語"]        = "ja",
        ["英語"]          = "en",
        ["中国語（簡体字）"] = "zh-Hans",
        ["中国語（繁体字）"] = "zh-Hant",
        ["ドイツ語"]      = "de",
        ["ロシア語"]      = "ru",
        ["フランス語"]    = "fr",
        ["スペイン語"]    = "es",
        ["ポルトガル語"]  = "pt",
        ["イタリア語"]    = "it",
        ["韓国語"]        = "ko",
    };

    var content = File.ReadAllText(csvPath, Encoding.UTF8);
    var allRows = ParseCsvRows(content).ToList();

    if (allRows.Count == 0)
    {
        Console.Error.WriteLine("CSV file is empty.");
        return 1;
    }

    var headers = allRows[0];
    // headers[0] = "msgid", headers[1..] = language labels
    var langCodes = headers.Skip(1)
        .Select(h => labelToCode.TryGetValue(h, out var code) ? code : h)
        .ToList();

    var poFileName = Path.GetFileNameWithoutExtension(csvPath) + ".po";
    var poHeader = "msgid \"\"\r\nmsgstr \"\"\r\n\"Content-Type: text/plain; charset=UTF-8\\n\"\r\n\"Content-Transfer-Encoding: 8bit\\n\"\r\n";

    var writers = new Dictionary<string, StreamWriter>();
    try
    {
        foreach (var lang in langCodes)
        {
            var langDir = Path.Combine(outputDir, lang);
            Directory.CreateDirectory(langDir);
            var w = new StreamWriter(Path.Combine(langDir, poFileName), false, new UTF8Encoding(false));
            writers[lang] = w;
            w.Write(poHeader);
        }

        foreach (var fields in allRows.Skip(1))
        {
            if (fields.Count == 0) continue;

            var msgid = fields[0];
            if (string.IsNullOrWhiteSpace(msgid)) continue;
            for (int i = 0; i < langCodes.Count; i++)
            {
                var msgstr = i + 1 < fields.Count ? fields[i + 1] : string.Empty;
                var w = writers[langCodes[i]];
                w.Write("\r\n");
                w.Write($"msgid \"{EscapePoString(msgid)}\"\r\n");
                w.Write($"msgstr \"{EscapePoString(msgstr)}\"\r\n");
            }
        }
    }
    finally
    {
        foreach (var w in writers.Values) w.Dispose();
    }

    Console.WriteLine($"PO files written to: {outputDir}");
    return 0;
}

static IEnumerable<List<string>> ParseCsvRows(string content)
{
    int i = 0;
    while (i < content.Length)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool rowEnded = false;

        while (i < content.Length)
        {
            char c = content[i];

            if (c == '"')
            {
                i++;
                while (i < content.Length)
                {
                    if (content[i] == '"')
                    {
                        i++;
                        if (i < content.Length && content[i] == '"') { sb.Append('"'); i++; }
                        else break;
                    }
                    else sb.Append(content[i++]);
                }
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
                i++;
            }
            else if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
            {
                fields.Add(sb.ToString());
                i += 2;
                rowEnded = true;
                break;
            }
            else if (c == '\n')
            {
                fields.Add(sb.ToString());
                i++;
                rowEnded = true;
                break;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        if (!rowEnded)
            fields.Add(sb.ToString());

        if (fields.Count > 0)
            yield return fields;
    }
}


static string EscapePoString(string s) =>
    s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
