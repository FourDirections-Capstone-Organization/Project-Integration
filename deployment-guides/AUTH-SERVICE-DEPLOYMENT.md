# Auth Service Deployment Guide

## What This System Does

A standalone authentication service built with .NET 9, PostgreSQL, and Scalar API. It issues JWT tokens via `POST /api/auth/login`, manages user accounts, and stores employees, accounts, and service accounts in a PostgreSQL database. The frontend is a React + Vite login page. Other systems validate the JWT tokens issued here to authenticate users.

---

## Prerequisites

- **Accounts**: GitHub, Vercel, Azure
- **Tools**: Git, Azure CLI, Docker Desktop
- **SDKs**: Node.js and .NET 9 SDK (for local testing)

---

## Phase 1: Push to GitHub

```bash
git init
git add -A
git commit -m "Initial commit"
git branch -m main
git remote add origin https://github.com/your-username/your-repo.git
git push -u origin main
```

---

## Phase 2: Set Up Azure Resources

### 2.1 Login and Create Resource Group

```bash
az login
az group create --name auth-app --location southeastasia
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.ContainerRegistry
```

### 2.2 Create PostgreSQL Database

Use a unique `<suffix>` (e.g., your initials + number like `mj06`):

```bash
az postgres flexible-server create \
  --name auth-db-<suffix> \
  --resource-group auth-app \
  --location southeastasia \
  --admin-user postgres \
  --admin-password MyStr0ngP@ss! \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --public-access 0.0.0.0
```

> **Cost note:** Standard_B1ms Burstable is covered by Azure Free Account (750 hours/month for 12 months). Azure may show a "Paid Tier" warning — this is normal.

### 2.3 Create Connection String

```bash
az postgres flexible-server show-connection-string \
  --server auth-db-<suffix> \
  --database authdb
```

The output is JSON. Extract the `"ado.net"` value and replace `{login}` → `postgres`, `{password}` → `MyStr0ngP@ss!`. Example result:

```
Server=auth-db-<suffix>.postgres.database.azure.com;Database=authdb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true
```

Save this — it goes into the `--secrets dbconn="..."` parameter later.

### 2.4 Create Container Registry

```bash
az acr create \
  --name authregistry<suffix> \
  --resource-group auth-app \
  --location southeastasia \
  --sku Standard \
  --admin-enabled true

az acr credential show \
  --name authregistry<suffix> \
  --resource-group auth-app
```

Save the **username** and **password** for GitHub secrets.

### 2.5 Create Container Apps Environment

```bash
az containerapp env create \
  --name auth-env \
  --resource-group auth-app \
  --location southeastasia
```

### 2.6 Deploy the Backend

Commands are split short to avoid Windows CLI length limits.

**A. Create the app:**

```bash
az containerapp create \
  --name auth-backend \
  --resource-group auth-app \
  --environment auth-env \
  --image mcr.microsoft.com/dotnet/samples:aspnetapp \
  --target-port 8080 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 10
```

**B. Set the database secret (paste your connection string):**

```bash
az containerapp secret set \
  --name auth-backend \
  --resource-group auth-app \
  --secrets dbconn="Server=auth-db-<suffix>.postgres.database.azure.com;Database=authdb;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true"
```

**C. Set environment variables (one per line):**

```bash
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars ASPNETCORE_ENVIRONMENT=Production
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars ASPNETCORE_URLS=http://0.0.0.0:8080
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars ConnectionStrings__DefaultConnection=secretref:dbconn
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars Jwt__Key=ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars Jwt__Issuer=CentralAuth
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars Jwt__Audience=InternalSystems
az containerapp update --name auth-backend --resource-group auth-app --set-env-vars Jwt__AccessTokenExpirationMinutes=60
```

### 2.7 Get Backend URL

```bash
az containerapp show \
  --name auth-backend \
  --resource-group auth-app \
  --query "properties.configuration.ingress.fqdn" \
  --output tsv
```

This outputs something like `auth-backend.abc123.southeastasia.azurecontainerapps.io`. Save the full URL.

---

## Phase 3: Set Up Vercel (Frontend)

1. Go to https://vercel.com → **Add New** → **Project**
2. Import your repository
3. **Root Directory**: select `auth-service/auth-frontend` (adjust to your actual frontend path)
4. **Environment Variables**:
   - `VITE_AUTH_URL`: `https://auth-backend.abc123.southeastasia.azurecontainerapps.io`
   - `VITE_API_URL`: `https://auth-backend.abc123.southeastasia.azurecontainerapps.io`

   > ⚠️ Must include `https://` — without it, the frontend calls Vercel instead of Azure (405 error).

5. Click **Deploy**

After deploy, get your Vercel project ID and Org ID for GitHub secrets.

---

## Phase 4: Configure GitHub Secrets

In your GitHub repo → **Settings** → **Secrets and variables** → **Actions**:

| Secret | Value |
|---|---|
| `AZURE_CREDENTIALS` | From `az ad sp create-for-rbac` (see below) |
| `AZURE_REGISTRY_NAME` | Your ACR name, e.g. `authregistry<suffix>` |
| `AZURE_REGISTRY_USERNAME` | From `az acr credential show` |
| `AZURE_REGISTRY_PASSWORD` | From `az acr credential show` |
| `VERCEL_TOKEN` | From https://vercel.com/account/tokens |
| `VERCEL_ORG_ID` | From Vercel account settings |
| `VERCEL_PROJECT_ID` | From Vercel project settings |

**Create Azure credentials:**

```bash
az ad sp create-for-rbac \
  --name "auth-app-github" \
  --role contributor \
  --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/auth-app \
  --sdk-auth
```

---

## Phase 5: CI/CD — GitHub Actions Workflow

Create `.github/workflows/deploy-auth.yml`:

```yaml
name: Deploy Auth Service

on:
  push:
    branches: [main]
    paths:
      - 'auth-service/**'
      - '.github/workflows/deploy-auth.yml'

env:
  AZURE_CONTAINER_REGISTRY: ${{ secrets.AZURE_REGISTRY_NAME }}.azurecr.io
  AZURE_REGISTRY_NAME: ${{ secrets.AZURE_REGISTRY_NAME }}
  BACKEND_IMAGE_NAME: auth-backend
  AZURE_CONTAINER_APP_NAME: auth-backend
  AZURE_RESOURCE_GROUP: auth-app
  FRONTEND_PATH: auth-service/auth-frontend

jobs:
  build-and-deploy-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Log in to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - name: Log in to ACR
        run: az acr login --name ${{ env.AZURE_REGISTRY_NAME }}
      - name: Build and push Docker image
        run: |
          docker build -t ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:${{ github.sha }} ./auth-service/AuthService.Api
          docker tag ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:${{ github.sha }} ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:latest
          docker push --all-tags ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}
      - name: Configure ACR credentials on Container App
        run: |
          az containerapp registry set \
            --name ${{ env.AZURE_CONTAINER_APP_NAME }} \
            --resource-group ${{ env.AZURE_RESOURCE_GROUP }} \
            --server ${{ env.AZURE_CONTAINER_REGISTRY }} \
            --username ${{ secrets.AZURE_REGISTRY_USERNAME }} \
            --password ${{ secrets.AZURE_REGISTRY_PASSWORD }}
      - name: Deploy to Azure Container Apps
        run: |
          az containerapp update \
            --name ${{ env.AZURE_CONTAINER_APP_NAME }} \
            --resource-group ${{ env.AZURE_RESOURCE_GROUP }} \
            --image ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:${{ github.sha }}

  deploy-frontend:
    runs-on: ubuntu-latest
    needs: build-and-deploy-backend
    steps:
      - uses: actions/checkout@v4
      - name: Deploy to Vercel
        run: npx vercel@latest --prod --token ${{ secrets.VERCEL_TOKEN }} --yes
        env:
          VERCEL_ORG_ID: ${{ secrets.VERCEL_ORG_ID }}
          VERCEL_PROJECT_ID: ${{ secrets.VERCEL_PROJECT_ID }}
```

---

## Phase 6: Test Your Deployment

```bash
curl -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"admin","password":"admin123"}'
```

Expected response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "role": "SystemAdmin",
  "name": "System Admin",
  "employeeNumber": "admin"
}
```

Also check the Scalar API docs at `https://auth-backend.abc123.southeastasia.azurecontainerapps.io/scalar/v1`

---

## Phase 7: Preparing for Integration with Other Systems

This is the most important section. When another system team wants to integrate with your Auth Service, here's everything they need.

### 7.1 What Other Systems Need From You

Copy this table and send it to the other system's team:

```
┌──────────────────────────┬──────────────────────────────────────────────────┐
│ Item                     │ Value                                           │
├──────────────────────────┼──────────────────────────────────────────────────┤
│ Auth Service URL         │ https://auth-backend.abc123.southeastasia.azurecontainerapps.io │
│ Login Endpoint           │ POST /api/auth/login                             │
│ Request Body             │ { "employeeNumber": "...", "password": "..." }   │
│ Response Body            │ { "accessToken": "eyJ...", "role": "...", ... }  │
│ JWT Header               │ Authorization: Bearer <token>                    │
│ Shared Jwt:Key           │ ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes! │
│ Jwt:Issuer               │ CentralAuth                                      │
│ Jwt:Audience             │ InternalSystems                                  │
│ JWT Claim: sub           │ User's GUID ID                                   │
│ JWT Claim: role          │ e.g. "Operational.Employee", "SystemAdmin"       │
│ JWT Claim: name          │ User's display name                              │
│ JWT Claim: employeeNumber│ User's employee number for login                 │
│ Token Expiration         │ 60 minutes (configurable)                        │
└──────────────────────────┴──────────────────────────────────────────────────┘
```

### 7.2 How Other Systems Validate the JWT

Other systems need this in their `Program.cs`:

```csharp
// Other system's Program.cs — copy this exactly
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
                Encoding.UTF8.GetBytes("ThisIsASuperSecretKeyForJwtThatIsAtLeast32Bytes!"))
        };
    });
```

### 7.3 How to Create Service Accounts for Other Systems

When another system needs to call your Auth Service programmatically (machine-to-machine), they need a **service account**. Create one for them:

```bash
# Login as admin first, get the token
TOKEN=$(curl -s -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"admin","password":"admin123"}' | jq -r '.accessToken')

# Create a service account for the other system
curl -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/admin/accounts \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "employeeNumber": "SVC-OTHER-SYSTEM",
    "name": "Other System Service Account",
    "email": "svc-other@system.com",
    "password": "generate-a-secure-password",
    "role": "OtherSystem.ExternalService"
  }'
```

> ⚠️ The role format is `<CallingSystem>.ExternalService`. If the other system is named "Finance", use `Finance.ExternalService`.

### 7.4 How to Test the Integration (From the Other System's Perspective)

The other system can test by calling your Auth Service:

```bash
# Step 1: Login as service account
curl -X POST https://auth-backend.abc123.southeastasia.azurecontainerapps.io/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNumber":"SVC-OTHER-SYSTEM","password":"generate-a-secure-password"}'

# Step 2: Use the returned token to call APIs
# (in their own system, they'd attach this to requests)
```

### 7.5 Your Integration Endpoints (What Others Can Call)

The Auth Service exposes:

- `POST /api/auth/login` — No auth needed. Anyone can log in.
- `POST /api/admin/accounts` — Requires `SystemAdmin` role. For creating accounts.

Other systems should only call `POST /api/auth/login` to authenticate their service accounts. The admin endpoints are for human admins only.

### 7.6 Security Notes

- The `Jwt:Key` must be **identical** across ALL systems. Share it securely (not in email/chat).
- Each system should store the key in GitHub Secrets or Azure environment variables, never in source code.
- Service account passwords should be generated securely and shared via a secure channel.
- The role naming convention (`<System>.ExternalService`) prevents systems from accidentally accessing each other's APIs.

---

## Troubleshooting (Auth Service Specific)

### Backend fails to start

```bash
az containerapp logs show --name auth-backend --resource-group auth-app --tail 50
```

Common causes: wrong `dbconn` secret, `Jwt:Key` too short, PostgreSQL firewall blocking.

### Frontend can't log in (405 error)

The `VITE_AUTH_URL` is missing `https://`. Fix in Vercel → project → Settings → Environment Variables.

---

## Cost Summary

| Service | Tier | Cost |
|---|---|---|
| PostgreSQL | Burstable B1MS (750h/month free) | $0 |
| Container Registry | Standard (1 free for 12 months) | $0 |
| Container Apps | Consumption (scale to zero) | $0 |
| Vercel | Hobby (free) | $0 |
| GitHub Actions | Public repo (free) | $0 |
| **Total** | | **$0/month** |
