# Deployment Guide — Two-System Mini Project

## Architecture Overview

```
                         ┌──────────────────────┐
                         │    Auth Service       │
                         │  (.NET 9, PostgreSQL) │
                         │  Azure Container Apps │
                         └──────┬───────────────┘
                                │ Issues JWTs
                    ┌───────────┴───────────┐
                    │                       │
         ┌──────────▼──────────┐  ┌─────────▼──────────┐
         │ Operational System  │  │  Delivery System    │
         │ (.NET 9, PostgreSQL)│  │ (.NET 8, SQL Server)│
         │ Azure Container Apps│  │ Azure Container Apps│
         │ Scalar API Docs     │  │ Swagger API Docs    │
         └──────────┬──────────┘  └─────────┬──────────┘
                    │                        │
         ┌──────────▼──────────┐  ┌─────────▼──────────┐
         │ Operational FE      │  │  Delivery FE        │
         │ Vercel              │  │  Vercel             │
         └─────────────────────┘  └─────────────────────┘
```

## Prerequisites

### Accounts
| Account | Sign Up Link | Why |
|---|---|---|
| GitHub | https://github.com | Store code, run CI/CD |
| Vercel | https://vercel.com | Host frontend (free tier) |
| Azure | https://azure.microsoft.com/free | Host backend + database |

### Tools to Install
1. **Git** — https://git-scm.com
2. **Azure CLI** — https://aka.ms/installazurecliwindows
3. **Docker Desktop** — https://www.docker.com/products/docker-desktop

---

## Phase 1: Push to GitHub

```bash
cd C:\Users\mikhj\Documents\Capstone-Project\Project-Integration
git init
git add -A
git commit -m "Initial commit - two system mini project"
git branch -m main
git remote add origin https://github.com/your-username/project-integration.git
git push -u origin main
```

---

> **💰 Cost Note:** All services below are covered under the [Azure Free Account](https://azure.microsoft.com/free) — either as 12-month free services or always-free services. No charges will occur within the free limits. See [Cost Summary](#cost-summary) at the end.

## Phase 2: Set Up Azure Resources

### 2.1 Login, Create Resource Group, and Register Providers

Run these once:
```bash
az login
az group create --name crud-app --location southeastasia

# Register required resource providers (needed for first-time Azure subscriptions)
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.Sql
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.ContainerRegistry
```

> **⚠️ Name uniqueness:** Azure server names (PostgreSQL, SQL Server, Container Registry) must be **globally unique**. Pick a **single suffix** (e.g., your initials + a number like `mj06`) and use it everywhere below. All commands use `<suffix>` as a placeholder — replace it with your chosen suffix.

### 2.2 Create PostgreSQL Server (Shared for Auth & Operational)

> **Free tier:** 750 hours/month of Burstable B1MS for 12 months — running one server 24/7 uses ~730 hours, well within the free grant.
>
> ⚠️ Azure may show a *"Paid Tier"* warning for `Standard_B1ms`. This is normal — it's a paid SKU, but your **Azure Free Account** covers the first 750 hours/month at **no charge** for 12 months. You will not be billed within that limit.

Create **one** PostgreSQL server, then create two databases inside it:

```bash
# Replace <suffix> with your unique string (e.g. mj06)
az postgres flexible-server create --name shared-postgres-<suffix> --resource-group crud-app --location southeastasia --admin-user postgres --admin-password MyStr0ngP@ss! --sku-name Standard_B1ms --tier Burstable --storage-size 32 --public-access 0.0.0.0

az postgres flexible-server db create --server-name shared-postgres-<suffix> --resource-group crud-app --name authdb
az postgres flexible-server db create --server-name shared-postgres-<suffix> --resource-group crud-app --name operationaldb
```

### 2.3 Create SQL Server Database (Delivery)

> **Free tier:** 100,000 vCore seconds/month of serverless Azure SQL Database with 32 GB storage — always free, no time limit.

```bash
az sql server create --name delivery-sqlserver-<suffix> --resource-group crud-app --location southeastasia --admin-user sqladmin --admin-password MyStr0ngP@ss!
az sql db create --resource-group crud-app --server delivery-sqlserver-<suffix> --name deliverydb --service-objective S0
az sql server firewall-rule create --resource-group crud-app --server delivery-sqlserver-<suffix> --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

### 2.4 Create Connection Strings (you'll paste these into the deploy commands below)

Run these commands to generate each connection string:

```bash
# --- For Auth Service (PostgreSQL) ---
az postgres flexible-server show-connection-string --server shared-postgres-<suffix> --database authdb

# --- For Operational System (PostgreSQL) ---
az postgres flexible-server show-connection-string --server shared-postgres-<suffix> --database operationaldb

# --- For Delivery System (SQL Server) ---
az sql db show-connection-string --server delivery-sqlserver-<suffix> --name deliverydb --client ado.net
```

Each command outputs JSON. Find the `"ado.net"` value — that's the connection string. It will contain `{login}` and `{password}` placeholders. **Replace them** with your actual credentials:

| Placeholder | Replace with |
|---|---|
| `{login}` | `postgres` (PostgreSQL) or `sqladmin` (SQL Server) |
| `{password}` | `MyStr0ngP@ss!` |
| `Trust Server Certificate` | Add `Trust Server Certificate=true` at the end if missing |

**Example** — after replacing, a PostgreSQL connection string looks like:
```
Server=shared-postgres-ab12.postgres.database.azure.com;Database=authdb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true
```

You will paste this **entire string** into the `--secrets dbconn="..."` argument of the deploy commands in steps 2.7, 2.8, and 2.9 below.

### 2.5 Create Azure Container Registry

> **Free tier:** 1 Standard tier registry with 100 GB storage for 12 months.

```bash
az acr create --name crudregistry<suffix> --resource-group crud-app --location southeastasia --sku Standard --admin-enabled true
az acr credential show --name crudregistry<suffix> --resource-group crud-app
```

Save the **username** and **password** for GitHub secrets.

### 2.6 Create Container Apps Environment

> **Free tier:** Consumption plan — 180,000 vCPU-seconds, 360,000 GiB-seconds, and 2 million requests per month, always free. With `--min-replicas 0`, apps scale to zero when idle and cost nothing.

```bash
az containerapp env create --name crud-env --resource-group crud-app --location southeastasia
```

### 2.7 Deploy Auth Service Backend

Each command is intentionally short to avoid Windows command-line length limits.

**A. Create the app:**
```bash
az containerapp create --name auth-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 0 --max-replicas 10
```

**B. Set the database secret:**
```bash
az containerapp secret set --name auth-backend --resource-group crud-app --secrets dbconn="Server=shared-postgres-<suffix>.postgres.database.azure.com;Database=authdb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true"
```

**C. Set environment variables (one at a time):**
```bash
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars ASPNETCORE_ENVIRONMENT=Production
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars ASPNETCORE_URLS=http://0.0.0.0:8080
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars ConnectionStrings__DefaultConnection=secretref:dbconn
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__Key=ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__Issuer=CentralAuth
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__Audience=InternalSystems
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__AccessTokenExpirationMinutes=60
```

### 2.8 Deploy Operational System Backend

**A. Create the app:**
```bash
az containerapp create --name operational-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 0 --max-replicas 10
```

**B. Set the database secret:**
```bash
az containerapp secret set --name operational-backend --resource-group crud-app --secrets dbconn="Server=shared-postgres-<suffix>.postgres.database.azure.com;Database=operationaldb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true"
```

**C. First get the Auth Service URL (replace `<suffix>` with yours):**
```bash
az containerapp show --name auth-backend --resource-group crud-app --query "properties.configuration.ingress.fqdn" --output tsv
```
This outputs something like `auth-backend.abc123.southeastasia.azurecontainerapps.io`. Note down the `abc123` part (the environment hash).

**D. Set environment variables (replace `abc123` with your actual hash):**
```bash
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars ASPNETCORE_ENVIRONMENT=Production
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars ASPNETCORE_URLS=http://0.0.0.0:8080
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars ConnectionStrings__DefaultConnection=secretref:dbconn
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars Jwt__Key=ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars Jwt__Issuer=CentralAuth
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars Jwt__Audience=InternalSystems
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars AuthService__BaseUrl=https://auth-backend.abc123.southeastasia.azurecontainerapps.io
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars ExternalSystems__Delivery__BaseUrl=https://delivery-backend.abc123.southeastasia.azurecontainerapps.io
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars ExternalSystems__Delivery__ServiceAccountEmployeeNumber=SVC-OPERATIONAL
az containerapp update --name operational-backend --resource-group crud-app --set-env-vars ExternalSystems__Delivery__ServiceAccountPassword=svc-operational-pwd
```

### 2.9 Deploy Delivery System Backend

**A. Create the app:**
```bash
az containerapp create --name delivery-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 0 --max-replicas 10
```

**B. Set the database secret:**
```bash
az containerapp secret set --name delivery-backend --resource-group crud-app --secrets dbconn="Server=delivery-sqlserver-<suffix>.database.windows.net;Database=deliverydb;User Id=sqladmin;Password=MyStr0ngP@ss!;TrustServerCertificate=True"
```

**C. Set environment variables (replace `abc123` with your hash from Auth Service URL):**
```bash
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars ASPNETCORE_ENVIRONMENT=Production
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars ASPNETCORE_URLS=http://0.0.0.0:8080
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars ConnectionStrings__DefaultConnection=secretref:dbconn
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars Jwt__Key=ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars Jwt__Issuer=CentralAuth
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars Jwt__Audience=InternalSystems
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars AuthService__BaseUrl=https://auth-backend.abc123.southeastasia.azurecontainerapps.io
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars ExternalSystems__Operational__BaseUrl=https://operational-backend.abc123.southeastasia.azurecontainerapps.io
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars ExternalSystems__Operational__ServiceAccountEmployeeNumber=SVC-DELIVERY
az containerapp update --name delivery-backend --resource-group crud-app --set-env-vars ExternalSystems__Operational__ServiceAccountPassword=svc-delivery-pwd
```

### 2.10 Get Backend URLs

```bash
az containerapp show --name auth-backend --resource-group crud-app --query "properties.configuration.ingress.fqdn" --output tsv
az containerapp show --name operational-backend --resource-group crud-app --query "properties.configuration.ingress.fqdn" --output tsv
az containerapp show --name delivery-backend --resource-group crud-app --query "properties.configuration.ingress.fqdn" --output tsv
```

---

## Phase 3: Set Up Vercel (Frontends)

### 3.1 Create Vercel Project for Operational Frontend

1. Go to https://vercel.com
2. Sign up with GitHub
3. Click **Add New** → **Project**
4. Import your repository
5. **Root Directory**: leave as `/` (root of the repo). The GitHub Actions workflow uses `--cwd` to point to the correct subdirectory.
6. **Environment Variables** (use the full URLs from step 2.10):

   | Variable | Value |
   |---|---|
   | `VITE_API_URL` | `https://operational-backend.abc123.southeastasia.azurecontainerapps.io` |
   | `VITE_AUTH_URL` | `https://auth-backend.abc123.southeastasia.azurecontainerapps.io` |

   > ⚠️ **Must include `https://`** — without it, the frontend will try to call a relative path on Vercel instead of Azure, resulting in a 405 error.

7. Click **Deploy**

> ⚠️ **Already created the project with a subdirectory root?** Go to Vercel → project → **Settings** → **General** → **Root Directory** and change it to `/`. The `--cwd` flag in the GitHub Action handles the subdirectory path automatically.

### 3.2 Create Vercel Project for Delivery Frontend

1. Click **Add New** → **Project**
2. Import your repository
3. **Root Directory**: leave as `/` (root of the repo).
4. **Environment Variables**:

   | Variable | Value |
   |---|---|
   | `VITE_API_URL` | `https://delivery-backend.abc123.southeastasia.azurecontainerapps.io` |
   | `VITE_AUTH_URL` | `https://auth-backend.abc123.southeastasia.azurecontainerapps.io` |

   > ⚠️ **Must include `https://`**

5. Click **Deploy**

### 3.3 Get Vercel Tokens and IDs

**Vercel Token:**
1. https://vercel.com/account/tokens → **Create** → name `GitHub Actions` → copy token

**Vercel Org ID:**
1. https://vercel.com/account → **General** → copy **ID**

**Vercel Project IDs:**
1. Open each project's **Settings** → scroll to **Project ID**

---

## Phase 4: Configure GitHub Secrets

In your GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret Name | Value |
|---|---|
| `AZURE_CREDENTIALS` | `az ad sp create-for-rbac` output (see below) |
| `AZURE_REGISTRY_NAME` | Your ACR name (e.g. `crudregistryab12` — no `.azurecr.io`) |
| `AZURE_REGISTRY_USERNAME` | From `az acr credential show` |
| `AZURE_REGISTRY_PASSWORD` | From `az acr credential show` |
| `VERCEL_TOKEN` | From Vercel account tokens |
| `VERCEL_ORG_ID` | From Vercel account settings |
| `VERCEL_PROJECT_ID_OPERATIONAL` | From Operational frontend project settings |
| `VERCEL_PROJECT_ID_DELIVERY` | From Delivery frontend project settings |

### Create Azure Credentials

```bash
az ad sp create-for-rbac --name "crud-app-github" --role contributor --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/crud-app --sdk-auth
```

Copy the entire JSON output and paste as `AZURE_CREDENTIALS`.

---

## Phase 5: Deploy via Git Push

```bash
git add -A
git commit -m "Configure CI/CD deployment"
git push
```

Go to GitHub → **Actions** tab to watch the three workflows:
- `Deploy Auth Service` — builds and deploys auth backend
- `Deploy Operational System` — builds and deploys operational backend + frontend
- `Deploy Delivery System` — builds and deploys delivery backend + frontend

---

## Phase 6: Testing the Full Flow

### 6.1 Test Auth Service

```bash
# Login as admin
curl -X POST https://auth-backend.<hash>.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"admin","password":"admin123"}'
```

Expected response:
```json
{
  "accessToken": "eyJ...",
  "role": "SystemAdmin",
  "name": "System Admin",
  "employeeNumber": "admin"
}
```

### 6.2 Test Operational System (Products CRUD)

```bash
# Create a product (use the token from login)
curl -X POST https://operational-backend.<hash>.southeastasia.azurecontainerapps.io/api/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"name":"Widget","description":"A useful widget","price":19.99}'

# Get all products
curl https://operational-backend.<hash>.southeastasia.azurecontainerapps.io/api/products \
  -H "Authorization: Bearer <token>"
```

### 6.3 Test Delivery System (Orders CRUD)

```bash
# Create an order
curl -X POST https://delivery-backend.<hash>.southeastasia.azurecontainerapps.io/api/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"productId":"<product-id-from-operational>","productName":"Widget","quantity":5,"status":"Pending","customerName":"John Doe"}'

# Get all orders
curl https://delivery-backend.<hash>.southeastasia.azurecontainerapps.io/api/orders \
  -H "Authorization: Bearer <token>"
```

### 6.4 Test Cross-System Integration

Login with a service account to test integration endpoints:

```bash
# Login as SVC-DELIVERY
TOKEN=$(curl -s -X POST https://auth-backend.<hash>.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"SVC-DELIVERY","password":"svc-delivery-pwd"}' | jq -r '.accessToken')

# Call Operational integration endpoint
curl https://operational-backend.<hash>.southeastasia.azurecontainerapps.io/api/integration/products \
  -H "Authorization: Bearer $TOKEN"
```

### 6.5 View API Documentation

- **Auth Service (Scalar)**: `https://auth-backend.<hash>.southeastasia.azurecontainerapps.io/scalar/v1`
- **Operational System (Scalar)**: `https://operational-backend.<hash>.southeastasia.azurecontainerapps.io/scalar/v1`
- **Delivery System (Swagger)**: `https://delivery-backend.<hash>.southeastasia.azurecontainerapps.io/swagger`

### 6.6 Test via Frontend

1. Open `https://operational-frontend.vercel.app` in your browser
2. Log in with `admin` / `admin123`
3. Create, edit, and delete products
4. Open `https://delivery-frontend.vercel.app`
5. Log in with `admin` / `admin123`
6. Create, edit, and delete orders

---

## Environment Variables Reference

### Auth Service
| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Key` | Shared JWT signing key (32+ characters) |
| `Jwt__Issuer` | Must be `CentralAuth` |
| `Jwt__Audience` | Must be `InternalSystems` |

### Operational System
| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Key` | Same as Auth Service |
| `Jwt__Issuer` | `CentralAuth` |
| `Jwt__Audience` | `InternalSystems` |
| `AuthService__BaseUrl` | URL of deployed Auth Service |
| `ExternalSystems__Delivery__BaseUrl` | URL of deployed Delivery backend |
| `ExternalSystems__Delivery__ServiceAccountEmployeeNumber` | `SVC-OPERATIONAL` |
| `ExternalSystems__Delivery__ServiceAccountPassword` | Service account password |

### Delivery System
| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Key` | Same as Auth Service |
| `Jwt__Issuer` | `CentralAuth` |
| `Jwt__Audience` | `InternalSystems` |
| `AuthService__BaseUrl` | URL of deployed Auth Service |
| `ExternalSystems__Operational__BaseUrl` | URL of deployed Operational backend |
| `ExternalSystems__Operational__ServiceAccountEmployeeNumber` | `SVC-DELIVERY` |
| `ExternalSystems__Operational__ServiceAccountPassword` | Service account password |

### Operational Frontend (Vercel)
| Variable | Description |
|---|---|
| `VITE_API_URL` | Operational backend URL |
| `VITE_AUTH_URL` | Auth Service URL |

### Delivery Frontend (Vercel)
| Variable | Description |
|---|---|
| `VITE_API_URL` | Delivery backend URL |
| `VITE_AUTH_URL` | Auth Service URL |

---

## Troubleshooting

### Backend fails to start
```bash
az containerapp logs show --name auth-backend --resource-group crud-app --tail 50
```
Common causes:
- Wrong database connection string in secrets
- JWT Key too short (must be 32+ characters)
- Database firewall not accepting Azure connections

### CORS errors (demo security note)
This project uses `AllowAnyOrigin()` in `Program.cs` for demo simplicity — it accepts requests from any domain. This is **not secure for production**. For production, replace with the specific Vercel URL:
```csharp
options.WithOrigins("https://your-app.vercel.app")
       .AllowAnyHeader()
       .AllowAnyMethod();
```

### Frontend shows blank page, 405 errors, or CORS errors
1. Open DevTools (F12) → Console → Network tab
2. If the request URL starts with your Vercel domain instead of the Azure domain, the `VITE_AUTH_URL` or `VITE_API_URL` is missing the `https://` prefix:
   ```
   ✅ VITE_AUTH_URL=https://auth-backend.abc123.southeastasia.azurecontainerapps.io
   ❌ VITE_AUTH_URL=auth-backend.abc123.southeastasia.azurecontainerapps.io (missing https://)
   ```
3. If you see **"CORS Missing Allow Origin"**, the backend is missing CORS headers. The backends already include `AllowAnyOrigin()` in their `Program.cs`, so ensure you've pushed the latest code and redeployed via GitHub Actions.
4. After fixing, redeploy the frontend from Vercel dashboard (or push a commit).

### Cross-system integration fails
1. Verify service account credentials match those seeded in Auth Service
2. Check that the target system's integration endpoint is accessible
3. Confirm JWT roles match (e.g., `Operational.ExternalService` for Delivery calling Operational)

### GitHub Actions fails
1. Go to repo → **Actions** → click the failed run
2. Read the error message
3. Common issues: secrets not set, Azure credentials expired, Docker build fails

---

## Cost Summary (Free Tier)

All services used in this project are covered under the [Azure Free Account](https://azure.microsoft.com/free) free tiers:

| Service | Tier Used | Free Tier Coverage | Monthly Cost |
|---|---|---|---|
| **Azure Database for PostgreSQL** (×1, shared) | Burstable B1MS, 32 GB | 750 hours/month for 12 months | **$0** (~730h used) |
| **Azure SQL Database** | S0 serverless, 32 GB | 100,000 vCore seconds/month, always free | **$0** |
| **Azure Container Registry** | Standard | 1 Standard registry for 12 months | **$0** |
| **Azure Container Apps** (×3) | Consumption plan | 180K vCPU-sec + 360K GiB-sec + 2M requests/month, always free | **$0** (scale to zero) |
| **Vercel** (×2) | Free (Hobby) | 100 GB bandwidth, unlimited sites | **$0** |
| **GitHub Actions** | Free (public repo) | 2,000 minutes/month | **$0** |
| **Docker Desktop** | Free | — | **$0** |
| | | **Total** | **$0/month** |

### Staying within free limits

- **PostgreSQL:** One shared server (B1MS) running 24/7 uses ~730 hours/month, well within the 750-hour free grant.
- **Container Apps:** `--min-replicas 0` scales to zero when idle — you only pay when traffic hits them (free grants cover thousands of requests).
- **SQL Database:** Serverless auto-pauses after 60 minutes of inactivity, consuming 0 vCore seconds when paused.

---

## Appendix: Local Development Setup

You do **not** need to install SQL Server or PostgreSQL directly on your machine. Everything runs via Docker containers.

### Install Docker Desktop

Download from https://www.docker.com/products/docker-desktop

### Start Databases

Run these three containers (PostgreSQL ×2, SQL Server ×1):

```bash
# Auth Service Database (PostgreSQL)
docker run -d --name auth-db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=authdb -p 5432:5432 postgres:16

# Operational Database (PostgreSQL)
docker run -d --name operational-db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=operationaldb -p 5433:5432 postgres:16

# Delivery Database (SQL Server)
docker run -d --name delivery-db -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

**Note:** The two PostgreSQL instances use different host ports (5432 vs 5433) so they don't conflict.

### Connection Strings (match the `appsettings.json` files)

| Project | Connection String |
|---|---|
| Auth Service | `Host=localhost;Database=authdb;Username=postgres;Password=postgres` |
| Operational | `Host=localhost;Port=5433;Database=operationaldb;Username=postgres;Password=postgres` |
| Delivery | `Server=localhost;Database=deliverydb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True` |

### Run All Backends

Open **three separate terminals**:

```bash
# Terminal 1 — Auth Service (port 5000)
cd auth-service/AuthService.Api
dotnet run --urls http://localhost:5000

# Terminal 2 — Operational System (port 5001)
cd operational-system/OperationalSystem.Api
dotnet run --urls http://localhost:5001

# Terminal 3 — Delivery System (port 5002)
cd delivery-system/DeliverySystem.Api
dotnet run --urls http://localhost:5002
```

### Run Frontends

In two more terminals:

```bash
# Terminal 4 — Operational Frontend (port 5173)
cd operational-system/operational-frontend
echo "VITE_API_URL=http://localhost:5001
VITE_AUTH_URL=http://localhost:5000" > .env
npm run dev

# Terminal 5 — Delivery Frontend (port 5174)
cd delivery-system/delivery-frontend
echo "VITE_API_URL=http://localhost:5002
VITE_AUTH_URL=http://localhost:5000" > .env
npm run dev
```

### Verify Local Setup

1. **Auth Service**: `curl http://localhost:5000/api/auth/login -X POST -H "Content-Type: application/json" -d "{\"employeeNumber\":\"admin\",\"password\":\"admin123\"}"`
2. **Operational API**: Open `http://localhost:5001/scalar/v1`
3. **Delivery API**: Open `http://localhost:5002/swagger`
4. **Operational Frontend**: Open `http://localhost:5173`
5. **Delivery Frontend**: Open `http://localhost:5174`

### Stopping Databases

```bash
docker stop auth-db operational-db delivery-db
docker rm auth-db operational-db delivery-db
```

To persist data between restarts, add `-v authdb_data:/var/lib/postgresql/data` (and similar volume flags) to the `docker run` commands above.
