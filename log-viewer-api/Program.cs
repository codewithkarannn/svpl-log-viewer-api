using System.Text.Json;
using log_viewer_api.Hubs;
using log_viewer_api.Interfaces;
using log_viewer_api.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors( options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Angular URL
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval     = TimeSpan.FromSeconds(15);
        options.MaximumReceiveMessageSize = 64 * 1024; // 64KB cap per message
    })
    .AddJsonProtocol(opts =>
    {
        opts.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });


builder.Services.AddSingleton<ILogFileService, LogFileService>();
builder.Services.AddSingleton<LogWatcherService>();
builder.Services.AddControllers();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("AngularPolicy");
app.MapHub<LogStreamHub>("/hubs/logs");
app.UseAuthorization();

app.MapControllers();

app.Run();