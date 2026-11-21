// FILE-MAP: stage 6 — controllers from three provenances (scaffold+deviations, template, hand-written) plus their views; deviations applied as anchored regex transforms.
using System.Text.RegularExpressions;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;
using GenesisV6.Core.Util;

namespace GenesisV6.Core.Stages;

/// <summary>
/// Stage 6 `emit controllers+views`.
///
/// Three controller provenances, matching how a student would really mix techniques:
///   generatedBy=scaffold            -> aspnet-codegenerator controller, THEN declarative
///                                      deviations applied (removedActions, redirects, sorting)
///   generatedBy=template            -> Scriban emission (SummaryController, HomeController...)
///   identity.applicationUser.controller (writtenFromScratch) -> dedicated template with the
///                                      AdminCannotDeleteSelf rule enforced SERVER-SIDE.
///
/// Deviations are applied as targeted source transforms on the scaffolded C# - this is
/// deliberately regex-on-known-scaffold-shapes, NOT a Roslyn rewrite: the scaffolder
/// output for a given SDK pin is stable, and the transforms fail loud when a pattern
/// is not found (so an SDK bump surfaces immediately instead of silently emitting
/// wrong code). ADR-006 records this trade-off.
/// </summary>
public sealed class EmitControllersViewsStage : IStage
{
    public string Name => "emit-controllers-views";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        // ImageService + partials first - views reference them.
        if (spec.Models.Any(m => m.Images is not null))
        {
            await EmitModelsStage.WriteAsync(ctx, "Services/ImageService.cs",
                engine.RenderFile(ctx.Template("controllers/image_service.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Views/Shared/_ImageUploadPartial.cshtml",
                engine.RenderFile(ctx.Template("views/image_upload_partial.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Views/Shared/_ImageGalleryPartial.cshtml",
                engine.RenderFile(ctx.Template("views/image_gallery_partial.sbn")), ct);
            // Task 14: per-model Admin image-management controller + view (template-generated,
            // not scaffold-based — avoids fragile scaffold transforms on Create/Edit actions).
            foreach (var imgModel in spec.Models.Where(m => m.Images is not null))
            {
                var extra = new { model = imgModel };
                await EmitModelsStage.WriteAsync(ctx,
                    $"Areas/Admin/Controllers/{imgModel.Name}ImagesController.cs",
                    engine.RenderFile(ctx.Template("controllers/product_images_controller.sbn"), extra), ct);
                await EmitModelsStage.WriteAsync(ctx,
                    $"Areas/Admin/Views/{imgModel.Name}Images/Index.cshtml",
                    engine.RenderFile(ctx.Template("views/admin_product_images.sbn"), extra), ct);
            }
        }

        // Task 15/16/10 (Package E): shared single-file upload helper (FileUploadService) - distinct from
        // the per-model ImageService. Emitted whenever a consumer exists: admin pfp upload
        // (ApplicationUser.ProfilePicturePath), review image upload (rating feature), or post image
        // upload (posts feature). Must stay in sync with program.sbn's DI registration condition
        // (has_pfp || features.rating || features.posts).
        var hasPfp = spec.Identity?.ApplicationUser.ExtraProperties.Any(p => p.Name == "ProfilePicturePath") ?? false;
        if (hasPfp || FeatureEnabled(spec, "rating") || FeatureEnabled(spec, "posts"))
            await EmitModelsStage.WriteAsync(ctx, "Services/FileUploadService.cs",
                engine.RenderFile(ctx.Template("controllers/file_upload_service.sbn")), ct);

        // Stale declared views from a previous failed run can break the scaffolder's
        // internal build - remove them; they are re-emitted at the end of this stage.
        if (spec.Views is { } vPre)
            foreach (var pagePre in vPre.Pages)
            {
                var stale = Path.Combine(ctx.ProjectDir, pagePre.File);
                if (File.Exists(stale)) File.Delete(stale);
            }

        foreach (var controller in spec.Controllers)
        {
            switch (controller.GeneratedBy)
            {
                case "scaffold":
                    await ScaffoldControllerAsync(ctx, controller, ct);
                    if (controller.Area is { } scaffoldArea)
                        InjectAreaAttribute(ctx, controller, scaffoldArea);
                    InjectImageInclude(ctx, controller);
                    ApplySoftDelete(ctx, controller);
                    ApplyBgResiduePass(ctx, controller);
                    StripSystemFieldsFromViews(ctx, controller);
                    InjectOwnerAutoAssign(ctx, controller);
                    InjectQuantityDerivedStatus(ctx, controller);
                    InjectImageRemoveOption(ctx, controller);
                    InjectImagesCreateHint(ctx, controller);
                    InjectPostCommentsIntoAdminDetails(ctx, controller);
                    InjectReviewLoadingIntoDetails(ctx, controller);
                    ApplyDeviations(ctx, controller, engine);
                    InjectAreaAuthorization(ctx, controller); // last: sees the final action set
                    break;
                case "template":
                    await EmitModelsStage.WriteAsync(ctx, controller.File,
                        engine.RenderFile(ctx.Template("controllers/controller.sbn"), new { controller }), ct);
                    // Template controllers are landing/home pages in the client area and must stay
                    // public; one placed in an admin-role area is locked like a scaffolded one.
                    if (IsAdminArea(ctx.Spec, controller.Area))
                        InjectAreaAuthorization(ctx, controller);
                    break;
            }
        }

        // Everything from here to the build gate is TEMPLATE emission: pure Scriban renders to
        // DISTINCT file paths, no external process, no read-after-write between items — so it fans
        // out across the named worker lanes (issue #5). It runs AFTER the scaffold loop above (which
        // MUST stay serial: each scaffold shells out to aspnet-codegenerator and mutates shared build
        // state), so the rating feature's product Details still correctly overwrites the scaffolded
        // one. Any per-block value (primary model, price property, page template) is computed HERE on
        // the single orchestrator thread and captured; only RenderFile + WriteAsync run in parallel.
        // Emit registers a lazy work item: the Scriban render (the CPU-heavy part) runs INSIDE the
        // lane via the Func<string>, not here on the orchestrator thread — so the expensive work,
        // not just the file write, is what actually parallelizes.
        var work = new List<(string Label, Func<Task> Work)>();
        void Emit(string relPath, Func<string> render) => work.Add((relPath, () => EmitModelsStage.WriteAsync(ctx, relPath, render(), ct)));

        // Written-from-scratch UsersController + its Index view (template-generated pair).
        if (spec.Identity?.ApplicationUser.Controller is { WrittenFromScratch: true } uc)
        {
            Emit(uc.File, () => engine.RenderFile(ctx.Template("controllers/users_controller.sbn"), new { uc }));
            Emit("Areas/Admin/Views/Users/Index.cshtml", () => engine.RenderFile(ctx.Template("views/users_index.sbn")));
            // Task 15: the Edit action previously returned View(user) with NO emitted view (a latent
            // 500). Emit the admin user Edit form (FirstName + profile-picture upload when present).
            Emit("Areas/Admin/Views/Users/Edit.cshtml", () => engine.RenderFile(ctx.Template("views/users_edit.sbn")));
            // Task 7: admin user Details page (aggregation via the shared UserProfileService). The
            // controller only exposes Details when posts||catalogues (the service exists), so gate the
            // view the same way to avoid emitting a view for a non-existent action.
            if (FeatureEnabled(spec, "posts") || FeatureEnabled(spec, "catalogues"))
                Emit("Areas/Admin/Views/Users/Details.cshtml", () => engine.RenderFile(ctx.Template("views/users_details.sbn")));
        }

        // A5: "Спецификации" - the evolved/renamed Характеристики (item 6/7). One consolidated
        // page: full-CRUD tabs for atomic reference models (isSpecification, e.g. Brand/Category -
        // reuses their scaffolded sub-controllers, no scaffolded-controller surgery), read-only
        // tabs for join/lookup models (isLookup, as Характеристики always did), and a read-only
        // Feedbacks tab (Review) when rating is enabled - each read-only/feedback tab carries a
        // generic auto-derived filter bar (FK relation / user / rating selects).
        if (spec.Models.Any(m => m.IsLookup || m.IsSpecification) || FeatureEnabled(spec, "rating"))
        {
            Emit("Areas/Admin/Controllers/SpecificationsController.cs", () => engine.RenderFile(ctx.Template("controllers/specifications_controller.sbn")));
            Emit("Areas/Admin/Views/Specifications/Index.cshtml", () => engine.RenderFile(ctx.Template("views/specifications_index.sbn")));
        }

        // Feature controller/view pairs - each enabled feature emits its full stack.
        if (FeatureEnabled(spec, "posts"))
        {
            Emit("Areas/Client/Controllers/PostsController.cs", () => engine.RenderFile(ctx.Template("controllers/posts_controller.sbn")));
            Emit("Areas/Client/Views/Posts/Index.cshtml", () => engine.RenderFile(ctx.Template("views/posts_index.sbn")));
            Emit("Areas/Client/Views/Posts/Details.cshtml", () => engine.RenderFile(ctx.Template("views/posts_details.sbn")));
            Emit("Areas/Client/Views/Posts/Create.cshtml", () => engine.RenderFile(ctx.Template("views/posts_create.sbn")));
        }
        // Changelog 141: the cart surface exists with `cart` OR `orders` (both fragments bring the
        // same CartItem); checkout, order history and order details only with `orders`, and the
        // templates gate their Order code on features.orders so a cart-only project compiles.
        var hasOrders = FeatureEnabled(spec, "orders");
        if (hasOrders || FeatureEnabled(spec, "cart"))
        {
            var primary = FeaturePrimaryModel(spec, hasOrders ? "orders" : "cart");
            var priceProp = spec.Models.First(m => m.Name == primary).Properties
                .FirstOrDefault(pp => pp.Type == "decimal")?.Name ?? "Price";
            var extra = new { primary, price_property = priceProp };
            Emit("Areas/Client/Controllers/CartController.cs", () => engine.RenderFile(ctx.Template("controllers/cart_controller.sbn"), extra));
            Emit("Areas/Client/Views/Cart/Index.cshtml", () => engine.RenderFile(ctx.Template("views/cart_index.sbn"), extra));
            if (hasOrders)
            {
                Emit("Areas/Client/Views/Cart/Orders.cshtml", () => engine.RenderFile(ctx.Template("views/cart_orders.sbn"), extra));
                // A6: per-order details (read-only, own orders only).
                Emit("Areas/Client/Views/Cart/OrderDetails.cshtml", () => engine.RenderFile(ctx.Template("views/cart_order_details.sbn"), extra));
            }
        }
        if (FeatureEnabled(spec, "follows"))
        {
            Emit("Areas/Client/Controllers/FollowsController.cs", () => engine.RenderFile(ctx.Template("controllers/follows_controller.sbn")));
            Emit("Areas/Client/Views/Follows/List.cshtml", () => engine.RenderFile(ctx.Template("views/follows_list.sbn")));
        }
        if (FeatureEnabled(spec, "rating"))
        {
            var primary = FeaturePrimaryModel(spec, "rating");
            Emit("Areas/Client/Controllers/ReviewsController.cs", () => engine.RenderFile(ctx.Template("controllers/reviews_controller.sbn"), new { primary }));
            // A15: NO standalone Отзиви page - feedback lives only under its product's Details.
            // A3/A14: emit the rich product Details (carousel + props + feedback) into the
            // Client Products view folder, replacing the scaffolded Details.
            Emit($"Areas/Client/Views/{primary}s/Details.cshtml", () => engine.RenderFile(ctx.Template("views/product_details.sbn"), new { primary }));
        }

        // Catalogues feature: per-user named lists over the configured primary model.
        if (FeatureEnabled(spec, "catalogues"))
        {
            var primary = CataloguesPrimaryModel(spec);
            Emit("Areas/Client/Controllers/CataloguesController.cs", () => engine.RenderFile(ctx.Template("controllers/catalogues_controller.sbn"), new { primary }));
            Emit("Areas/Client/Views/Catalogues/Index.cshtml", () => engine.RenderFile(ctx.Template("views/catalogues_index.sbn"), new { primary }));
            Emit("Areas/Client/Views/Catalogues/_PickerPartial.cshtml", () => engine.RenderFile(ctx.Template("views/catalogues_picker_partial.sbn"), new { primary }));
            // A5: public (anonymous) catalogue browsing.
            Emit("Areas/Client/Views/Catalogues/Public.cshtml", () => engine.RenderFile(ctx.Template("views/catalogues_public.sbn"), new { primary }));
            Emit("Areas/Client/Views/Catalogues/PublicDetails.cshtml", () => engine.RenderFile(ctx.Template("views/catalogues_public_details.sbn"), new { primary }));
            // Task 19: rename own catalogue.
            Emit("Areas/Client/Views/Catalogues/EditName.cshtml", () => engine.RenderFile(ctx.Template("views/catalogues_edit_name.sbn"), new { primary }));
        }

        // A9 / Task 26 / Task 7: shared user-aggregation stack (view model + service + partial) reused by
        // the public profile (Client/Profiles/Public) AND the admin user detail (Admin/Users/Details).
        // Gated on posts||catalogues (same as the profile surface) - Program.cs registers the service
        // under the identical condition (kept in sync; drift = startup DI crash, see changelog 033).
        if (FeatureEnabled(spec, "posts") || FeatureEnabled(spec, "catalogues"))
        {
            Emit("Models/UserProfileViewModel.cs", () => engine.RenderFile(ctx.Template("models/user_profile_view_model.sbn")));
            Emit("Services/UserProfileService.cs", () => engine.RenderFile(ctx.Template("controllers/user_profile_service.sbn")));
            Emit("Views/Shared/_UserProfileContent.cshtml", () => engine.RenderFile(ctx.Template("views/user_profile_content_partial.sbn")));
            Emit("Areas/Client/Controllers/ProfilesController.cs", () => engine.RenderFile(ctx.Template("controllers/profiles_controller.sbn")));
            Emit("Areas/Client/Views/Profiles/Public.cshtml", () => engine.RenderFile(ctx.Template("views/profiles_public.sbn")));
            // ProfileHash.cs (item 17) is emitted earlier, from IdentityStage - _ManageNav.cshtml
            // references it and IdentityStage runs (and build-checks) before this stage does.
        }

        // Views: layout replaced wholesale, then each declared page by kind.
        if (spec.Views is { } views)
        {
            Emit(views.Layout.File, () => engine.RenderFile(ctx.Template("views/layout.sbn"),
                new { design_overlay_html = RenderDesignOverlay(ctx, engine) }));
            foreach (var page in views.Pages)
            {
                // Resolve the template kind eagerly (single-threaded) so an unknown-kind spec still
                // fails loud HERE, before any lane starts, exactly as it did when this was sequential.
                var template = page.Kind switch
                {
                    "landing" => "views/landing.sbn",
                    "contactFormStatic" => "views/contact.sbn",
                    "cardGrid" => "views/card_grid.sbn",
                    "adminTable" => "views/admin_table.sbn",
                    _ => throw new InvalidOperationException($"Unknown page kind '{page.Kind}' for {page.File}"),
                };
                var model = ModelForPage(spec, page);
                var owner = ControllerForPage(spec, page); // lets templates respect removedActions/sorting
                var has_rating = ModelHasRating(spec, model?.Name);
                Emit(page.File, () => engine.RenderFile(ctx.Template(template), new { page, model, owner, has_rating }));
            }
        }

        await ParallelEmit.RunAsync(work);

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("build after emit-controllers-views");
        ctx.Info("controllers+views emitted, build green");
    }

    /// <summary>Renders the A10 design overlay stylesheet to inline into the layout head.</summary>
    internal static string RenderDesignOverlay(StageContext ctx, TemplateEngine engine)
    {
        var path = ctx.Template("views/design_overlay.sbn");
        return File.Exists(path) ? engine.RenderFile(path) : "";
    }

    internal static bool FeatureEnabled(ProjectSpec spec, string name) =>
        spec.Features.TryGetValue(name, out var cfg)
        && cfg.TryGetProperty("enabled", out var en) && en.GetBoolean();

    internal static string CataloguesPrimaryModel(ProjectSpec spec) => FeaturePrimaryModel(spec, "catalogues");

    internal static string FeaturePrimaryModel(ProjectSpec spec, string feature) =>
        spec.Features.TryGetValue(feature, out var cfg)
            ? SpecExpandStage.ResolvePrimaryModel(spec, cfg)
            : spec.Models.First(x => x.Images is not null).Name;

    /// <summary>Whether card/index surfaces for this model should show the bulk average-rating stars.</summary>
    internal static bool ModelHasRating(ProjectSpec spec, string? modelName) =>
        modelName is not null && FeatureEnabled(spec, "rating") && FeaturePrimaryModel(spec, "rating") == modelName;

    internal static ControllerSpec? ControllerForPage(ProjectSpec spec, PageSpec page)
    {
        var parts = page.File.Split('/');
        var viewsIdx = Array.IndexOf(parts, "Views");
        if (viewsIdx < 0 || viewsIdx + 1 >= parts.Length) return null;
        return spec.Controllers.FirstOrDefault(c => c.Name == parts[viewsIdx + 1] + "Controller");
    }

    /// <summary>Card grid / admin table pages infer their model from the owning controller's route segment.</summary>
    internal static ModelSpec? ModelForPage(ProjectSpec spec, PageSpec page)
    {
        // e.g. Areas/Admin/Views/Cars/Index.cshtml -> controller "CarsController" -> model Car
        var parts = page.File.Split('/');
        var viewsIdx = Array.IndexOf(parts, "Views");
        if (viewsIdx < 0 || viewsIdx + 1 >= parts.Length) return null;
        var controllerName = parts[viewsIdx + 1] + "Controller";
        var controller = spec.Controllers.FirstOrDefault(c => c.Name == controllerName);
        return controller?.Model is { } m ? spec.Models.FirstOrDefault(x => x.Name == m) : null;
    }

    private static async Task ScaffoldControllerAsync(StageContext ctx, ControllerSpec c, CancellationToken ct)
    {
        var spec = ctx.Spec;
        var outDir = Path.GetDirectoryName(c.File)!.Replace('\\', '/');
        var result = await Proc.RunAsync("dotnet",
            $"aspnet-codegenerator controller -name {c.Name} -m {spec.Meta.RootNamespace}.Models.{c.Model} " +
            $"-dc {spec.Meta.RootNamespace}.{spec.Meta.ProjectName}Context --relativeFolderPath {outDir} --useDefaultLayout --referenceScriptLibraries",
            ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5));
        result.ThrowIfFailed($"scaffold controller {c.Name}");

        // The codegenerator has NO -area flag (verified on SDK 10.0.100) and always drops
        // CRUD views under root Views/{Controller}/, ignoring --relativeFolderPath. For an
        // area-scoped controller they must live under Areas/{Area}/Views/{Controller}/.
        if (c.Area is { } area)
        {
            var shortName = c.Name.Replace("Controller", "");
            var scaffoldedViewsDir = Path.Combine(ctx.ProjectDir, "Views", shortName);
            var targetViewsDir = Path.Combine(ctx.ProjectDir, "Areas", area, "Views", shortName);
            if (Directory.Exists(scaffoldedViewsDir))
            {
                Directory.CreateDirectory(targetViewsDir);
                foreach (var file in Directory.GetFiles(scaffoldedViewsDir))
                {
                    File.Copy(file, Path.Combine(targetViewsDir, Path.GetFileName(file)), overwrite: true);
                    File.Delete(file);
                }
                Directory.Delete(scaffoldedViewsDir);
            }
        }
    }

    /// <summary>Scaffolder never adds [Area(...)] or the area namespace - inject both (verified on SDK 10.0.100).</summary>
    internal static void InjectAreaAttribute(StageContext ctx, ControllerSpec c, string area)
    {
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);
        if (source.Contains("[Area(")) return; // idempotent rerun

        source = source.Replace($"namespace {ctx.Spec.Meta.RootNamespace}.Controllers",
                                 $"namespace {ctx.Spec.Meta.RootNamespace}.Areas.{area}.Controllers");
        if (!source.Contains("using Microsoft.AspNetCore.Mvc;"))
            source = "using Microsoft.AspNetCore.Mvc;\n" + source;

        var rx = new Regex($@"public\s+class\s+{c.Name}\b");
        var m = rx.Match(source);
        if (!m.Success)
            throw new InvalidOperationException($"InjectAreaAttribute: class declaration not found in {c.File}.");
        source = source.Insert(m.Index, $"[Area(\"{area}\")]\n");
        File.WriteAllText(path, source);
        ctx.Info($"injected [Area(\"{area}\")] into {c.Name}");
    }

    /// <summary>True when <paramref name="area"/> is mapped (areas[].forRole) to a role flagged isAdmin.</summary>
    internal static bool IsAdminArea(ProjectSpec spec, string? area)
    {
        if (area is null) return false;
        var role = spec.Areas.FirstOrDefault(a => a.Name == area)?.ForRole;
        return role is not null && (spec.Identity?.Roles.Any(r => r.IsAdmin && r.Name == role) ?? false);
    }

    /// <summary>
    /// Changelog 141: turn areas[].forRole into an authorization fact. The scaffolder emits no
    /// [Authorize], and before this nothing added one, so every scaffolded admin CRUD controller
    /// was open to anonymous users.
    ///  - area mapped to an admin role: [Authorize(Roles = "&lt;role&gt;")] on the class;
    ///  - any other role-mapped area: [Authorize] on the class and [AllowAnonymous] on the read
    ///    actions (Index, Details), so card grids and detail pages stay public while
    ///    Create/Edit/Delete need a login (ownership deviations then decide whose rows).
    /// An area with no areas[] entry is left alone. Idempotent: a controller that already
    /// carries any [Authorize is skipped.
    /// </summary>
    internal static void InjectAreaAuthorization(StageContext ctx, ControllerSpec c)
    {
        if (c.Area is null) return;
        var role = ctx.Spec.Areas.FirstOrDefault(a => a.Name == c.Area)?.ForRole;
        if (string.IsNullOrEmpty(role)) return;

        var path = Path.Combine(ctx.ProjectDir, c.File);
        if (!File.Exists(path)) return;
        var source = File.ReadAllText(path);
        if (source.Contains("[Authorize")) return; // idempotent rerun / hand-authored template

        var classRx = new Regex($@"public\s+class\s+{c.Name}\b");
        var cm = classRx.Match(source);
        if (!cm.Success)
            throw new InvalidOperationException($"InjectAreaAuthorization: class declaration not found in {c.File}.");

        var admin = IsAdminArea(ctx.Spec, c.Area);
        if (!admin)
        {
            // Insert from the end backwards so earlier indexes stay valid.
            var reads = new Regex(@"public\s+(async\s+Task<IActionResult>|IActionResult)\s+(Index|Details)\s*\(")
                .Matches(source).Cast<Match>().Where(m => m.Index > cm.Index).OrderByDescending(m => m.Index);
            foreach (var m in reads)
                source = source.Insert(m.Index, "[AllowAnonymous]\n        ");
        }
        source = source.Insert(cm.Index, admin ? $"[Authorize(Roles = \"{role}\")]\n    " : "[Authorize]\n    ");
        if (!source.Contains("using Microsoft.AspNetCore.Authorization;"))
            source = "using Microsoft.AspNetCore.Authorization;\n" + source;

        File.WriteAllText(path, source);
        ctx.Info(admin
            ? $"injected [Authorize(Roles = \"{role}\")] into {c.Name}"
            : $"injected [Authorize] (+ [AllowAnonymous] on Index/Details) into {c.Name}");
    }

    /// <summary>
    /// B2: rewrite scaffolder EN residue ("Back to List", "Edit", h1 headers, submit values)
    /// to Bulgarian using templates/bg-term-map.json, and restyle bare action links as
    /// buttons. Runs over every view of the scaffolded controller (all areas). Idempotent:
    /// replacements are exact-match, absent after first pass.
    /// </summary>
    internal static void ApplyBgResiduePass(StageContext ctx, ControllerSpec c)
    {
        // Retrofit-translates the scaffolder's own English residue into Bulgarian - only
        // meaningful for a Bulgarian project. For an English one the scaffolder's native output
        // is already correct, so there's nothing to translate away from.
        if (!ctx.Spec.Meta.IsBulgarian) return;
        var mapPath = Path.Combine(ctx.GenesisRoot, "templates", "bg-term-map.json");
        if (!File.Exists(mapPath)) return;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(mapPath));
        var exact = doc.RootElement.GetProperty("exact").EnumerateObject()
            .Select(p => (p.Name, p.Value.GetString()!)).ToList();
        var buttonize = doc.RootElement.TryGetProperty("buttonize", out var b)
            ? b.EnumerateArray().Select(e => (e.GetProperty("match").GetString()!, e.GetProperty("class").GetString()!)).ToList()
            : [];

        foreach (var file in ViewFilesOf(ctx, c))
        {
            var src = File.ReadAllText(file);
            var before = src;
            foreach (var (from, to) in exact) src = src.Replace(from, to);
            foreach (var (match, cls) in buttonize)
                // only add a class where the anchor has none - leave already-styled links alone
                src = src.Replace(match + " asp-route", match + $" class=\"{cls}\" asp-route")
                         .Replace(match + ">", match + $" class=\"{cls}\">");
            if (src != before) { File.WriteAllText(file, src); ctx.Info($"BG residue pass: {Path.GetFileName(file)}"); }
        }
    }

    /// <summary>
    /// A2: system-owned fields (CreatedAt/UpdatedAt/DeletedAt/IsDeleted) must never be
    /// user-editable. Removes their form-groups from Create/Edit, their [Bind] entries in
    /// the controller, and their display rows from CLIENT-area Details/Delete (admin keeps
    /// read-only display). SaveChanges override owns them entirely.
    /// </summary>
    /// <summary>Model has a raw ownership "UserId" scalar (not a relation nav) - auto-stamped
    /// from the logged-in user rather than left as a hand-typed form field (item 18).</summary>
    internal static PropertySpec? OwnerProperty(ModelSpec model) =>
        model.Properties.FirstOrDefault(p => p.Name == "UserId" && p.Type == "string" && !p.ForeignKey);

    /// <summary>Model has a "Quantity" int alongside a "Status" enum - Status is derived from
    /// Quantity server-side rather than hand-set (item 19). Returns the Status property, or null.</summary>
    internal static PropertySpec? DerivedStatusProperty(ModelSpec model) =>
        model.Properties.Any(p => p.Name == "Quantity" && p.Type == "int")
            ? model.Properties.FirstOrDefault(p => p.Name == "Status" && p.Type == "enum")
            : null;

    /// <summary>Model has a single raw stored-path string field (e.g. Post.ImagePath) - the same
    /// "ends with Path" heuristic admin_table.sbn already uses to detect an image column. Editable
    /// only via upload/remove, never as a hand-typed URL (item 11).</summary>
    internal static PropertySpec? LegacyImagePathProperty(ModelSpec model) =>
        model.Properties.FirstOrDefault(p => p.Type == "string" && p.Name.EndsWith("Path"));

    internal static void StripSystemFieldsFromViews(StageContext ctx, ControllerSpec c)
    {
        List<string> fieldList = ["CreatedAt", "UpdatedAt", "DeletedAt", "IsDeleted"];
        var model = c.Model is { } mn ? ctx.Spec.Models.FirstOrDefault(m => m.Name == mn) : null;
        if (model is not null && OwnerProperty(model) is not null) fieldList.Add("UserId");
        // Status (item 19): never editable via a form, but - unlike UserId/audit fields - it
        // should STAY visible on Details/Index (both areas), just not hand-settable. Stripped
        // from [Bind] + Create/Edit forms only, never from the display-stripping loop below.
        var derivedStatus = model is not null ? DerivedStatusProperty(model) : null;
        if (derivedStatus is not null) fieldList.Add("Status");
        // Legacy image-path field (item 11): never a hand-typed URL on Create/Edit, but stays
        // visible on Details/Index as an actual <img> (admin_table.sbn already renders it there).
        var legacyImage = model is not null ? LegacyImagePathProperty(model) : null;
        if (legacyImage is not null) fieldList.Add(legacyImage.Name);
        var fields = fieldList.ToArray();
        List<string> formOnlyList = [];
        if (derivedStatus is not null) formOnlyList.Add("Status");
        if (legacyImage is not null) formOnlyList.Add(legacyImage.Name);
        var formOnlyFields = formOnlyList.ToArray();

        // controller [Bind("A,B,CreatedAt,...")] lists: rewrite each list without the
        // system entries (parse -> filter -> rejoin; never blind string surgery).
        var ctrlPath = Path.Combine(ctx.ProjectDir, c.File);
        if (File.Exists(ctrlPath))
        {
            var ctrlSrc = File.ReadAllText(ctrlPath);
            var beforeCtrl = ctrlSrc;
            ctrlSrc = System.Text.RegularExpressions.Regex.Replace(ctrlSrc,
                @"\[Bind\(""([^""]*)""\)\]",
                m =>
                {
                    var kept = m.Groups[1].Value.Split(',')
                        .Select(x => x.Trim())
                        .Where(x => !fields.Contains(x));
                    return $"[Bind(\"{string.Join(",", kept)}\")]";
                });
            if (ctrlSrc != beforeCtrl) { File.WriteAllText(ctrlPath, ctrlSrc); ctx.Info($"system fields removed from [Bind] in {c.Name}"); }
        }

        var isClient = string.Equals(c.Area, "Client", StringComparison.OrdinalIgnoreCase);
        foreach (var file in ViewFilesOf(ctx, c))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var isForm = name is "Create" or "Edit";
            var isDisplay = name is "Details" or "Delete" or "Index";
            if (!isForm && !(isClient && isDisplay)) continue; // admin keeps read-only display

            var src = File.ReadAllText(file);
            var before = src;
            var fieldsHere = isForm ? fields : fields.Except(formOnlyFields).ToArray();
            foreach (var f in fieldsHere)
            {
                // form group: <div class="form-group"> ... asp-for="F" ... </div>
                src = System.Text.RegularExpressions.Regex.Replace(src,
                    $@"<div class=""form-group"">\s*(?:(?!</div>).)*?\b{f}\b(?:(?!</div>).)*?</div>\s*",
                    "", System.Text.RegularExpressions.RegexOptions.Singleline);
                // display pair: <dt ...>...F...</dt> <dd ...>...F...</dd>
                src = System.Text.RegularExpressions.Regex.Replace(src,
                    $@"<dt[^>]*>\s*(?:(?!</dt>).)*?\b{f}\b(?:(?!</dt>).)*?</dt>\s*<dd[^>]*>(?:(?!</dd>).)*?</dd>\s*",
                    "", System.Text.RegularExpressions.RegexOptions.Singleline);
                // table column in Index: header + per-row cells are positional - skip tables (low risk, admin only)
            }
            if (src != before) { File.WriteAllText(file, src); ctx.Info($"system fields stripped: {Path.GetFileName(file)} ({c.Name})"); }
        }
    }

    /// <summary>
    /// A3/A14: the rich product Details view expects ViewData["Reviews"], ["AverageRating"],
    /// ["ReviewAuthors"], ["CurrentUserId"]. Inject that load into the scaffolded Details
    /// action of the rating feature's primary-model CLIENT controller, plus the image Include.
    /// </summary>
    internal static void InjectReviewLoadingIntoDetails(StageContext ctx, ControllerSpec c)
    {
        if (!FeatureEnabled(ctx.Spec, "rating")) return;
        if (!string.Equals(c.Area, "Client", StringComparison.OrdinalIgnoreCase)) return;
        if (c.Model is null || c.Model != FeaturePrimaryModel(ctx.Spec, "rating")) return;

        var path = Path.Combine(ctx.ProjectDir, c.File);
        if (!File.Exists(path)) return;
        var src = File.ReadAllText(path);
        if (src.Contains("ViewData[\"Reviews\"]")) return; // idempotent

        // Ensure the controller has UserManager injected; scaffolded ones don't.
        if (!src.Contains("UserManager<ApplicationUser>"))
        {
            src = src.Replace($"using {ctx.Spec.Meta.RootNamespace}.Models;",
                $"using {ctx.Spec.Meta.RootNamespace}.Models;\nusing Microsoft.AspNetCore.Identity;");
            // add field + ctor param - scaffolded ctor takes only the context
            var ctorRx = new Regex($@"public {c.Name}\(([^)]*){ctx.Spec.Meta.ProjectName}Context (\w+)\)");
            var m = ctorRx.Match(src);
            if (m.Success)
            {
                var ctxParam = m.Groups[2].Value;
                src = src.Replace(m.Value,
                    $"private readonly UserManager<ApplicationUser> _userManager;\n" +
                    $"    public {c.Name}({m.Groups[1].Value}{ctx.Spec.Meta.ProjectName}Context {ctxParam}, UserManager<ApplicationUser> userManager)");
                // assign in body
                var bodyRx = new Regex($@"(public {c.Name}\([^)]*\)\s*\{{)");
                src = bodyRx.Replace(src, "$1\n        _userManager = userManager;", 1);
            }
        }

        // Replace the Details action body's data load. Scaffolded shape:
        //   var x = await _context.Products.FirstOrDefaultAsync(m => m.Id == id);
        var loadRx = new Regex(
            @"var (\w+) = await _context\." + c.Model + @"s(\s*\.Include\([^;]*?)?\s*\.FirstOrDefaultAsync\(([^;]+?)\);",
            RegexOptions.Singleline);
        var lm = loadRx.Match(src);
        if (lm.Success)
        {
            var varName = lm.Groups[1].Value;
            var pred = lm.Groups[3].Value;
            var replacement =
                $"var {varName} = await _context.{c.Model}s\n" +
                $"            .Include(x => x.Images)\n" +
                $"            .Include(x => x.Reviews)\n" +
                $"            .FirstOrDefaultAsync({pred});\n" +
                $"        if ({varName} is not null)\n" +
                $"        {{\n" +
                $"            var reviews = {varName}.Reviews.Where(r => !r.IsDeleted).OrderByDescending(r => r.CreatedAt).ToList();\n" +
                $"            ViewData[\"Reviews\"] = reviews;\n" +
                $"            ViewData[\"AverageRating\"] = reviews.Count > 0 ? reviews.Average(r => r.Stars) : (double?)null;\n" +
                $"            ViewData[\"CurrentUserId\"] = _userManager.GetUserId(User);\n" +
                $"            var authorIds = reviews.Select(r => r.AuthorUserId).Distinct().ToList();\n" +
                $"            ViewData[\"ReviewAuthors\"] = await _userManager.Users\n" +
                $"                .Where(u => authorIds.Contains(u.Id))\n" +
                $"                .ToDictionaryAsync(u => u.Id, u => u.FirstName ?? u.Email ?? \"Потребител\");\n" +
                $"            var seller = await _userManager.FindByIdAsync({varName}.UserId);\n" +
                $"            ViewData[\"SellerName\"] = seller?.FirstName is {{ Length: > 0 }} sellerFirstName ? sellerFirstName : seller?.UserName;\n" +
                $"        }}";
            src = loadRx.Replace(src, replacement, 1);
            File.WriteAllText(path, src);
            ctx.Info($"injected review loading into {c.Name}.Details");
        }
    }

    /// <summary>
    /// Item 18: models with a raw ownership "UserId" scalar (see OwnerProperty) get it
    /// auto-stamped from the logged-in user on Create - StripSystemFieldsFromViews already
    /// removed it from [Bind] and the form, so nothing sets it otherwise. Edit needs the
    /// opposite care: since UserId isn't bound from the form anymore, the incoming object's
    /// UserId is default/null - _context.Update(...) would blank the real value in the DB
    /// unless re-stamped from the existing row first (same "don't blank an omitted field"
    /// concern already solved by hand in users_controller.sbn's Edit action).
    /// </summary>
    internal static void InjectOwnerAutoAssign(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        if (model is null || OwnerProperty(model) is null) return;

        var path = Path.Combine(ctx.ProjectDir, c.File);
        if (!File.Exists(path)) return;
        var src = File.ReadAllText(path);
        if (src.Contains("// owner auto-assign")) return; // idempotent rerun

        if (!src.Contains("UserManager<ApplicationUser>"))
        {
            if (!src.Contains("using Microsoft.AspNetCore.Identity;"))
                src = src.Replace($"using {ctx.Spec.Meta.RootNamespace}.Models;",
                    $"using {ctx.Spec.Meta.RootNamespace}.Models;\nusing Microsoft.AspNetCore.Identity;");
            var ctorRx = new Regex($@"public {c.Name}\(([^)]*){ctx.Spec.Meta.ProjectName}Context (\w+)\)");
            var cm = ctorRx.Match(src);
            if (cm.Success)
            {
                var ctxParam = cm.Groups[2].Value;
                src = src.Replace(cm.Value,
                    $"private readonly UserManager<ApplicationUser> _userManager;\n" +
                    $"    public {c.Name}({cm.Groups[1].Value}{ctx.Spec.Meta.ProjectName}Context {ctxParam}, UserManager<ApplicationUser> userManager)");
                var bodyRx = new Regex($@"(public {c.Name}\([^)]*\)\s*\{{)");
                src = bodyRx.Replace(src, "$1\n        _userManager = userManager;", 1);
            }
        }

        // Create POST: stamp UserId right after entering the ModelState.IsValid branch.
        // The [Bind("...")] attribute in the parameter list has its own closing paren before the
        // method's - a naive [^)]* stops there, so this needs a lazy dot-all match that can
        // backtrack past it to find the actual method-signature close.
        var createRx = new Regex(
            @"(public async Task<IActionResult> Create\([\s\S]*?\)\s*\{\s*if \(ModelState\.IsValid\)\s*\{\s*)(_context\.Add\((\w+)\);)");
        var crm = createRx.Match(src);
        if (crm.Success)
        {
            var varName = crm.Groups[3].Value;
            src = createRx.Replace(src,
                $"{crm.Groups[1].Value}// owner auto-assign\n                {varName}.UserId = _userManager.GetUserId(User)!;\n                {crm.Groups[2].Value}",
                1);
        }

        // Edit POST: re-stamp the original UserId before Update - it was stripped from [Bind],
        // so the freshly-bound object's UserId is null/default and must not overwrite the real one.
        // On a Client-area controller, also Forbid non-owners (admin bypass) - same idiom already
        // hand-written in posts_controller.sbn's Delete action.
        var isClient = string.Equals(c.Area, "Client", StringComparison.OrdinalIgnoreCase);
        var adminRole = ctx.Spec.Identity?.Roles.FirstOrDefault(r => r.IsAdmin)?.Name;
        var editRx = new Regex(
            @"(public async Task<IActionResult> Edit\(int id, \[Bind\(""[^""]*""\)\] " + c.Model + @" (\w+)\)[\s\S]*?try\s*\{\s*)(_context\.Update\(\2\);)");
        var edm = editRx.Match(src);
        if (edm.Success)
        {
            var varName = edm.Groups[2].Value;
            var guard = isClient && adminRole is not null
                ? $"var {varName}Owner = (await _context.{c.Model}s.AsNoTracking().FirstAsync(x => x.Id == {varName}.Id)).UserId;\n" +
                  $"                    if ({varName}Owner != _userManager.GetUserId(User) && !User.IsInRole(\"{adminRole}\")) return Forbid();\n" +
                  $"                    {varName}.UserId = {varName}Owner;\n"
                : $"{varName}.UserId = (await _context.{c.Model}s.AsNoTracking().FirstAsync(x => x.Id == {varName}.Id)).UserId;\n";
            src = editRx.Replace(src,
                $"{edm.Groups[1].Value}// owner auto-assign\n                    {guard}                    {edm.Groups[3].Value}",
                1);
        }

        // DeleteConfirmed (Client area only): same Forbid guard before the delete/soft-delete.
        if (isClient && adminRole is not null)
        {
            var delRx = new Regex(
                @"(public async Task<IActionResult> DeleteConfirmed\(int id\)\s*\{\s*)(var (\w+) = await _context\." + c.Model + @"s\.FindAsync\(id\);[\s\S]*?)(\r?\n\s*)(if \(\3 != null\)\s*\{)",
                RegexOptions.Singleline);
            var dm = delRx.Match(src);
            if (dm.Success)
            {
                var varName = dm.Groups[3].Value;
                src = delRx.Replace(src,
                    $"{dm.Groups[1].Value}{dm.Groups[2].Value}{dm.Groups[4].Value}if ({varName} is not null && {varName}.UserId != _userManager.GetUserId(User) && !User.IsInRole(\"{adminRole}\")) return Forbid();\n            {dm.Groups[5].Value}",
                    1);
            }
        }

        if (crm.Success || edm.Success)
        {
            File.WriteAllText(path, src);
            ctx.Info($"owner auto-assign wired into {c.Name}");
        }
    }

    /// <summary>
    /// Item 19: Status is derived from Quantity (0 -> the enum's LAST value, e.g. "sold out";
    /// &gt;0 -> the FIRST value, e.g. "in stock") instead of hand-set - same first/last convention
    /// cart_controller.sbn already uses for the purchase-flow status flip. Computed on Create+Edit,
    /// right after StripSystemFieldsFromViews has already removed Status from [Bind]/the form.
    /// </summary>
    internal static void InjectQuantityDerivedStatus(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        if (model is null || DerivedStatusProperty(model) is not { } statusProp || statusProp.EnumValues.Count == 0) return;

        var path = Path.Combine(ctx.ProjectDir, c.File);
        if (!File.Exists(path)) return;
        var src = File.ReadAllText(path);
        if (src.Contains("// derived status")) return; // idempotent rerun

        var inStock = statusProp.EnumValues[0].Value;
        var outOfStock = statusProp.EnumValues[^1].Value;
        string DeriveLine(string varName) =>
            $"// derived status\n                {varName}.Status = {varName}.Quantity > 0 ? {c.Model}Status.{inStock} : {c.Model}Status.{outOfStock};\n                ";

        var createRx = new Regex(
            @"(public async Task<IActionResult> Create\([\s\S]*?\)\s*\{\s*if \(ModelState\.IsValid\)\s*\{\s*)(?:// owner auto-assign\s*\n\s*(\w+)\.UserId = [^\n]+\n\s*)?(_context\.Add\((\w+)\);)");
        var crm = createRx.Match(src);
        if (crm.Success)
        {
            var varName = crm.Groups[4].Value;
            var ownerLine = crm.Groups[2].Success ? $"// owner auto-assign\n                {crm.Groups[2].Value}.UserId = _userManager.GetUserId(User)!;\n                " : "";
            src = createRx.Replace(src, $"{crm.Groups[1].Value}{ownerLine}{DeriveLine(varName)}{crm.Groups[3].Value}", 1);
        }

        var editRx = new Regex(
            @"(public async Task<IActionResult> Edit\(int id, \[Bind\(""[^""]*""\)\] " + c.Model + @" (\w+)\)[\s\S]*?try\s*\{\s*)([\s\S]*?)(_context\.Update\(\2\);)");
        var edm = editRx.Match(src);
        if (edm.Success)
        {
            var varName = edm.Groups[2].Value;
            src = editRx.Replace(src, $"{edm.Groups[1].Value}{edm.Groups[3].Value}{DeriveLine(varName)}{edm.Groups[4].Value}", 1);
        }

        if (crm.Success || edm.Success)
        {
            File.WriteAllText(path, src);
            ctx.Info($"derived status wired into {c.Name}");
        }
    }

    /// <summary>
    /// Item 11: a legacy single-image-path field (e.g. Post.ImagePath) is never hand-typed as a
    /// URL - StripSystemFieldsFromViews already removed it from [Bind] and the form entirely.
    /// This adds a "remove image" checkbox to Edit only (Create has no existing image to remove)
    /// and preserves the existing value unless the checkbox was ticked - same
    /// preserve-unless-told-otherwise idiom as InjectOwnerAutoAssign's Edit handling.
    /// </summary>
    internal static void InjectImageRemoveOption(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        if (model is null || LegacyImagePathProperty(model) is not { } imgProp) return;

        var ctrlPath = Path.Combine(ctx.ProjectDir, c.File);
        if (File.Exists(ctrlPath))
        {
            var src = File.ReadAllText(ctrlPath);
            if (!src.Contains("// image remove option"))
            {
                var editRx = new Regex(
                    @"(public async Task<IActionResult> Edit\(int id, \[Bind\(""[^""]*""\)\] " + c.Model + @" )(\w+)(\)[\s\S]*?try\s*\{\s*)([\s\S]*?)(_context\.Update\(\2\);)");
                var em = editRx.Match(src);
                if (em.Success)
                {
                    var varName = em.Groups[2].Value;
                    var line = $"// image remove option\n                    {varName}.{imgProp.Name} = removeImage ? null : (await _context.{c.Model}s.AsNoTracking().FirstAsync(x => x.Id == {varName}.Id)).{imgProp.Name};\n                    ";
                    src = editRx.Replace(src,
                        $"{em.Groups[1].Value}{varName}{em.Groups[3].Value}{em.Groups[4].Value}{line}{em.Groups[5].Value}", 1);
                    // add the bool parameter to the method signature (kept separate from [Bind] - a plain form field, not model-bound).
                    src = new Regex(@"(public async Task<IActionResult> Edit\(int id, \[Bind\(""[^""]*""\)\] " + c.Model + @" \w+)(\))")
                        .Replace(src, $"$1, bool removeImage = false$2", 1);
                    File.WriteAllText(ctrlPath, src);
                    ctx.Info($"image remove option wired into {c.Name}");
                }
            }
        }

        var editViewPath = Path.Combine(ctx.ProjectDir, "Areas", c.Area ?? "", "Views", c.Name.Replace("Controller", ""), "Edit.cshtml");
        if (File.Exists(editViewPath))
        {
            var vsrc = File.ReadAllText(editViewPath);
            if (!vsrc.Contains("removeImage"))
            {
                var checkbox =
                    $"            <div class=\"form-group form-check\">\n" +
                    $"                <label class=\"form-check-label\">\n" +
                    $"                    <input class=\"form-check-input\" type=\"checkbox\" name=\"removeImage\" value=\"true\" /> Премахни текущата снимка\n" +
                    $"                </label>\n" +
                    $"            </div>\n";
                var beforeSubmit = new Regex(@"(\s*)(<input type=""submit""[^>]*/>)");
                var vNew = beforeSubmit.Replace(vsrc, m => $"\n{checkbox}{m.Groups[1].Value}{m.Groups[2].Value}", 1);
                if (vNew != vsrc) { File.WriteAllText(editViewPath, vNew); ctx.Info($"remove-image checkbox added to {c.Name}/Edit.cshtml"); }
            }
        }
    }

    /// <summary>
    /// Gallery-style images (model.Images, e.g. Product - multiple photos, uploaded/managed via
    /// the separate {Model}ImagesController the "Снимки" button on Index links to, Task 14) have
    /// no upload control on the scaffolded Create form - there's no entity Id yet for the images
    /// to attach to. Without a hint, that reads as a missing feature rather than a two-step flow.
    /// Injects a short note above the submit button pointing at the post-save step.
    /// </summary>
    internal static void InjectImagesCreateHint(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        if (model?.Images is null) return;

        var createViewPath = Path.Combine(ctx.ProjectDir, "Areas", c.Area ?? "", "Views", c.Name.Replace("Controller", ""), "Create.cshtml");
        if (!File.Exists(createViewPath)) return;

        var vsrc = File.ReadAllText(createViewPath);
        if (vsrc.Contains("images-create-hint")) return; // idempotent re-run guard

        var hint =
            "            <div class=\"alert alert-info images-create-hint\" role=\"alert\">\n" +
            "                Снимки се добавят след запазването, от бутон „Снимки\" в списъка.\n" +
            "            </div>\n";
        var beforeSubmit = new Regex(@"(\s*)(<input type=""submit""[^>]*/>)");
        var vNew = beforeSubmit.Replace(vsrc, m => $"\n{hint}{m.Groups[1].Value}{m.Groups[2].Value}", 1);
        if (vNew != vsrc) { File.WriteAllText(createViewPath, vNew); ctx.Info($"images-create hint added to {c.Name}/Create.cshtml"); }
    }

    /// <summary>
    /// Post's admin Details page didn't surface its comments at all - narrowly gated to the Post
    /// admin controller specifically (same style as InjectReviewLoadingIntoDetails's feature+model
    /// gate) rather than a generic "show every child collection" mechanism, since only this one
    /// case was actually requested.
    /// </summary>
    internal static void InjectPostCommentsIntoAdminDetails(StageContext ctx, ControllerSpec c)
    {
        if (!string.Equals(c.Area, "Admin", StringComparison.OrdinalIgnoreCase) || c.Model != "Post") return;

        var path = Path.Combine(ctx.ProjectDir, c.File);
        if (!File.Exists(path)) return;
        var src = File.ReadAllText(path);
        if (src.Contains("// admin post comments")) return;

        var loadRx = new Regex(
            @"var (\w+) = await _context\.Posts\s*\.FirstOrDefaultAsync\(([^;]+?)\);", RegexOptions.Singleline);
        var lm = loadRx.Match(src);
        if (!lm.Success) return;
        var varName = lm.Groups[1].Value;
        var pred = lm.Groups[2].Value;
        src = loadRx.Replace(src,
            $"// admin post comments\n            var {varName} = await _context.Posts\n" +
            $"                .Include(x => x.Comments)\n" +
            $"                .FirstOrDefaultAsync({pred});", 1);
        File.WriteAllText(path, src);
        ctx.Info($"post comments included in {c.Name}.Details");

        var detailsViewPath = Path.Combine(ctx.ProjectDir, "Areas", "Admin", "Views", "Posts", "Details.cshtml");
        if (!File.Exists(detailsViewPath)) return;
        var vsrc = File.ReadAllText(detailsViewPath);
        if (vsrc.Contains("Model.Comments")) return;
        var commentsBlock =
            "\n<h4 class=\"mt-4\">Коментари (@Model.Comments.Count)</h4>\n" +
            "<ul class=\"list-group mb-3\">\n" +
            "    @foreach (var c in Model.Comments.OrderBy(x => x.CreatedAt))\n" +
            "    {\n" +
            "        <li class=\"list-group-item\">\n" +
            "            <div>@c.Content</div>\n" +
            "            <small class=\"text-muted\">@c.CreatedAt.ToString(\"dd.MM.yyyy HH:mm\")</small>\n" +
            "        </li>\n" +
            "    }\n" +
            "</ul>\n";
        // Insert right before the closing </div> that wraps the scaffolded <dl>, or fall back to appending before the edit/back links.
        var anchorRx = new Regex(@"(\r?\n)(<div>\s*\r?\n\s*<a asp-action=""Edit"")");
        var vNew = anchorRx.Replace(vsrc, m => $"{commentsBlock}{m.Groups[1].Value}{m.Groups[2].Value}", 1);
        if (vNew != vsrc) File.WriteAllText(detailsViewPath, vNew);
    }

    private static IEnumerable<string> ViewFilesOf(StageContext ctx, ControllerSpec c)
    {
        var shortName = c.Name.Replace("Controller", "");
        var dir = c.Area is { } a
            ? Path.Combine(ctx.ProjectDir, "Areas", a, "Views", shortName)
            : Path.Combine(ctx.ProjectDir, "Views", shortName);
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*.cshtml") : [];
    }

    /// <summary>
    /// conventions.deleteBehavior=soft (or model.softDelete): rewrite the scaffolded
    /// DeleteConfirmed body from physical Remove to flag-set. The global query filter
    /// in the DbContext hides flagged rows everywhere else automatically.
    /// </summary>
    internal static void ApplySoftDelete(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        var soft = (model?.SoftDelete ?? false) || ctx.Spec.Conventions.DeleteBehavior == "soft";
        if (!soft) return;
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);
        if (source.Contains("IsDeleted = true")) return; // idempotent

        var before = source;
        source = Regex.Replace(source,
            $@"_context\.{c.Model}s\.Remove\((\w+)\);",
            "$1.IsDeleted = true; $1.DeletedAt = DateTime.UtcNow; // меко изтриване (глобалният филтър го скрива)");
        if (source != before)
        {
            File.WriteAllText(path, source);
            ctx.Info($"soft-delete transform applied to {c.Name}");
        }
    }

    /// <summary>
    /// Scaffolded Index() queries have no .Include() - cards/tables would show empty images.
    /// Injects .Include(x => x.Images) into the Index query when the model has images.
    /// </summary>
    internal static void InjectImageInclude(StageContext ctx, ControllerSpec c)
    {
        if (c.Model is null) return;
        var model = ctx.Spec.Models.FirstOrDefault(m => m.Name == c.Model);
        if (model?.Images is null) return;
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);
        if (source.Contains(".Include(x => x.Images)")) return; // idempotent

        var before = source;
        source = Regex.Replace(source,
            $@"_context\.{c.Model}s(\s*\.To(List|Array)Async\(\))",
            $"_context.{c.Model}s.Include(x => x.Images)$1");
        if (source != before)
        {
            if (!source.Contains("using Microsoft.EntityFrameworkCore;"))
                source = "using Microsoft.EntityFrameworkCore;\n" + source;
            File.WriteAllText(path, source);
            ctx.Info($"injected image Include into {c.Name}.Index");
        }
    }

    /// <summary>
    /// Declarative deviations over scaffolded output. Each transform throws if its
    /// anchor pattern is missing - loud beats wrong.
    /// </summary>
    internal static void ApplyDeviations(StageContext ctx, ControllerSpec c, TemplateEngine engine)
    {
        var path = Path.Combine(ctx.ProjectDir, c.File);
        var source = File.ReadAllText(path);

        foreach (var removed in c.RemovedActions)
        {
            // Method start (tolerant: optional GET/POST comment, attributes, CRLF, async or not),
            // then brace-walk to the matching close - immune to nested braces and formatting.
            var startRx = new Regex(
                $@"([ \t]*//\s*(GET|POST):[^\r\n]*\r?\n)?(\s*\[[^\]]+\]\r?\n)*\s*public\s+(async\s+)?Task<IActionResult>\s+{removed}\s*\(",
                RegexOptions.Singleline);
            var mm = startRx.Match(source);
            if (!mm.Success)
            {
                // Idempotent rerun: already removed by a prior (partially failed) run.
                ctx.Info($"deviation removedActions:{removed} - not present in {c.File} (already applied?), skipping");
                continue;
            }
            var braceStart = source.IndexOf('{', mm.Index + mm.Length);
            var depth = 0; var endIdx = braceStart;
            for (var i = braceStart; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) { endIdx = i; break; }
            }
            if (depth != 0)
                throw new InvalidOperationException($"Deviation removedActions:{removed} - unbalanced braces in {c.File}.");
            source = source.Remove(mm.Index, endIdx - mm.Index + 1);

            var viewPath = Path.Combine(ctx.ProjectDir, Path.GetDirectoryName(c.File)!.Replace("Controllers", "Views"),
                c.Name.Replace("Controller", ""), removed + ".cshtml");
            if (File.Exists(viewPath)) File.Delete(viewPath);
        }

        foreach (var red in c.Redirects)
        {
            var (targetController, targetAction) = (red.To.Split('.')[0].Replace("Controller", ""), red.To.Split('.')[1]);
            var rx = new Regex(
                $@"(public\s+(async\s+)?Task<IActionResult>\s+{red.After}\b(?:(?!public\s+(async\s+)?Task).)*?)return\s+RedirectToAction\(nameof\(Index\)\);",
                RegexOptions.Singleline);
            var before = source;
            source = rx.Replace(source,
                $"$1return RedirectToAction(\"{targetAction}\", \"{targetController}\");", 1);
            if (source == before)
            {
                if (source.Contains($"RedirectToAction(\"{targetAction}\", \"{targetController}\")"))
                { ctx.Info($"deviation redirect after {red.After} - already applied, skipping"); continue; }
                throw new InvalidOperationException($"Deviation redirect after {red.After} - anchor not found in {c.File}.");
            }
        }

        if (c.Sorting is { Enabled: true, ImplementedIn: "controller" } sSort)
        {
            // Idempotency marker: ViewData["TotalPages"] only appears in the sorting+pagination
            // Index body we emit - independent of the exact parameter list, which has changed
            // shape before (adding search/page) and may again.
            if (source.Contains("ViewData[\"TotalPages\"]"))
            {
                ctx.Info("deviation sorting - already applied, skipping");
            }
            else
            {
                var snippet = engine.RenderFile(ctx.Template("controllers/sorting_index_body.sbn"),
                    new { controller = c, has_rating = ModelHasRating(ctx.Spec, c.Model) });
                // Anchor on ANY Index(...) action - scaffolded (no params) or a previous
                // differently-shaped generated one - not just the parameterless scaffolded shape.
                var rx = new Regex(
                    @"public\s+(async\s+)?Task<IActionResult>\s+Index\([^)]*\)\s*\{",
                    RegexOptions.Singleline);
                var m = rx.Match(source);
                if (!m.Success)
                    throw new InvalidOperationException($"Deviation sorting - Index() method not found in {c.File}.");
                var braceStart = source.IndexOf('{', m.Index + m.Length - 1);
                var depth = 0; var endIdx = braceStart;
                for (var i = braceStart; i < source.Length; i++)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}' && --depth == 0) { endIdx = i; break; }
                }
                if (depth != 0)
                    throw new InvalidOperationException($"Deviation sorting - unbalanced braces in {c.File}.");
                source = source.Remove(m.Index, endIdx - m.Index + 1).Insert(m.Index, snippet.TrimEnd());
            }
        }

        File.WriteAllText(path, source);
        ctx.Info($"deviations applied to {c.Name}: -{c.RemovedActions.Count} actions, {c.Redirects.Count} redirects, sorting={(c.Sorting?.Enabled ?? false)}");
    }
}
