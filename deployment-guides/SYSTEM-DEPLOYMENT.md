# System Deployment Guide

> **Generic/Abstract** — Replace `{YourEntity}` and `{YourSystem}` with your actual domain (Products, Orders, Invoices, Deliveries, etc.).

---

## What This Guide Covers

Deploying a single .NET CRUD system that can integrate with other systems via shared JWT authentication. Use this template for Operational System, Delivery System, or any other custom system.

**Architecture at a glance:**

```
┌──────────────────────┐       ┌────────────────────────┐
│   React (Vercel)     │──────▶│  .NET API (Azure CA)   │
│   VITE_API_URL       │ JWT   │  JWT validation         │
│                      │◀──────│  CRUD endpoints         │
└──────────────────────┘       │  Integration endpoints  │
                               │  SystemClient           │
                               └────────┬───────────────┘
                                        │ JWT (service acct)
                                        ▼
                               ┌────────────────────────┐
                               │  Auth Service (shared)  │
                               │  POST /api/auth/login   │
                               └────────────────────────┘
```

---

## Prerequisites

| Item | Purpose |
|---|---|
| **GitHub account** | Source control + Actions CI/CD |
| **Vercel account** | Frontend hosting |
| **Azure subscription** | Backend + database hosting |
| **Git** | Clone repos, commit changes |
| **Azure CLI** (`az`) | Create Azure resources |
| **Docker Desktop** | Build container images locally |
| **Node.js 18+** | Build frontend |
| **.NET SDK 8 or 9** | Build backend |
| **jq** (optional) | Parse JSON in terminal tests |

---

## Phase 1: Set Up Your Project

Your project should have this structure:

```
{your-system}/
├── {YourSystem}.Api/              # .NET Web API
│   ├── Controllers/
│   │   ├── {YourEntity}Controller.cs   # CRUD endpoints
│   │   └── IntegrationController.cs    # For other systems
│   ├── Models/
│   │   └── {YourEntity}.cs
│   ├── Data/
│   │   └── AppDbContext.cs
│   ├── Services/
│   │   ├── I{YourEntity}Service.cs
│   │   ├── {YourEntity}Service.cs
│   │   └── OtherSystemClient.cs        # SystemClient for calling others
│   ├── Dtos/
│   │   └── {YourEntity}Dto.cs
│   ├── Program.cs
│   └── Dockerfile
└── {your-frontend}/               # React + Vite
    ├── src/
    ├── package.json
    └── vercel.json
```

### 1.1 Program.cs — JWT + CORS + DI

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// CORS — required so your Vercel frontend can call Azure backend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT validation — tokens come from Auth Service
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "CentralAuth",
            ValidAudience = "InternalSystems",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// Register your services
builder.Services.AddScoped<I{YourEntity}Service, {YourEntity}Service>();

// Register SystemClient for calling other systems (optional)
// builder.Services.AddHttpClient<IOtherSystemClient, OtherSystemClient>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware pipeline
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// Auto-create database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
```

### 1.2 appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database={yourdb};Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!",
    "Issuer": "CentralAuth",
    "Audience": "InternalSystems"
  },
  "AuthService": {
    "BaseUrl": "https://auth-backend.abc123.southeastasia.azurecontainerapps.io"
  },
  "ExternalSystems": {
    "{OtherSystem}": {
      "BaseUrl": "https://{other-system}.azurecontainerapps.io",
      "ServiceAccountEmployeeNumber": "SVC-{YOURSYSTEM}",
      "ServiceAccountPassword": "{your-service-account-password}"
    }
  }
}
```

> **Concrete example:**
> ```json
> {
>   "ExternalSystems": {
>     "Delivery": {
>       "BaseUrl": "https://delivery-api.abc123.southeastasia.azurecontainerapps.io",
>       "ServiceAccountEmployeeNumber": "SVC-OPERATIONAL",
>       "ServiceAccountPassword": "p@ssw0rd-d3l1v3ry"
>     }
>   }
> }
> ```

### 1.3 AppDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;
using {YourSystem}.Api.Models;

namespace {YourSystem}.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<{YourEntity}> {YourEntities} { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<{YourEntity}>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            // Add other property configurations as needed
        });
    }
}
```

### 1.4 {YourEntity}.cs (Model)

```csharp
namespace {YourSystem}.Api.Models;

public class {YourEntity}
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### 1.5 {YourEntity}Controller.cs (CRUD)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace {YourSystem}.Api.Controllers;

[ApiController]
[Route("api/{your-entities}")]
[Authorize]
public class {YourEntity}Controller : ControllerBase
{
    private readonly I{YourEntity}Service _service;

    public {YourEntity}Controller(I{YourEntity}Service service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] {YourEntity}Dto dto)
    {
        var item = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] {YourEntity}Dto dto)
    {
        var item = await _service.UpdateAsync(id, dto);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }
}
```

### 1.6 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "{YourSystem}.Api.dll"]
```

> **Note:** For .NET 8, replace `9.0` with `8.0`.

### 1.7 vercel.json (Frontend)

```json
{
  "rewrites": [
    { "source": "/(.*)", "destination": "/index.html" }
  ]
}
```

This ensures client-side routing works for all paths in your SPA.

---

## Phase 2: Set Up Azure Resources

Use a **unique suffix** (e.g., your initials + numbers) for all resource names below.

### 2.1 Login and Create Resource Group

```bash
az login
az group create --name {yoursys}-app --location southeastasia
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.ContainerRegistry
```

> **For SQL Server**, add: `az provider register --namespace Microsoft.Sql`

### 2.2 Create Database Server

**Option A — PostgreSQL (recommended):**

```bash
az postgres flexible-server create `
  --name {yoursys}-db-{suffix} `
  --resource-group {yoursys}-app `
  --location southeastasia `
  --admin-user postgres `
  --admin-password MyStr0ngP@ss! `
  --sku-name Standard_B1ms `
  --tier Burstable `
  --storage-size 32 `
  --public-access 0.0.0.0
```

**Option B — SQL Server:**

```bash
az sql server create `
  --name {yoursys}-sql-{suffix} `
  --resource-group {yoursys}-app `
  --location southeastasia `
  --admin-user postgres `
  --admin-password MyStr0ngP@ss!

az sql db create `
  --name {yoursys}db `
  --resource-group {yoursys}-app `
  --server {yoursys}-sql-{suffix} `
  --service-objective Basic
```

### 2.3 Get Connection String

**PostgreSQL:**

```bash
az postgres flexible-server show-connection-string `
  --server {yoursys}-db-{suffix} `
  --database postgres
```

Take the `ado.net` value, replace `{login}` → `postgres`, `{password}` → `MyStr0ngP@ss!`.

**Concrete result:**
```
Host={yoursys}-db-{suffix}.postgres.database.azure.com;Database=postgres;Username=postgres;Password=MyStr0ngP@ss!;SSL Mode=Require
```

### 2.4 Configure Database Firewall (for local development)

```bash
az postgres flexible-server firewall-rule create `
  --rule-name AllowAllAzure `
  --resource-group {yoursys}-app `
  --server {yoursys}-db-{suffix} `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 255.255.255.255
```

### 2.5 Create Container Registry

```bash
az acr create `
  --name {yoursys}registry{suffix} `
  --resource-group {yoursys}-app `
  --location southeastasia `
  --sku Standard `
  --admin-enabled true
```

Save the credentials — you'll need them for GitHub Actions:

```bash
az acr credential show --name {yoursys}registry{suffix} --resource-group {yoursys}-app
```

### 2.6 Create Container Apps Environment

```bash
az containerapp env create `
  --name {yoursys}-env `
  --resource-group {yoursys}-app `
  --location southeastasia
```

### 2.7 Deploy Your Backend

**A. Create the container app:**

```bash
az containerapp create `
  --name {yoursys}-backend `
  --resource-group {yoursys}-app `
  --environment {yoursys}-env `
  --image mcr.microsoft.com/dotnet/samples:aspnetapp `
  --target-port 8080 `
  --ingress external `
  --min-replicas 0 `
  --max-replicas 10
```

> This creates the app with a placeholder image. The actual image will be pushed via CI/CD.

**B. Set database secret:**

```bash
az containerapp secret set `
  --name {yoursys}-backend `
  --resource-group {yoursys}-app `
  --secrets dbconn="Host={yoursys}-db-{suffix}.postgres.database.azure.com;Database=postgres;Username=postgres;Password=MyStr0ngP@ss!;SSL Mode=Require"
```

**C. Set environment variables:**

```bash
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars ASPNETCORE_ENVIRONMENT=Production
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars ASPNETCORE_URLS=http://0.0.0.0:8080
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars ConnectionStrings__DefaultConnection=secretref:dbconn
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars Jwt__Key=ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars Jwt__Issuer=CentralAuth
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars Jwt__Audience=InternalSystems
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app `
  --set-env-vars AuthService__BaseUrl=https://auth-backend.abc123.southeastasia.azurecontainerapps.io
```

> **Tip:** You can also do all env vars in one command by repeating `--set-env-vars`, but the Azure CLI only accepts one per call. A script is recommended.

### 2.8 Get Backend URL

```bash
az containerapp show `
  --name {yoursys}-backend `
  --resource-group {yoursys}-app `
  --query "properties.configuration.ingress.fqdn" `
  --output tsv
```

**Concrete result:**
```
{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io
```

---

## Phase 3: Set Up Vercel (Frontend)

1. Go to https://vercel.com → **Add New** → **Project**
2. Import your GitHub repository
3. **Root Directory**: select your frontend subdirectory (e.g., `{your-frontend}/`)
4. **Framework Preset**: Vite (auto-detected)
5. **Environment Variables** (include `https://`):
   - `VITE_API_URL` → `https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io`
   - `VITE_AUTH_URL` → `https://auth-backend.abc123.southeastasia.azurecontainerapps.io`
6. **Build Command**: `npm run build` (default)
7. **Output Directory**: `dist` (default)
8. Click **Deploy**

> Every push to your main branch will auto-deploy to Vercel.

### Vercel project IDs (for CI/CD)

After deploying once, retrieve these from the Vercel dashboard:

| Variable | Where to find |
|---|---|
| `VERCEL_ORG_ID` | Vercel Dashboard → Settings → General → **ID** |
| `VERCEL_PROJECT_ID` | Vercel Dashboard → Project → Settings → General → **Project ID** |
| `VERCEL_TOKEN` | Vercel Dashboard → Settings → Tokens → **Create Token** |

---

## Phase 4: Configure GitHub Secrets

These secrets are used by the GitHub Actions workflow to deploy to Azure and Vercel.

### 4.1 Create Azure Service Principal

```bash
az ad sp create-for-rbac `
  --name "{yoursys}-github" `
  --role contributor `
  --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/{yoursys}-app `
  --sdk-auth
```

This outputs JSON like:

```json
{
  "clientId": "...",
  "clientSecret": "...",
  "subscriptionId": "...",
  "tenantId": "..."
}
```

Copy the entire JSON object — that's your `AZURE_CREDENTIALS` secret.

### 4.2 Add Secrets to GitHub

Go to your repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**.

| Secret | Value |
|---|---|
| `AZURE_CREDENTIALS` | Full JSON from `az ad sp create-for-rbac` |
| `AZURE_REGISTRY_NAME` | `{yoursys}registry{suffix}` |
| `AZURE_REGISTRY_USERNAME` | Username from `az acr credential show` |
| `AZURE_REGISTRY_PASSWORD` | Password from `az acr credential show` |
| `AZURE_RESOURCE_GROUP` | `{yoursys}-app` |
| `AZURE_CONTAINER_APP` | `{yoursys}-backend` |
| `VERCEL_TOKEN` | From Vercel account settings |
| `VERCEL_ORG_ID` | From Vercel project settings |
| `VERCEL_PROJECT_ID` | From Vercel project settings |

---

## Phase 5: CI/CD — GitHub Actions Workflow

Create `.github/workflows/deploy.yml` in your repository root:

```yaml
name: Deploy {YourSystem}

on:
  push:
    branches: [main]
    paths-ignore:
      - "**.md"
      - ".github/**"
  workflow_dispatch:

env:
  DOTNET_VERSION: "9.0"
  CONTAINER_APP_NAME: "{yoursys}-backend"
  RESOURCE_GROUP: "{yoursys}-app"
  CONTAINER_REGISTRY: "{yoursys}registry{suffix}.azurecr.io"
  IMAGE_NAME: "{yoursys}-backend"

jobs:
  build-and-deploy-backend:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: ./{YourSystem}.Api

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal

      - name: Azure Login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Login to ACR
        run: az acr login --name ${{ secrets.AZURE_REGISTRY_NAME }}

      - name: Build and Push Container
        run: |
          docker build -t ${{ env.CONTAINER_REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }} -t ${{ env.CONTAINER_REGISTRY }}/${{ env.IMAGE_NAME }}:latest .
          docker push ${{ env.CONTAINER_REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
          docker push ${{ env.CONTAINER_REGISTRY }}/${{ env.IMAGE_NAME }}:latest

      - name: Deploy to Azure Container Apps
        run: |
          az containerapp update \
            --name ${{ env.CONTAINER_APP_NAME }} \
            --resource-group ${{ env.RESOURCE_GROUP }} \
            --image ${{ env.CONTAINER_REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }} \
            --set-env-vars ASPNETCORE_ENVIRONMENT=Production

  deploy-frontend:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: ./{your-frontend}

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 18

      - name: Install Dependencies
        run: npm ci

      - name: Build
        run: npm run build

      - name: Deploy to Vercel
        uses: amondnet/vercel-action@v25
        with:
          vercel-token: ${{ secrets.VERCEL_TOKEN }}
          vercel-org-id: ${{ secrets.VERCEL_ORG_ID }}
          vercel-project-id: ${{ secrets.VERCEL_PROJECT_ID }}
          vercel-args: "--prod"
          working-directory: ./
```

---

## Phase 6: Test Your System

### 6.1 Test CRUD Endpoints

```bash
# 1. Login to get a JWT token
TOKEN=$(curl -s -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"admin","password":"admin123"}' | jq -r '.accessToken')

echo "Token: $TOKEN"

# 2. Create an item
curl -X POST https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io/api/{your-entities} \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Sample Item","description":"A test item for deployment verification","price":9.99}'

# 3. List all items
curl -s https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io/api/{your-entities} \
  -H "Authorization: Bearer $TOKEN" | jq

# 4. Get a specific item (replace {id})
curl -s https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io/api/{your-entities}/{id} \
  -H "Authorization: Bearer $TOKEN" | jq

# 5. Update an item
curl -X PUT https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io/api/{your-entities}/{id} \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Updated Item","description":"Updated description","price":19.99}'

# 6. Delete an item
curl -X DELETE https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io/api/{your-entities}/{id} \
  -H "Authorization: Bearer $TOKEN"
```

### 6.2 Test Without a Token (should get 401)

```bash
curl -s -o /dev/null -w "%{http_code}" https://{yoursys}-backend.abc123.southeastasia.azurecontainerapps.io/api/{your-entities}
# Expected: 401
```

### 6.3 Test the Frontend

Open the Vercel URL in a browser. You should see your application's login page.

---

## Phase 7: Integration — How to Connect Your System with Others

This is the core section. It covers both directions: calling other systems, and letting other systems call you.

### 7.1 How to Call Another System's API

You need a **SystemClient** — a class that logs in as a service account and calls the other system's endpoints.

#### Step 1: Configure the other system in appsettings.json

```json
{
  "ExternalSystems": {
    "Delivery": {
      "BaseUrl": "https://delivery-api.abc123.southeastasia.azurecontainerapps.io",
      "ServiceAccountEmployeeNumber": "SVC-{YOURSYSTEM}",
      "ServiceAccountPassword": "{password-from-other-team}"
    }
  }
}
```

#### Step 2: Create the DTO for the other system's data

```csharp
// Dtos/OtherEntityDto.cs
namespace {YourSystem}.Api.Dtos;

public class OtherEntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

#### Step 3: Create the SystemClient class

```csharp
// Services/OtherSystemClient.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace {YourSystem}.Api.Services;

public interface IOtherSystemClient
{
    Task<List<OtherEntityDto>> GetAllAsync();
    Task<OtherEntityDto?> GetByIdAsync(Guid id);
}

public class OtherSystemClient : IOtherSystemClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ExternalSystemsOptions> _options;
    private string? _cachedToken;
    private DateTime _tokenExpiresAt;

    public OtherSystemClient(HttpClient httpClient, IOptions<ExternalSystemsOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    private async Task<string> GetTokenAsync()
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        var config = _options.Value.Delivery;
        var loginPayload = new
        {
            employeeNumber = config.ServiceAccountEmployeeNumber,
            password = config.ServiceAccountPassword
        };

        var response = await _httpClient.PostAsync(
            $"{config.AuthBaseUrl}/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<LoginResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _cachedToken = result!.AccessToken;
        _tokenExpiresAt = DateTime.UtcNow.AddMinutes(55);

        return _cachedToken;
    }

    public async Task<List<OtherEntityDto>> GetAllAsync()
    {
        var token = await GetTokenAsync();
        var config = _options.Value.Delivery;

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{config.BaseUrl}/api/integration/items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<OtherEntityDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new List<OtherEntityDto>();
    }

    public async Task<OtherEntityDto?> GetByIdAsync(Guid id)
    {
        var token = await GetTokenAsync();
        var config = _options.Value.Delivery;

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{config.BaseUrl}/api/integration/items/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OtherEntityDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private class LoginResult
    {
        public string AccessToken { get; set; } = "";
    }
}

// Options class for typed configuration
public class ExternalSystemsOptions
{
    public const string SectionName = "ExternalSystems";

    public ExternalSystemConfig Delivery { get; set; } = new();
    // Add other systems as needed
}

public class ExternalSystemConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthBaseUrl { get; set; } = "https://auth-backend.abc123.southeastasia.azurecontainerapps.io";
    public string ServiceAccountEmployeeNumber { get; set; } = string.Empty;
    public string ServiceAccountPassword { get; set; } = string.Empty;
}
```

> **Key design points:**
> - Caches the JWT so it doesn't login on every call
> - Automatically refreshes before expiry (55-minute cache window)
> - Service account credentials come from configuration, not hardcoded
> - Uses `IOptions<T>` for strongly-typed config injection

#### Step 4: Register in Program.cs

```csharp
// Register SystemClient
builder.Services.AddHttpClient<IOtherSystemClient, OtherSystemClient>();

// Bind ExternalSystems config
builder.Services.Configure<ExternalSystemsOptions>(
    builder.Configuration.GetSection(ExternalSystemsOptions.SectionName));
```

#### Step 5: Use in a controller

```csharp
[ApiController]
[Route("api/proxy/{your-entities}")]
[Authorize]
public class ProxyController : ControllerBase
{
    private readonly IOtherSystemClient _client;

    public ProxyController(IOtherSystemClient client) => _client = client;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _client.GetAllAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _client.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }
}
```

#### What actually happens (HTTP trace):

```
Your Backend ──▶ Auth Service: POST /api/auth/login
  Body: { employeeNumber: "SVC-{YOURSYSTEM}", password: "..." }
  ◀── Response: { accessToken: "eyJhbGciOiJIUzI1NiIs..." }

Your Backend ──▶ Other System: GET /api/integration/items
  Header: Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
  ◀── Response: [{ id: "...", name: "...", ... }]
```

### 7.2 How to Let Other Systems Call Your API

#### Step 1: Create an Integration Controller

This controller is for **machine-to-machine** communication only. Only service accounts and admins can access it.

```csharp
[ApiController]
[Route("api/integration")]
[Authorize(Roles = "{OtherSystem}.ExternalService,SystemAdmin")]
public class IntegrationController : ControllerBase
{
    private readonly I{YourEntity}Service _service;

    public IntegrationController(I{YourEntity}Service service) => _service = service;

    /// <summary>
    /// Other systems can fetch all items.
    /// </summary>
    [HttpGet("items")]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();
        return Ok(items);
    }

    /// <summary>
    /// Other systems can fetch a single item by ID.
    /// </summary>
    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    /// <summary>
    /// Other systems can create items (e.g., sync data).
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> Create([FromBody] {YourEntity}Dto dto)
    {
        var item = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>
    /// Other systems can update items.
    /// </summary>
    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] {YourEntity}Dto dto)
    {
        var item = await _service.UpdateAsync(id, dto);
        if (item == null) return NotFound();
        return Ok(item);
    }

    /// <summary>
    /// Health check for integration — no auth required for this one.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy", system = "{YourSystem}" });
}
```

#### Step 2: What to send to the other team

When another team needs to integrate with your system, send them this table:

```
┌──────────────────────────┬──────────────────────────────────────────────────┐
│ Item                     │ Value                                           │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ Your System URL          │ https://{yoursys}-backend.abc123.azurecontainerapps.io │
│ Integration Endpoint     │ GET    /api/integration/items                   │
│                          │ GET    /api/integration/items/{id}              │
│                          │ POST   /api/integration/items                   │
│                          │ PUT    /api/integration/items/{id}              │
│ Allowed Role             │ {OtherSystem}.ExternalService                   │
│ Service Account (create) │ EmployeeNumber: SVC-{THEIRSYSTEM}               │
│                          │ Role: {TheirSystem}.ExternalService             │
│ Auth Service URL         │ https://auth-backend.abc123.azurecontainerapps.io│
│ Shared Jwt:Key           │ (same as yours — from Auth Service team)        │
└──────────────────────────┴──────────────────────────────────────────────────┘
```

#### Step 3: Create a service account for them

```bash
# Login as admin
TOKEN=$(curl -s -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"admin","password":"admin123"}' | jq -r '.accessToken')

# Create service account for the other system
curl -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/admin/accounts \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "employeeNumber": "SVC-{THEIRSYSTEM}",
    "name": "{Their System Name} Service Account",
    "email": "svc-{their}@system.com",
    "password": "{generate-secure-password-here}",
    "role": "{TheirSystem}.ExternalService"
  }'
```

> **Important:** The role must follow the pattern `{TheirSystem}.ExternalService`. This tells the Auth Service what role to put in the JWT, and your `[Authorize(Roles = "...")]` attribute checks this role.

### 7.3 The Role-Based Access Model Explained

```
Who is calling?                      Role in JWT                  Allowed on your
                                                                  IntegrationController?
─────────────────────────────────────────────────────────────────────────────────────
Your own frontend (human user)       {YourSystem}.Employee         ❌ No (role doesn't match)
Your own backend (service account)   {YourSystem}.ExternalService  ❌ No (prevent self-calling)
Other system's backend               {OtherSystem}.ExternalService ✅ Yes (role matches)
Human admin                          SystemAdmin                   ✅ Yes (bypass)
```

**Why the self-calling prevention?** If your own system tries to call its own integration endpoint, the role in the JWT would be `{YourSystem}.ExternalService`, not `{OtherSystem}.ExternalService`. This prevents circular dependencies and ensures integration endpoints are only used for cross-system communication.

### 7.4 Integration Checklist (Two Teams Exchanging Info)

When two teams want to connect their systems, they need to exchange:

**From Auth Service team to both system teams:**
- [ ] Auth Service URL
- [ ] Jwt:Key (the shared secret)
- [ ] Jwt:Issuer (`CentralAuth`)
- [ ] Jwt:Audience (`InternalSystems`)
- [ ] How to login as admin to create service accounts

**From System A team to System B team:**
- [ ] System A's URL
- [ ] System A's integration endpoints (with example payloads)
- [ ] System A's allowed role for System B (`SystemB.ExternalService`)
- [ ] Service account EmployeeNumber + Password for System B to use (created in Step 3 above)

**From System B team to System A team:**
- [ ] System B's URL
- [ ] System B's integration endpoints (with example payloads)
- [ ] System B's allowed role for System A (`SystemA.ExternalService`)
- [ ] Service account EmployeeNumber + Password for System A to use (created in Step 3 above)

### 7.5 Example: Two Systems Integrating

Here's a concrete example using **Operational System** (Products) and **Delivery System** (Orders):

```
Operational needs to show orders
  → creates DeliverySystemClient
  → Logs in to Auth Service as SVC-OPERATIONAL (role: Operational.ExternalService)
  → Calls Delivery's GET /api/integration/orders
  → Delivery checks: role is "Operational.ExternalService" → ✅ Allowed

Delivery needs products for dropdown
  → creates OperationalSystemClient
  → Logs in to Auth Service as SVC-DELIVERY (role: Delivery.ExternalService)
  → Calls Operational's GET /api/integration/products
  → Operational checks: role is "Delivery.ExternalService" → ✅ Allowed
```

### 7.6 Security Best Practices

1. **Never hardcode** `Jwt:Key` or service account passwords in source code. Use Azure Container App secrets or GitHub Secrets.
2. **Store secrets** with:
   ```bash
   az containerapp secret set --name {yoursys}-backend --resource-group {yoursys}-app --secrets mysecret="..."
   ```
   Then reference via `secretref:mysecret` in env vars.
3. **Role naming convention**: `{SourceSystem}.ExternalService` — the source system is who's **calling**.
4. **Service accounts** should have minimal permissions — only access to integration endpoints, not human CRUD endpoints.
5. **JWT expiry** — tokens expire automatically. SystemClients refresh them transparently (55-minute cache window).
6. **CORS with `AllowAnyOrigin()`** is fine for demo/development. For production, restrict to specific frontend URLs:
   ```csharp
   policy.WithOrigins("https://your-frontend.vercel.app")
         .AllowAnyHeader().AllowAnyMethod();
   ```
7. **Logging** — add logging to SystemClient calls for debugging integration issues:
   ```csharp
   private readonly ILogger<OtherSystemClient> _logger;
   ```
8. **HTTPS only** — ensure all URLs use `https://`. Azure Container Apps and Vercel enforce this by default.

---

## Phase 8: Maintenance & Operations

### 8.1 Viewing Logs

```bash
# Tail live logs
az containerapp logs show --name {yoursys}-backend --resource-group {yoursys}-app --tail 50

# Stream logs (follow mode)
az containerapp logs show --name {yoursys}-backend --resource-group {yoursys}-app --follow
```

### 8.2 Restart the App

```bash
az containerapp revision restart --name {yoursys}-backend --resource-group {yoursys}-app
```

### 8.3 Scaling

```bash
# Update min/max replicas
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app --min-replicas 1 --max-replicas 5
```

### 8.4 Database Maintenance

```bash
# Connect to PostgreSQL from local machine
psql "host={yoursys}-db-{suffix}.postgres.database.azure.com port=5432 dbname=postgres user=postgres password=MyStr0ngP@ss! sslmode=require"
```

### 8.5 Updating Environment Variables

```bash
az containerapp update --name {yoursys}-backend --resource-group {yoursys}-app \
  --set-env-vars SomeNewVar=somevalue
```

---

## Troubleshooting

### "401 Unauthorized" when calling my API
- The JWT is missing or invalid. Check `Authorization: Bearer <token>` header.
- The `Jwt:Key` in your system doesn't match the Auth Service's key.
- The JWT has expired. Tokens typically last 60 minutes.

### "403 Forbidden" when calling an integration endpoint
- The JWT's role doesn't match the `[Authorize(Roles = "...")]` attribute.
- Check the role prefix. It should be `{OtherSystem}.ExternalService` or `SystemAdmin`.
- Verify the service account was created with the correct role.

### SystemClient returns empty data
- Check the other system's URL in configuration.
- Verify the service account credentials are correct.
- Check that the integration endpoint exists and is reachable (`curl -v` helps).
- Ensure the other system's firewall allows your Azure Container App's outbound IP.

### Backend won't start after CI/CD

```bash
az containerapp logs show --name {yoursys}-backend --resource-group {yoursys}-app --tail 50
```

Common causes:
- Wrong connection string (check database host, credentials, firewall)
- `Jwt:Key` too short (must be at least 32 bytes / 256 bits)
- Database firewall blocking Azure Container App's outbound IP
- Missing environment variables
- NuGet package restore failure (check build logs)

### CI/CD pipeline fails

Check the GitHub Actions logs in your repository:
1. Go to your repo on GitHub
2. Click **Actions** tab
3. Click the failed workflow run
4. Expand the failing step to see the error message

Common causes:
- `AZURE_CREDENTIALS` secret is expired (re-run `az ad sp create-for-rbac`)
- Docker build fails (check Dockerfile paths)
- Vercel token expired (regenerate in Vercel dashboard)

### CORS errors in browser console

If your frontend can't reach the backend from the browser:
- Verify `VITE_API_URL` is set correctly in Vercel environment variables
- Check that `app.UseCors()` is called **before** `app.UseAuthentication()` in Program.cs
- For production, use explicit origins instead of `AllowAnyOrigin()`

---

## Cost Summary

| Service | Tier | Cost |
|---|---|---|
| Database (PostgreSQL Flexible) | Burstable B1ms | ~$15/mo |
| Container Registry | Standard (1 free/12 mo) | $0 (first year) |
| Container Apps | Consumption (scale-to-zero) | ~$0-5/mo (idle) |
| Vercel | Hobby (free) | $0 |
| GitHub Actions | Public repo (free) | $0 |
| **Total (first year)** | | **~$20/mo** |
| **Total (after first year)** | | **~$20/mo** |

> **Cost-saving tips:**
> - Scale to zero replicas when not in use (`--min-replicas 0`)
> - Use PostgreSQL free tier if available in your region
> - Database can be stopped when not needed for development

---

## Quick Reference Card

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         {YourSystem} — Quick Reference                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  📦 Backend URL:  https://{yoursys}-backend.{region}.azurecontainerapps.io  │
│  🌐 Frontend URL: https://{yoursys}-frontend.vercel.app                     │
│  🔐 Auth Service: https://auth-backend.{region}.azurecontainerapps.io       │
│  📖 Swagger Docs: https://{yoursys}-backend.{region}.azurecontainerapps.io  │
│                     /swagger                                                │
│                                                                             │
│  🗄️ Database:      {yoursys}-db-{suffix}.postgres.database.azure.com       │
│  📦 Registry:      {yoursys}registry{suffix}.azurecr.io                    │
│                                                                             │
│  GitHub Repo:     github.com/{org}/{your-system}                            │
│  CI/CD:           .github/workflows/deploy.yml                              │
│                                                                             │
│  📡 Integration endpoints (for other systems):                              │
│    GET    /api/integration/items                                            │
│    GET    /api/integration/items/{id}                                       │
│    POST   /api/integration/items                                            │
│    PUT    /api/integration/items/{id}                                       │
│    GET    /api/integration/health                                           │
│                                                                             │
│  🔑 Required role to call integration: {OtherSystem}.ExternalService        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```
