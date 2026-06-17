using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkDemoServer.Contracts;

namespace WorkDemoServer.Services;

/// <summary>
/// 热更内容服务。采用"发布(Publish)"模型，而非实时扫盘：
/// - 编辑 HotUpdateContent 下的文件不会立刻影响客户端；
/// - 调用 Publish() 才会扫盘、和上一版对比出差异、把带版本号的清单冻结到 Data/hotupdate_published.json；
/// - 客户端拿到的是这份【已发布】清单。
/// 好处：避免半成品被下发、可追踪版本与改动、清单只在发布时算一次。
/// </summary>
public class HotUpdateService
{
    private const string BuildTarget = "StandaloneWindows64";

    private readonly string _root;
    private readonly string _publishedFile;
    private readonly string _addressablesContent;
    private readonly string _projectRoot;
    private static readonly object _lock = new object();

    public HotUpdateService(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "HotUpdateContent");
        _publishedFile = Path.Combine(env.ContentRootPath, "Data", "hotupdate_published.json");
        _addressablesContent = Path.Combine(env.ContentRootPath, "AddressablesContent");
        // 服务器在工程根/WorkDemoServer 下运行，上一级就是 Unity 工程根
        _projectRoot = Directory.GetParent(env.ContentRootPath)?.FullName ?? env.ContentRootPath;
    }

    /// <summary>
    /// 从 Unity 工程把配置 JSON 和 Addressables 远程产物同步到服务器（等价于 deploy-hotupdate.bat）。
    /// 返回操作日志。注意：Unity 里的"导 JSON / Build Addressables"仍需先在编辑器做。
    /// </summary>
    public List<string> DeployFromProject()
    {
        var log = new List<string>();
        log.AddRange(DeployConfigJson());
        log.AddRange(SyncAddressables());
        return log;
    }

    // 配置 JSON：Assets/Resources/GameData/**.json → HotUpdateContent/GameData/**
    private List<string> DeployConfigJson()
    {
        var log = new List<string>();
        string cfgSrc = Path.Combine(_projectRoot, "Assets", "Resources", "GameData");
        string cfgDst = Path.Combine(_root, "GameData");
        if (!Directory.Exists(cfgSrc))
        {
            log.Add($"[配置] 未找到 {cfgSrc}，跳过");
            return log;
        }

        int count = 0;
        foreach (var f in Directory.EnumerateFiles(cfgSrc, "*.json", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(cfgSrc, f);
            string target = Path.Combine(cfgDst, rel);
            string dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.Copy(f, target, true);
            count++;
        }

        log.Add($"[配置] 已部署 {count} 个 JSON (Resources/GameData → HotUpdateContent/GameData)");
        return log;
    }

    // Addressables：ServerData/<BuildTarget> 的 catalog + 远程 bundle → AddressablesContent/<BuildTarget>
    private List<string> SyncAddressables()
    {
        var log = new List<string>();
        string src = Path.Combine(_projectRoot, "ServerData", BuildTarget);
        string dst = Path.Combine(_addressablesContent, BuildTarget);
        if (!Directory.Exists(src))
        {
            log.Add($"[资源] 未找到 {src}（没构建过 Addressables？），跳过");
            return log;
        }

        string catalog = Directory.GetFiles(src, "catalog_*.json").FirstOrDefault();
        if (catalog == null)
        {
            log.Add("[资源] ServerData 里没有 catalog_*.json，跳过");
            return log;
        }

        string catalogText = File.ReadAllText(catalog);
        var remoteBundles = Regex.Matches(catalogText, "http://[^\"]*?/([^\"/]+\\.bundle)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (Directory.Exists(dst))
        {
            Directory.Delete(dst, true);
        }
        Directory.CreateDirectory(dst);

        foreach (var f in Directory.GetFiles(src, "catalog_*.json"))
        {
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        }
        foreach (var f in Directory.GetFiles(src, "catalog_*.hash"))
        {
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        }
        foreach (var b in remoteBundles)
        {
            string bp = Path.Combine(src, b);
            if (File.Exists(bp))
            {
                File.Copy(bp, Path.Combine(dst, b), true);
            }
        }

        log.Add($"[资源] 已同步 Addressables：catalog + {remoteBundles.Count} 个远程 bundle");
        return log;
    }

    /// <summary>客户端拿到的【已发布】清单（不是实时扫盘）。未发布过则版本 0、文件为空。</summary>
    public HotUpdateManifestResponse GetPublishedManifest()
    {
        PublishedManifest pub = LoadPublished();
        return new HotUpdateManifestResponse(pub.Version, pub.Files);
    }

    /// <summary>
    /// 发布：扫盘 → 和上一版对比 → 记录新版本与差异 → 冻结清单。返回本次差异。
    /// </summary>
    public HotUpdatePublishResult Publish()
    {
        lock (_lock)
        {
            List<HotUpdateFileEntry> current = ScanCurrent();
            PublishedManifest prev = LoadPublished();

            var prevMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in prev.Files)
            {
                prevMap[f.Path] = f.Hash;
            }

            var currMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in current)
            {
                currMap[f.Path] = f.Hash;
            }

            var added = new List<string>();
            var modified = new List<string>();
            foreach (var f in current)
            {
                if (!prevMap.TryGetValue(f.Path, out var oldHash))
                {
                    added.Add(f.Path);
                }
                else if (!string.Equals(oldHash, f.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    modified.Add(f.Path);
                }
            }

            var removed = new List<string>();
            foreach (var f in prev.Files)
            {
                if (!currMap.ContainsKey(f.Path))
                {
                    removed.Add(f.Path);
                }
            }

            var published = new PublishedManifest
            {
                Version = prev.Version + 1,
                PublishedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
                Files = current
            };
            SavePublished(published);

            return new HotUpdatePublishResult(published.Version, published.PublishedAt, added, modified, removed);
        }
    }

    /// <summary>按相对路径读取文件内容（带目录穿越防护）。客户端按已发布清单来下载这些文件。</summary>
    public bool TryReadFile(string relativePath, out string content)
    {
        content = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        string fullRoot = Path.GetFullPath(_root);
        string candidate = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

        bool insideRoot =
            candidate.Equals(fullRoot, StringComparison.Ordinal) ||
            candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (!insideRoot || !File.Exists(candidate))
        {
            return false;
        }

        content = File.ReadAllText(candidate);
        return true;
    }

    /// <summary>实时扫描当前 HotUpdateContent（仅 Publish 内部使用）。</summary>
    private List<HotUpdateFileEntry> ScanCurrent()
    {
        var entries = new List<HotUpdateFileEntry>();
        if (Directory.Exists(_root))
        {
            var files = Directory.EnumerateFiles(_root, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase));
            foreach (var file in files)
            {
                string relative = Path.GetRelativePath(_root, file).Replace('\\', '/');
                entries.Add(new HotUpdateFileEntry(relative, ComputeMd5(file)));
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        }

        return entries;
    }

    private PublishedManifest LoadPublished()
    {
        try
        {
            if (File.Exists(_publishedFile))
            {
                string json = File.ReadAllText(_publishedFile);
                var pub = JsonSerializer.Deserialize<PublishedManifest>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (pub != null)
                {
                    pub.Files ??= new List<HotUpdateFileEntry>();
                    return pub;
                }
            }
        }
        catch
        {
            // 损坏则当作未发布
        }

        return new PublishedManifest { Version = 0, Files = new List<HotUpdateFileEntry>() };
    }

    private void SavePublished(PublishedManifest pub)
    {
        string dir = Path.GetDirectoryName(_publishedFile);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(pub, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_publishedFile, json);
    }

    private static string ComputeMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = md5.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private class PublishedManifest
    {
        public int Version { get; set; }
        public string PublishedAt { get; set; } = string.Empty;
        public List<HotUpdateFileEntry> Files { get; set; } = new List<HotUpdateFileEntry>();
    }
}
