.PHONY: restore build test publish

SOLUTION = Kanban.sln
WEB_PROJ = src/Kanban.Web/Kanban.Web.csproj
RUNNER_PROJ = src/Kanban.Runner/Kanban.Runner.csproj
PUBLISH_DIR = ./artifacts/publish

restore:
	dotnet restore $(SOLUTION)

build:
	dotnet build $(SOLUTION) --no-restore

test:
	dotnet test $(SOLUTION) --no-build

publish:
	dotnet publish $(WEB_PROJ) -c Release --no-build -o $(PUBLISH_DIR)/Kanban.Web
	dotnet publish $(RUNNER_PROJ) -c Release --no-build -o $(PUBLISH_DIR)/Kanban.Runner