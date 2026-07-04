// Frontend mirrors of the backend tag contracts.
// Source of truth: src/Favourites.Api/Contracts/Tags/TagContracts.cs

export interface CreateTagRequest {
  readonly name: string;
}

export interface UpdateTagRequest {
  readonly name: string;
}

export interface TagResponse {
  readonly id: string;
  readonly name: string;
  readonly linkCount: number;
  readonly createdAtUtc: string;
  readonly lastUsedAtUtc: string | null;
}

export interface PagedTagsResponse {
  readonly items: TagResponse[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface TagsSummaryResponse {
  readonly totalTags: number;
  readonly unusedTags: number;
  readonly mostUsed: TagMostUsedResponse | null;
  readonly recentlyAdded: TagSummaryItemResponse | null;
  readonly possibleDuplicates: number;
}

export interface TagMostUsedResponse {
  readonly id: string;
  readonly name: string;
  readonly count: number;
}

export interface TagSummaryItemResponse {
  readonly id: string;
  readonly name: string;
}
