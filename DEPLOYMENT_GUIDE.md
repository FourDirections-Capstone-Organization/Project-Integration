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
         │ Products CRUD       │  │ Orders CRUD         │
         │ Scalar API Docs     │  │ Swagger API Docs    │
         │ Azure Container Apps│  │ Azure Container Apps│
         └──────────┬──────────┘  └─────────┬──────────┘
                    │                        │
         ┌──────────▼──────────┐  ┌─────────▼──────────┐
         │ Operational FE      │  │  Delivery FE        │
         │ Vercel              │  │  Vercel             │
         └─────────────────────┘  └─────────────────────┘
```

> **💰 All services below are covered under the [Azure Free Account](https://azure.microsoft.com/free)** — either as 12-month free services or always-free. See [Cost Summary](#cost-summary).

---

## 🚨 Read This First — Common Pitfalls

| # | Pitfall | Fix |
|---|---|---|
| 1 | `Specified server name is already used` | Azure names are globally unique. Change your `<suffix>` and try again. |
| 2 | `The system cannot find the file specified` | Windows CLI has a length limit. Commands are pre-split into short calls — don't rejoin them. |
| 3 | `vite: command not found` in Vercel | Set **Root Directory** in Vercel → project → Settings → General to the frontend subdirectory. |
| 4 | Frontend calls Vercel instead of Azure | `VITE_API_URL` / `VITE_AUTH_URL` must include `https://`. |
| 5 | `CORS Missing Allow Origin` (404 on OPTIONS) | Container App is still using the sample image. Wait for CI/CD to deploy the custom code. |
| 6 | CI/CD fails with `ACR not found` | The `AZURE_REGISTRY_NAME` GitHub secret doesn't match your registry name. |
| 7 | `Resource provider not registered` | First-time subscriptions need `az provider register` for PostgreSQL, SQL, Container Apps, and ACR. |
| 8 | Auth Service connection string shows `{login}` / `{password}` | Extract the `"ado.net"` value from JSON and replace with `postgres` / `MyStr0ngP@ss!`. |

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
git commit -m "Initial commit"
git branch -m main
git remote add origin https://github.com/your-username/project-integration.git
git push -u origin main
```

---

## Phase 2: Set Up Azure Resources

> **⚠️ Naming:** Azure server names (PostgreSQL, SQL Server, Container Registry) must be **globally unique**. Pick a **single suffix** (e.g., your initials + number like `mj06`) and use it everywhere below. All commands use `<suffix>` as a placeholder.

### 2.1 Login and Register Resource Providers

Run these once:
```bash
az login
az group create --name crud-app --location southeastasia

# First-time subscriptions need these providers registered:
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.Sql
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.ContainerRegistry
```

### 2.2 Create PostgreSQL Server (Shared for Auth & Operational)

> **Free:** 750 hours/month of Burstable B1MS for 12 months. One server 24/7 uses ~730h — well within the free grant.
> ⚠️ Azure may show a *"Paid Tier"* warning — this is normal. The free account covers this SKU for 750h/month.

```bash
az postgres flexible-server create --name shared-postgres-<suffix> --resource-group crud-app --location southeastasia --admin-user postgres --admin-password MyStr0ngP@ss! --sku-name Standard_B1ms --tier Burstable --storage-size 32 --public-access 0.0.0.0

az postgres flexible-server db create --server-name shared-postgres-<suffix> --resource-group crud-app --name authdb
az postgres flexible-server db create --server-name shared-postgres-<suffix> --resource-group crud-app --name operationaldb
```

### 2.3 Create SQL Server Database (Delivery)

> **Free:** 100,000 vCore seconds/month of Azure SQL Database — always free, no time limit.

```bash
az sql server create --name delivery-sqlserver-<suffix> --resource-group crud-app --location southeastasia --admin-user sqladmin --admin-password MyStr0ngP@ss!
az sql db create --resource-group crud-app --server delivery-sqlserver-<suffix> --name deliverydb --service-objective S0
az sql server firewall-rule create --resource-group crud-app --server delivery-sqlserver-<suffix> --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

### 2.4 Create Connection Strings

```bash
# Auth Service (PostgreSQL)
az postgres flexible-server show-connection-string --server shared-postgres-<suffix> --database authdb

# Operational System (PostgreSQL)
az postgres flexible-server show-connection-string --server shared-postgres-<suffix> --database operationaldb

# Delivery System (SQL Server)
az sql db show-connection-string --server delivery-sqlserver-<suffix> --name deliverydb --client ado.net
```

Each outputs JSON. Find the `"ado.net"` value, then replace placeholders:

| Placeholder | Replace with |
|---|---|
| `{login}` | `postgres` (PostgreSQL) or `sqladmin` (SQL Server) |
| `{password}` | `MyStr0ngP@ss!` |
| *(missing)* | Add `Trust Server Certificate=true` at the end if not present |

Example result:
```
Server=shared-postgres-ab12.postgres.database.azure.com;Database=authdb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true
```

Save these three strings — they go into `--secrets dbconn="..."` in the deploy steps below.

### 2.5 Create Azure Container Registry

> **Free:** 1 Standard tier registry with 100 GB storage for 12 months.

```bash
az acr create --name crudregistry<suffix> --resource-group crud-app --location southeastasia --sku Standard --admin-enabled true
az acr credential show --name crudregistry<suffix> --resource-group crud-app
```

Save the **username** and **password** — these go into GitHub secrets.

### 2.6 Create Container Apps Environment

> **Free:** Consumption plan — 180K vCPU-sec, 360K GiB-sec, 2M requests/month. Scale to zero when idle.

```bash
az containerapp env create --name crud-env --resource-group crud-app --location southeastasia
```

### 2.7 Deploy Auth Service Backend

Commands are split into short steps to avoid Windows command-line length limits.

```bash
# A. Create the app
az containerapp create --name auth-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 0 --max-replicas 10

# B. Set the database secret (paste your auth connection string inside the quotes)
az containerapp secret set --name auth-backend --resource-group crud-app --secrets dbconn="Server=shared-postgres-<suffix>.postgres.database.azure.com;Database=authdb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true"

# C. Set environment variables (one per line to avoid length limits)
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars ASPNETCORE_ENVIRONMENT=Production
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars ASPNETCORE_URLS=http://0.0.0.0:8080
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars ConnectionStrings__DefaultConnection=secretref:dbconn
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__Key=ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__Issuer=CentralAuth
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__Audience=InternalSystems
az containerapp update --name auth-backend --resource-group crud-app --set-env-vars Jwt__AccessTokenExpirationMinutes=60
```

### 2.8 Deploy Operational System Backend

```bash
# A. Create the app
az containerapp create --name operational-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 0 --max-replicas 10

# B. Set the database secret
az containerapp secret set --name operational-backend --resource-group crud-app --secrets dbconn="Server=shared-postgres-<suffix>.postgres.database.azure.com;Database=operationaldb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true"
```

**C. Get the Auth Service URL hash:**
```bash
az containerapp show --name auth-backend --resource-group crud-app --query "properties.configuration.ingress.fqdn" --output tsv
```
This outputs `auth-backend.abc123.southeastasia.azurecontainerapps.io`. Note the `abc123` (environment hash).

**D. Set environment variables (replace `abc123` with your hash):**
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

```bash
# A. Create the app
az containerapp create --name delivery-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 0 --max-replicas 10

# B. Set the database secret
az containerapp secret set --name delivery-backend --resource-group crud-app --secrets dbconn="Server=delivery-sqlserver-<suffix>.database.windows.net;Database=deliverydb;User Id=sqladmin;Password=MyStr0ngP@ss!;TrustServerCertificate=True"

# C. Set environment variables (replace abc123 with your hash)
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

> **⚠️ Important:** The Root Directory setting below is **critical**. If left blank, Vercel auto-deployments will fail with `vite: command not found` because it runs from the repo root where there's no frontend.

### 3.1 Create Vercel Project for Operational Frontend

1. Go to https://vercel.com → **Add New** → **Project**
2. Import your repository
3. **Root Directory**: select `operational-system/operational-frontend`
4. **Environment Variables** (from step 2.10 URLs — include `https://`):

   | Variable | Value |
   |---|---|
   | `VITE_API_URL` | `https://operational-backend.abc123.southeastasia.azurecontainerapps.io` |
   | `VITE_AUTH_URL` | `https://auth-backend.abc123.southeastasia.azurecontainerapps.io` |

   > ⚠️ **Must include `https://`** — without it, the frontend calls Vercel instead of Azure (405 error).

5. Click **Deploy**

### 3.2 Create Vercel Project for Delivery Frontend

1. Click **Add New** → **Project**
2. Import your repository
3. **Root Directory**: select `delivery-system/delivery-frontend`
4. **Environment Variables**:

  | Variable | Value |
  |---|---|
  | `VITE_API_URL` | `https://delivery-backend.abc123.southeastasia.azurecontainerapps.io` |
  | `VITE_AUTH_URL` | `https://auth-backend.abc123.southeastasia.azurecontainerapps.io` |

5. Click **Deploy**

### 3.3 Get Vercel Tokens and IDs

| Item | Where to find it |
|---|---|
| **Vercel Token** | https://vercel.com/account/tokens → **Create** |
| **Org ID** | https://vercel.com/account → **General** → copy **ID** |
| **Project ID** | Each project → **Settings** → scroll to **Project ID** |

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

Watch progress at GitHub → **Actions** tab:
- **Deploy Auth Service** — builds Docker image → pushes to ACR → updates Container App
- **Deploy Operational System** — builds Docker → updates Container App → deploys frontend to Vercel
- **Deploy Delivery System** — builds Docker → updates Container App → deploys frontend to Vercel

> After the first successful CI/CD run, your backends will run the **custom code** (with CORS, controllers, etc.) instead of the sample image.

---

## Phase 6: Testing the Full Flow

### 6.1 Login via Auth Service

```bash
curl -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"admin","password":"admin123"}'
```

Expected:
```json
{ "accessToken": "eyJ...", "role": "SystemAdmin", "name": "System Admin", "employeeNumber": "admin" }
```

### 6.2 Test Operational System (Products CRUD)

```bash
TOKEN="<paste-token-here>"

# Create
curl -X POST https://operational-backend.abc123.southeastasia.azurecontainerapps.io/api/products \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Widget","description":"A useful widget","price":19.99}'

# Get all
curl https://operational-backend.abc123.southeastasia.azurecontainerapps.io/api/products \
  -H "Authorization: Bearer $TOKEN"
```

### 6.3 Test Delivery System (Orders CRUD)

```bash
# Create
curl -X POST https://delivery-backend.abc123.southeastasia.azurecontainerapps.io/api/orders \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
  -d '{"productId":"<guid>","productName":"Widget","quantity":5,"status":"Pending","customerName":"John Doe"}'

# Get all
curl https://delivery-backend.abc123.southeastasia.azurecontainerapps.io/api/orders \
  -H "Authorization: Bearer $TOKEN"
```

### 6.4 Test Cross-System Integration

```bash
# Login as service account
SVC_TOKEN=$(curl -s -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"SVC-DELIVERY","password":"svc-delivery-pwd"}' | jq -r '.accessToken')

# Call Operational's integration endpoint
curl https://operational-backend.abc123.southeastasia.azurecontainerapps.io/api/integration/products \
  -H "Authorization: Bearer $SVC_TOKEN"
```

### 6.5 View API Documentation

| System | URL |
|---|---|
| Auth Service (Scalar) | `https://auth-backend.abc123.southeastasia.azurecontainerapps.io/scalar/v1` |
| Operational (Scalar) | `https://operational-backend.abc123.southeastasia.azurecontainerapps.io/scalar/v1` |
| Delivery (Swagger) | `https://delivery-backend.abc123.southeastasia.azurecontainerapps.io/swagger` |

### 6.6 Test via Frontend

1. Open `https://operational-frontend.vercel.app` → login with `admin` / `admin123`
2. Create, edit, and delete products
3. Open `https://delivery-frontend.vercel.app` → login with `admin` / `admin123`
4. Create, edit, and delete orders

---

## Environment Variables Reference

### Auth Service
| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (set via secret `dbconn`) |
| `Jwt__Key` | Shared JWT signing key (32+ chars) |
| `Jwt__Issuer` | Must be `CentralAuth` |
| `Jwt__Audience` | Must be `InternalSystems` |

### Operational System
| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (set via secret `dbconn`) |
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
| `ConnectionStrings__DefaultConnection` | SQL Server connection string (set via secret `dbconn`) |
| `Jwt__Key` | Same as Auth Service |
| `Jwt__Issuer` | `CentralAuth` |
| `Jwt__Audience` | `InternalSystems` |
| `AuthService__BaseUrl` | URL of deployed Auth Service |
| `ExternalSystems__Operational__BaseUrl` | URL of deployed Operational backend |
| `ExternalSystems__Operational__ServiceAccountEmployeeNumber` | `SVC-DELIVERY` |
| `ExternalSystems__Operational__ServiceAccountPassword` | Service account password |

### Frontends (Vercel)
| Variable | Description |
|---|---|
| `VITE_API_URL` | Backend URL (must include `https://`) |
| `VITE_AUTH_URL` | Auth Service URL (must include `https://`) |

---

## Troubleshooting

### Backend fails to start
```bash
az containerapp logs show --name auth-backend --resource-group crud-app --tail 50
```
**Common causes:**
- Wrong database connection string in secret
- JWT Key too short (must be 32+ characters)
- Database firewall not accepting Azure connections
- Resource provider not registered (run `az provider register` from step 2.1)

### CI/CD — ACR login fails ("resource not found")
The `AZURE_REGISTRY_NAME` GitHub secret doesn't match your actual ACR name:
```bash
az acr list --resource-group crud-app --query "[].name" --output tsv
```
Set the output as the `AZURE_REGISTRY_NAME` secret (no `.azurecr.io` suffix).

### CI/CD — Vercel auto-deployments fail with "vite: command not found"
This happens when Vercel's auto-deployment runs from the repo root (no frontend there). **Fix:**
1. Vercel → project → **Settings** → **General** → **Root Directory** → set to the subdirectory (e.g. `operational-system/operational-frontend`)
2. Push again — auto-deployments will now build from the correct directory

### CI/CD — Vercel "vite: command not found" in GitHub Actions workflow
The GitHub Actions workflow builds the frontend locally using `npx vercel build`, then deploys via `npx vercel deploy --prebuilt`. This does NOT depend on Vercel's build cache. If it fails:
1. Check that `npm install` succeeded (vite is installed)
2. Re-run the workflow from GitHub Actions tab

### Frontend shows blank page, 405 errors, or wrong domain
1. Open DevTools (F12) → Network tab
2. If the request URL starts with your **Vercel domain** instead of Azure → `VITE_API_URL` or `VITE_AUTH_URL` is missing `https://`
   ```
   ✅ VITE_AUTH_URL=https://auth-backend.abc123.southeastasia.azurecontainerapps.io
   ❌ VITE_AUTH_URL=auth-backend.abc123.southeastasia.azurecontainerapps.io
   ```
3. Fix in Vercel → project → **Settings** → **Environment Variables**
4. Redeploy from Vercel dashboard or push a commit

### CORS errors (OPTIONS returns 404 without CORS headers)
The backend is still running the **sample Docker image** (`mcr.microsoft.com/dotnet/samples:aspnetapp`). The CI/CD pipeline must successfully build and deploy our custom image (which includes CORS middleware). Wait for GitHub Actions → **Deploy Auth Service** to turn green.

### CORS security note
This demo uses `AllowAnyOrigin()` — it accepts requests from any domain. **Not secure for production.** Replace in `Program.cs`:
```csharp
options.WithOrigins("https://your-app.vercel.app")
       .AllowAnyHeader()
       .AllowAnyMethod();
```

### Cross-system integration fails
1. Verify service account credentials match those seeded in Auth Service (`SVC-OPERATIONAL` / `SVC-DELIVERY`)
2. Check the target system's integration endpoint is accessible via curl
3. Confirm JWT role prefixes: `Operational.ExternalService` for Delivery→Operational, `Delivery.ExternalService` for Operational→Delivery

### GitHub Actions fails
1. Go to repo → **Actions** → click the failed run
2. Read the red error message
3. Common issues:
   - ACR name mismatch → set `AZURE_REGISTRY_NAME` secret
   - Azure credentials expired → re-run `az ad sp create-for-rbac`
   - Vercel project IDs wrong → check Vercel project settings

### Windows CLI "The system cannot find the file specified"
Commands with too many environment variables hit a Windows command-line length limit. **Fix:** The commands in this guide are already split into individual `az containerapp update --set-env-vars KEY=VALUE` calls. Copy them exactly.

### Azure "Specified server name is already used"
Server names (PostgreSQL, SQL Server, ACR) must be globally unique. **Fix:** Change your `<suffix>` and recreate.

---

## Cost Summary (Free Tier)

| Service | Tier Used | Free Coverage | Cost |
|---|---|---|---|
| **Azure Database for PostgreSQL** (×1 shared) | Burstable B1MS, 32 GB | 750h/month for 12 months | **$0** |
| **Azure SQL Database** | S0 serverless, 32 GB | 100K vCore-sec/month, always free | **$0** |
| **Azure Container Registry** | Standard | 1 Standard registry for 12 months | **$0** |
| **Azure Container Apps** (×3) | Consumption plan | 180K vCPU-sec + 360K GiB-sec + 2M req/month, always free | **$0** |
| **Vercel** (×2) | Free (Hobby) | 100 GB bandwidth | **$0** |
| **GitHub Actions** | Free (public repo) | 2,000 min/month | **$0** |
| **Docker Desktop** | Free | — | **$0** |
| | | **Total** | **$0/month** |

### Staying within free limits
- **PostgreSQL:** One server B1MS running 24/7 uses ~730h/month — within the 750h free grant.
- **Container Apps:** `--min-replicas 0` scales to zero when idle. Free grants cover thousands of requests.
- **SQL Database:** Serverless auto-pauses after 60 min idle, consuming 0 vCore seconds.

---

## Appendix: Local Development Setup

Databases run via Docker — no manual SQL Server or PostgreSQL installation needed.

### Start Databases

```bash
docker run -d --name auth-db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=authdb -p 5432:5432 postgres:16
docker run -d --name operational-db -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=operationaldb -p 5433:5432 postgres:16
docker run -d --name delivery-db -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

### Run Backends

```bash
# Terminal 1
cd auth-service/AuthService.Api && dotnet run --urls http://localhost:5000

# Terminal 2
cd operational-system/OperationalSystem.Api && dotnet run --urls http://localhost:5001

# Terminal 3
cd delivery-system/DeliverySystem.Api && dotnet run --urls http://localhost:5002
```

### Run Frontends

```bash
# Terminal 4
cd operational-system/operational-frontend
echo "VITE_API_URL=http://localhost:5001\nVITE_AUTH_URL=http://localhost:5000" > .env && npm run dev

# Terminal 5
cd delivery-system/delivery-frontend
echo "VITE_API_URL=http://localhost:5002\nVITE_AUTH_URL=http://localhost:5000" > .env && npm run dev
```

### Stop Databases

```bash
docker stop auth-db operational-db delivery-db && docker rm auth-db operational-db delivery-db
```
