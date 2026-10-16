using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure SQLite Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=bibliotheca.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 2. Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Configure Authentication Cookie Paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
});

// 4. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// 5. Shared circulation rules + Public Self-Service Kiosk (Achintha)
builder.Services.AddScoped<ICirculationService, CirculationService>();
builder.Services.Configure<KioskOptions>(builder.Configuration.GetSection(KioskOptions.SectionName));

// [Moon] CSV bulk importer (Admin)
builder.Services.AddScoped<Bibliotheca.Services.CsvImportService>();

// Reservation summary shown on the kiosk account page.
// Replace NoReservationSummaryProvider with the reservation module's implementation when it is ready.
builder.Services.AddScoped<IReservationSummaryProvider, NoReservationSummaryProvider>();

// Kiosk patron sessions are kept in server memory (separate from the staff login cookie).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    var kiosk = builder.Configuration.GetSection(KioskOptions.SectionName).Get<KioskOptions>() ?? new KioskOptions();
    options.Cookie.Name = ".Bibliotheca.Kiosk";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IdleTimeout = TimeSpan.FromSeconds(Math.Max(60, kiosk.IdleTimeoutSeconds + kiosk.IdleWarningSeconds + 30));
});

var app = builder.Build();

// 6. Auto-Initialize & Seed Database on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitializer.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// 7. HTTP Request Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Required by the Self-Service Kiosk

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Catalog}/{action=Index}/{id?}");

app.Run();
