/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

export enum ListPagesStatus {
  NotArchived = 0,
  Published = 1,
  Unpublished = 2,
  Archived = 3,
  All = 4,
}

export enum ListPagesStringFilterType {
  Contains = 0,
  StartsWith = 1,
  EndsWith = 2,
  Regex = 3,
}

export enum ListPagesSort {
  SlugAsc = 0,
  SlugDesc = 1,
  PathAsc = 2,
  PathDesc = 3,
  CreatedAtAsc = 4,
  CreatedAtDesc = 5,
  UpdatedAtAsc = 6,
  UpdatedAtDesc = 7,
  SeoTitleAsc = 8,
  SeoTitleDesc = 9,
  PublishedAsc = 10,
  PublishedDesc = 11,
  StatusAsc = 12,
  StatusDesc = 13,
  LinksInAsc = 14,
  LinksInDesc = 15,
}

export enum PagePathAvailability {
  Available = 0,
  Taken = 1,
  Restricted = 2,
  ParentNotFound = 3,
  CircularParent = 4,
}

export interface PageLifecycleConflict {
  code: string;
  message: string;
  /** @format int32 */
  totalPages: number;
  pages: PageLifecycleConflictPage[];
  /** @format int32 */
  totalMenus: number;
  menus?: UnpublishMenuReference[] | null;
}

export interface PageLifecycleConflictPage {
  /** @format uuid */
  pageId: string;
  path: string;
}

export interface UnpublishMenuReference {
  /** @format uuid */
  menuId: string;
  name: string;
  itemTitle: string;
}

export interface CreatePageResponse {
  /** @format uuid */
  id: string;
}

/** @example {"parentId":null,"slug":"about-us","blocks":[{"blockTypeId":"87654321-4321-4321-4321-210987654321","blockData":"{\"title\": \"About Us\", \"content\": \"Welcome to our website\"}","instanceId":null}],"seo":{"title":"About us","description":"Learn who we are and what we do.","socialImageId":null,"index":true,"follow":true},"publish":true,"navigationMenuId":null} */
export interface CreatePageRequest {
  /**
   * @format uuid
   * @example null
   */
  parentId?: string | null;
  /**
   * @minLength 1
   * @example "about-us"
   */
  slug: string;
  /** @example [{"blockTypeId":"87654321-4321-4321-4321-210987654321","blockData":"{\"title\": \"About Us\", \"content\": \"Welcome to our website\"}","instanceId":null}] */
  blocks: CreatePageBlock[];
  /** @example {"title":"About us","description":"Learn who we are and what we do.","socialImageId":null,"index":true,"follow":true} */
  seo?: PageSeoRequest | null;
  /** @example true */
  publish: boolean;
  /**
   * @format uuid
   * @example null
   */
  navigationMenuId?: string | null;
}

export interface CreatePageBlock {
  /**
   * @format uuid
   * @minLength 1
   */
  blockTypeId: string;
  /** @minLength 1 */
  blockData: string;
  /** @format uuid */
  instanceId?: string | null;
}

export interface PageSeoRequest {
  /**
   * @minLength 0
   * @maxLength 60
   */
  title?: string | null;
  /**
   * @minLength 0
   * @maxLength 160
   */
  description?: string | null;
  /** @format uuid */
  socialImageId?: string | null;
  index: boolean;
  follow: boolean;
}

/** the dto used to send an error response to the client */
export interface ErrorResponse {
  /**
   * the http status code sent to the client. default is 400.
   * @format int32
   * @default 400
   */
  statusCode: number;
  /**
   * the message for the error response
   * @default "One or more errors occurred!"
   */
  message: string;
  /** the collection of errors for the current context */
  errors: Record<string, string[]>;
}

export interface GetPageResponse {
  /** @format uuid */
  pageId: string;
  /** @format uuid */
  pageVersionId: string;
  /** @format uuid */
  parentId?: string | null;
  /** @format uuid */
  navigationMenuId?: string | null;
  /** @format uuid */
  effectiveNavigationMenuId?: string | null;
  slug: string;
  path: string;
  version: number;
  seo: PageSeoResponse;
  blocks: GetPageResponseBlock[];
  /** @format date-time */
  createdAt: string;
  /** @format date-time */
  updatedAt: string;
  breadcrumbs?: PageBreadcrumbItem[] | null;
  isPublished: boolean;
  /** @format uuid */
  currentPageVersionId?: string | null;
  /** @format uuid */
  latestPageVersionId?: string | null;
  /** @format date-time */
  archivedAt?: string | null;
  references?: PageReferenceResponse[] | null;
}

export interface PageSeoResponse {
  title?: string | null;
  description?: string | null;
  socialImage?: GetPageResponseBlockImage | null;
  index: boolean;
  follow: boolean;
}

export interface GetPageResponseBlockImage {
  /** @format uuid */
  id: string;
  /** @format int32 */
  width: number;
  /** @format int32 */
  height: number;
  imageUrl: string;
  altText: string;
}

export interface GetPageResponseBlock {
  /** @format uuid */
  id: string;
  /** @format uuid */
  blockTypeId: string;
  blockName: string;
  blockVersion: string;
  blockData: string;
  images: GetPageResponseBlockImage[];
  pages: GetPageResponseBlockPage[];
  /** @format uuid */
  instanceId: string;
}

export interface GetPageResponseBlockPage {
  /** @format uuid */
  id: string;
  slug: string;
  path: string;
  title: string;
  heroImage?: GetPageResponseBlockImage | null;
}

export interface PageBreadcrumbItem {
  /** @format uuid */
  pageId: string;
  slug: string;
  path: string;
}

export interface PageReferenceResponse {
  kind: string;
  /** @format uuid */
  targetId: string;
}

export type GetPageRequest = object;

export type GetPageByPathRequest = object;

export type GetPageByVersionRequest = object;

export interface GetPageHistoryResponse {
  versions: GetPageHistoryResponseItem[];
  page?: PageHistoryMetadata | null;
}

export interface GetPageHistoryResponseItem {
  /** @format uuid */
  pageVersionId: string;
  isCurrent: boolean;
  changeSummary: PageVersionChangeSummary;
  /** @format date-time */
  createdAt: string;
}

export interface PageVersionChangeSummary {
  /** @format int32 */
  schemaVersion: number;
  headline: string;
  changes: string[];
}

export interface PageHistoryMetadata {
  /** @format uuid */
  pageId: string;
  /** @format uuid */
  parentId?: string | null;
  slug: string;
  path: string;
  /** @format uuid */
  navigationMenuId?: string | null;
  /** @format date-time */
  createdAt: string;
  /** @format date-time */
  archivedAt?: string | null;
  navigationMenuName?: string | null;
}

export type GetPageHistoryRequest = object;

export interface GetPagePathResponse {
  path: string;
}

export interface GetPagePathAvailabilityResponse {
  path?: string | null;
  status: PagePathAvailability;
}

export type GetPagePathAvailabilityRequest = object;

export interface ListPagesResponse {
  /** @format int32 */
  total: number;
  /** @format int32 */
  limit: number;
  /** @format int32 */
  nextOffset?: number | null;
  pages: ListPagesResponseItem[];
}

export interface ListPagesResponseItem {
  /** @format uuid */
  pageId: string;
  /** @format uuid */
  parentPageId?: string | null;
  /** @format uuid */
  currentPageVersionId?: string | null;
  slug: string;
  path: string;
  /** @format int32 */
  versionCount: number;
  /** @format date-time */
  createdAt: string;
  /** @format date-time */
  updatedAt: string;
  isPublished: boolean;
  seoTitle?: string | null;
  /** @format date-time */
  archivedAt?: string | null;
  /** @format int32 */
  linksFromPages: number;
  /** @format int32 */
  linksFromMenus: number;
}

export type ListPagesRequest = object;

export interface PagePublicationRequest {
  /** @format uuid */
  expectedCurrentPageVersionId?: string | null;
  expectedIsPublished: boolean;
}

export interface ReparentPageChildrenResponse {
  /** @format int32 */
  movedPages: number;
}

export interface ReparentPageChildrenRequest {
  /** @format uuid */
  parentId?: string | null;
}

export interface UnpublishPageConflict {
  code: string;
  message: string;
  /** @format int32 */
  totalPages: number;
  pages: UnpublishPageReference[];
  /** @format int32 */
  totalMenus: number;
  menus: UnpublishMenuReference[];
}

export interface UnpublishPageReference {
  /** @format uuid */
  pageId: string;
  /** @format uuid */
  pageVersionId: string;
  path: string;
  reason: string;
}

export interface UpdateCurrentPageVersionResponse {
  /** @format uuid */
  previousPageVersionId: string;
}

export interface PageDraftConflict {
  /** @format uuid */
  latestPageVersionId?: string | null;
}

export interface UpdatePageResponse {
  /** @format uuid */
  pageVersionId: string;
}

export interface UpdatePageRequest {
  /**
   * @format uuid
   * @minLength 1
   */
  expectedLatestPageVersionId: string;
  newBlocks: UpdatePageNewBlock[];
  existingBlocks: UpdatePageExistingBlock[];
  seo?: PageSeoRequest | null;
}

export interface UpdatePageNewBlock {
  /** @format uuid */
  blockTypeId: string;
  /**
   * @format int32
   * @min 0
   */
  sortOrder: number;
  blockData: string;
  /** @format uuid */
  instanceId?: string | null;
}

export interface UpdatePageExistingBlock {
  /** @format uuid */
  blockId: string;
  /**
   * @format int32
   * @min 0
   */
  sortOrder: number;
}

export interface ConflictResponse {
  /** @format uuid */
  id: string;
  version: number;
  /** @format date-time */
  updatedAt: string;
}

export interface UpdatePageDetailsRequest {
  /** @format uuid */
  parentId?: string | null;
  /** @minLength 1 */
  slug: string;
  version: number;
  /** @format uuid */
  navigationMenuId?: string | null;
}

export interface NavigationSettingsResponse {
  /** @format uuid */
  defaultMenuId?: string | null;
  /** @format uuid */
  footerMenuId?: string | null;
  version: number;
}

export interface UpdateNavigationSettingsRequest {
  /** @format uuid */
  defaultMenuId?: string | null;
  /** @format uuid */
  footerMenuId?: string | null;
  version: number;
}

export interface CreateCmsTypeDescriptorResponse {
  /** @format uuid */
  id: string;
}

/** @example {"name":"hero-section","version":1,"jsonSchema":"{\"type\": \"object\", \"properties\": {\"title\": {\"type\": \"string\"}, \"subtitle\": {\"type\": \"string\"}}}"} */
export interface CreateCmsTypeDescriptorRequest {
  /**
   * @minLength 1
   * @pattern ^[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]$
   * @example "hero-section"
   */
  name: string;
  /**
   * @format int32
   * @min 0
   * @exclusiveMin true
   * @example 1
   */
  version: number;
  /**
   * @minLength 1
   * @example "{"type": "object", "properties": {"title": {"type": "string"}, "subtitle": {"type": "string"}}}"
   */
  jsonSchema: string;
}

export interface CmsTypeDescriptor {
  /** @format uuid */
  id: string;
  name: string;
  /** @format int32 */
  version: number;
  jsonSchema: string;
  /** @format date-time */
  createdAt: string;
  isRetired: boolean;
}

export type GetMenuActionTypeRequest = object;

export type GetMenuActionTypeByNameRequest = object;

export interface ListMenuActionTypesResponse {
  items: CmsTypeDescriptor[];
}

export interface CreateMenuResponse {
  /** @format uuid */
  id: string;
}

/** @example {"name":"main-navigation","rootItems":[{"title":"Home","actionTypeId":"466963d3-ffe3-4f9d-8368-5c0436f8ffe2","actionData":"{\"pageId\": \"00000000-0000-0000-0000-000000000000\"}","children":null}]} */
export interface CreateMenuRequest {
  /**
   * @minLength 1
   * @pattern ^[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]$
   * @example "main-navigation"
   */
  name: string;
  /** @example [{"title":"Home","actionTypeId":"466963d3-ffe3-4f9d-8368-5c0436f8ffe2","actionData":"{\"pageId\": \"00000000-0000-0000-0000-000000000000\"}","children":null}] */
  rootItems?: MenuItem[] | null;
}

export interface MenuItem {
  /**
   * @minLength 0
   * @maxLength 200
   */
  title: string;
  /**
   * @format uuid
   * @minLength 1
   */
  actionTypeId: string;
  /** @minLength 1 */
  actionData: string;
  children?: MenuItem[] | null;
}

export interface GetMenuResponse {
  /** @format uuid */
  id: string;
  name: string;
  version: number;
  /** @format date-time */
  createdAt: string;
  /** @format date-time */
  updatedAt: string;
  rootItems: GetMenuRequestMenuItem[];
  pages: GetMenuResponsePage[];
}

export interface GetMenuRequestMenuItem {
  /** @format uuid */
  id: string;
  title: string;
  actionTypeName: string;
  /** @format int32 */
  actionTypeVersion: number;
  actionData: string;
  children: GetMenuRequestMenuItem[];
}

export interface GetMenuResponsePage {
  /** @format uuid */
  id: string;
  slug: string;
  path: string;
}

export type GetMenuRequest = object;

export interface ListMenusResponse {
  items: ListMenusMenuItem[];
  /** @format int32 */
  totalCount: number;
  /** @format int32 */
  limit: number;
  /** @format int32 */
  offset: number;
}

export interface ListMenusMenuItem {
  /** @format uuid */
  id: string;
  name: string;
  /** @format date-time */
  createdAt: string;
  /** @format date-time */
  updatedAt: string;
}

export type ListMenusRequest = object;

/** @example {"version":1,"rootItems":[{"title":"Home","actionTypeId":"eab13195-20eb-48f2-a121-74d2901f0af7","actionData":"{\"pageId\": \"00000000-0000-0000-0000-000000000000\"}","children":[{"title":"About","actionTypeId":"93886a29-3ee6-4c74-b9e0-94d9074ca68a","actionData":"{\"pageId\": \"00000000-0000-0000-0000-000000000001\"}","children":null}]}]} */
export interface UpdateMenuRequest {
  /** @example 1 */
  version: number;
  /** @example [{"title":"Home","actionTypeId":"eab13195-20eb-48f2-a121-74d2901f0af7","actionData":"{\"pageId\": \"00000000-0000-0000-0000-000000000000\"}","children":[{"title":"About","actionTypeId":"93886a29-3ee6-4c74-b9e0-94d9074ca68a","actionData":"{\"pageId\": \"00000000-0000-0000-0000-000000000001\"}","children":null}]}] */
  rootItems?: MenuItem[] | null;
}

export type GetBlockTypeRequest = object;

export type GetBlockTypeByNameRequest = object;

export interface ListBlockTypesResponse {
  items: CmsTypeDescriptor[];
  /** @format int32 */
  total: number;
  /** @format int32 */
  nextOffset?: number | null;
  /** @format int32 */
  limit: number;
}

export type ListBlockTypesRequest = object;

export type QueryParamsType = Record<string | number, any>;
export type ResponseFormat = keyof Omit<Body, "body" | "bodyUsed">;

export interface FullRequestParams extends Omit<RequestInit, "body"> {
  /** set parameter to `true` for call `securityWorker` for this request */
  secure?: boolean;
  /** request path */
  path: string;
  /** content type of request body */
  type?: ContentType;
  /** query params */
  query?: QueryParamsType;
  /** format of response (i.e. response.json() -> format: "json") */
  format?: ResponseFormat;
  /** request body */
  body?: unknown;
  /** base url */
  baseUrl?: string;
  /** request cancellation token */
  cancelToken?: CancelToken;
}

export type RequestParams = Omit<
  FullRequestParams,
  "body" | "method" | "query" | "path"
>;

export interface ApiConfig<SecurityDataType = unknown> {
  baseUrl?: string;
  baseApiParams?: Omit<RequestParams, "baseUrl" | "cancelToken" | "signal">;
  securityWorker?: (
    securityData: SecurityDataType | null,
  ) => Promise<RequestParams | void> | RequestParams | void;
  customFetch?: typeof fetch;
}

export interface HttpResponse<D extends unknown, E extends unknown = unknown>
  extends Response {
  data: D;
  error: E;
}

type CancelToken = Symbol | string | number;

export enum ContentType {
  Json = "application/json",
  JsonApi = "application/vnd.api+json",
  FormData = "multipart/form-data",
  UrlEncoded = "application/x-www-form-urlencoded",
  Text = "text/plain",
}

export class HttpClient<SecurityDataType = unknown> {
  public baseUrl: string = "http://localhost:5100";
  private securityData: SecurityDataType | null = null;
  private securityWorker?: ApiConfig<SecurityDataType>["securityWorker"];
  private abortControllers = new Map<CancelToken, AbortController>();
  private customFetch = (...fetchParams: Parameters<typeof fetch>) =>
    fetch(...fetchParams);

  private baseApiParams: RequestParams = {
    credentials: "same-origin",
    headers: {},
    redirect: "follow",
    referrerPolicy: "no-referrer",
  };

  constructor(apiConfig: ApiConfig<SecurityDataType> = {}) {
    Object.assign(this, apiConfig);
  }

  public setSecurityData = (data: SecurityDataType | null) => {
    this.securityData = data;
  };

  protected encodeQueryParam(key: string, value: any) {
    const encodedKey = encodeURIComponent(key);
    return `${encodedKey}=${encodeURIComponent(typeof value === "number" ? value : `${value}`)}`;
  }

  protected addQueryParam(query: QueryParamsType, key: string) {
    return this.encodeQueryParam(key, query[key]);
  }

  protected addArrayQueryParam(query: QueryParamsType, key: string) {
    const value = query[key];
    return value.map((v: any) => this.encodeQueryParam(key, v)).join("&");
  }

  protected toQueryString(rawQuery?: QueryParamsType): string {
    const query = rawQuery || {};
    const keys = Object.keys(query).filter(
      (key) => "undefined" !== typeof query[key],
    );
    return keys
      .map((key) =>
        Array.isArray(query[key])
          ? this.addArrayQueryParam(query, key)
          : this.addQueryParam(query, key),
      )
      .join("&");
  }

  protected addQueryParams(rawQuery?: QueryParamsType): string {
    const queryString = this.toQueryString(rawQuery);
    return queryString ? `?${queryString}` : "";
  }

  private contentFormatters: Record<ContentType, (input: any) => any> = {
    [ContentType.Json]: (input: any) =>
      input !== null && (typeof input === "object" || typeof input === "string")
        ? JSON.stringify(input)
        : input,
    [ContentType.JsonApi]: (input: any) =>
      input !== null && (typeof input === "object" || typeof input === "string")
        ? JSON.stringify(input)
        : input,
    [ContentType.Text]: (input: any) =>
      input !== null && typeof input !== "string"
        ? JSON.stringify(input)
        : input,
    [ContentType.FormData]: (input: any) =>
      Object.keys(input || {}).reduce((formData, key) => {
        const property = input[key];
        formData.append(
          key,
          property instanceof Blob
            ? property
            : typeof property === "object" && property !== null
              ? JSON.stringify(property)
              : `${property}`,
        );
        return formData;
      }, new FormData()),
    [ContentType.UrlEncoded]: (input: any) => this.toQueryString(input),
  };

  protected mergeRequestParams(
    params1: RequestParams,
    params2?: RequestParams,
  ): RequestParams {
    return {
      ...this.baseApiParams,
      ...params1,
      ...(params2 || {}),
      headers: {
        ...(this.baseApiParams.headers || {}),
        ...(params1.headers || {}),
        ...((params2 && params2.headers) || {}),
      },
    };
  }

  protected createAbortSignal = (
    cancelToken: CancelToken,
  ): AbortSignal | undefined => {
    if (this.abortControllers.has(cancelToken)) {
      const abortController = this.abortControllers.get(cancelToken);
      if (abortController) {
        return abortController.signal;
      }
      return void 0;
    }

    const abortController = new AbortController();
    this.abortControllers.set(cancelToken, abortController);
    return abortController.signal;
  };

  public abortRequest = (cancelToken: CancelToken) => {
    const abortController = this.abortControllers.get(cancelToken);

    if (abortController) {
      abortController.abort();
      this.abortControllers.delete(cancelToken);
    }
  };

  public request = async <T = any, E = any>({
    body,
    secure,
    path,
    type,
    query,
    format,
    baseUrl,
    cancelToken,
    ...params
  }: FullRequestParams): Promise<HttpResponse<T, E>> => {
    const secureParams =
      ((typeof secure === "boolean" ? secure : this.baseApiParams.secure) &&
        this.securityWorker &&
        (await this.securityWorker(this.securityData))) ||
      {};
    const requestParams = this.mergeRequestParams(params, secureParams);
    const queryString = query && this.toQueryString(query);
    const payloadFormatter = this.contentFormatters[type || ContentType.Json];
    const responseFormat = format || requestParams.format;

    return this.customFetch(
      `${baseUrl || this.baseUrl || ""}${path}${queryString ? `?${queryString}` : ""}`,
      {
        ...requestParams,
        headers: {
          ...(requestParams.headers || {}),
          ...(type && type !== ContentType.FormData
            ? { "Content-Type": type }
            : {}),
        },
        signal:
          (cancelToken
            ? this.createAbortSignal(cancelToken)
            : requestParams.signal) || null,
        body:
          typeof body === "undefined" || body === null
            ? null
            : payloadFormatter(body),
      },
    ).then(async (response) => {
      const r = response.clone() as HttpResponse<T, E>;
      r.data = null as unknown as T;
      r.error = null as unknown as E;

      const data = !responseFormat
        ? r
        : await response[responseFormat]()
            .then((data) => {
              if (r.ok) {
                r.data = data;
              } else {
                r.error = data;
              }
              return r;
            })
            .catch((e) => {
              r.error = e;
              return r;
            });

      if (cancelToken) {
        this.abortControllers.delete(cancelToken);
      }

      if (!response.ok) throw data;
      return data;
    });
  };
}

/**
 * @title C4M
 * @version v1
 * @baseUrl http://localhost:5100
 *
 * API for managing content-related operations in the Parapet system.
 */
export class Api<
  SecurityDataType extends unknown,
> extends HttpClient<SecurityDataType> {
  pages = {
    /**
     * No description
     *
     * @tags Pages
     * @name ArchivePage
     * @summary Archives an unpublished page with no unarchived children, retaining its content and history.
     * @request POST:/pages/{id}/archive
     */
    archivePage: (id: string, params: RequestParams = {}) =>
      this.request<void, void | PageLifecycleConflict>({
        path: `/pages/${id}/archive`,
        method: "POST",
        ...params,
      }),

    /**
     * @description Creates a new page with the specified content blocks, optionally leaving its initial version as a draft.
     *
     * @tags Pages
     * @name CreatePage
     * @summary Creates a new page.
     * @request POST:/pages
     */
    createPage: (data: CreatePageRequest, params: RequestParams = {}) =>
      this.request<CreatePageResponse, void | ErrorResponse>({
        path: `/pages`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a paginated list of pages with support for sorting, searching by SEO title or path, and filtering by slug, path, and creation/update dates.
     *
     * @tags Pages
     * @name ListPages
     * @summary Lists pages with pagination, sorting, and filtering.
     * @request GET:/pages
     */
    listPages: (
      query?: {
        /**
         * @format int32
         * @default 20
         */
        limit?: number;
        /**
         * @format int32
         * @default 0
         */
        offset?: number;
        /** @default 0 */
        sort?: ListPagesSort;
        slugFilter?: string | null;
        /** @default 0 */
        slugFilterType?: ListPagesStringFilterType;
        pathFilter?: string | null;
        /** @default 0 */
        pathFilterType?: ListPagesStringFilterType;
        /** @format date-time */
        createdAfter?: string | null;
        /** @format date-time */
        createdBefore?: string | null;
        /** @format date-time */
        updatedAfter?: string | null;
        /** @format date-time */
        updatedBefore?: string | null;
        search?: string | null;
        /** @format uuid */
        parentId?: string | null;
        /** @default false */
        publishedOnly?: boolean;
        /** @default 0 */
        status?: ListPagesStatus;
      },
      params: RequestParams = {},
    ) =>
      this.request<ListPagesResponse, void>({
        path: `/pages`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a specific page by its unique identifier including all related blocks. Optionally specify a version ID to get a specific version, otherwise returns the current version.
     *
     * @tags Pages
     * @name GetPage
     * @summary Gets a page by ID.
     * @request GET:/pages/{id}
     */
    getPage: (
      id: string,
      query?: {
        /** @default false */
        latest?: boolean;
        /** @default false */
        includeBreadcrumbs?: boolean;
        /** @default false */
        includeArchived?: boolean;
      },
      params: RequestParams = {},
    ) =>
      this.request<GetPageResponse, ErrorResponse>({
        path: `/pages/${id}`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Pages
     * @name UpdatePage
     * @summary Creates a content and SEO draft without changing page details or publication.
     * @request PUT:/pages/{id}
     */
    updatePage: (
      id: string,
      data: UpdatePageRequest,
      params: RequestParams = {},
    ) =>
      this.request<UpdatePageResponse, ErrorResponse | PageDraftConflict>({
        path: `/pages/${id}`,
        method: "PUT",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a specific page by path including all related blocks. Returns the current version of the page.
     *
     * @tags Pages
     * @name GetPageByPath
     * @summary Gets a page by path.
     * @request GET:/pages/by-path
     */
    getPageByPath: (
      query: {
        path: string;
      },
      params: RequestParams = {},
    ) =>
      this.request<GetPageResponse, ErrorResponse>({
        path: `/pages/by-path`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a specific page by its version identifier including all related blocks.
     *
     * @tags Pages
     * @name GetPageByVersion
     * @summary Gets a page by version.
     * @request GET:/pages/by-version/{id}
     */
    getPageByVersion: (
      id: string,
      query?: {
        /** @default false */
        includeBreadcrumbs?: boolean;
        /** @default false */
        includeArchived?: boolean;
      },
      params: RequestParams = {},
    ) =>
      this.request<GetPageResponse, ErrorResponse>({
        path: `/pages/by-version/${id}`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * @description Sets the specified page version as the current version and unsets the previous current version.
     *
     * @tags Pages
     * @name UpdateCurrentPageVersion
     * @summary Sets a page version as current.
     * @request PUT:/pages/by-version/{id}
     */
    updateCurrentPageVersion: (
      id: string,
      data: PagePublicationRequest,
      params: RequestParams = {},
    ) =>
      this.request<UpdateCurrentPageVersionResponse, ErrorResponse>({
        path: `/pages/by-version/${id}`,
        method: "PUT",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves the complete version history for a specific page, including high-level change summaries.
     *
     * @tags Pages
     * @name GetPageHistory
     * @summary Gets page version history by page ID.
     * @request GET:/pages/{id}/history
     */
    getPageHistory: (
      id: string,
      query?: {
        /** @default false */
        includeArchived?: boolean;
      },
      params: RequestParams = {},
    ) =>
      this.request<GetPageHistoryResponse, ErrorResponse>({
        path: `/pages/${id}/history`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves just the path of a specific page by its unique identifier. This is a performance-optimized endpoint that returns only the path without loading the full page data.
     *
     * @tags Pages
     * @name GetPagePath
     * @summary Gets a page path by ID.
     * @request GET:/pages/{id}/path
     */
    getPagePath: (id: string, params: RequestParams = {}) =>
      this.request<GetPagePathResponse, ErrorResponse>({
        path: `/pages/${id}/path`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Resolves the path a page would have with the given parent and slug, and reports whether that path can be used. Pass the ID of a page being edited to exclude it from the check.
     *
     * @tags Pages
     * @name GetPagePathAvailability
     * @summary Checks whether a page path is available.
     * @request GET:/pages/path-availability
     */
    getPagePathAvailability: (
      query: {
        slug: string;
        /** @format uuid */
        parentId?: string | null;
        /** @format uuid */
        pageId?: string | null;
      },
      params: RequestParams = {},
    ) =>
      this.request<GetPagePathAvailabilityResponse, void>({
        path: `/pages/path-availability`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Pages
     * @name PublishPage
     * @summary Publishes the saved Current version of a page.
     * @request POST:/pages/{id}/publish
     */
    publishPage: (
      id: string,
      data: PagePublicationRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void>({
        path: `/pages/${id}/publish`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Pages
     * @name ReparentPageChildren
     * @summary Moves all unarchived direct children and their subtrees to another parent or the top level.
     * @request POST:/pages/{id}/reparent-children
     */
    reparentPageChildren: (
      id: string,
      data: ReparentPageChildrenRequest,
      params: RequestParams = {},
    ) =>
      this.request<ReparentPageChildrenResponse, void | PageLifecycleConflict>({
        path: `/pages/${id}/reparent-children`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * No description
     *
     * @tags Pages
     * @name UnpublishPage
     * @summary Unpublishes a page unless published pages or menus reference it.
     * @request POST:/pages/{id}/unpublish
     */
    unpublishPage: (
      id: string,
      data: PagePublicationRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void | UnpublishPageConflict>({
        path: `/pages/${id}/unpublish`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        ...params,
      }),

    /**
     * No description
     *
     * @tags Pages
     * @name UpdatePageDetails
     * @summary Updates unversioned page details without creating a content version.
     * @request PUT:/pages/{id}/details
     */
    updatePageDetails: (
      id: string,
      data: UpdatePageDetailsRequest,
      params: RequestParams = {},
    ) =>
      this.request<GetPageResponse, ErrorResponse | ConflictResponse>({
        path: `/pages/${id}/details`,
        method: "PUT",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),
  };
  navigationSettings = {
    /**
     * @description Retrieves the default navigation and footer menu selections.
     *
     * @tags Navigation-Settings
     * @name GetNavigationSettings
     * @summary Gets navigation settings.
     * @request GET:/navigation-settings
     */
    getNavigationSettings: (params: RequestParams = {}) =>
      this.request<NavigationSettingsResponse, any>({
        path: `/navigation-settings`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Changes the default navigation and footer menus immediately.
     *
     * @tags Navigation-Settings
     * @name UpdateNavigationSettings
     * @summary Updates navigation settings.
     * @request PUT:/navigation-settings
     */
    updateNavigationSettings: (
      data: UpdateNavigationSettingsRequest,
      params: RequestParams = {},
    ) =>
      this.request<
        NavigationSettingsResponse,
        ErrorResponse | ConflictResponse
      >({
        path: `/navigation-settings`,
        method: "PUT",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),
  };
  menuActionTypes = {
    /**
     * @description Adds a new menu action type to the system.
     *
     * @tags Menu-Action-Types
     * @name CreateMenuActionType
     * @summary Creates a new menu action type.
     * @request POST:/menu-action-types
     */
    createMenuActionType: (
      data: CreateCmsTypeDescriptorRequest,
      params: RequestParams = {},
    ) =>
      this.request<CreateCmsTypeDescriptorResponse, void | ErrorResponse>({
        path: `/menu-action-types`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves all available menu action types with their schemas.
     *
     * @tags Menu-Action-Types
     * @name ListMenuActionTypes
     * @summary Lists all menu action types.
     * @request GET:/menu-action-types
     */
    listMenuActionTypes: (params: RequestParams = {}) =>
      this.request<ListMenuActionTypesResponse, any>({
        path: `/menu-action-types`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a menu action type including its JSON schema for validation.
     *
     * @tags Menu-Action-Types
     * @name GetMenuActionType
     * @summary Gets a menu action type by ID.
     * @request GET:/menu-action-types/{id}
     */
    getMenuActionType: (id: string, params: RequestParams = {}) =>
      this.request<CmsTypeDescriptor, ErrorResponse>({
        path: `/menu-action-types/${id}`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a specific menu action type from the system by its name. If version is not specified, returns the latest version.
     *
     * @tags Menu-Action-Types
     * @name GetMenuActionTypeByName
     * @summary Gets a menu action type by name.
     * @request GET:/menu-action-types/by-name/{name}
     */
    getMenuActionTypeByName: (
      name: string,
      query?: {
        /** @format int32 */
        version?: number | null;
      },
      params: RequestParams = {},
    ) =>
      this.request<CmsTypeDescriptor, void>({
        path: `/menu-action-types/by-name/${name}`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),
  };
  menus = {
    /**
     * @description Adds a new menu with optional menu items to the system.
     *
     * @tags Menus
     * @name CreateMenu
     * @summary Creates a new menu.
     * @request POST:/menus
     */
    createMenu: (data: CreateMenuRequest, params: RequestParams = {}) =>
      this.request<CreateMenuResponse, void | ErrorResponse>({
        path: `/menus`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a paginated list of menus.
     *
     * @tags Menus
     * @name ListMenus
     * @summary Lists menus.
     * @request GET:/menus
     */
    listMenus: (
      query?: {
        /**
         * @format int32
         * @default 20
         */
        limit?: number;
        /**
         * @format int32
         * @default 0
         */
        offset?: number;
      },
      params: RequestParams = {},
    ) =>
      this.request<ListMenusResponse, ErrorResponse>({
        path: `/menus`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves the menu configured as the site-wide navigation fallback.
     *
     * @tags Menus
     * @name GetDefaultMenu
     * @summary Gets the default navigation menu.
     * @request GET:/menus/default
     */
    getDefaultMenu: (params: RequestParams = {}) =>
      this.request<GetMenuResponse, ErrorResponse>({
        path: `/menus/default`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves the menu configured for the site-wide footer.
     *
     * @tags Menus
     * @name GetFooterMenu
     * @summary Gets the footer menu.
     * @request GET:/menus/footer
     */
    getFooterMenu: (params: RequestParams = {}) =>
      this.request<GetMenuResponse, ErrorResponse>({
        path: `/menus/footer`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a menu with its hierarchical structure of menu items.
     *
     * @tags Menus
     * @name GetMenu
     * @summary Gets a menu by ID.
     * @request GET:/menus/{id}
     */
    getMenu: (id: string, params: RequestParams = {}) =>
      this.request<GetMenuResponse, ErrorResponse>({
        path: `/menus/${id}`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Replaces all menu items with the provided tree structure.
     *
     * @tags Menus
     * @name UpdateMenu
     * @summary Updates an existing menu.
     * @request PUT:/menus/{menuId}
     */
    updateMenu: (
      menuId: string,
      data: UpdateMenuRequest,
      params: RequestParams = {},
    ) =>
      this.request<void, void | ErrorResponse>({
        path: `/menus/${menuId}`,
        method: "PUT",
        body: data,
        type: ContentType.Json,
        ...params,
      }),
  };
  blockTypes = {
    /**
     * @description Adds a new block type to the system.
     *
     * @tags Block-Types
     * @name CreateBlockType
     * @summary Creates a new block type.
     * @request POST:/block-types
     */
    createBlockType: (
      data: CreateCmsTypeDescriptorRequest,
      params: RequestParams = {},
    ) =>
      this.request<CreateCmsTypeDescriptorResponse, void | ErrorResponse>({
        path: `/block-types`,
        method: "POST",
        body: data,
        type: ContentType.Json,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a paginated list of block types grouped by name. All versions of each block type are included together.
     *
     * @tags Block-Types
     * @name ListBlockTypes
     * @summary Lists block types with pagination.
     * @request GET:/block-types
     */
    listBlockTypes: (
      query?: {
        /**
         * @format int32
         * @default 0
         */
        offset?: number;
        /**
         * @format int32
         * @default 50
         */
        limit?: number;
      },
      params: RequestParams = {},
    ) =>
      this.request<ListBlockTypesResponse, void>({
        path: `/block-types`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a specific block type from the system.
     *
     * @tags Block-Types
     * @name GetBlockType
     * @summary Gets a block type by ID.
     * @request GET:/block-types/{id}
     */
    getBlockType: (id: string, params: RequestParams = {}) =>
      this.request<CmsTypeDescriptor, void>({
        path: `/block-types/${id}`,
        method: "GET",
        format: "json",
        ...params,
      }),

    /**
     * @description Retrieves a specific block type from the system by its name. If version is not specified, returns the latest version.
     *
     * @tags Block-Types
     * @name GetBlockTypeByName
     * @summary Gets a block type by name.
     * @request GET:/block-types/by-name/{name}
     */
    getBlockTypeByName: (
      name: string,
      query?: {
        /** @format int32 */
        version?: number | null;
      },
      params: RequestParams = {},
    ) =>
      this.request<CmsTypeDescriptor, void>({
        path: `/block-types/by-name/${name}`,
        method: "GET",
        query: query,
        format: "json",
        ...params,
      }),
  };
}
