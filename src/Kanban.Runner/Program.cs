using Kanban.Core;
using Kanban.Runner;
using Kanban.Runner.Options;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection(RunnerOptions.SectionName));

builder.Services.AddDbContext<KanbanDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Kanban")));

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(o => o.ServiceName = "Kanban Runner");

var host = builder.Build();
host.Run();