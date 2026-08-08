using Kanban.Core;
using Kanban.Runner;
using Kanban.Runner.Agents;
using Kanban.Runner.Git;
using Kanban.Runner.Options;
using Kanban.Runner.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection(RunnerOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Kanban")!;
builder.Services.AddDbContext<KanbanDbContext>(options =>
{
    if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
});

builder.Services.AddSingleton<IGitOperations, GitCli>();
builder.Services.AddSingleton<IAgentProcess, CrushAgentProcess>();
builder.Services.AddScoped<ICardQueue, SqlCardQueue>();
builder.Services.AddScoped<StartupReconciler>();
builder.Services.AddScoped<CardRunner>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(o => o.ServiceName = "Kanban Runner");

var host = builder.Build();
host.Run();