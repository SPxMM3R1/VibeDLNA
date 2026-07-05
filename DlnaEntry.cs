namespace FolderDlnaServer;

internal sealed class DlnaEntry
{
    public required string Id { get; init; }

    public required string ParentId { get; init; }

    public required string Title { get; init; }

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public required bool IsDirectory { get; init; }

    public long Size { get; init; }

    public int ChildCount { get; init; }

    public string MimeType { get; init; } = string.Empty;

    public string UpnpClass { get; init; } = "object.container.storageFolder";
}
