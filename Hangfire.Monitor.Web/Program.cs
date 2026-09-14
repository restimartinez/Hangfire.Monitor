using Hangfire.Monitor.Domain;
using Hangfire.Monitor.Web.Configuration;
using Hangfire.Monitor.Infrastructure.Storage;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddOptions<HangfireMonitorOptions>()
    .Bind(builder.Configuration.GetSection("HangfireMonitor"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<HangfireMonitorOptions>, HangfireMonitorOptionsValidator>();
builder.Services.AddSingleton<SqlServerStorageFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
