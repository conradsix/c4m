#pragma warning disable SA1402 // The endpoint kinds are kept with the attribute and helper that use them.
#pragma warning disable SA1649 // File name should match first type name
namespace ConradSix.C4M;

using System.Reflection;

using FastEndpoints;

public enum CmsEndpointKind
{
    // Drafts, history, schemas and every change.
    Editor,

    // What a public site reads: published pages by path, and menus.
    Delivery,
}

public static class C4MEndpoints
{
    // Add it to the host's FastEndpoints assemblies to serve the CMS's endpoints.
    public static Assembly Assembly { get; } = typeof(C4MEndpoints).Assembly;
}

public static class CmsEndpointDefinitionExtensions
{
    // For the host's FastEndpoints configurator. Returns false for endpoints that aren't the CMS's.
    public static bool ConfigureCmsEndpoint(
        this EndpointDefinition endpoint,
        Action<EndpointDefinition> editor,
        Action<EndpointDefinition> delivery)
    {
        switch (endpoint.EndpointType.GetCustomAttribute<CmsEndpointAttribute>()?.Kind)
        {
            case CmsEndpointKind.Editor:
                editor(endpoint);
                return true;
            case CmsEndpointKind.Delivery:
                delivery(endpoint);
                return true;
            default:
                return false;
        }
    }
}

// Marks a CMS endpoint so the host can authorize editor and delivery endpoints differently.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CmsEndpointAttribute(CmsEndpointKind kind) : Attribute
{
    public CmsEndpointKind Kind { get; } = kind;
}
