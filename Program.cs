using System.Threading.RateLimiting;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using System.Text.Json.Serialization;
using ShoppingPortal.Application;
using ShoppingPortal.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<OtpDeliveryOptions>(builder.Configuration.GetSection("OtpDelivery"));
builder.Services.AddHttpClient();
builder.Services.AddAuthentication("Bearer").AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("client", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Client:Origins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("otp", limiter =>
{
    limiter.PermitLimit = 5;
    limiter.Window = TimeSpan.FromMinutes(10);
    limiter.QueueLimit = 0;
}));
builder.Services.AddSingleton<IOtpStore, InMemoryOtpStore>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<ICodeDelivery, ConfiguredCodeDelivery>();
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<ICatalogService, CatalogService>();

var app = builder.Build();

Process? spaProcess = null;
var spaUrl = builder.Configuration["Spa:DevelopmentServerUrl"] ?? "http://localhost:5173";
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Spa:StartOnBackendLaunch"))
{
    var alreadyRunning = false;
    try
    {
        using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        using var response = await probe.GetAsync(spaUrl);
        alreadyRunning = response.IsSuccessStatusCode;
    }
    catch (HttpRequestException) { }
    catch (TaskCanceledException) { }

    if (alreadyRunning)
    {
        app.Logger.LogInformation("Using the React development server already running at {Url}", spaUrl);
    }
    else
    {
        var spaWorkingDirectory = Path.GetFullPath(
            builder.Configuration["Spa:WorkingDirectory"] ?? "../ShoppingPortal.Client",
            builder.Environment.ContentRootPath);
        if (!Directory.Exists(spaWorkingDirectory))
            throw new DirectoryNotFoundException($"React project folder was not found: {spaWorkingDirectory}");

        var startInfo = new ProcessStartInfo
        {
            FileName = builder.Configuration["Spa:Command"] ?? "npm",
            WorkingDirectory = spaWorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in (builder.Configuration["Spa:Arguments"] ?? "run dev -- --host 127.0.0.1 --port 5173 --strictPort")
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            startInfo.ArgumentList.Add(argument);

        spaProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the React development server.");
        spaProcess.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null) app.Logger.LogInformation("[React] {Output}", eventArgs.Data);
        };
        spaProcess.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null) app.Logger.LogWarning("[React] {Output}", eventArgs.Data);
        };
        spaProcess.BeginOutputReadLine();
        spaProcess.BeginErrorReadLine();
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                if (spaProcess is { HasExited: false }) spaProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        app.Logger.LogInformation("Started React development server from {WorkingDirectory} at {Url}", spaWorkingDirectory, spaUrl);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    if (app.Environment.IsDevelopment()
        && builder.Configuration.GetValue<bool>("Spa:StartOnBackendLaunch")
        && context.Request.Path == "/")
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(30) && !context.RequestAborted.IsCancellationRequested)
        {
            if (spaProcess is { HasExited: true })
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("The React dev server exited. Check the backend log and run npm install in ShoppingPortal.Client.");
                return;
            }

            try
            {
                using var response = await client.GetAsync(spaUrl, context.RequestAborted);
                if (response.IsSuccessStatusCode)
                {
                    context.Response.Redirect(spaUrl);
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested) { }

            await Task.Delay(300, context.RequestAborted);
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsync("React did not become available at http://localhost:5173 within 30 seconds.", context.RequestAborted);
        return;
    }
    await next();
});

app.UseCors("client");
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
