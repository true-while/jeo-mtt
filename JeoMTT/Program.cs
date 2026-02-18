using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using JeoMTT.Data;
using JeoMTT.Models;
using JeoMTT.Services;
using JeoMTT.HostedServices;
using JeoMTT.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add Application Insights only if InstrumentationKey is configured
var instrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"];
if (!string.IsNullOrEmpty(instrumentationKey))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// Add DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<JeoGameDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<JeoGameDbContext>()
.AddDefaultTokenProviders();

// Configure authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/User/Login";
    options.LogoutPath = "/User/LogoutConfirm";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Add session archive service
builder.Services.AddScoped<ISessionArchiveService, SessionArchiveService>();

// Add hosted background service for archiving expired sessions every 10 minutes
// Only register in non-development environments to avoid running during local testing
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<SessionArchiveHostedService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Map SignalR hub
app.MapHub<GameSessionHub>("/gamehub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
