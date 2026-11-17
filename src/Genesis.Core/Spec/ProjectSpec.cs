// FILE-MAP: the POCO object model mirroring spec.schema.json — every group (meta, models, identity, controllers, views, features, seeding...) as a C# type.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenesisV6.Core.Spec;

/// <summary>
/// C# mirror of project-spec.json / project-spec.lock.json.
/// TEACHING NOTE: the spec is the single declarative input of the whole pipeline.
/// It is IMMUTABLE after load - stages never write back into it. Expansion
/// produces a NEW spec instance serialized to project-spec.lock.json (ADR-001).
/// Pipeline runtime state lives in plan/state.json, never here.
/// </summary>
public sealed class ProjectSpec
{
    [JsonPropertyName("specVersion")] public string SpecVersion { get; set; } = "1.1";
    [JsonPropertyName("meta")] public MetaSection Meta { get; set; } = new();
    [JsonPropertyName("conventions")] public ConventionsSection Conventions { get; set; } = new();
    [JsonPropertyName("variationProfile")] public VariationProfileSection? VariationProfile { get; set; }
    [JsonPropertyName("models")] public List<ModelSpec> Models { get; set; } = [];
    [JsonPropertyName("dtos")] public List<DtoSpec> Dtos { get; set; } = [];
    [JsonPropertyName("customValidationAttributes")] public CustomValidationSection? CustomValidationAttributes { get; set; }
    [JsonPropertyName("identity")] public IdentitySection? Identity { get; set; }
    [JsonPropertyName("areas")] public List<AreaSpec> Areas { get; set; } = [];
    [JsonPropertyName("controllers")] public List<ControllerSpec> Controllers { get; set; } = [];
    [JsonPropertyName("views")] public ViewsSection? Views { get; set; }
    [JsonPropertyName("features")] public Dictionary<string, JsonElement> Features { get; set; } = [];
    [JsonPropertyName("seeding")] public SeedingSection? Seeding { get; set; }
    [JsonPropertyName("outputReport")] public OutputReportSection? OutputReport { get; set; }

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // keep Cyrillic readable in emitted JSON
    };
}

public sealed class MetaSection
{
    [JsonPropertyName("projectName")] public string ProjectName { get; set; } = "";
    [JsonPropertyName("solutionName")] public string SolutionName { get; set; } = "";
    [JsonPropertyName("rootNamespace")] public string RootNamespace { get; set; } = "";
    [JsonPropertyName("domainHint")] public string DomainHint { get; set; } = "";
    [JsonPropertyName("domainHintBg")] public string? DomainHintBg { get; set; }
    [JsonPropertyName("language")] public string Language { get; set; } = "bg";
    [JsonPropertyName("targetFramework")] public string TargetFramework { get; set; } = "net10.0";
    [JsonPropertyName("sdkPin")] public string? SdkPin { get; set; }
    [JsonPropertyName("database")] public DatabaseMeta Database { get; set; } = new();
    [JsonPropertyName("style")] public StyleMeta Style { get; set; } = new();
    [JsonPropertyName("routing")] public RoutingMeta Routing { get; set; } = new();

    /// <summary>
    /// Capability-gap report written by the agent (`dps-nn create`): what the description asked
    /// for that the generator cannot express yet. Informational only - never drives generation;
    /// it is the backlog for new feature-templates. See docs/explanation/agent-mode.md.
    /// </summary>
    [JsonPropertyName("unsupportedRequests")] public List<UnsupportedRequest>? UnsupportedRequests { get; set; }

    /// <summary>
    /// True when this project's generated UI language is Bulgarian - the ONLY language the
    /// retrofit BG-translation passes are written for (bg-term-map residue rewriting,
    /// identity-bg-map replacement, BG validation-message injection, BgIdentityErrorDescriber,
    /// verify's English-leak gate, and the seed/spec-build LLM instructions). False (currently
    /// only "en") skips those passes.
    ///
    /// SCOPE BOUNDARY (see changelog 054): this does NOT make the generated app actually
    /// English. Most hand-written .sbn templates (card_grid, layout, every feature controller's
    /// views...) contain literal Bulgarian strings baked in directly, not sourced from the spec -
    /// only the passes that retrofit-translate SCAFFOLDED (aspnet-codegenerator-produced) code,
    /// plus a few generator-side LLM instructions, are gated by this flag. A genuinely bilingual
    /// UI would need those ~54 templates rewritten, which is deliberately out of scope here.
    /// </summary>
    [JsonIgnore]
    public bool IsBulgarian => string.Equals(Language, "bg", StringComparison.OrdinalIgnoreCase);
}

public sealed class UnsupportedRequest
{
    [JsonPropertyName("request")] public string Request { get; set; } = "";
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
}

public sealed class RoutingMeta
{
    // TravelMap style: ONE route with a default area ("{area=User}/{controller=Home}/...").
    // null => classic pair of routes (areas route + default route).
    [JsonPropertyName("defaultArea")] public string? DefaultArea { get; set; }
}

public sealed class DatabaseMeta
{
    [JsonPropertyName("provider")] public string Provider { get; set; } = "SqlServer";
    [JsonPropertyName("migrateOnStartup")] public bool MigrateOnStartup { get; set; } = true;
}

public sealed class StyleMeta
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "explicit";
    [JsonPropertyName("bootstrapTheme")] public string BootstrapTheme { get; set; } = "flatly";
    [JsonPropertyName("pickedBy")] public string PickedBy { get; set; } = "user";
    [JsonPropertyName("preset")] public string? Preset { get; set; } // A10 named design preset
}

public sealed class ConventionsSection
{
    [JsonPropertyName("timestamps")] public TimestampsConvention Timestamps { get; set; } = new();
    [JsonPropertyName("deleteBehavior")] public string DeleteBehavior { get; set; } = "hard";
    [JsonPropertyName("validationMessagesLanguage")] public string ValidationMessagesLanguage { get; set; } = "bg";
}

public sealed class TimestampsConvention
{
    [JsonPropertyName("createdAt")] public bool CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public bool UpdatedAt { get; set; }
    [JsonPropertyName("deletedAt")] public bool DeletedAt { get; set; }
}

public sealed class VariationProfileSection
{
    [JsonPropertyName("seed")] public string Seed { get; set; } = "";
    // Axes kept as a loose dictionary: axis registry (VariationResolver) owns the
    // known-axis list + option sets; the spec may pin any subset explicitly.
    [JsonPropertyName("axes")] public Dictionary<string, JsonElement> Axes { get; set; } = [];
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class ModelSpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; } // Bulgarian plural noun, used for nav/admin-dropdown labels
    [JsonPropertyName("isLookup")] public bool IsLookup { get; set; } // small join/lookup model: no own admin nav entry, shown on the combined Спецификации page instead (read-only tab)
    [JsonPropertyName("isSpecification")] public bool IsSpecification { get; set; } // atomic no-FK reference model (Brand, Category): no own admin nav entry, CRUD tab on Спецификации instead
    [JsonPropertyName("softDelete")] public bool SoftDelete { get; set; }
    [JsonPropertyName("properties")] public List<PropertySpec> Properties { get; set; } = [];
    [JsonPropertyName("relations")] public List<RelationSpec> Relations { get; set; } = [];
    [JsonPropertyName("images")] public ImagesSpec? Images { get; set; }
}

public sealed class PropertySpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "string";
    [JsonPropertyName("key")] public bool Key { get; set; }
    [JsonPropertyName("foreignKey")] public bool ForeignKey { get; set; }
    [JsonPropertyName("nullable")] public bool Nullable { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; } // Bulgarian [Display(Name=...)]
    [JsonPropertyName("computed")] public string? Computed { get; set; }       // read-only expression-bodied prop
    [JsonPropertyName("seedHint")] public string? SeedHint { get; set; }        // guidance for the seed LLM per property
    [JsonPropertyName("enumValues")] public List<EnumValueSpec> EnumValues { get; set; } = []; // type:"enum" -> emitted C# enum {Model}{Prop}
    [JsonPropertyName("validations")] public List<ValidationSpec> Validations { get; set; } = [];
}

public sealed class EnumValueSpec
{
    [JsonPropertyName("value")] public string Value { get; set; } = ""; // C# identifier (e.g. "Nalichen")
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; } // Bulgarian [Display(Name=...)] on the enum member
}

public sealed class ValidationSpec
{
    [JsonPropertyName("attribute")] public string Attribute { get; set; } = "";
    [JsonPropertyName("custom")] public bool Custom { get; set; }
    [JsonPropertyName("args")] public List<JsonElement> Args { get; set; } = [];
    [JsonPropertyName("kwargs")] public Dictionary<string, JsonElement> Kwargs { get; set; } = [];
    [JsonPropertyName("errorMessage")] public string ErrorMessage { get; set; } = "";
}

public sealed class RelationSpec
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "manyToOne";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("navigation")] public string Navigation { get; set; } = "";
    [JsonPropertyName("inverseNavigation")] public string InverseNavigation { get; set; } = "";
    [JsonPropertyName("onDelete")] public string OnDelete { get; set; } = "Restrict";
}

public sealed class ImagesSpec
{
    [JsonPropertyName("mode")] public string Mode { get; set; } = "single"; // single | multiple
    [JsonPropertyName("editSelectable")] public bool EditSelectable { get; set; }
    [JsonPropertyName("storedIn")] public string StoredIn { get; set; } = "";
}

public sealed class DtoSpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("composedOf")] public List<string> ComposedOf { get; set; } = [];
    [JsonPropertyName("properties")] public List<DtoPropertySpec> Properties { get; set; } = [];
}

public sealed class DtoPropertySpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
}

public sealed class CustomValidationSection
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("emit")] public List<string> Emit { get; set; } = [];
}

public sealed class IdentitySection
{
    [JsonPropertyName("modules")] public List<string> Modules { get; set; } = [];
    [JsonPropertyName("localization")] public IdentityLocalization? Localization { get; set; }
    [JsonPropertyName("applicationUser")] public ApplicationUserSpec ApplicationUser { get; set; } = new();
    [JsonPropertyName("errorDescriber")] public ErrorDescriberSpec? ErrorDescriber { get; set; }
    [JsonPropertyName("roles")] public List<RoleSpec> Roles { get; set; } = [];
    [JsonPropertyName("seedUsers")] public SeedUsersSpec? SeedUsers { get; set; }
}

public sealed class IdentityLocalization
{
    [JsonPropertyName("strategy")] public string Strategy { get; set; } = "staticReplacementMap";
    [JsonPropertyName("mapFile")] public string MapFile { get; set; } = "identity-bg-map.json";
    [JsonPropertyName("comment")] public string? Comment { get; set; }
}

public sealed class ApplicationUserSpec
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("extends")] public string Extends { get; set; } = "IdentityUser";
    [JsonPropertyName("keyType")] public string KeyType { get; set; } = "guid-default";
    [JsonPropertyName("extraProperties")] public List<PropertySpec> ExtraProperties { get; set; } = [];
    [JsonPropertyName("controller")] public UsersControllerSpec? Controller { get; set; }
}

public sealed class UsersControllerSpec
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("writtenFromScratch")] public bool WrittenFromScratch { get; set; }
    [JsonPropertyName("actions")] public List<string> Actions { get; set; } = [];
    [JsonPropertyName("rules")] public List<string> Rules { get; set; } = [];
}

public sealed class ErrorDescriberSpec
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "bg";
}

public sealed class RoleSpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("isAdmin")] public bool IsAdmin { get; set; }
}

public sealed class SeedUsersSpec
{
    [JsonPropertyName("inContextSeeding")] public bool InContextSeeding { get; set; } = true;
    [JsonPropertyName("users")] public List<SeedUserSpec> Users { get; set; } = [];
}

public sealed class SeedUserSpec
{
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    // Resolved at expand time from variation seedPasswordPattern; echoed into the lock
    // so the verify stage can log in with it (never hardcode in verify).
    [JsonPropertyName("password")] public string? Password { get; set; }
}

public sealed class AreaSpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("forRole")] public string ForRole { get; set; } = "";
}

public sealed class ControllerSpec
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("area")] public string? Area { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("dto")] public string? Dto { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; } // Bulgarian nav label; falls back to the raw model/controller name
    [JsonPropertyName("isLookup")] public bool IsLookup { get; set; } // small join/lookup model: no own admin nav entry, shown on the combined Спецификации page instead (read-only tab)
    [JsonPropertyName("isSpecification")] public bool IsSpecification { get; set; } // atomic no-FK reference model (Brand, Category): no own admin nav entry, CRUD tab on Спецификации instead
    [JsonPropertyName("generatedBy")] public string GeneratedBy { get; set; } = "template"; // scaffold | template | writtenFromScratch
    [JsonPropertyName("actions")] public List<string> Actions { get; set; } = [];
    [JsonPropertyName("removedActions")] public List<string> RemovedActions { get; set; } = [];
    [JsonPropertyName("removalReason")] public string? RemovalReason { get; set; }
    [JsonPropertyName("redirects")] public List<RedirectSpec> Redirects { get; set; } = [];
    [JsonPropertyName("sorting")] public SortingSpec? Sorting { get; set; }
    [JsonPropertyName("pagination")] public PaginationSpec? Pagination { get; set; }
    [JsonPropertyName("ownership")] public OwnershipSpec? Ownership { get; set; }
}

public sealed class PaginationSpec
{
    [JsonPropertyName("pageSize")] public int PageSize { get; set; } = 20;
}

public sealed class OwnershipSpec
{
    // Property on the model holding the owner's Identity user id (e.g. "UserId").
    // Template-generated controllers guard Edit/Delete server-side with it and
    // views hide the buttons for non-owners. Admin role bypasses.
    [JsonPropertyName("userProperty")] public string UserProperty { get; set; } = "UserId";
}

public sealed class RedirectSpec
{
    [JsonPropertyName("after")] public string After { get; set; } = "";
    [JsonPropertyName("to")] public string To { get; set; } = "";
}

public sealed class SortingSpec
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("implementedIn")] public string ImplementedIn { get; set; } = "controller";
    [JsonPropertyName("fields")] public List<string> Fields { get; set; } = [];
}

public sealed class ViewsSection
{
    [JsonPropertyName("layout")] public LayoutSpec Layout { get; set; } = new();
    [JsonPropertyName("pages")] public List<PageSpec> Pages { get; set; } = [];
}

public sealed class LayoutSpec
{
    [JsonPropertyName("file")] public string File { get; set; } = "Views/Shared/_Layout.cshtml";
    [JsonPropertyName("header")] public JsonElement Header { get; set; }
    [JsonPropertyName("footer")] public bool Footer { get; set; }
}

public sealed class PageSpec
{
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("content")] public List<string> Content { get; set; } = [];
    [JsonPropertyName("card")] public List<string> Card { get; set; } = [];
    [JsonPropertyName("search")] public JsonElement? Search { get; set; }
    [JsonPropertyName("unauthenticatedAction")] public string? UnauthenticatedAction { get; set; }
    [JsonPropertyName("rowButtons")] public List<string> RowButtons { get; set; } = [];
}

public sealed class SeedingSection
{
    [JsonPropertyName("generator")] public string Generator { get; set; } = "haiku";
    [JsonPropertyName("rowsPerModel")] public Dictionary<string, int> RowsPerModel { get; set; } = [];
    [JsonPropertyName("imageSource")] public string ImageSource { get; set; } = "picsum"; // pexels | pixabay | picsum
    [JsonPropertyName("fkStrategy")] public string FkStrategy { get; set; } = "symbolicRefs";
    [JsonPropertyName("fkStrategyComment")] public string? FkStrategyComment { get; set; }
    [JsonPropertyName("outputFile")] public string OutputFile { get; set; } = "Data/SeedData.cs";
}

public sealed class OutputReportSection
{
    [JsonPropertyName("file")] public string File { get; set; } = "wwwroot/output/project-summary.json";
    [JsonPropertyName("include")] public List<JsonElement> Include { get; set; } = [];
}
