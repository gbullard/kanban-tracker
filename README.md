# Kanban Tracker

An AI-powered kanban board for managing development tasks. Drag cards between columns, and an AI agent runner picks up "Ready" cards, creates git branches, runs [Crush](https://github.com/crush-ai/crush) to implement them, and reports results — all with live log streaming.

## Features

- **Kanban board** — five-column workflow (New, Ready, In Progress, Review, Completed) with drag-and-drop
- **AI card runner** — Windows service that claims Ready cards, creates git branches, invokes Crush with a composed prompt, and classifies outcomes
- **Live run logs** — per-card streaming log viewer on the detail page
- **Project management** — associate cards with git repositories, toggle active projects
- **Theme support** — Tokyo Night, Dracula, Catppuccin, and Nord themes

## Tech Stack

- **Backend:** ASP.NET Core 8, Entity Framework Core
- **Database:** SQL Server (primary) or SQLite (optional)
- **Frontend:** HTMX, SortableJS, CSS custom properties
- **Runner:** .NET 8 Worker Service (Windows Service)

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (any edition) or SQLite

### Setup

```bash
git clone <repo-url>
cd kanban-tracker

# Set your connection string (SQL Server default)
set KANBAN_CONNECTION_STRING=Server=localhost;Database=KanbanBoard;Trusted_Connection=True;TrustServerCertificate=True
# Or use SQLite:
# set KANBAN_CONNECTION_STRING=Data Source=kanban.db

make restore
make build

# Create the database
dotnet ef database update --project src/Kanban.Core --startup-project src/Kanban.Web

# Start the web app
dotnet run --project src/Kanban.Web
```

Open `http://localhost:28763` in your browser.

### Running the AI Runner (optional)

The runner invokes [Crush](https://github.com/crush-ai/crush) to process cards. See [docs/deployment.md](docs/deployment.md) for full setup.

```bash
dotnet run --project src/Kanban.Runner
```

## Deployment

See [docs/deployment.md](docs/deployment.md) for IIS + Windows Service deployment instructions.

## License

MIT