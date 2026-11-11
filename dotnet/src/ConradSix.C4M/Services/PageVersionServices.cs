namespace ConradSix.C4M.Services;

using System.Text.Json;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Services.Model;
using ConradSix.C4M.Validation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal class PageVersionServices(
    CmsEngineDbContext ctx,
    BlockReferenceIndexer referenceIndexer,
    PageVersionChangeSummaryService changeSummaryService,
    IOptions<C4MOptions> options,
    IEnumerable<IBlockValidator> blockValidators,
    IEnumerable<IPublicationRule> publicationRules,
    CmsJsonSchemaCache schemas)
    : IPageVersionServices
{
    public async Task<Guid> CreatePageVersionAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        CancellationToken cancellationToken) => await this.CreatePageVersionAsync(
            pageId,
            blocks,
            PageVersionMetadata.Empty,
            cancellationToken);

    public async Task<Guid> CreatePageVersionAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        PageVersionMetadata metadata,
        CancellationToken cancellationToken) =>
        await this.CreatePageVersionAsync(pageId, blocks, metadata, PageVersionWrite.Unconditional, cancellationToken) switch
        {
            PageVersionSaved saved => saved.PageVersionId,
            PageVersionPageMissing => throw new InvalidOperationException($"Page with ID {pageId} not found."),
            var result => throw new InvalidOperationException($"An unconditional write cannot end as {result}."),
        };

    public async Task<PageVersionWriteResult> CreatePageVersionAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        PageVersionMetadata metadata,
        PageVersionWrite write,
        CancellationToken cancellationToken = default)
    {
        CmsTransactions.EnsureNoCallerTransaction(ctx.Database);
        _ = NormalizeAndValidateLength(metadata.SeoTitle, "SEO title", PageVersionEntity.SeoTitleMaxLength);
        _ = NormalizeAndValidateLength(metadata.SeoDescription, "SEO description", PageVersionEntity.SeoDescriptionMaxLength);
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
        await CmsLocks.PageAsync(ctx.Database, pageId, cancellationToken);
        if (!await ctx.Pages.AnyAsync(page => page.Id == pageId && page.ArchivedAt == null, cancellationToken))
        {
            return new PageVersionPageMissing();
        }

        if (write.IdempotencyKey is { } key &&
            await ctx.PageVersions.Where(version => version.PageId == pageId && version.IdempotencyKey == key)
                .Select(version => (Guid?)version.Id).SingleOrDefaultAsync(cancellationToken) is { } saved)
        {
            return new PageVersionSaved(saved, Replayed: true);
        }

        if (write.ExpectedLatestPageVersionId is { } expected)
        {
            var latest = await ctx.PageVersions.Where(version => version.PageId == pageId)
                .OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id)
                .Select(version => (Guid?)version.Id).FirstOrDefaultAsync(cancellationToken);
            if (latest != expected)
            {
                return new PageVersionConflict(latest);
            }
        }

        var pageVersionId = await this.AppendAsync(pageId, blocks, metadata, write.IdempotencyKey, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PageVersionSaved(pageVersionId, Replayed: false);
    }

    public async Task<IReadOnlyList<string>> ValidateForPublicationAsync(
        Guid pageVersionId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await ctx.PageVersions
            .Where(pv => pv.Id == pageVersionId && pv.Page.ArchivedAt == null)
            .Select(pv => new { pv.PageId, pv.Page.ParentId, pv.SeoTitle, pv.SeoDescription })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Page version with ID {pageVersionId} not found.");

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(metadata.SeoTitle))
        {
            errors.Add("SEO title is required before publishing.");
        }

        if (string.IsNullOrWhiteSpace(metadata.SeoDescription))
        {
            errors.Add("SEO description is required before publishing.");
        }

        var unpublishedAncestors = await PagePublicationGuard.GetUnpublishedAncestorsAsync(ctx, metadata.ParentId, cancellationToken);
        foreach (var path in unpublishedAncestors)
        {
            errors.Add($"Ancestor page '{path}' must be published first.");
        }

        var linkedPageIds = ctx.PageBlockReferences
            .Where(reference => reference.Kind == CmsReferenceKinds.Page && reference.TargetId != metadata.PageId &&
                reference.PageBlock.VersionBlocks.Any(block => block.PageVersionId == pageVersionId))
            .Select(reference => reference.TargetId);
        var unpublishedTargets = await ctx.Pages
            .Where(page => linkedPageIds.Contains(page.Id) &&
                (!page.IsPublished || !page.Versions.Any(version => version.IsCurrent == true)))
            .Select(page => page.Path)
            .OrderBy(path => path).ToArrayAsync(cancellationToken);
        foreach (var path in unpublishedTargets)
        {
            errors.Add($"Linked page '{path}' must be published first.");
        }

        foreach (var rule in publicationRules)
        {
            errors.AddRange(await rule.ValidateAsync(metadata.PageId, pageVersionId, cancellationToken));
        }

        return errors;
    }

    public async Task<Guid> SetCurrentPageVersionAsync(
        Guid pageVersionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = ctx.Database.CurrentTransaction is null
            ? await ctx.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await PagePublicationGuard.LockAsync(ctx, cancellationToken);
        var pageVersion = await ctx.PageVersions
            .SingleOrDefaultAsync(pv => pv.Id == pageVersionId && pv.Page.ArchivedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Page version with ID {pageVersionId} not found.");

        var existingCurrentVersion = await ctx.PageVersions
            .SingleOrDefaultAsync(pv => pv.PageId == pageVersion.PageId && pv.IsCurrent == true, cancellationToken);

        existingCurrentVersion?.IsCurrent = null;

        await ctx.SaveChangesAsync(cancellationToken);

        pageVersion.IsCurrent = true;

        await ctx.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return existingCurrentVersion?.Id ?? Guid.Empty;
    }

    // Adds a version inside an engine operation's own transaction, which holds the page's lock and has checked the
    // page exists.
    internal async Task<Guid> AppendAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        PageVersionMetadata metadata,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var seoTitle = NormalizeAndValidateLength(
            metadata.SeoTitle,
            "SEO title",
            PageVersionEntity.SeoTitleMaxLength);
        var seoDescription = NormalizeAndValidateLength(
            metadata.SeoDescription,
            "SEO description",
            PageVersionEntity.SeoDescriptionMaxLength);
        await this.ValidateBlocksAsync(blocks, cancellationToken);

        var hasVersions = await ctx.PageVersions.AnyAsync(version => version.PageId == pageId, cancellationToken);
        var pageVersion = new PageVersionEntity
        {
            PageId = pageId,
            IsCurrent = hasVersions ? null : true,
            SeoTitle = seoTitle,
            SeoDescription = seoDescription,
            SeoImageId = metadata.SeoImageId,
            RobotsIndex = metadata.RobotsIndex,
            RobotsFollow = metadata.RobotsFollow,
            IdempotencyKey = idempotencyKey,
        };
        ctx.PageVersions.Add(pageVersion);

        var versionBlocks = new List<PageVersionBlockEntity>();
        var linkedPageIds = new HashSet<Guid>();
        var sortOrder = 1;
        foreach (var blockData in blocks)
        {
            PageBlockEntity? block;
            if (blockData.IsExisting)
            {
                var existingBlock = blockData.ToExistingBlock();
                block = await ctx.PageBlocks.FindAsync([existingBlock.BlockId], cancellationToken)
                    ?? throw new InvalidOperationException($"Block with ID {existingBlock.BlockId} not found.");
            }
            else if (blockData.IsNew)
            {
                var newBlock = blockData.ToNewBlock();
                block = new PageBlockEntity
                {
                    InstanceId = blockData.InstanceId ?? GetInitialInstanceId(newBlock.BlockData),
                    BlockTypeId = newBlock.BlockTypeId,
                    BlockData = newBlock.BlockData,
                };

                ctx.PageBlocks.Add(block);

                foreach (var reference in referenceIndexer.Index(newBlock.BlockData))
                {
                    ctx.PageBlockReferences.Add(new PageBlockReferenceEntity
                    {
                        PageBlock = block,
                        Kind = reference.Kind,
                        TargetId = reference.TargetId,
                        JsonPath = reference.JsonPath,
                        Ordinal = reference.Ordinal,
                    });

                    if (reference.Kind == CmsReferenceKinds.Page)
                    {
                        linkedPageIds.Add(reference.TargetId);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "Either BlockId must be provided for existing blocks, or both BlockTypeId and BlockData must be provided for new blocks.");
            }

            if (block.InstanceId == Guid.Empty || versionBlocks.Any(item => item.PageBlock.InstanceId == block.InstanceId))
            {
                throw new ArgumentException("Each block must have a unique instance identity within the page.");
            }

            var versionBlock = new PageVersionBlockEntity
            {
                PageBlock = block,
                PageVersion = pageVersion,
                SortOrder = sortOrder++,
            };

            ctx.PageVersionBlocks.Add(versionBlock);
            versionBlocks.Add(versionBlock);
        }

        var missingPageIds = linkedPageIds.Except(await ctx.Pages
            .Where(page => linkedPageIds.Contains(page.Id))
            .Select(page => page.Id)
            .ToArrayAsync(cancellationToken)).ToArray();
        if (missingPageIds.Length > 0)
        {
            throw new CmsBlockValidationException($"Linked page {missingPageIds[0]} does not exist.");
        }

        pageVersion.ChangeSummaryJson = await changeSummaryService.CreateForNewVersionAsync(
            pageId,
            metadata,
            versionBlocks,
            cancellationToken);

        await ctx.SaveChangesAsync(cancellationToken);
        return pageVersion.Id;
    }

    private static Guid GetInitialInstanceId(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("imageSlotId", out var slot) && slot.TryGetGuid(out var id) && id != Guid.Empty
            ? id
            : Guid.NewGuid();
    }

    private static string? NormalizeAndValidateLength(
        string? value,
        string fieldName,
        int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{fieldName} must not exceed {maximumLength} characters; received {normalized.Length}.",
                nameof(value));
        }

        return normalized;
    }

    // Refuses blocks of a retired type, new blocks that don't match their type's schema, and blocks a host's validator
    // rejects. Reused blocks matched their schema when first saved, and a type's schema never changes.
    private async Task ValidateBlocksAsync(IReadOnlyList<PageBlock> blocks, CancellationToken cancellationToken)
    {
        var typeIds = blocks.Where(block => block.IsNew && !block.IsExisting).Select(block => block.BlockTypeId!.Value).ToArray();
        var existingIds = blocks.Where(block => block.IsExisting).Select(block => block.BlockId!.Value).ToArray();
        var types = await ctx.BlockTypes.Where(type => typeIds.Contains(type.Id))
            .Select(type => new { type.Id, type.Name, type.JsonSchema })
            .ToDictionaryAsync(type => type.Id, cancellationToken);
        var reused = await ctx.PageBlocks.Where(block => existingIds.Contains(block.Id))
            .Select(block => new { block.Id, block.BlockType.Name, block.BlockData })
            .ToDictionaryAsync(block => block.Id, cancellationToken);
        var retiredTypes = options.Value.RetiredBlockTypes;
        if (types.Values.Select(type => type.Name).Concat(reused.Values.Select(block => block.Name)).FirstOrDefault(retiredTypes.Contains) is { } retired)
        {
            throw new CmsBlockRetiredException(retired);
        }

        var validators = blockValidators.ToArray();
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            string name;
            string data;
            if (block.IsExisting)
            {
                // The write reports a reused block that doesn't exist.
                if (!reused.TryGetValue(block.BlockId!.Value, out var existing))
                {
                    continue;
                }

                (name, data) = (existing.Name, existing.BlockData);
            }
            else if (block.IsNew)
            {
                if (!types.TryGetValue(block.BlockTypeId!.Value, out var type))
                {
                    throw new CmsBlockValidationException($"Block {index + 1} has a block type that doesn't exist.");
                }

                var errors = schemas.Check(type.Name, type.JsonSchema, block.BlockData!);
                if (errors.Count > 0)
                {
                    throw new CmsBlockValidationException(
                        $"Block {index + 1} ({type.Name}) doesn't match its schema: {string.Join("; ", errors.Select(error => error.Path is null ? error.Kind : $"{error.Path} {error.Kind}"))}.");
                }

                (name, data) = (type.Name, block.BlockData!);
            }
            else
            {
                // The write reports a block that is neither new nor reused.
                continue;
            }

            using var document = JsonDocument.Parse(data);
            if (block.IsNew && !block.IsExisting &&
                document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("__typename", out var typename) &&
                typename.ValueKind == JsonValueKind.String &&
                typename.GetString() is { } named &&
                named != name)
            {
                throw retiredTypes.Contains(named)
                    ? new CmsBlockRetiredException(named)
                    : new CmsBlockValidationException($"Block {index + 1} is a {name} block but its data names {named}.");
            }

            foreach (var validator in validators)
            {
                validator.Validate(name, document.RootElement);
            }
        }
    }
}
