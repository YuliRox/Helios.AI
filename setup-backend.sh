#!/bin/bash

# LumiRise Backend Setup Script
# This script scaffolds the .NET 10 backend project structure

set -e  # Exit on any error

echo "=========================================="
echo "LumiRise Backend Project Setup"
echo "=========================================="

# Configuration
SOLUTION_NAME="LumiRise"
PROJECT_NAME="LumiRise.Api"
TEST_PROJECT_NAME="LumiRise.Tests"
INTEGRATION_TEST_PROJECT_NAME="LumiRise.IntegrationTests"

# Check .NET SDK version
echo ""
echo "[1/11] Checking .NET SDK..."
dotnet --version

# Create .gitignore
echo ""
echo "[2/11] Creating .gitignore file..."
dotnet new gitignore

# Create solution
echo ""
echo "[3/11] Creating solution..."
dotnet new sln -n "$SOLUTION_NAME" -o src

# Create main API project
echo ""
echo "[4/11] Creating ASP.NET Core Web API project..."
dotnet new webapi -n "$PROJECT_NAME" -o "src/$PROJECT_NAME" --framework net10.0

# Create unit test project
echo ""
echo "[5/11] Creating unit test project..."
dotnet new xunit -n "$TEST_PROJECT_NAME" -o "src/$TEST_PROJECT_NAME" --framework net10.0

# Create integration test project
echo ""
echo "[6/11] Creating integration test project..."
dotnet new xunit -n "$INTEGRATION_TEST_PROJECT_NAME" -o "src/$INTEGRATION_TEST_PROJECT_NAME" --framework net10.0

# Add projects to solution
echo ""
echo "[7/11] Adding projects to solution..."
dotnet sln src/$SOLUTION_NAME.sln add "src/$PROJECT_NAME/$PROJECT_NAME.csproj"
dotnet sln src/$SOLUTION_NAME.sln add "src/$TEST_PROJECT_NAME/$TEST_PROJECT_NAME.csproj"
dotnet sln src/$SOLUTION_NAME.sln add "src/$INTEGRATION_TEST_PROJECT_NAME/$INTEGRATION_TEST_PROJECT_NAME.csproj"

# Add project references for test projects
echo ""
echo "[8/11] Adding project references..."
dotnet add "src/$TEST_PROJECT_NAME/$TEST_PROJECT_NAME.csproj" reference "src/$PROJECT_NAME/$PROJECT_NAME.csproj"
dotnet add "src/$INTEGRATION_TEST_PROJECT_NAME/$INTEGRATION_TEST_PROJECT_NAME.csproj" reference "src/$PROJECT_NAME/$PROJECT_NAME.csproj"

# Add NuGet packages to main project
echo ""
echo "[9/11] Adding NuGet packages to API project..."
cd "src/$PROJECT_NAME"

# MQTT
dotnet add package MQTTnet

# Database / EF Core
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design

# Scheduling
dotnet add package Hangfire.Core
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql

# Logging
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File

# OpenAPI / Swagger (should be included but ensure latest)
dotnet add package Swashbuckle.AspNetCore

cd ../..

# Add test packages
echo ""
echo "[10/11] Adding test packages..."
cd "src/$TEST_PROJECT_NAME"
dotnet add package Moq
dotnet add package AwesomeAssertions
cd ../..

cd "src/$INTEGRATION_TEST_PROJECT_NAME"
dotnet add package Moq
dotnet add package AwesomeAssertions
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Testcontainers.PostgreSql
dotnet add package Testcontainers.Mosquitto
cd ../..

# Create folder structure in main project
echo ""
echo "[11/11] Creating folder structure..."

# API project folders
mkdir -p "src/$PROJECT_NAME/Controllers"
mkdir -p "src/$PROJECT_NAME/Services/Alarm"
mkdir -p "src/$PROJECT_NAME/Services/Mqtt"
mkdir -p "src/$PROJECT_NAME/Data/Entities"
mkdir -p "src/$PROJECT_NAME/Data/Repositories"
mkdir -p "src/$PROJECT_NAME/Models/Requests"
mkdir -p "src/$PROJECT_NAME/Models/Responses"
mkdir -p "src/$PROJECT_NAME/Configuration"

# Create .gitkeep files to preserve empty directories
touch "src/$PROJECT_NAME/Controllers/.gitkeep"
touch "src/$PROJECT_NAME/Services/Alarm/.gitkeep"
touch "src/$PROJECT_NAME/Services/Mqtt/.gitkeep"
touch "src/$PROJECT_NAME/Data/Entities/.gitkeep"
touch "src/$PROJECT_NAME/Data/Repositories/.gitkeep"
touch "src/$PROJECT_NAME/Models/Requests/.gitkeep"
touch "src/$PROJECT_NAME/Models/Responses/.gitkeep"
touch "src/$PROJECT_NAME/Configuration/.gitkeep"

echo ""
echo "=========================================="
echo "Setup complete!"
echo "=========================================="
echo ""
echo "Project structure created:"
echo "  .gitignore"
echo "  src/"
echo "  ├── $SOLUTION_NAME.sln"
echo "  ├── $PROJECT_NAME/"
echo "  │   ├── Controllers/"
echo "  │   ├── Services/"
echo "  │   │   ├── Alarm/"
echo "  │   │   └── Mqtt/"
echo "  │   ├── Data/"
echo "  │   │   ├── Entities/"
echo "  │   │   └── Repositories/"
echo "  │   ├── Models/"
echo "  │   │   ├── Requests/"
echo "  │   │   └── Responses/"
echo "  │   └── Configuration/"
echo "  ├── $TEST_PROJECT_NAME/"
echo "  └── $INTEGRATION_TEST_PROJECT_NAME/"
echo ""
echo "Next steps:"
echo "  1. cd src && dotnet restore"
echo "  2. dotnet build"
echo "  3. Create Docker and docker-compose files"
echo ""
