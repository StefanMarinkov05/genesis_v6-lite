using System.Text.Json;
using GenesisV6.Core.Cli;
using GenesisV6.Core.Llm;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;
using GenesisV6.Core.Stages;
using GenesisV6.Core.Util;
using GenesisV6.Core.Variation;
using Json.Schema;
using Xunit;

namespace Genesis.Tests;

// Paths are relative to the test output dir - the csproj copies fixtures/, templates/,
// feature-templates/ and spec.schema.json next to the test binaries.
public static class Paths
{
    public static readonly string Schema = "spec.schema.json";
    public static readonly string Fixture = Path.Combine("fixtures", "project-spec.facenote.json");
}

public class SpecValidationTests
{
    private readonly SpecLoader _loader = new(Paths.Schema);

    [Fact]
    public void Fixture_spec_loads_and_validates()
    {
        var spec = _loader.Load(Paths.Fixture);
        Assert.Equal("FaceNote", spec.Meta.ProjectName);
        Assert.Contains(spec.Models, m => m.Name == "Product");
    }

    [Fact]
    public void Unknown_top_level_key_fails_loud()
    {
        var json = File.ReadAllText(Paths.Fixture)
            .Replace("\"specVersion\"", "\"specVresion_typo\": \"x\", \"specVersion\"");
        var ex = Assert.Throws<SpecValidationException>(() => _loader.LoadFromString(json));
        Assert.Contains(ex.Errors, e => e.Contains("specVresion_typo") || e.Contains("additional"));
    }

    [Fact]
    public void Relation_to_unknown_model_is_semantic_error()
    {
        var spec = _loader.Load(Paths.Fixture);
        spec.Models[0].Relations.Add(new RelationSpec { Kind = "manyToOne", Target = "Ghost" });
        var errors = SpecLoader.SemanticCheck(spec).ToList();
        Assert.Contains(errors, e => e.Contains("Ghost"));
    }

    [Fact]
    public void Model_without_key_property_is_semantic_error()
    {
        var spec = _loader.Load(Paths.Fixture);
        spec.Models[0].Properties.First(p => p.Key).Key = false;
        Assert.Contains(SpecLoader.SemanticCheck(spec), e => e.Contains("exactly one"));
    }

    [Fact]
    public void Sorting_field_not_on_model_is_semantic_error()
    {
        var spec = _loader.Load(Paths.Fixture);
        var withSorting = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        withSorting.Sorting!.Fields.Add("NoSuchField");
        Assert.Contains(SpecLoader.SemanticCheck(spec), e => e.Contains("NoSuchField"));
    }

    // Task 1 (v6): a visible (non-key, non-FK, non-computed) property with no displayName used to
    // be a soft warning ("[warn]" prefix); it is now a HARD GATE. Assert both that SemanticCheck
    // reports it AND that the message is fatal (no "[warn]" prefix, so LoadFromString would throw).
    [Fact]
    public void Missing_displayName_on_visible_property_is_now_a_hard_error()
    {
        var spec = _loader.Load(Paths.Fixture);
        var prod = spec.Models.First(m => m.Name == "Product");
        var visible = prod.Properties.First(p => !p.Key && !p.ForeignKey && p.Computed is null);
        visible.DisplayName = null;

        var errors = SpecLoader.SemanticCheck(spec).ToList();
        var hit = errors.FirstOrDefault(e => e.Contains($"Product.{visible.Name}") && e.Contains("lacks displayName"));
        Assert.NotNull(hit);
        Assert.DoesNotContain("[warn]", hit); // fatal, not a nudge
    }

    // Guards the backfill: the real shipping fixture must have zero visible props missing a
    // displayName, or the new hard gate would fail generation for FaceNote itself.
    [Fact]
    public void Fixture_has_full_displayName_coverage()
    {
        var spec = _loader.Load(Paths.Fixture);
        Assert.DoesNotContain(SpecLoader.SemanticCheck(spec), e => e.Contains("lacks displayName"));
    }

    // Regression: a feature can be enabled with no explicit "model" key (legal per schema -
    // FaceNote's "rating": {"enabled": true} has none). SpecExpandStage.ResolvePrimaryModel must
    // fall back to the first model with images (same rule as EmitControllersViewsStage's own
    // fallback), or the fragment's "$primaryModel" token survives verbatim into a relation target
    // and fails semantic validation with "relation target '$primaryModel' is not a declared model".
    [Fact]
    public void Resolve_primary_model_falls_back_to_images_model_when_feature_config_omits_model()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var primary = SpecExpandStage.ResolvePrimaryModel(spec, spec.Features["rating"]);
        Assert.Equal("Product", primary); // Product is the only model with an images block
    }

    [Fact]
    public void Resolve_primary_model_prefers_explicit_model_over_images_fallback()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var primary = SpecExpandStage.ResolvePrimaryModel(spec, spec.Features["catalogues"]); // has "model": "Product" explicitly
        Assert.Equal("Product", primary);
    }

    // Task: language flag - schema now accepts "en" alongside "bg" (was bg-only). The FaceNote
    // fixture itself stays "bg" - this only proves the SCHEMA no longer hard-rejects "en".
    [Fact]
    public void Schema_accepts_english_language_alongside_bulgarian()
    {
        const string json = """
        {
          "specVersion": "1.1",
          "meta": { "projectName": "TestProj", "solutionName": "TestProjSolution", "rootNamespace": "TestProj", "language": "en" },
          "models": []
        }
        """;
        var spec = new SpecLoader(Paths.Schema).LoadFromString(json);
        Assert.Equal("en", spec.Meta.Language);
    }

    [Fact]
    public void Facenote_fixture_language_is_bulgarian()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        Assert.Equal("bg", spec.Meta.Language);
        Assert.True(spec.Meta.IsBulgarian);
    }
}

// The single authoritative switch (MetaSection.IsBulgarian) that every retrofit BG-translation
// pass reads, so a language-flag bug can't silently diverge between stages (the "two paths, one
// concept" bug class this project has been bitten by before - see changelog 054).
public class LanguageGateTests
{
    [Theory]
    [InlineData("bg", true)]
    [InlineData("BG", true)]
    [InlineData("Bg", true)]
    [InlineData("en", false)]
    [InlineData("EN", false)]
    [InlineData("fr", false)]
    public void Is_bulgarian_matches_language_case_insensitively(string language, bool expected)
    {
        var meta = new MetaSection { Language = language };
        Assert.Equal(expected, meta.IsBulgarian);
    }

    [Fact]
    public void Is_bulgarian_defaults_true_when_language_unset()
    {
        Assert.True(new MetaSection().IsBulgarian); // MetaSection.Language defaults to "bg"
    }
}

public class VariationTests
{
    [Fact]
    public void Same_seed_same_axes_every_time()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var a = VariationResolver.Resolve(spec);
        var b = VariationResolver.Resolve(spec);
        Assert.Equal(a.Axes, b.Axes);
    }

    [Fact]
    public void Different_seeds_differ_on_at_least_one_axis()
    {
        // Statistical sanity: with 8 axes, two seeds colliding on all is ~0.
        var spec1 = new ProjectSpec { VariationProfile = new() { Seed = "alpha" } };
        var spec2 = new ProjectSpec { VariationProfile = new() { Seed = "omega" } };
        var a = VariationResolver.Resolve(spec1);
        var b = VariationResolver.Resolve(spec2);
        Assert.NotEqual(a.Axes.OrderBy(kv => kv.Key).Select(kv => kv.Value),
                        b.Axes.OrderBy(kv => kv.Key).Select(kv => kv.Value));
    }

    [Fact]
    public void Explicit_axis_pin_wins_over_derivation()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var v = VariationResolver.Resolve(spec);
        // Fixture pins namespaceStyle=blockScoped; unpinned, this seed would hash-derive
        // to fileScoped - so this genuinely exercises "pin beats derivation", not a
        // coincidental match.
        Assert.Equal("blockScoped", v["namespaceStyle"]);
    }

    [Fact]
    public void Seed_password_is_deterministic_and_per_email()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var v = VariationResolver.Resolve(spec);
        Assert.Equal(v.SeedPassword("a@x.bg"), v.SeedPassword("a@x.bg"));
        Assert.NotEqual(v.SeedPassword("a@x.bg"), v.SeedPassword("b@x.bg"));
    }

    [Fact]
    public void Theme_axis_engages_when_style_mode_is_auto()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture); // fixture: mode=auto
        var v = VariationResolver.Resolve(spec);
        Assert.Contains(v["bootstrapTheme"], new[] { "flatly", "cosmo", "lumen", "sandstone", "united", "yeti", "journal", "spacelab" });
    }

    [Fact]
    public void Substitute_replaces_variation_placeholders()
    {
        var v = VariationResolver.Resolve(new ProjectSpec { VariationProfile = new() { Seed = "s" } });
        var result = v.Substitute("role=$variation:adminRoleName");
        Assert.DoesNotContain("$variation:", result);
    }
}

public class TemplateEmissionTests
{
    private static (ProjectSpec spec, TemplateEngine engine) Arrange()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        return (spec, new TemplateEngine(spec, VariationResolver.Resolve(spec)));
    }

    [Fact]
    public void Model_template_renders_Product_with_bulgarian_messages_and_key()
    {
        var (spec, engine) = Arrange();
        var product = spec.Models.First(m => m.Name == "Product");
        var code = engine.RenderFile(Path.Combine("templates", "models", "model.sbn"), new { model = product });
        Assert.Contains("public class Product", code);
        Assert.Contains("[Key]", code);
        Assert.Contains("задължително", code); // BG validation message came through
        Assert.Contains("FaceNote.Models", code);
        Assert.DoesNotContain("$variation:", code);
    }

    [Fact]
    public void DbContext_template_declares_all_dbsets_and_relations()
    {
        var (spec, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "models", "dbcontext.sbn"));
        foreach (var m in spec.Models) Assert.Contains($"DbSet<{m.Name}>", code);
        Assert.Contains("ProductImages", code); // per-model image table
        Assert.Contains("OnDelete", code);
    }

    [Fact]
    public void Error_describer_template_is_fully_bulgarian()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "identity", "bg_identity_error_describer.sbn"));
        Assert.Contains("Паролата", code);
        Assert.Contains("PasswordRequiresDigit", code);
    }

    // Task 17 (v6 Package H): followers + following on ONE page via a view model, not two
    // separate actions/views keyed by a "Mode" flag. Render both templates to prove Scriban
    // parses them and the combined shape (single List action + ViewModel + two lists) is emitted.
    [Fact]
    public void Follows_controller_emits_single_list_action_and_view_model()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "follows_controller.sbn"));
        Assert.Contains("IActionResult> List()", code);
        Assert.Contains("class FollowersFollowingViewModel", code);
        Assert.Contains("Followers", code);
        Assert.Contains("Following", code);
        Assert.DoesNotContain("IActionResult> Followers()", code); // old split actions gone
        Assert.DoesNotContain("IActionResult> Following()", code);
    }

    [Fact]
    public void Follows_view_renders_two_lists_bound_to_the_view_model()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "follows_list.sbn"));
        Assert.Contains("FollowersFollowingViewModel", code);      // strongly-typed model
        Assert.Contains("Model.Followers", code);
        Assert.Contains("Model.Following", code);
        Assert.Contains("Последователи", code);
        Assert.Contains("Следвани", code);
        Assert.DoesNotContain("$variation:", code);
    }

    // Task 15 (v6 Package E): shared file-upload helper + admin profile-picture upload. All three
    // pieces are template-controlled (no scaffold transform), so rendering them proves the wiring.
    [Fact]
    public void File_upload_service_is_security_conscious()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "file_upload_service.sbn"));
        Assert.Contains("class FileUploadService", code);
        Assert.Contains("SaveImageAsync", code);
        Assert.Contains("Guid.NewGuid()", code);              // filename never from client input
        Assert.Contains("AllowedImageExtensions", code);      // extension allowlist
        Assert.Contains("MaxBytes", code);                    // size cap
        Assert.DoesNotContain("$variation:", code);
    }

    [Fact]
    public void Admin_user_edit_view_wires_multipart_pfp_upload()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "users_edit.sbn"));
        Assert.Contains("enctype=\"multipart/form-data\"", code); // required for file upload
        Assert.Contains("name=\"profilePicture\"", code);
        Assert.Contains("Име", code);                             // FirstName field (FaceNote has it)
        Assert.DoesNotContain("$variation:", code);
    }

    [Fact]
    public void Users_controller_saves_uploaded_pfp_without_blanking_it()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "users_controller.sbn"));
        Assert.Contains("FileUploadService", code);               // injected
        Assert.Contains("IFormFile? profilePicture", code);       // POST accepts the file
        Assert.Contains("SaveImageAsync(profilePicture", code);
        // The blind copy loop must NOT overwrite ProfilePicturePath from the (empty) form field.
        Assert.DoesNotContain("user.ProfilePicturePath = input.ProfilePicturePath", code);
    }

    // Task 16 (v6 Package E): feedback can attach an image, reusing the FileUploadService helper.
    [Fact]
    public void Reviews_controller_accepts_optional_image_upload()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "reviews_controller.sbn"),
            new { primary = "Product" });
        Assert.Contains("FileUploadService", code);           // reuses the shared helper
        Assert.Contains("IFormFile? image", code);
        Assert.Contains("SaveImageAsync(image", code);
        Assert.Contains("ImagePath = imagePath", code);
    }

    [Fact]
    public void Product_details_feedback_form_supports_image_attachment()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "product_details.sbn"),
            new { primary = "Product" });
        Assert.Contains("enctype=\"multipart/form-data\"", code); // form can carry a file
        Assert.Contains("name=\"image\"", code);
        Assert.Contains("review.ImagePath", code);                // uploaded image is displayed
    }

    // Task 10 (v6 Package C): blog posts can attach a photo, reusing the FileUploadService helper.
    [Fact]
    public void Posts_controller_accepts_optional_image_upload()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "posts_controller.sbn"));
        Assert.Contains("FileUploadService", code);              // reuses the shared helper
        Assert.Contains("IFormFile? image", code);
        Assert.Contains("SaveImageAsync(image", code);
        Assert.Contains("uploads/posts", code);
        Assert.Contains("post.ImagePath", code);
    }

    [Fact]
    public void Posts_create_form_supports_image_attachment()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "posts_create.sbn"));
        Assert.Contains("enctype=\"multipart/form-data\"", code);
        Assert.Contains("name=\"image\"", code);
    }

    [Fact]
    public void Posts_views_render_attached_photo()
    {
        var (_, engine) = Arrange();
        var index = engine.RenderFile(Path.Combine("templates", "views", "posts_index.sbn"));
        var details = engine.RenderFile(Path.Combine("templates", "views", "posts_details.sbn"));
        Assert.Contains("post.ImagePath", index);      // feed thumbnail
        Assert.Contains("Model.ImagePath", details);   // full image on the post page
    }

    [Fact]
    public void Program_registers_file_upload_service_when_posts_enabled()
    {
        var (_, engine) = Arrange();
        // FaceNote enables posts, so FileUploadService must be DI-registered for post photo uploads.
        var code = engine.RenderFile(Path.Combine("templates", "project", "program.sbn"));
        Assert.Contains("AddScoped<FileUploadService>", code);
    }

    // Task 26/7 (v6 Packages G/D): shared user-aggregation component (view model + service + partial)
    // powering both the public profile and the admin user detail page.
    [Fact]
    public void User_profile_view_model_has_feature_gated_collections()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "models", "user_profile_view_model.sbn"));
        Assert.Contains("class UserProfileViewModel", code);
        Assert.Contains("ApplicationUser User", code);
        Assert.Contains("List<Product> Products", code);   // listing model detected (Product has images+UserId)
        Assert.Contains("List<Post> Posts", code);         // posts enabled in FaceNote
        Assert.Contains("List<Review> Feedbacks", code);   // rating enabled
        Assert.Contains("List<ApplicationUser> Followers", code);
        Assert.Contains("List<ApplicationUser> Following", code);
    }

    [Fact]
    public void User_profile_service_aggregates_all_content()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "user_profile_service.sbn"));
        Assert.Contains("class UserProfileService", code);
        Assert.Contains("BuildAsync(string userId, bool publicOnly, string? viewerUserId = null)", code);
        Assert.Contains("_context.Products", code);        // listed products by seller
        Assert.Contains("_context.Posts", code);
        Assert.Contains("_context.Reviews", code);
        Assert.Contains("f.FollowedUserId == userId", code); // followers query
        Assert.Contains("f.FollowerUserId == userId", code); // following query
        Assert.Contains("!publicOnly || c.IsPublic", code);  // public-only catalogue filter
        Assert.Contains("vm.IsOwnProfile = viewerUserId == userId", code);
        Assert.Contains("vm.IsFollowedByViewer = viewerUserId is not null && followerIds.Contains(viewerUserId)", code);
    }

    [Fact]
    public void User_profile_content_partial_renders_all_sections()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "user_profile_content_partial.sbn"));
        Assert.Contains("UserProfileViewModel", code);
        Assert.Contains("Последователи", code);
        Assert.Contains("Следвани", code);
        Assert.Contains("Обяви", code);
        Assert.Contains("Публикации", code);
        Assert.Contains("Отзиви", code);
        // the partial branches on adminLinks to pick admin-detail vs public-profile user links
        Assert.Contains("adminLinks", code);
        Assert.Contains("Users\" asp-action=\"Details", code); // admin-mode user link target
    }

    [Fact]
    public void Profiles_controller_uses_shared_profile_service()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "profiles_controller.sbn"));
        Assert.Contains("UserProfileService", code);
        Assert.Contains("BuildAsync(userId, publicOnly: true, viewerUserId)", code);
        Assert.Contains("_userManager.GetUserId(User)", code); // current viewer, null if anonymous
        Assert.Contains("ProfileHash.Of(uid) == id", code); // item 17: route id is a hash, resolved back to the real user id
        Assert.DoesNotContain("$variation:", code);
    }

    [Fact]
    public void Profiles_public_view_uses_shared_partial()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "profiles_public.sbn"));
        Assert.Contains("UserProfileViewModel", code);
        Assert.Contains("_UserProfileContent", code);
        Assert.Contains("Model.User", code);
    }

    [Fact]
    public void Program_registers_profile_service_when_posts_or_catalogues_enabled()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "project", "program.sbn"));
        Assert.Contains("AddScoped<UserProfileService>", code);
    }

    // Task 7 (v6 Package D): admin user Details page aggregating the same content, sharing the component.
    [Fact]
    public void Users_controller_has_details_action_using_profile_service()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "users_controller.sbn"));
        Assert.Contains("UserProfileService", code);
        Assert.Contains("Details(string id)", code);
        Assert.Contains("BuildAsync(id, publicOnly: false)", code); // admin sees all (not public-only)
        Assert.DoesNotContain("$variation:", code);
    }

    [Fact]
    public void Users_details_view_shows_data_and_shared_partial()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "users_details.sbn"));
        Assert.Contains("UserProfileViewModel", code);
        Assert.Contains("ViewData[\"AdminLinks\"] = true", code); // links go to admin detail pages
        Assert.Contains("_UserProfileContent", code);            // reuses the shared aggregation partial
        Assert.Contains("Model.User.Id", code);                  // user data shown
        Assert.Contains("Model.User.Email", code);
    }

    [Fact]
    public void Users_index_links_to_details()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "users_index.sbn"));
        Assert.Contains("asp-action=\"Details\"", code);
    }

    [Fact]
    public void Program_template_honors_di_registration_variation()
    {
        var (spec, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "project", "program.sbn"));
        // AddDatabaseDeveloperPageExceptionFilter is deliberately absent: it needs
        // Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore, which the default MVC
        // template only references under Individual auth - we scaffold --auth None
        // (confirmed via a live SDK 10.0.100 build; see program.sbn's own comment).
        // diRegistrationStyle is NOT read by program.sbn at all (known dead axis, see
        // decisions.md) - the identity call is unconditionally one fluent chain regardless
        // of what that axis resolves to. Assertions below check the actual real output.
        Assert.Contains("AddDefaultIdentity<ApplicationUser>", code);
        Assert.Contains(".AddRoles<IdentityRole>()", code);
        Assert.Contains("AddErrorDescriber<BgIdentityErrorDescriber>", code);
        Assert.Contains("MapControllerRoute", code);
    }

    [Fact]
    public void Seed_template_uses_symbolic_refs_not_hardcoded_ids()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "models", "seed_data.sbn"));
        Assert.Contains("refKeys", code);
        Assert.Contains("parentRef_", code);
        Assert.DoesNotContain("Id = 1", code);
    }

    // Task 9 (v6 Package D): admin total supervisor - every model gets an Admin CRUD controller + table.
    [Fact]
    public void Pluralize_handles_common_english_forms()
    {
        Assert.Equal("Posts", SpecExpandStage.Pluralize("Post"));
        Assert.Equal("Categories", SpecExpandStage.Pluralize("Category"));
        Assert.Equal("Reviews", SpecExpandStage.Pluralize("Review"));
        Assert.Equal("Boxes", SpecExpandStage.Pluralize("Box"));
        Assert.Equal("CatalogueItems", SpecExpandStage.Pluralize("CatalogueItem"));
        Assert.Equal("Follows", SpecExpandStage.Pluralize("Follow"));
    }

    [Fact]
    public void Admin_supervisor_injects_crud_for_uncovered_models()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(
            "{ \"models\": [ {\"name\":\"Product\"}, {\"name\":\"Post\"}, {\"name\":\"Review\"} ]," +
            " \"controllers\": [ {\"name\":\"ProductsController\",\"area\":\"Admin\",\"model\":\"Product\",\"generatedBy\":\"scaffold\"} ]," +
            " \"views\": { \"pages\": [] } }")!.AsObject();

        SpecExpandStage.InjectAdminSupervisorSurface(node, null);

        var controllers = node["controllers"]!.AsArray();
        // Product already had an Admin controller -> not duplicated.
        Assert.Equal(1, controllers.Count(c => c!["model"]?.GetValue<string>() == "Product"));
        // Post + Review get new Admin scaffold controllers.
        Assert.Contains(controllers, c => c!["name"]!.GetValue<string>() == "PostsController"
            && c["area"]!.GetValue<string>() == "Admin" && c["generatedBy"]!.GetValue<string>() == "scaffold");
        Assert.Contains(controllers, c => c!["name"]!.GetValue<string>() == "ReviewsController");
        // ...and matching adminTable pages.
        var pages = node["views"]!["pages"]!.AsArray();
        Assert.Contains(pages, p => p!["file"]!.GetValue<string>() == "Areas/Admin/Views/Posts/Index.cshtml"
            && p["kind"]!.GetValue<string>() == "adminTable");
    }

    // Category-1 short-task audit: any single-property model (not just hand-authored Brand/
    // Category) should get its Details action/view removed automatically, so an auto-injected
    // isLookup model like CatalogueItem (only "Position") never ends up with an orphaned,
    // dead-but-reachable Details page nobody explicitly removed. Order keeps its existing
    // "no admin Create" removal too - both must coexist in one removedActions array.
    [Fact]
    public void Admin_supervisor_removes_details_for_single_property_models_and_keeps_order_rule()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse("""
            {
              "models": [
                { "name": "CatalogueItem", "isLookup": true,
                  "properties": [
                    { "name": "Id", "type": "int", "key": true },
                    { "name": "CatalogueId", "type": "int", "foreignKey": true },
                    { "name": "ProductId", "type": "int", "foreignKey": true },
                    { "name": "Position", "type": "int" }
                  ] },
                { "name": "Order",
                  "properties": [
                    { "name": "Id", "type": "int", "key": true },
                    { "name": "UserId", "type": "string" },
                    { "name": "Total", "type": "decimal" },
                    { "name": "Status", "type": "enum" }
                  ] },
                { "name": "Product",
                  "properties": [
                    { "name": "Id", "type": "int", "key": true },
                    { "name": "Title", "type": "string" },
                    { "name": "Price", "type": "decimal" }
                  ] }
              ],
              "controllers": [],
              "views": { "pages": [] }
            }
            """)!.AsObject();

        SpecExpandStage.InjectAdminSupervisorSurface(node, null);

        var controllers = node["controllers"]!.AsArray();
        var catalogueItem = controllers.First(c => c!["model"]!.GetValue<string>() == "CatalogueItem")!;
        var removedCi = catalogueItem["removedActions"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains("Details", removedCi); // only "Position" -> single-property rule fires

        var order = controllers.First(c => c!["model"]!.GetValue<string>() == "Order")!;
        var removedOrder = order["removedActions"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains("Create", removedOrder);      // pre-existing rule
        Assert.DoesNotContain("Details", removedOrder); // 3 non-key props -> rule does NOT fire

        var product = controllers.First(c => c!["model"]!.GetValue<string>() == "Product")!;
        Assert.Null(product["removedActions"]); // 2 non-key props -> no removedActions at all
    }

    // Task 5 (v6 Package B): cart badge must read from the database via injected context,
    // not from ViewData["CartCount"] which was never populated.
    [Fact]
    public void Layout_injects_cart_context_and_queries_count_when_orders_enabled()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "layout.sbn"));
        Assert.Contains("NavCartContext", code);                          // context injected
        Assert.Contains("NavCartContext.CartItems.Count(", code);         // real DB count
        Assert.DoesNotContain("ViewData[\"CartCount\"]", code);           // dead pattern gone
    }

    // Task 13 (v6 Package I): sort dropdown must preserve its selected value after form submit.
    [Fact]
    public void Card_grid_sort_dropdown_preserves_selected_field()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var model = spec.Models.First(m => m.Name == owner.Model);
        var page = spec.Views!.Pages.First(p => p.Kind == "cardGrid");
        var code = engine.RenderFile(Path.Combine("templates", "views", "card_grid.sbn"),
            new { owner, model, page });  // engine already provides features from spec
        Assert.Contains("ViewData[\"SortBy\"]?.ToString() ==", code);     // selected attribute check present
    }

    // Task 24a (v6 Package I): sort direction (asc/desc) select must appear on sorted card grids.
    [Fact]
    public void Card_grid_has_sort_direction_select_when_sorting_enabled()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var model = spec.Models.First(m => m.Name == owner.Model);
        var page = spec.Views!.Pages.First(p => p.Kind == "cardGrid");
        var code = engine.RenderFile(Path.Combine("templates", "views", "card_grid.sbn"),
            new { owner, model, page });  // engine already provides features from spec
        Assert.Contains("name=\"sortDir\"", code);                         // direction select rendered
        Assert.Contains("Намаляващо", code);                               // Bulgarian label
        Assert.Contains("ViewData[\"SortDir\"]?.ToString() == \"desc\"", code); // selected state check
    }

    // Task 11 (v6 Package I): card grids use infinite scroll (append on scroll), not a numbered pager.
    [Fact]
    public void Card_grid_uses_infinite_scroll_not_pager()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var model = spec.Models.First(m => m.Name == owner.Model);
        var page = spec.Views!.Pages.First(p => p.Kind == "cardGrid");
        var code = engine.RenderFile(Path.Combine("templates", "views", "card_grid.sbn"),
            new { owner, model, page });
        Assert.Contains("scrollSentinel", code);          // the intersection sentinel
        Assert.Contains("IntersectionObserver", code);    // scroll-triggered loading
        Assert.Contains("data-total-pages", code);
        Assert.DoesNotContain("class=\"pagination\"", code); // the numbered pager is gone
        Assert.DoesNotContain("page-link", code);
    }

    // Card grid: bulk average rating stars per product (Index had none before - only Details computed one).
    [Fact]
    public void Card_grid_shows_bulk_average_rating_stars_when_has_rating()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var model = spec.Models.First(m => m.Name == owner.Model);
        var page = spec.Views!.Pages.First(p => p.Kind == "cardGrid");
        var code = engine.RenderFile(Path.Combine("templates", "views", "card_grid.sbn"),
            new { owner, model, page, has_rating = true });
        Assert.Contains("ViewData[\"AverageRatings\"]", code);
        Assert.Contains("Math.Round(itemAvgDisplay)", code);
    }

    [Fact]
    public void Card_grid_omits_rating_stars_when_has_rating_false()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var model = spec.Models.First(m => m.Name == owner.Model);
        var page = spec.Views!.Pages.First(p => p.Kind == "cardGrid");
        var code = engine.RenderFile(Path.Combine("templates", "views", "card_grid.sbn"),
            new { owner, model, page, has_rating = false });
        Assert.DoesNotContain("AverageRatings", code);
    }

    [Fact]
    public void Sorting_index_body_computes_bulk_average_ratings_when_has_rating()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "sorting_index_body.sbn"),
            new { controller = owner, has_rating = true });
        Assert.Contains("ViewData[\"AverageRatings\"]", code);
        Assert.Contains(".Include(x => x.Reviews)", code);
    }

    [Fact]
    public void Sorting_index_body_omits_reviews_include_when_has_rating_false()
    {
        var (spec, engine) = Arrange();
        var owner = spec.Controllers.First(c => c.Sorting is { Enabled: true });
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "sorting_index_body.sbn"),
            new { controller = owner, has_rating = false });
        Assert.DoesNotContain("AverageRatings", code);
        Assert.DoesNotContain(".Include(x => x.Reviews)", code);
    }

    // Task 6 (v6 Package D): admin cannot delete another admin account.
    [Fact]
    public void Users_controller_blocks_deleting_admin_accounts()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "users_controller.sbn"));
        Assert.Contains("GetRolesAsync(user)", code);             // role check performed
        Assert.Contains("администраторски акаунт", code);          // Bulgarian error message
        Assert.Contains("targetRoles.Contains(", code);            // explicit contains check
    }

    // Task 8 (v6 Package F): Order model carries delivery info; cart views updated accordingly.
    [Fact]
    public void Orders_feature_template_has_receiver_and_address_fields()
    {
        var json = File.ReadAllText(Path.Combine("feature-templates", "orders.json"));
        Assert.Contains("ReceiverName", json);
        Assert.Contains("DeliveryAddress", json);
        Assert.Contains("Получател", json);           // Bulgarian displayName
        Assert.Contains("Адрес за доставка", json);
    }

    [Fact]
    public void Cart_checkout_view_has_delivery_form_fields()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "cart_index.sbn"),
            new { primary = "Product", price_property = "Price" });
        Assert.Contains("receiverName", code);
        Assert.Contains("deliveryAddress", code);
        Assert.Contains("Данни за доставка", code);  // section header
    }

    [Fact]
    public void Cart_controller_passes_delivery_fields_to_order()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "cart_controller.sbn"),
            new { primary = "Product", price_property = "Price" });
        Assert.Contains("string? receiverName", code);
        Assert.Contains("string? deliveryAddress", code);
        Assert.Contains("ReceiverName = receiverName", code);
    }

    // Task 19 (v6 Package B): users can rename their own catalogues via a dedicated action + view.
    [Fact]
    public void Catalogues_controller_has_edit_name_action()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "controllers", "catalogues_controller.sbn"),
            new { primary = "Product" });
        Assert.Contains("IActionResult> EditName(int id)", code);  // GET + POST both present
        Assert.Contains("catalogue.Name = name.Trim()", code);
        Assert.Contains("c.UserId == userId", code);               // owner-gated
    }

    [Fact]
    public void Catalogues_edit_name_view_renders_rename_form()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "catalogues_edit_name.sbn"),
            new { primary = "Product" });
        Assert.Contains("Преименуване на каталог", code);
        Assert.Contains("name=\"name\"", code);     // text input for new name
        Assert.Contains("asp-action=\"EditName\"", code);
    }

    // Task 2 (v6 Package B): "Add to catalogue" modal button on product details page.
    [Fact]
    public void Product_details_has_catalogue_modal_button_when_catalogues_enabled()
    {
        var (_, engine) = Arrange();
        var code = engine.RenderFile(Path.Combine("templates", "views", "product_details.sbn"),
            new { primary = "Product" });
        Assert.Contains("catalogue-open-details", code);           // button CSS class
        Assert.Contains("catalogueModalDetails", code);            // modal element id
        Assert.Contains("/Client/Catalogues/Picker", code);        // AJAX picker endpoint
        Assert.Contains("Добави в каталог", code);
    }

    // Task 14 (v6 Package E): admin image management controller + view for multi-image models.
    [Fact]
    public void Product_images_controller_renders_upload_and_delete_actions()
    {
        var (spec, engine) = Arrange();
        var product = spec.Models.First(m => m.Name == "Product");
        var code = engine.RenderFile(
            Path.Combine("templates", "controllers", "product_images_controller.sbn"),
            new { model = product });
        Assert.Contains("ProductImagesController", code);
        Assert.Contains("ImageService", code);                     // injected dependency
        Assert.Contains("IActionResult> Upload(int id", code);     // upload action
        Assert.Contains("IActionResult> DeleteImage(int id", code);// delete action
        Assert.Contains("Save{{ model.name }}ImageAsync".Replace("{{ model.name }}", "Product"), code);
    }

    [Fact]
    public void Product_images_view_has_gallery_and_multifile_form()
    {
        var (spec, engine) = Arrange();
        var product = spec.Models.First(m => m.Name == "Product");
        var code = engine.RenderFile(
            Path.Combine("templates", "views", "admin_product_images.sbn"),
            new { model = product });
        Assert.Contains("ProductImage", code);                     // view model type
        Assert.Contains("multiple", code);                         // multi-file input
        Assert.Contains("asp-action=\"DeleteImage\"", code);       // per-image delete
        Assert.Contains("asp-action=\"Upload\"", code);            // upload form action
        Assert.Contains("Добави снимки", code);                    // Bulgarian label
    }

    // Category-3 backlog: images.mode:"single" was previously indistinguishable from "multiple"
    // in the admin UI (always a multi-upload gallery, even for a model that can only ever have
    // one photo). Real Product is always "multiple" in the shipping fixture, so this uses a
    // hand-built single-image model to exercise the other branch.
    [Fact]
    public void Single_mode_image_view_shows_replace_ui_not_gallery()
    {
        var (_, engine) = Arrange();
        var singleModel = new ModelSpec
        {
            Name = "Avatar",
            Images = new ImagesSpec { Mode = "single", StoredIn = "wwwroot/images/avatars" },
        };
        var code = engine.RenderFile(
            Path.Combine("templates", "views", "admin_product_images.sbn"),
            new { model = singleModel });
        Assert.DoesNotContain("multiple", code);                   // no multi-file input
        Assert.Contains("Премахни снимката", code);                // single-photo remove action
        Assert.Contains("Качването на нова снимка заменя текущата.", code); // replace-semantics hint
        Assert.DoesNotContain("row-cols-md-4", code);               // no gallery grid markup
    }

    [Fact]
    public void Admin_table_has_images_button_for_models_with_images()
    {
        var (spec, engine) = Arrange();
        var product = spec.Models.First(m => m.Name == "Product");
        var page = spec.Views!.Pages.First(p => p.Kind == "adminTable" && p.File.Contains("Products"));
        var owner = spec.Controllers.First(c => c.Name == "ProductsController" && c.Area == "Admin");
        var code = engine.RenderFile(
            Path.Combine("templates", "views", "admin_table.sbn"),
            new { model = product, owner, page });
        Assert.Contains("ProductImages", code);                    // controller name in button
        Assert.Contains("Снимки", code);                           // Bulgarian label
    }

    [Fact]
    public void Admin_table_fk_column_headers_use_target_model_display_name()
    {
        // Product's manyToOne relations target Brand/Category - the header used to render the
        // raw navigation property name ("Brand"/"Category"), leaking English into an otherwise
        // fully-Bulgarian admin table. It must render each target model's own displayName instead.
        var (spec, engine) = Arrange();
        var product = spec.Models.First(m => m.Name == "Product");
        var page = spec.Views!.Pages.First(p => p.Kind == "adminTable" && p.File.Contains("Products"));
        var owner = spec.Controllers.First(c => c.Name == "ProductsController" && c.Area == "Admin");
        var code = engine.RenderFile(
            Path.Combine("templates", "views", "admin_table.sbn"),
            new { model = product, owner, page });
        Assert.Contains("<th>Марки</th>", code);
        Assert.Contains("<th>Категории</th>", code);
        Assert.DoesNotContain("<th>Brand</th>", code);
        Assert.DoesNotContain("<th>Category</th>", code);
    }

    // Category-1 short-task audit: Brand/Category had their Details action removed
    // (removedActions:["Details"]) - the Products admin table's FK-relation link used to point
    // at asp-action="Details" unconditionally, producing a dead 404 link from a live admin page.
    // Must fall back to "Edit" (which always exists) instead.
    [Fact]
    public void Admin_table_fk_link_falls_back_to_edit_when_target_details_is_removed()
    {
        var (spec, engine) = Arrange();
        var product = spec.Models.First(m => m.Name == "Product");
        var page = spec.Views!.Pages.First(p => p.Kind == "adminTable" && p.File.Contains("Products"));
        var owner = spec.Controllers.First(c => c.Name == "ProductsController" && c.Area == "Admin");
        var code = engine.RenderFile(
            Path.Combine("templates", "views", "admin_table.sbn"),
            new { model = product, owner, page });
        Assert.Contains("asp-controller=\"Brands\" asp-action=\"Edit\"", code);
        Assert.Contains("asp-controller=\"Categories\" asp-action=\"Edit\"", code);
        Assert.DoesNotContain("asp-controller=\"Brands\" asp-action=\"Details\"", code);
        Assert.DoesNotContain("asp-controller=\"Categories\" asp-action=\"Details\"", code);
    }

    // A5: "Спецификации" consolidation - hand-built minimal spec (not the raw fixture, which has
    // no isLookup/Review models until spec-expand injects them from feature-templates, and no
    // dependency on the gitignored *.lock.json). Covers all three tab flavours in one render:
    // isSpecification (Brand, CRUD tab reusing its scaffolded controller), isLookup (CartItem,
    // read-only + relation/user filters), and Feedbacks (Review, read-only + product/user/rating
    // filters, gated on features.rating).
    [Fact]
    public void Specifications_controller_and_view_render_all_three_tab_flavours_with_filters()
    {
        var spec = new ProjectSpec
        {
            Meta = new MetaSection { ProjectName = "TestProj", RootNamespace = "TestProj" },
            Identity = new IdentitySection
            {
                Roles = [new RoleSpec { Name = "Admin", IsAdmin = true }],
                ApplicationUser = new ApplicationUserSpec { ExtraProperties = [] },
            },
            Models =
            [
                new ModelSpec
                {
                    Name = "Brand",
                    IsSpecification = true,
                    DisplayName = "Марки",
                    Properties =
                    [
                        new PropertySpec { Name = "Id", Type = "int", Key = true },
                        new PropertySpec { Name = "Name", Type = "string", DisplayName = "Марка" },
                    ],
                },
                new ModelSpec
                {
                    Name = "Product",
                    Properties =
                    [
                        new PropertySpec { Name = "Id", Type = "int", Key = true },
                        new PropertySpec { Name = "Title", Type = "string", DisplayName = "Заглавие" },
                    ],
                },
                new ModelSpec
                {
                    Name = "CartItem",
                    IsLookup = true,
                    DisplayName = "Артикули в кошница",
                    Properties =
                    [
                        new PropertySpec { Name = "Id", Type = "int", Key = true },
                        new PropertySpec { Name = "UserId", Type = "string", DisplayName = "Потребител" },
                        new PropertySpec { Name = "Quantity", Type = "int", DisplayName = "Количество" },
                    ],
                    Relations = [new RelationSpec { Kind = "manyToOne", Target = "Product", Navigation = "Product" }],
                },
                new ModelSpec
                {
                    Name = "Review",
                    Properties =
                    [
                        new PropertySpec { Name = "Id", Type = "int", Key = true },
                        new PropertySpec { Name = "AuthorUserId", Type = "string", DisplayName = "Автор" },
                        new PropertySpec { Name = "Stars", Type = "int", DisplayName = "Оценка" },
                        new PropertySpec { Name = "Text", Type = "string", DisplayName = "Отзив", Nullable = true },
                    ],
                    Relations = [new RelationSpec { Kind = "manyToOne", Target = "Product", Navigation = "Product" }],
                },
            ],
            Controllers =
            [
                new ControllerSpec { Name = "BrandsController", Area = "Admin", Model = "Brand", IsSpecification = true },
            ],
            Features = new Dictionary<string, JsonElement>
            {
                ["rating"] = JsonSerializer.SerializeToElement(new { enabled = true }),
            },
        };
        var engine = new TemplateEngine(spec, VariationResolver.Resolve(spec));

        var controllerCode = engine.RenderFile(Path.Combine("templates", "controllers", "specifications_controller.sbn"));
        Assert.Contains("class SpecificationsController", controllerCode);
        Assert.Contains("vm.Brands = await _context.Brands.AsNoTracking().ToListAsync();", controllerCode);
        // Filter param prefix is the model name fully lowercased ("cartitem", not camelCase
        // "cartItem") - a deliberate, internally-consistent style choice (matches the local
        // query-variable name), not a bug.
        Assert.Contains("int? cartitemProductId = null", controllerCode);      // relation filter param
        Assert.Contains("string? cartitemUserId = null", controllerCode);      // user filter param
        Assert.Contains("int? reviewProductId = null", controllerCode);        // feedback relation filter
        Assert.Contains("string? reviewAuthorUserId = null", controllerCode);  // feedback user filter
        Assert.Contains("int? reviewStars = null", controllerCode);            // feedback rating filter
        Assert.Contains("if (reviewStars.HasValue) reviewQuery = reviewQuery.Where(x => x.Stars == reviewStars);", controllerCode);

        var viewCode = engine.RenderFile(Path.Combine("templates", "views", "specifications_index.sbn"));
        Assert.Contains("Спецификации", viewCode);
        Assert.Contains("id=\"tab-brand\"", viewCode);      // CRUD tab
        Assert.Contains("id=\"tab-cartitem\"", viewCode);   // read-only lookup tab
        Assert.Contains("id=\"tab-feedback\"", viewCode);   // feedback tab
        Assert.Contains("name=\"cartitemProductId\"", viewCode);  // relation filter select
        Assert.Contains("name=\"cartitemUserId\"", viewCode);     // user filter select
        Assert.Contains("name=\"reviewStars\"", viewCode);        // rating filter select
        Assert.Contains("@for (var s = 1; s <= 5; s++)", viewCode); // 1-5 star options loop (Razor, evaluated at ASP.NET runtime)
        Assert.Contains("@s ★", viewCode);
    }
}

public class DeviationTests
{
    // Canned scaffolder-shaped controller - the anchors ApplyDeviations relies on.
    private const string CannedScaffold = """
        public class ProductsController : Controller
        {
            // GET: Products
            public async Task<IActionResult> Index()
            {
                return View(await _context.Products.ToListAsync());
            }

            // GET: Products/Details/5
            public async Task<IActionResult> Details(int? id)
            {
                if (id == null) return NotFound();
                return View();
            }

            // POST: Products/Edit/5
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Edit(int id, Product product)
            {
                _context.Update(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
        }
        """;

    [Fact]
    public void Removed_action_regex_strips_details_block()
    {
        var rx = new System.Text.RegularExpressions.Regex(
            @"[ \t]*//\s*(GET|POST):[^\n]*\n[ \t]*(\[[^\]]+\]\s*\n[ \t]*)*public\s+async\s+Task<IActionResult>\s+Details\b.*?\n[ \t]*\}\n",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var result = rx.Replace(CannedScaffold, "");
        Assert.DoesNotContain("Details", result);
        Assert.Contains("Index", result);
        Assert.Contains("Edit", result);
    }

    [Fact]
    public void Redirect_regex_rewrites_only_the_target_action()
    {
        var rx = new System.Text.RegularExpressions.Regex(
            @"(public\s+async\s+Task<IActionResult>\s+Edit\b(?:(?!public\s+async).)*?)return\s+RedirectToAction\(nameof\(Index\)\);",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var result = rx.Replace(CannedScaffold, "$1return RedirectToAction(\"Index\", \"Summary\");", 1);
        Assert.Contains("RedirectToAction(\"Index\", \"Summary\")", result);
    }

    // A5: verifies the REAL ApplyDeviations method (not a reimplemented regex) against a literal
    // copy of the actual scaffolded BrandsController.cs shape produced by the 2026-07-12/13 A5
    // regenerate (post Details-removal from tonight's earlier fix) - confirms Create/Edit/
    // DeleteConfirmed's redirects can be safely retargeted to SpecificationsController.Index so
    // Brand/Category CRUD, reused as Спецификации's write sub-controllers, lands the user back on
    // the consolidated page instead of the orphaned (nav-hidden) Brands/Index.
    private const string RealBrandsControllerShape = """
        using System;
        using System.Threading.Tasks;
        using Microsoft.AspNetCore.Mvc;
        using Microsoft.EntityFrameworkCore;
        using FaceNote.Models;

        namespace FaceNote.Areas_Admin_Controllers
        {
            [Area("Admin")]
        public class BrandsController : Controller
            {
                private readonly FaceNoteContext _context;

                public BrandsController(FaceNoteContext context)
                {
                    _context = context;
                }

                public async Task<IActionResult> Index()
                {
                    return View(await _context.Brands.ToListAsync());
                }

                public IActionResult Create()
                {
                    return View();
                }

                [HttpPost]
                [ValidateAntiForgeryToken]
                public async Task<IActionResult> Create([Bind("Id,Name")] Brand brand)
                {
                    if (ModelState.IsValid)
                    {
                        _context.Add(brand);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index));
                    }
                    return View(brand);
                }

                public async Task<IActionResult> Edit(int? id)
                {
                    if (id == null) return NotFound();
                    var brand = await _context.Brands.FindAsync(id);
                    if (brand == null) return NotFound();
                    return View(brand);
                }

                [HttpPost]
                [ValidateAntiForgeryToken]
                public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Brand brand)
                {
                    if (id != brand.Id) return NotFound();
                    if (ModelState.IsValid)
                    {
                        _context.Update(brand);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index));
                    }
                    return View(brand);
                }

                public async Task<IActionResult> Delete(int? id)
                {
                    if (id == null) return NotFound();
                    var brand = await _context.Brands.FirstOrDefaultAsync(m => m.Id == id);
                    if (brand == null) return NotFound();
                    return View(brand);
                }

                [HttpPost, ActionName("Delete")]
                [ValidateAntiForgeryToken]
                public async Task<IActionResult> DeleteConfirmed(int id)
                {
                    var brand = await _context.Brands.FindAsync(id);
                    if (brand != null)
                    {
                        brand.IsDeleted = true; brand.DeletedAt = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }

                private bool BrandExists(int id) => _context.Brands.Any(e => e.Id == id);
            }
        }
        """;

    [Fact]
    public void Real_ApplyDeviations_redirects_brand_crud_back_to_specifications_index()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dps-nn-tests-" + Guid.NewGuid());
        var projectDir = Path.Combine(tempDir, "TestProj");
        var controllerPath = Path.Combine(projectDir, "Areas", "Admin", "Controllers", "BrandsController.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(controllerPath)!);
        File.WriteAllText(controllerPath, RealBrandsControllerShape);
        try
        {
            var spec = new ProjectSpec { Meta = new MetaSection { ProjectName = "TestProj", RootNamespace = "TestProj" } };
            var ctx = new StageContext(spec, VariationResolver.Resolve(spec), tempDir, ".",
                new StateStore(Path.Combine(tempDir, "state.json")), TextWriter.Null);
            var controllerSpec = new ControllerSpec
            {
                Name = "BrandsController",
                File = "Areas/Admin/Controllers/BrandsController.cs",
                Redirects =
                [
                    new RedirectSpec { After = "Create", To = "SpecificationsController.Index" },
                    new RedirectSpec { After = "Edit", To = "SpecificationsController.Index" },
                    new RedirectSpec { After = "DeleteConfirmed", To = "SpecificationsController.Index" },
                ],
            };
            var engine = new TemplateEngine(spec, ctx.Variation);

            EmitControllersViewsStage.ApplyDeviations(ctx, controllerSpec, engine);

            var result = File.ReadAllText(controllerPath);
            var hits = System.Text.RegularExpressions.Regex.Matches(result, @"RedirectToAction\(""Index"",\s*""Specifications""\)").Count;
            Assert.Equal(3, hits); // Create, Edit (POST), DeleteConfirmed - all three redirect
            Assert.Contains("public IActionResult Create()", result);           // GET Create untouched
            Assert.Contains("public async Task<IActionResult> Edit(int? id)", result); // GET Edit untouched
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }
}

public class LlmBridgeTests
{
    private static readonly JsonSchema Simple = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
        .Required("name").AdditionalProperties(false).Build();

    [Fact]
    public void Valid_json_passes()
    {
        var (doc, violations) = LlmBridge.Validate("""{"name": "x"}""", Simple);
        Assert.NotNull(doc);
        Assert.Empty(violations);
    }

    [Fact]
    public void Markdown_fenced_json_is_tolerated()
    {
        var (doc, violations) = LlmBridge.Validate("```json\n{\"name\": \"x\"}\n```", Simple);
        Assert.NotNull(doc);
        Assert.Empty(violations);
    }

    [Fact]
    public void Schema_violation_is_reported()
    {
        var (doc, violations) = LlmBridge.Validate("""{"nom": "x"}""", Simple);
        Assert.Null(doc);
        Assert.NotEmpty(violations);
    }

    [Fact]
    public void Garbage_is_reported_as_invalid_json()
    {
        var (doc, violations) = LlmBridge.Validate("not json at all", Simple);
        Assert.Null(doc);
        Assert.Contains(violations, v => v.Contains("not valid JSON"));
    }
}

public class SeedSchemaTests
{
    [Fact]
    public void Seed_schema_requires_refkey_on_parents_and_parentref_on_children()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var schema = SeedStage.BuildSeedSchema(spec, spec.Seeding!);
        var json = JsonSerializer.Serialize(schema);
        Assert.Contains("refKey", json);       // Brand/Category are parents
        Assert.Contains("parentRef_Category", json); // Product is a child
        Assert.Contains("imageKeywords", json); // Product has images (array - "multiple" mode = 2-4 per row)
    }

    [Fact]
    public void Canned_seed_response_validates_against_generated_schema()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var schema = SeedStage.BuildSeedSchema(spec, spec.Seeding!);
        var canned = File.ReadAllText(Path.Combine("fixtures", "seed-response.facenote.json"));
        var (doc, violations) = LlmBridge.Validate(canned, schema);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
        Assert.NotNull(doc);
    }
}

public class VerifyHelpersTests
{
    [Theory]
    [InlineData("Views/Home/Index.cshtml", "/")]
    [InlineData("Views/Summary/Index.cshtml", "/Summary")]
    [InlineData("Areas/Admin/Views/Products/Index.cshtml", "/Admin/Products")]
    [InlineData("Areas/Client/Views/Products/Index.cshtml", "/Client/Products")]
    public void View_file_maps_to_route(string file, string route)
        => Assert.Equal(route, VerifyStage.ViewFileToRoute(file));

    [Fact]
    public void English_leak_detector_flags_common_phrases_but_not_proper_nouns()
    {
        var html = "<html><body><h1>Добре дошли</h1><p>BMW X5</p><a>Log in here</a><script>var the = 1;</script></body></html>";
        var leaks = VerifyStage.EnglishLeaks(html).ToList();
        Assert.Contains(leaks, l => l.Contains("Log in"));
        Assert.DoesNotContain(leaks, l => l.Contains("BMW"));
    }

    // B4: the language gate must scan the whole route surface, not one page per kind.
    // FaceNote has every feature enabled, so this only covers the "included" side;
    // the "excluded when disabled" side is covered separately below with a minimal
    // in-memory spec, so that case doesn't depend on any fixture's current toggles.
    [Fact]
    public void Discover_routes_covers_every_cardgrid_and_admintable_and_enabled_feature()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var (pub, authed) = VerifyStage.DiscoverRoutes(spec);

        Assert.Contains("/Client/Products", pub);          // cardGrid -> anonymous
        Assert.Contains("/Admin/Products", authed);        // adminTable -> authed
        Assert.Contains("/Client/Catalogues", authed);     // catalogues feature enabled
        Assert.Contains("/Client/Catalogues/Public", pub); // A5 public page -> anonymous, scanned
        Assert.Contains("/Client/Cart", authed);           // orders feature enabled -> scanned
    }

    [Fact]
    public void Discover_routes_excludes_a_disabled_feature_regardless_of_fixture_state()
    {
        // Minimal in-memory spec (no fixture file) so this negative case can never
        // silently stop testing anything just because a fixture's toggles changed.
        var spec = new ProjectSpec
        {
            Views = new ViewsSection { Pages = [] },
            Features = new Dictionary<string, JsonElement>(), // nothing enabled
        };
        var (pub, authed) = VerifyStage.DiscoverRoutes(spec);
        Assert.DoesNotContain("/Client/Cart", authed);
        Assert.DoesNotContain("/Client/Catalogues", authed);
        Assert.DoesNotContain("/Client/Catalogues/Public", pub);
    }

    [Fact]
    public void Discover_routes_dedupes_and_stays_stable()
    {
        var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
        var (pub, authed) = VerifyStage.DiscoverRoutes(spec);
        Assert.Equal(pub.Distinct().Count(), pub.Count);
        Assert.Equal(authed.Distinct().Count(), authed.Count);
    }
}

public class InteractiveSelectionTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "dps-nn-tests-" + Guid.NewGuid());

    public InteractiveSelectionTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() { try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort cleanup */ } }

    [Fact]
    public void Find_candidates_matches_pattern_dedupes_and_sorts()
    {
        File.WriteAllText(Path.Combine(_tempDir, "project-spec.b.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "project-spec.a.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "unrelated.json"), "{}");

        var found = InteractiveSelection.FindCandidates(new[] { _tempDir, _tempDir }, "project-spec*.json");

        Assert.Equal(2, found.Count); // deduped despite the same dir listed twice
        Assert.All(found, f => Assert.Contains("project-spec", Path.GetFileName(f)));
        // sorted by filename: "a" before "b"
        Assert.True(string.Compare(Path.GetFileName(found[0]), Path.GetFileName(found[1]), StringComparison.OrdinalIgnoreCase) < 0);
    }

    [Fact]
    public void Find_candidates_skips_missing_directories_without_throwing()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");
        var found = InteractiveSelection.FindCandidates(new[] { missing }, "*.json");
        Assert.Empty(found);
    }

    [Fact]
    public void Format_menu_lists_candidates_1_indexed()
    {
        var menu = InteractiveSelection.FormatMenu(["a.json", "b.json"], "Pick one:");
        Assert.Contains("1. ", menu);
        Assert.Contains("2. ", menu);
        Assert.Contains("Pick one:", menu);
    }

    [Fact]
    public void Parse_selection_blank_input_picks_first()
    {
        var result = InteractiveSelection.ParseSelection("", ["a.json", "b.json"]);
        Assert.Equal("a.json", result);
    }

    [Fact]
    public void Parse_selection_valid_number_picks_that_entry()
    {
        var result = InteractiveSelection.ParseSelection("2", ["a.json", "b.json"]);
        Assert.Equal("b.json", result);
    }

    [Fact]
    public void Parse_selection_out_of_range_or_non_numeric_returns_null()
    {
        Assert.Null(InteractiveSelection.ParseSelection("99", ["a.json", "b.json"]));
        Assert.Null(InteractiveSelection.ParseSelection("not a number", ["a.json", "b.json"]));
    }

    [Fact]
    public void Parse_selection_with_zero_candidates_returns_null()
    {
        Assert.Null(InteractiveSelection.ParseSelection("1", []));
    }
}

// GitHub issue #5: the pipeline's pure-emission fan-out runs across up to five named worker lanes
// (Кольо, Наско, Иван, Мишо, Киро). These prove the executor's contract without needing a full
// generate: every item runs exactly once, each runs under a named worker, failures propagate, and
// empty input is a no-op.
public class ParallelEmitTests
{
    [Fact]
    public async Task Runs_every_work_item_exactly_once()
    {
        var counter = 0;
        var items = Enumerable.Range(0, 50)
            .Select(_ => ("item", new Func<Task>(() => { Interlocked.Increment(ref counter); return Task.CompletedTask; })))
            .ToList();

        await ParallelEmit.RunAsync(items);

        Assert.Equal(50, counter);
    }

    [Fact]
    public async Task Every_item_runs_under_a_bulgarian_worker_name()
    {
        var seen = new System.Collections.Concurrent.ConcurrentBag<string?>();
        // Each item holds its lane until a second lane has picked up work. With instantly
        // completing items, the first lane the thread pool starts could drain all 30 before
        // another is scheduled - on a loaded or single-CPU machine it always did (30/30 under
        // `taskset -c 0`, changelog 134). If the code ever regresses to one lane, this times out.
        var secondLane = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = Enumerable.Range(0, 30)
            .Select(_ => ("item", new Func<Task>(async () =>
            {
                seen.Add(ParallelEmit.CurrentWorker.Value);
                if (seen.Distinct().Count() > 1) secondLane.TrySetResult();
                await secondLane.Task.WaitAsync(TimeSpan.FromSeconds(10));
            })))
            .ToList();

        await ParallelEmit.RunAsync(items);

        Assert.Equal(30, seen.Count);
        Assert.All(seen, name => Assert.Contains(name, ParallelEmit.WorkerNames));
        // With 30 items across 5 lanes, work spreads beyond a single worker.
        Assert.True(seen.Distinct().Count() > 1, "expected work to spread across multiple named lanes");
    }

    [Fact]
    public void Worker_names_are_the_expected_bulgarian_crew_in_order()
    {
        Assert.Equal(new[] { "Кольо", "Наско", "Иван", "Мишо", "Киро" }, ParallelEmit.WorkerNames);
    }

    [Fact]
    public void English_worker_names_are_the_expected_funny_crew_in_order()
    {
        Assert.Equal(new[] { "Bubba", "Chuck", "Duke", "Rusty", "Slim" }, ParallelEmit.EnglishWorkerNames);
    }

    [Theory]
    [InlineData(null)]      // unset: default to Bulgarian (issue #5's original ask)
    [InlineData("")]
    [InlineData("bg")]
    [InlineData("something-else")]
    public void Resolve_worker_names_defaults_to_bulgarian(string? envValue)
    {
        Assert.Equal(ParallelEmit.WorkerNames, ParallelEmit.ResolveWorkerNames(envValue));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("EN")]
    [InlineData("En")]
    public void Resolve_worker_names_switches_to_english_case_insensitively(string envValue)
    {
        Assert.Equal(ParallelEmit.EnglishWorkerNames, ParallelEmit.ResolveWorkerNames(envValue));
    }

    [Fact]
    public async Task An_item_throwing_surfaces_as_a_thrown_exception()
    {
        var items = new List<(string, Func<Task>)>
        {
            ("ok", () => Task.CompletedTask),
            ("boom", () => throw new InvalidOperationException("emit failed")),
        };

        await Assert.ThrowsAnyAsync<Exception>(() => ParallelEmit.RunAsync(items));
    }

    [Fact]
    public async Task Empty_input_is_a_noop()
    {
        await ParallelEmit.RunAsync([]); // must not throw
    }

    [Theory]
    [InlineData(500, "0.5s")]              // sub-minute: seconds only
    [InlineData(42300, "42.3s")]
    [InlineData(60000, "1m 0s / 60.0s")]   // exactly a minute crosses over
    [InlineData(385593, "6m 25s / 385.6s")] // the value from the real generate run
    public void Human_duration_reads_in_minutes_and_seconds(long ms, string expected)
    {
        Assert.Equal(expected, PipelineRunner.HumanDuration(ms));
    }

    [Fact]
    public async Task Sequential_context_has_no_worker_tag_after_a_parallel_run()
    {
        await ParallelEmit.RunAsync([("item", () => Task.CompletedTask)]);
        // The orchestrator thread never adopts a lane's AsyncLocal value.
        Assert.Null(ParallelEmit.CurrentWorker.Value);
    }
}

// dps-nn's OWN pipeline narration (StageContext.Info/Debug/Error), not the generated app's UI,
// gets translated to Bulgarian for a Bulgarian project via templates/log-bg-map.json - the
// "Bulgarian everything" idea already applied to the parallel worker names (issue #5).
public class LogTranslationTests : IDisposable
{
    // ActionLogPath is CWD-rooted and not injectable, so real StageContext.Info calls really do
    // write logs/<ProjectName>/actions.log.jsonl next to the test binaries - use a distinctive
    // project name and clean up afterward so tests don't leave litter behind (best-effort,
    // matching AppendActionLog's own "never let a logging failure break anything" philosophy).
    private const string ProjectName = "LogTranslationTestProject";
    public void Dispose() { try { Directory.Delete(Path.Combine("logs", ProjectName), recursive: true); } catch { } }

    private static (StageContext ctx, StringWriter console) Arrange(string language)
    {
        var spec = new ProjectSpec { Meta = new MetaSection { ProjectName = ProjectName, Language = language } };
        var variation = VariationResolver.Resolve(spec);
        var state = new StateStore(Path.Combine(Path.GetTempPath(), "dps-nn-tests-" + Guid.NewGuid(), "state.json"));
        var console = new StringWriter();
        var ctx = new StageContext(spec, variation, Directory.GetCurrentDirectory(), Directory.GetCurrentDirectory(), state, console);
        return (ctx, console);
    }

    [Fact]
    public void Bulgarian_project_gets_translated_log_lines()
    {
        var (ctx, console) = Arrange("bg");
        ctx.Info("wrote Models/Product.cs");
        Assert.Contains("записа Models/Product.cs", console.ToString());
        Assert.DoesNotContain("wrote Models/Product.cs", console.ToString());
    }

    [Fact]
    public void English_project_keeps_log_lines_in_english()
    {
        var (ctx, console) = Arrange("en");
        ctx.Info("wrote Models/Product.cs");
        Assert.Contains("wrote Models/Product.cs", console.ToString());
    }

    [Fact]
    public void Longer_fragment_wins_over_a_shorter_one_it_contains()
    {
        // " missing" (generic, used by the codePointer warning) must NOT eat into the longer,
        // more specific "identity map ... missing - Identity pages remain English..." phrase.
        var (ctx, console) = Arrange("bg");
        ctx.Info("WARNING: identity map foo.json missing - Identity pages remain English. Verify WILL fail on them.");
        var output = console.ToString();
        Assert.Contains("липсва - страниците за автентикация остават на английски", output);
        Assert.DoesNotContain(" missing", output);
    }

    [Fact]
    public void Stage_timing_summary_line_translates()
    {
        var (ctx, console) = Arrange("bg");
        ctx.Info("=== stage timings ===");
        Assert.Contains("=== времена на етапите ===", console.ToString());
    }

    [Fact]
    public void Error_messages_are_also_translated()
    {
        var (ctx, console) = Arrange("bg");
        ctx.Error("emit-models FAILED: boom");
        Assert.Contains("emit-models НЕУСПЕШЕН: boom", console.ToString());
    }
}
