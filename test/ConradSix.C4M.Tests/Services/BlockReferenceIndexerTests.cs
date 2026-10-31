namespace ConradSix.C4M.Tests.Services;

using AwesomeAssertions;

using ConradSix.C4M;

public class BlockReferenceIndexerTests
{
    private readonly BlockReferenceIndexer _resolver = new([new TypedReferenceExtractor()]);

    [Fact]
    public void Index_SingleResourceRefAtRoot_ReturnsCorrectResolvedRef()
    {
        var json = TestJsonAssetHelper.LoadJsonString("simple-resource-ref.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Single(results);
        var result = results[0];
        Assert.Equal("image-ref", result.Kind);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), result.TargetId);
        Assert.Equal("$", result.JsonPath);
    }

    [Fact]
    public void Index_MultipleNestedResourceRefs_ReturnsAllWithCorrectPaths()
    {
        var json = TestJsonAssetHelper.LoadJsonString("nested-resource-refs.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Equal(2, results.Count);

        var imageRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440001"));
        Assert.NotNull(imageRef);
        Assert.Equal("image-ref", imageRef.Kind);
        Assert.Equal("$.content.image", imageRef.JsonPath);

        var backgroundRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440002"));
        Assert.NotNull(backgroundRef);
        Assert.Equal("image-ref", backgroundRef.Kind);
        Assert.Equal("$.content.sections.hero.backgroundImage", backgroundRef.JsonPath);
    }

    [Fact]
    public void Index_ResourceRefsInArray_ReturnsWithArrayIndexPaths()
    {
        var json = TestJsonAssetHelper.LoadJsonString("array-resource-refs.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Equal(3, results.Count);

        var galleryRef1 = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440003"));
        Assert.NotNull(galleryRef1);
        Assert.Equal("$.gallery[0]", galleryRef1.JsonPath);

        var galleryRef2 = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440004"));
        Assert.NotNull(galleryRef2);
        Assert.Equal("$.gallery[1]", galleryRef2.JsonPath);

        var carouselRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440005"));
        Assert.NotNull(carouselRef);
        Assert.Equal("$.carousel.images[0]", carouselRef.JsonPath);
    }

    [Fact]
    public void Index_ComplexNestedStructure_ReturnsAllValidRefs()
    {
        var json = TestJsonAssetHelper.LoadJsonString("mixed-complex.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Equal(4, results.Count);

        var headerRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440006"));
        Assert.NotNull(headerRef);
        Assert.Equal("$.page.header", headerRef.JsonPath);

        var heroRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440007"));
        Assert.NotNull(heroRef);
        Assert.Equal("$.page.sections[0].background", heroRef.JsonPath);

        var contentRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440008"));
        Assert.NotNull(contentRef);
        Assert.Equal("$.page.sections[0].content.images[0]", contentRef.JsonPath);

        var documentRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440009"));
        Assert.NotNull(documentRef);
        Assert.Equal("document-ref", documentRef.Kind);
        Assert.Equal("$.page.sections[1].items[0].image", documentRef.JsonPath);
    }

    [Fact]
    public void Index_ShouldNumberReferencesTheSame_WhateverOrderTheBlocksPropertiesAreIn()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var image = $$"""{ "__typename": "image-ref", "id": "{{first}}" }""";
        var page = $$"""{ "__typename": "page-ref", "id": "{{second}}" }""";

        var asWritten = this._resolver.Index($$"""{ "zImage": {{image}}, "aPage": {{page}} }""");
        var asStored = this._resolver.Index($$"""{ "aPage": {{page}}, "zImage": {{image}} }""");

        asWritten.Should().Equal(asStored);
        asWritten.Select(reference => (reference.TargetId, reference.Ordinal)).Should().Equal((second, 0), (first, 1));
    }

    [Fact]
    public void Index_EmptyJson_ReturnsEmptyResult()
    {
        var results1 = this._resolver.Index(string.Empty).ToList();
        var results2 = this._resolver.Index("   ").ToList();
        var results3 = this._resolver.Index(null!).ToList();

        Assert.Empty(results1);
        Assert.Empty(results2);
        Assert.Empty(results3);
    }

    [Fact]
    public void Index_InvalidJson_ReturnsEmptyResult()
    {
        var results1 = this._resolver.Index("{invalid json}").ToList();
        var results2 = this._resolver.Index("not json at all").ToList();
        var results3 = this._resolver.Index("{\"unclosed\":").ToList();

        Assert.Empty(results1);
        Assert.Empty(results2);
        Assert.Empty(results3);
    }

    [Fact]
    public void Index_NoResourceRefs_ReturnsEmptyResult()
    {
        var json = TestJsonAssetHelper.LoadJsonString("no-resource-refs.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Index_InvalidResourceRefFormats_IgnoresInvalidRefs()
    {
        var json = TestJsonAssetHelper.LoadJsonString("invalid-resource-refs.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Single(results);
        var validRef = results[0];
        Assert.Equal("image-ref", validRef.Kind);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440010"), validRef.TargetId);
        Assert.Equal("$.validRef", validRef.JsonPath);
    }

    [Fact]
    public void Index_EmptyObjectsAndArrays_ReturnsOnlyValidRefs()
    {
        var json = TestJsonAssetHelper.LoadJsonString("empty-objects.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Single(results);
        var validRef = results[0];
        Assert.Equal("image-ref", validRef.Kind);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440015"), validRef.TargetId);
        Assert.Equal("$.arrayWithEmpty[1].validRef", validRef.JsonPath);
    }

    [Fact]
    public void Index_MalformedGuids_IgnoresInvalidGuidsReturnsOnlyValid()
    {
        var json = TestJsonAssetHelper.LoadJsonString("malformed-guids.json");

        var results = this._resolver.Index(json).ToList();

        Assert.Single(results);
        var validRef = results[0];
        Assert.Equal("image-ref", validRef.Kind);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440016"), validRef.TargetId);
        Assert.Equal("$.validGuid", validRef.JsonPath);
    }

    [Fact]
    public void Index_ResourceRefWithDifferentTypes_PreservesTypeNames()
    {
        var json = /*lang=json,strict*/ """
        {
          "imageRef": {
            "__typename": "image-ref",
            "id": "550e8400-e29b-41d4-a716-446655440001"
          },
          "documentRef": {
            "__typename": "document-ref",
            "id": "550e8400-e29b-41d4-a716-446655440002"
          },
          "customRef": {
            "__typename": "custom-type",
            "id": "550e8400-e29b-41d4-a716-446655440003"
          }
        }
        """;

        var results = this._resolver.Index(json).ToList();

        Assert.Equal(3, results.Count);

        var imageRef = results.FirstOrDefault(r => r.Kind == "image-ref");
        Assert.NotNull(imageRef);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440001"), imageRef.TargetId);

        var documentRef = results.FirstOrDefault(r => r.Kind == "document-ref");
        Assert.NotNull(documentRef);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440002"), documentRef.TargetId);

        var customRef = results.FirstOrDefault(r => r.Kind == "custom-type");
        Assert.NotNull(customRef);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440003"), customRef.TargetId);
    }

    [Fact]
    public void Index_MarkdownPageRefs_ReturnsUniquePagesAtTheirStringPaths()
    {
        var json = /*lang=json,strict*/ """
        {
          "markdown": "[First](page-ref:550e8400-e29b-41d4-a716-446655440001) and [again](page-ref:550e8400-e29b-41d4-a716-446655440001)",
          "items": [
            {
              "answerMarkdown": "[Second](page-ref:550e8400-e29b-41d4-a716-446655440002)"
            },
            {
              "answerMarkdown": "[First](page-ref:550e8400-e29b-41d4-a716-446655440001) and [invalid](page-ref:not-a-guid)"
            }
          ]
        }
        """;

        var results = this._resolver.Index(json).ToList();

        Assert.Equal(3, results.Count);
        Assert.Contains(
            results,
            result =>
                result.Kind == "page-ref" &&
                result.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440001") &&
                result.JsonPath == "$.markdown");
        Assert.Contains(
            results,
            result =>
                result.Kind == "page-ref" &&
                result.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440002") &&
                result.JsonPath == "$.items[0].answerMarkdown");
        Assert.Contains(
            results,
            result =>
                result.Kind == "page-ref" &&
                result.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440001") &&
                result.JsonPath == "$.items[1].answerMarkdown");
    }

    [Fact]
    public void Index_PropertyNameCaseSensitivity_RequiresExactCase()
    {
        var json = /*lang=json,strict*/ """
        {
          "validRef": {
            "__typename": "image-ref",
            "id": "550e8400-e29b-41d4-a716-446655440001"
          },
          "wrongCaseType": {
            "__TYPENAME": "image-ref",
            "id": "550e8400-e29b-41d4-a716-446655440002"
          },
          "wrongCaseId": {
            "__typename": "image-ref",
            "ID": "550e8400-e29b-41d4-a716-446655440003"
          }
        }
        """;

        var results = this._resolver.Index(json).ToList();

        Assert.Single(results);
        Assert.Equal("image-ref", results[0].Kind);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440001"), results[0].TargetId);
    }

    [Fact]
    public void Index_JsonPathGeneration_HandlesSpecialCharacters()
    {
        var json = /*lang=json,strict*/ """
        {
          "special-property": {
            "nested_property": {
              "__typename": "image-ref",
              "id": "550e8400-e29b-41d4-a716-446655440001"
            }
          },
          "property with spaces": {
            "__typename": "image-ref",
            "id": "550e8400-e29b-41d4-a716-446655440002"
          }
        }
        """;

        var results = this._resolver.Index(json).ToList();

        Assert.Equal(2, results.Count);

        var specialRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440001"));
        Assert.NotNull(specialRef);
        Assert.Equal("$.special-property.nested_property", specialRef.JsonPath);

        var spacesRef = results.FirstOrDefault(r => r.TargetId == Guid.Parse("550e8400-e29b-41d4-a716-446655440002"));
        Assert.NotNull(spacesRef);
        Assert.Equal("$.property with spaces", spacesRef.JsonPath);
    }

    [Fact]
    public void Index_NonStringTypename_IgnoresTheObject()
    {
        var json = /*lang=json,strict*/ """
        {
          "numeric": { "__typename": 1, "id": "550e8400-e29b-41d4-a716-446655440001" },
          "valid": { "__typename": "image-ref", "id": "550e8400-e29b-41d4-a716-446655440002" }
        }
        """;

        var results = this._resolver.Index(json);

        Assert.Single(results);
        Assert.Equal(Guid.Parse("550e8400-e29b-41d4-a716-446655440002"), results[0].TargetId);
    }

    [Fact]
    public void Index_OrdinalsFollowPropertyNamesAcrossExtractors()
    {
        var indexer = new BlockReferenceIndexer([new TypedReferenceExtractor(), new RecordingExtractor("other-ref")]);
        var json = /*lang=json,strict*/ """
        {
          "first": { "__typename": "image-ref", "id": "550e8400-e29b-41d4-a716-446655440001" },
          "other": "550e8400-e29b-41d4-a716-446655440002",
          "last": "[Page](page-ref:550e8400-e29b-41d4-a716-446655440003)"
        }
        """;

        var results = indexer.Index(json);

        Assert.Equal(["$.first", "$.last", "$.other"], results.OrderBy(result => result.Ordinal).Select(result => result.JsonPath));
        Assert.Equal([0, 1, 2], results.Select(result => result.Ordinal));
    }

    // Reports any string property named "other" as a reference.
    private sealed class RecordingExtractor(string kind) : IReferenceExtractor
    {
        public IEnumerable<ExtractedReference> Extract(ReferenceNode node) =>
            node.PropertyName == "other" && node.Element.TryGetGuid(out var id)
                ? [new ExtractedReference(kind, id, node.JsonPath)]
                : [];
    }
}
