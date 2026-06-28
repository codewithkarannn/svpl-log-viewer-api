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

builder.Services.AddScoped<ILogFileService, LogFileService>();
builder.Services.AddHostedService<LogWatcherService>();
builder.Services.AddControllers();
builder.Services.AddSignalR();
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
app.MapHub<LogHub>("/hubs/logs");
app.UseAuthorization();

app.MapControllers();

app.Run();