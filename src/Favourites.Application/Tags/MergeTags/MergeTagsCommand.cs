namespace Favourites.Application.Tags.MergeTags;

/// <summary>
/// Merges one or more tags into a single target tag.
/// Links from the merged tags are reassigned to <see cref="KeepTagId"/>,
/// and the merged tags are deleted.
/// </summary>
public sealed record MergeTagsCommand(
    Guid KeepTagId,
    IReadOnlyList<Guid> MergeTagIds);
