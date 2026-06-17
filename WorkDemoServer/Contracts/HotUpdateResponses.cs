namespace WorkDemoServer.Contracts;

/// <summary>热更清单：版本号 + 文件列表（含 MD5）。客户端拿到的是【已发布】的这一份。</summary>
public record HotUpdateManifestResponse(int Version, IReadOnlyList<HotUpdateFileEntry> Files);

/// <summary>单个热更文件项。Path 为相对 HotUpdateContent 的正斜杠路径。</summary>
public record HotUpdateFileEntry(string Path, string Hash);

/// <summary>一次"发布"的结果：新版本号 + 相对上一版的差异。</summary>
public record HotUpdatePublishResult(
    int Version,
    string PublishedAt,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Modified,
    IReadOnlyList<string> Removed);
