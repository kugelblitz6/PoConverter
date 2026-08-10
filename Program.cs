using System.Text;

var modeIndex = Array.IndexOf(args, "-mode");
var modeValue = modeIndex >= 0 && modeIndex + 1 < args.Length ? args[modeIndex + 1] : "csv";

if (modeValue != "csv" && modeValue != "po")
{
    Console.Error.WriteLine($"Error: -mode の値が不正です: {modeValue}（csv または po を指定してください）");
    return 1;
}

var poMode = modeValue == "po";
var split = args.Contains("-split");

if (!TryParseArgs(args, out var inputPath, out var outputPath))
{
    Console.Error.WriteLine("Usage: PoConverter.exe [-mode csv] -input <po-directory> -output <csv-filepath> [-split]");
    Console.Error.WriteLine("       PoConverter.exe -mode po -input <csv-filepath> -output <po-directory> [-split]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  -split  msgid の名前空間（先頭のカンマまでの部分）ごとにCSVを分割する。");
    Console.Error.WriteLine("          csv: -output に Foo.csv を指定すると Foo_<名前空間>.csv を出力する（Foo.csv 自体は作らない）");
    Console.Error.WriteLine("          po : -input に Foo.csv を指定すると Foo_*.csv をすべて読んで1組のPOファイルにまとめる");
    return 1;
}

return poMode ? CsvToPo(inputPath, outputPath, split) : PoToCsv(inputPath, outputPath, split);

static bool TryParseArgs(string[] args, out string inputPath, out string outputPath)
{
    inputPath = string.Empty;
    outputPath = string.Empty;
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "-input") inputPath = args[i + 1];
        else if (args[i] == "-output") outputPath = args[i + 1];
    }
    return !string.IsNullOrEmpty(inputPath) && !string.IsNullOrEmpty(outputPath);
}

static int PoToCsv(string inputDir, string outputFile, bool split)
{
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

    var headerLine = QuoteCsv("msgid") + "," +
        string.Join(",", languages.Select(l => QuoteCsv(headerLabels.TryGetValue(l, out var label) ? label : l)));

    void WriteCsv(string path, List<string> msgids)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.Write(headerLine);
        writer.Write("\r\n");

        foreach (var msgid in msgids)
        {
            var row = languages.Select(lang =>
                translations[lang].TryGetValue(msgid, out var val) ? QuoteCsv(val) : "\"\"");
            writer.Write(QuoteCsv(msgid) + "," + string.Join(",", row));
            writer.Write("\r\n");
        }
    }

    if (!split)
    {
        WriteCsv(outputFile, msgidOrder);
        Console.WriteLine($"CSV written to: {outputFile}");
        return 0;
    }

    var outDir = Path.GetDirectoryName(Path.GetFullPath(outputFile)) ?? ".";
    var baseName = Path.GetFileNameWithoutExtension(outputFile);
    var ext = Path.GetExtension(outputFile);
    if (string.IsNullOrEmpty(ext)) ext = ".csv";

    // 名前空間ごとにまとめる。ファイル内の並びはPOに現れた順のまま
    var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var msgid in msgidOrder)
    {
        var category = CategoryOf(msgid);
        if (!groups.TryGetValue(category, out var list))
        {
            list = [];
            groups[category] = list;
        }
        list.Add(msgid);
    }

    Directory.CreateDirectory(outDir);

    // ファイル名順に出力する。生成順が実行ごとに変わらないようにするため
    foreach (var category in groups.Keys.OrderBy(c => c, StringComparer.Ordinal))
    {
        var path = Path.Combine(outDir, $"{baseName}_{SanitizeFileName(category)}{ext}");
        WriteCsv(path, groups[category]);
        Console.WriteLine($"  {Path.GetFileName(path)}  ({groups[category].Count} entries)");
    }

    Console.WriteLine($"CSV written to: {outDir}  ({groups.Count} files, {msgidOrder.Count} entries)");

    // -split では分割前のファイルを読み書きしないので、残っていると古い内容を編集してしまいやすい
    var unsplitPath = Path.Combine(outDir, baseName + ext);
    if (File.Exists(unsplitPath))
        Console.WriteLine($"Note: 分割前の {Path.GetFileName(unsplitPath)} が残っています。-split 指定時は読み書きされません。");

    return 0;
}

static int CsvToPo(string csvPath, string outputDir, bool split)
{
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

    var baseName = Path.GetFileNameWithoutExtension(csvPath);
    var poFileName = baseName + ".po";
    List<string> inputFiles;

    if (split)
    {
        // -input には分割前のパス（例 Game.csv）を渡してもらい、そこから Game_*.csv を集める。
        // こうすると csv 側と po 側で -input / -output の指定が対称になり、
        // POファイル名も分割前の名前のまま決まる
        var csvDir = Path.GetDirectoryName(Path.GetFullPath(csvPath)) ?? ".";
        var ext = Path.GetExtension(csvPath);
        if (string.IsNullOrEmpty(ext)) ext = ".csv";

        if (!Directory.Exists(csvDir))
        {
            Console.Error.WriteLine($"Directory not found: {csvDir}");
            return 1;
        }

        inputFiles = Directory.GetFiles(csvDir, $"{baseName}_*{ext}")
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (inputFiles.Count == 0)
        {
            Console.Error.WriteLine($"分割CSVが見つかりません: {Path.Combine(csvDir, $"{baseName}_*{ext}")}");
            return 1;
        }
    }
    else
    {
        if (!File.Exists(csvPath))
        {
            Console.Error.WriteLine($"CSV file not found: {csvPath}");
            return 1;
        }
        inputFiles = [csvPath];
    }

    // 全CSVを読み、列構成の一致とmsgidの重複を確認したうえで行を連結する
    List<string>? headers = null;
    var headerSource = string.Empty;
    var rows = new List<List<string>>();
    var msgidSource = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var path in inputFiles)
    {
        var fileRows = ParseCsvRows(File.ReadAllText(path, Encoding.UTF8)).ToList();

        if (fileRows.Count == 0)
        {
            Console.Error.WriteLine($"CSV file is empty: {path}");
            return 1;
        }

        if (headers == null)
        {
            headers = fileRows[0];
            headerSource = path;
        }
        else if (!headers.SequenceEqual(fileRows[0], StringComparer.Ordinal))
        {
            // 言語列がずれたまま連結すると、翻訳が別の言語のPOに書き込まれる
            Console.Error.WriteLine("列構成が一致しません。");
            Console.Error.WriteLine($"  {Path.GetFileName(headerSource)}: {string.Join(" | ", headers)}");
            Console.Error.WriteLine($"  {Path.GetFileName(path)}: {string.Join(" | ", fileRows[0])}");
            return 1;
        }

        var count = 0;
        foreach (var fields in fileRows.Skip(1))
        {
            if (fields.Count == 0) continue;

            var msgid = fields[0];
            if (string.IsNullOrWhiteSpace(msgid)) continue;

            if (msgidSource.TryGetValue(msgid, out var prev))
            {
                // 同じmsgidが2度出るPOは不正。どちらが採用されるか分からない状態でインポートさせない
                Console.Error.WriteLine($"msgid が重複しています: {msgid}");
                Console.Error.WriteLine($"  {Path.GetFileName(prev)} と {Path.GetFileName(path)}");
                return 1;
            }

            msgidSource[msgid] = path;
            rows.Add(fields);
            count++;
        }

        if (split)
            Console.WriteLine($"  {Path.GetFileName(path)}  ({count} entries)");
    }

    // headers[0] = "msgid", headers[1..] = language labels
    var langCodes = headers!.Skip(1)
        .Select(h => labelToCode.TryGetValue(h, out var code) ? code : h)
        .ToList();

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

        foreach (var fields in rows)
        {
            var msgid = fields[0];
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

    if (split)
        Console.WriteLine($"PO files written to: {outputDir}  ({inputFiles.Count} files, {rows.Count} entries)");
    else
        Console.WriteLine($"PO files written to: {outputDir}");

    return 0;
}

// msgid は "名前空間,キー" の形。名前空間が無いものはまとめて1ファイルにする
static string CategoryOf(string msgid)
{
    var index = msgid.IndexOf(',');
    return index > 0 ? msgid[..index] : "_NoNamespace";
}

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var sb = new StringBuilder(name.Length);
    foreach (var c in name)
    {
        sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
    }
    return sb.ToString();
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
