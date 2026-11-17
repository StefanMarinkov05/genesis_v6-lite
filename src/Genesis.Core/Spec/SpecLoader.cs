// FILE-MAP: loads a project-spec.json file and validates it against spec.schema.json before returning a ProjectSpec.
using System.Text.Json;
using Json.Schema;

namespace GenesisV6.Core.Spec;

public sealed class SpecValidationException(string message, IReadOnlyList<string> errors) : Exception(message)
{
    public IReadOnlyList<string> Errors { get; } = errors;
    public override string ToString() => $"{Message}\n - " + string.Join("\n - ", Errors);
}

/// <summary>
/// Loads and validates a project spec. Validation is TWO-LAYER by design:
///  1. JSON Schema (spec.schema.json) - shape, enums, additionalProperties:false
///     so unknown keys FAIL LOUD instead of being silently ignored by the
///     System.Text.Json deserializer (which would happily drop them).
///  2. Semantic checks the schema cannot express (cross-references: relation
///     targets exist, controller models exist, area roles exist, etc.).
/// </summary>
public sealed class SpecLoader(string schemaPath)
{
    private readonly JsonSchema _schema = JsonSchema.FromFile(schemaPath);

    public ProjectSpec Load(string specPath)
    {
        var raw = File.ReadAllText(specPath);
        return LoadFromString(raw, specPath);
    }

    public ProjectSpec LoadFromString(string json, string sourceName = "<string>")
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new SpecValidationException($"Spec '{sourceName}' is not valid JSON.", [ex.Message]);
        }

        // Layer 1: schema
        var result = _schema.Evaluate(doc.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List // flat list of all failures, not just the first
        });
        if (!result.IsValid)
        {
            var errors = result.Details
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
                .Distinct()
                .ToList();
            throw new SpecValidationException($"Spec '{sourceName}' failed schema validation.", errors);
        }

        var spec = JsonSerializer.Deserialize<ProjectSpec>(json, ProjectSpec.JsonOpts)
                   ?? throw new SpecValidationException($"Spec '{sourceName}' deserialized to null.", []);

        // Layer 2: semantic cross-checks
        var semanticResults = SemanticCheck(spec).ToList();
        foreach (var w in semanticResults.Where(x => x.StartsWith("[warn]")))
            Console.Error.WriteLine($"[dps-nn] {w}");
        var semanticErrors = semanticResults.Where(x => !x.StartsWith("[warn]")).ToList();
        if (semanticErrors.Count > 0)
            throw new SpecValidationException($"Spec '{sourceName}' failed semantic validation.", semanticErrors);

        return spec;
    }

    public static IEnumerable<string> SemanticCheck(ProjectSpec spec)
    {
        var modelNames = spec.Models.Select(m => m.Name).ToHashSet();
        var roleNames = spec.Identity?.Roles.Select(r => r.Name).ToHashSet() ?? [];
        var controllerNames = spec.Controllers.Select(c => c.Name).ToHashSet();

        foreach (var m in spec.Models)
        {
            if (m.Properties.Count(p => p.Key) != 1)
                yield return $"Model {m.Name}: exactly one property must have key=true.";
            foreach (var r in m.Relations)
                if (!modelNames.Contains(r.Target))
                    yield return $"Model {m.Name}: relation target '{r.Target}' is not a declared model.";
        }

        foreach (var d in spec.Dtos)
            foreach (var src in d.ComposedOf)
                if (!modelNames.Contains(src))
                    yield return $"DTO {d.Name}: composedOf '{src}' is not a declared model.";

        foreach (var c in spec.Controllers)
        {
            if (c.Model is not null && !modelNames.Contains(c.Model))
                yield return $"Controller {c.Name}: model '{c.Model}' is not declared.";
            if (c.Dto is not null && spec.Dtos.All(d => d.Name != c.Dto))
                yield return $"Controller {c.Name}: dto '{c.Dto}' is not declared.";
            if (c.GeneratedBy == "scaffold" && c.Model is null)
                yield return $"Controller {c.Name}: generatedBy=scaffold requires a model.";
            foreach (var red in c.Redirects)
            {
                var targetController = red.To.Split('.')[0];
                if (!controllerNames.Contains(targetController))
                    yield return $"Controller {c.Name}: redirect target '{red.To}' references unknown controller.";
            }
            if (c.Sorting is { Enabled: true } s && c.Model is not null)
            {
                var props = spec.Models.First(m => m.Name == c.Model).Properties.Select(p => p.Name).ToHashSet();
                // CreatedAt/UpdatedAt live on the shared AuditableEntity base, not in the
                // model's own properties list - still valid sort targets when enabled.
                if (spec.Conventions.Timestamps.CreatedAt) props.Add("CreatedAt");
                if (spec.Conventions.Timestamps.UpdatedAt) props.Add("UpdatedAt");
                foreach (var f in s.Fields.Where(f => !props.Contains(f)))
                    yield return $"Controller {c.Name}: sorting field '{f}' not on model {c.Model}.";
            }
        }

        foreach (var a in spec.Areas)
            if (!a.ForRole.StartsWith("$variation:") && !roleNames.Contains(a.ForRole))
                yield return $"Area {a.Name}: forRole '{a.ForRole}' is not a declared role.";

        // Every template-generated controller with an Index action must have a page in
        // views.pages that will render it - otherwise the app 500s at runtime with
        // "The view 'Index' was not found" (silent gap discovered in live testing).
        var pageControllerDirs = spec.Views?.Pages
            .Select(pg => pg.File.Split('/'))
            .Where(parts => Array.IndexOf(parts, "Views") is var vi && vi >= 0 && vi + 1 < parts.Length)
            .Select(parts => parts[Array.IndexOf(parts, "Views") + 1])
            .ToHashSet() ?? [];
        foreach (var c in spec.Controllers.Where(c => c.GeneratedBy == "template" && c.Actions.Contains("Index") && c.Model is not null))
            if (!pageControllerDirs.Contains(c.Name.Replace("Controller", "")))
                yield return $"Controller {c.Name}: template-generated Index has no matching page in views.pages ('{c.Name.Replace("Controller", "")}/Index').";

        // Feature fragments inject models at expand time (Post, Order, Review...), so a raw
        // spec may legitimately seed models it does not declare itself - only flag keys that
        // no declared model AND no enabled feature could provide.
        var anyFeatureEnabled = spec.Features.Values.Any(f =>
            f.TryGetProperty("enabled", out var en) && en.GetBoolean());
        // Task 1 (v6): Bulgarian UI is a hard requirement, so full [Display(Name)] coverage is
        // now a HARD GATE, not a nudge - generation fails if any visible (non-key, non-FK,
        // non-computed) property lacks a Bulgarian displayName, so English property names can
        // never leak into a rendered label. Computed props are exempt: they carry an optional
        // displayName in model.sbn but are read-only derived values, not user-facing form/table
        // columns the way stored props are. This runs on BOTH the raw spec AND the expanded lock
        // (SpecExpandStage re-Loads the lock), so feature-injected models are covered too.
        foreach (var m in spec.Models)
            foreach (var pr in m.Properties.Where(pr => !pr.Key && !pr.ForeignKey && pr.Computed is null && pr.DisplayName is null))
                yield return $"model {m.Name}.{pr.Name} lacks displayName - Bulgarian UI requires a displayName on every visible property (non-key, non-FK).";

        if (spec.Seeding is not null)
            foreach (var (model, _) in spec.Seeding.RowsPerModel)
                if (!modelNames.Contains(model) && !anyFeatureEnabled)
                    yield return $"Seeding: rowsPerModel key '{model}' is not a declared model.";
    }
}
