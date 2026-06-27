namespace ReadyOrNotModManager.Core.Nexus;

public abstract record NexusReference(string GameDomain, string SourceUrl);

public sealed record NexusModReference(string GameDomain, int ModId, string SourceUrl) : NexusReference(GameDomain, SourceUrl);

public sealed record NexusCollectionReference(string GameDomain, string Slug, int? RevisionNumber, string SourceUrl) : NexusReference(GameDomain, SourceUrl);

public sealed record NexusModFile(
    int ModId,
    int FileId,
    string Name,
    string Version,
    string SourceUrl);

public sealed record DownloadLink(Uri Uri);

public sealed record NexusApiValidationResult(bool IsValid, string UserName, bool IsPremium, string Message);
