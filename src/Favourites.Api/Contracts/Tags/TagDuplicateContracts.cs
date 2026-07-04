namespace Favourites.Api.Contracts.Tags;

public sealed record TagDuplicateGroupResponse(IReadOnlyList<TagResponse> Tags);

public sealed record MergeTagsRequest(Guid KeepTagId, IReadOnlyList<Guid> MergeTagIds);
