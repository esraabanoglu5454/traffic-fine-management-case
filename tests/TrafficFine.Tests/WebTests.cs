using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrafficFine.Web.Data;
using TrafficFine.Web.Domain;
using Xunit;

namespace TrafficFine.Tests;

public sealed class TestApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            var databaseName = Guid.NewGuid().ToString();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }
    public async Task Seed()
    {
        using var scope = Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var role in new[] { Roles.Operations, Roles.Manager, Roles.Finance })
        {
            await roles.CreateAsync(new IdentityRole(role));
            var user = new AppUser { Id = role, Email = role + "@test.local", UserName = role + "@test.local", DisplayName = role };
            var created = await users.CreateAsync(user, "Test-Only!12345");
            if (!created.Succeeded) throw new InvalidOperationException("Test user creation failed.");
            await users.AddToRoleAsync(user, role);
        }
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Vehicles.Add(new Vehicle { Id = 1, Plate = "34TEST01", Brand = "Ford", Model = "Focus" });
        db.Fines.Add(new FineRecord { Id = 1, Reference = "WEB-001", VehicleId = 1, CreatedById = Roles.Operations,
            CreatedAt = DateTimeOffset.UtcNow, Amount = 1500, ViolationDate = new DateOnly(2026, 1, 1), Description = "Test description" });
        await db.SaveChangesAsync();
    }
}
public sealed class WebTests : IAsyncLifetime
{
    private readonly TestApp app = new();
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await app.Seed();
    }
    public async Task DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
    private static string Hidden(string html, string name) => WebUtility.HtmlDecode(
        Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private async Task Login(string role)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = role + "@test.local", ["Password"] = "Test-Only!12345", ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }
    [Fact]
    public async Task Anonymous_user_is_redirected_to_login()
    {
        var response = await client.GetAsync("/Fines");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }
    [Fact]
    public async Task Unsafe_post_without_antiforgery_token_returns_400()
    {
        await Login(Roles.Operations);
        var response = await client.PostAsync("/Fines/Decide", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = "1", ["Action"] = "Submitted" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Manager_cannot_access_operations_create_screen()
    {
        await Login(Roles.Manager);
        var response = await client.GetAsync("/Vehicles/Create");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
    }
    [Fact]
    public async Task Authenticated_MVC_screens_render_without_server_error()
    {
        await Login(Roles.Operations);
        foreach (var path in new[] { "/", "/Vehicles", "/Vehicles/Create", "/Fines", "/Fines/Create", "/Fines/Details/1", "/Fines/Edit/1" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("<html lang=\"tr\">", html);
            var output = Environment.GetEnvironmentVariable("CASE_RENDER_DIR");
            if (output is not null)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, (path == "/" ? "Home" : path.Replace('/', '_').Trim('_')) + ".html"), html);
            }
        }
    }
    [Fact]
    public async Task Turkish_decimal_input_is_saved_correctly()
    {
        await Login(Roles.Operations);
        var html = await client.GetStringAsync("/Fines/Create");
        var response = await client.PostAsync("/Fines/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Reference"] = "WEB-002", ["VehicleId"] = "1", ["ViolationDate"] = "2026-01-01",
            ["Amount"] = "1500,50", ["Description"] = "New test fine", ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1500.50m, (await db.Fines.SingleAsync(x => x.Reference == "WEB-002")).Amount);
    }
    [Fact]
    public async Task Forged_finance_approval_by_operations_changes_no_record()
    {
        await Login(Roles.Operations);
        var html = await client.GetStringAsync("/Fines/Details/1");
        var response = await client.PostAsync("/Fines/Decide", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "1", ["Version"] = Hidden(html, "Version"), ["Action"] = "FinanceApproved",
            ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken")
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(FineStatus.New, (await db.Fines.SingleAsync()).Status);
        Assert.Equal(0, await db.ApprovalHistory.CountAsync());
    }
    [Fact]
    public async Task Login_return_url_cannot_redirect_to_external_site()
    {
        var html = await client.GetStringAsync("/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "Operations@test.local", ["Password"] = "Test-Only!12345", ["ReturnUrl"] = "https://example.invalid/",
            ["__RequestVerificationToken"] = Hidden(html, "__RequestVerificationToken")
        }));
        Assert.Equal("/", response.Headers.Location?.ToString());
    }
}
