using Microsoft.EntityFrameworkCore;
using JeoMTT.Data;
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

// Add session archive service
builder.Services.AddScoped<ISessionArchiveService, SessionArchiveService>();

// Add hosted background service for archiving expired sessions every 10 minutes
builder.Services.AddHostedService<SessionArchiveHostedService>();

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

app.UseAuthorization();

app.MapStaticAssets();

// Map SignalR hub
app.MapHub<GameSessionHub>("/gamehub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
