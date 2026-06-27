# Industry-Standard Deployment Guide

A beginner-friendly, step-by-step guide to deploying this CRUD app for real clients.

## What We're Building

```
User's Browser
     │
     ├── Static files (HTML, JS, CSS) → Vercel (global CDN)
     │
     └── API calls (/api/*) → Azure Container Apps (ASP.NET Core)
                                   │
                                   └── Azure Database for PostgreSQL
```

Every push to `main` on GitHub auto-deploys everything via **GitHub Actions**.

---

## What You'll Need

### Accounts (all free to start)
| Account | Sign Up Link | Why |
|---|---|---|
| GitHub | https://github.com | Store code, run CI/CD |
| Vercel | https://vercel.com | Host the frontend (free tier: 100GB bandwidth) |
| Azure | https://azure.microsoft.com/free/students | Host backend + database (get $100 free credits with school email) |
| Docker Hub | https://hub.docker.com (optional) | Store Docker images (public repos free) |

### Tools to Install on Your Computer
1. **Git** — https://git-scm.com
2. **Azure CLI** — https://aka.ms/installazurecliwindows
3. **Docker Desktop** — https://www.docker.com/products/docker-desktop
4. **VS Code** (or any editor) — https://code.visualstudio.com

---

## How This All Fits Together

Every time you push code to GitHub:
1. **GitHub Actions** starts a build
2. It builds the backend into a **Docker image**
3. It pushes the image to **Azure Container Registry** (Docker storage)
4. It tells **Azure Container Apps** to use the new image
5. It tells **Vercel** to rebuild the frontend
6. Your users see the new version with zero downtime

You don't manually deploy after this. Git push = deploy.

---

# Step-by-Step Instructions

## Phase 1: Prepare Your Project

### 1.1 Push to GitHub (if not done)

```bash
cd E:\ProjectTestingLang

# If you haven't pushed yet:
git init
git add -A
git commit -m "Initial commit"
git branch -m main
git remote add origin https://github.com/your-username/crud-app.git
git push -u origin main
```

Your repo URL will be like: `https://github.com/your-username/crud-app`

---

## Phase 2: Set Up Azure

Azure is Microsoft's cloud. We use it for two things: hosting the backend API and hosting the database.

### 2.1 Sign Up for Azure for Students

1. Go to https://azure.microsoft.com/free/students
2. Sign in with your school email
3. Verify your student status (usually instant)
4. You get **$100 in free credits** — no credit card needed
5. These credits last 12 months (if you don't use them all)

### 2.2 Install and Log In to Azure CLI

```bash
# Install Azure CLI (Windows): https://aka.ms/installazurecliwindows
# After installing, restart PowerShell, then:

az login
```

This opens a browser. Log in with the same Microsoft account you used for Azure.

### 2.3 Create a Resource Group

A resource group is like a folder that holds all your Azure services together.

```bash
az group create --name crud-app --location eastus
```

- `--name crud-app` — you can name it anything
- `--location eastus` — choose a region close to you (eastus, westus, southeastasia, etc.)

### 2.4 Create the PostgreSQL Database

```bash
az postgres flexible-server create --name crud-db --resource-group crud-app --location eastus --admin-user postgres --admin-password MyStr0ngP@ss! --sku-name Standard_B1ms --tier Burstable --storage-size 32 --public-access 0.0.0.0
```

**What each flag does:**
- `--name crud-db` — the server name (must be globally unique)
- `--admin-user postgres` — database username
- `--admin-password` — database password (make it strong, save it somewhere)
- `--sku-name Standard_B1ms` — cheapest tier ($15/mo, covered by your $100 credits)
- `--storage-size 32` — 32GB of storage
- `--public-access 0.0.0.0` — allow Azure services to connect

**Wait 3-5 minutes** for this to finish.

### 2.5 Get the Database Connection String

```bash
az postgres flexible-server show-connection-string --server crud-db --database postgres --resource-group crud-app --query "connectionStrings.ado.net"
```

This outputs something like:
```
Server=crud-db.postgres.database.azure.com;Database=postgres;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true
```

**Save this somewhere**. You'll need it for the backend.

> **Troubleshooting**: If the command fails, you can find the connection string in the Azure Portal:
> 1. Go to https://portal.azure.com
> 2. Search "crud-db" in the top bar
> 3. Click your database server
> 4. Left menu → **Connection strings**
> 5. Copy the **ADO.NET** connection string
> 6. Replace `{your_password}` with your actual password

### 2.6 Create Azure Container Registry

Azure Container Registry (ACR) is a place to store Docker images (like a private Docker Hub).

```bash
az acr create --name crudregistry2026 --resource-group crud-app --location eastus --sku Basic --admin-enabled true
```

> **Note**: `crudregistry2026` must be globally unique. If it's taken, try `crudappregistry123`, `mycrudregistry`, etc. The name can contain letters and numbers only.

Get the admin credentials (we'll need these for GitHub Actions):

```bash
az acr credential show --name crudregistry2026 --resource-group crud-app
```

Save the **username** and one of the **passwords** shown.

### 2.7 Create the Container Apps Environment

Azure Container Apps runs your Docker containers without managing servers (serverless).

```bash
az containerapp env create --name crud-env --resource-group crud-app --location eastus
```

### 2.8 Deploy the Backend to Azure Container Apps

```bash
az containerapp create --name crud-backend --resource-group crud-app --environment crud-env --image mcr.microsoft.com/dotnet/samples:aspnetapp --target-port 8080 --ingress external --min-replicas 1 --max-replicas 10 --secrets dbconn="Server=crud-db.postgres.database.azure.com;Database=postgres;Port=5432;User Id=postgres;Password=MyStr0ngP@ss!;Ssl Mode=Require;Trust Server Certificate=true" --env-vars ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:8080 ConnectionStrings__DefaultConnection=secretref:dbconn Jwt__Key=YourSuperSecretKeyThatIsAtLeast32CharactersLong! Jwt__Issuer=Backend Jwt__Audience=Frontend
```

**Important**: Replace `server=...`, `password=...` with YOUR actual database values from step 2.5.

**What each flag does:**
- `--name crud-backend` — name of this container app
- `--image` — which Docker image to use (we'll update this later with our actual app)
- `--target-port 8080` — the port our backend listens on
- `--ingress external` — accessible from the internet
- `--min-replicas 1` — always keep at least 1 instance running
- `--max-replicas 10` — auto-scale up to 10 when traffic is high
- `--secrets dbconn="..."` — stores the DB password securely (not as plain text)
- `secretref:dbconn` — references the secret above instead of putting the password in an env var

### 2.9 Get the Backend URL

```bash
az containerapp show --name crud-backend --resource-group crud-app --query "properties.configuration.ingress.fqdn" --output tsv
```

This outputs something like: `crud-backend.cleverhill-abc123.eastus.azurecontainerapps.io`

Your backend API lives at: `https://crud-backend.cleverhill-abc123.eastus.azurecontainerapps.io`

> **Testing**: Open this URL in a browser. You should see `404` (that's fine — it means the API is running but `/` has no route). Try `https://your-url/swagger` to see the API docs.

---

## Phase 3: Set Up Continuous Deployment with GitHub Actions

GitHub Actions is an automation tool built into GitHub. Every time you push code, it runs a workflow that builds and deploys your app automatically.

### 3.1 Create GitHub Repository Secrets

Secrets are like environment variables but encrypted. GitHub sends them to the Actions runner when needed.

In your browser:
1. Go to your GitHub repo: `https://github.com/your-username/crud-app`
2. Click **Settings** tab (near the top right)
3. Left menu → **Secrets and variables** → **Actions**
4. Click **New repository secret**

Add these secrets one at a time:

| Secret Name | Value |
|---|---|
| `AZURE_CREDENTIALS` | (see step 3.1.1 below) |
| `AZURE_REGISTRY_USERNAME` | The username from `az acr credential show` (step 2.6) |
| `AZURE_REGISTRY_PASSWORD` | The password from `az acr credential show` (step 2.6) |

#### 3.1.1 Create `AZURE_CREDENTIALS`

Run this command in PowerShell to let GitHub log into Azure:

```bash
az ad sp create-for-rbac --name "crud-app-github" --role contributor --scopes /subscriptions/$(az account show --query id -o tsv)/resourceGroups/crud-app --sdk-auth
```

This outputs a big JSON block. Copy the **entire output** (including the `{}` braces) and paste it as the `AZURE_CREDENTIALS` secret in GitHub.

> **What is this?** It creates a "service principal" — an automated account that GitHub Actions can use to log into Azure and deploy things.

### 3.2 Create the GitHub Actions Workflow File

In your project, create this folder structure:

```
YourProject/
├── .github/
│   └── workflows/
│       └── deploy.yml
```

Create the file `.github/workflows/deploy.yml` with this content:

```yaml
name: Deploy to Production

on:
  push:
    branches: [main]

env:
  AZURE_CONTAINER_REGISTRY: crudregistry2026.azurecr.io
  BACKEND_IMAGE_NAME: backend

jobs:
  build-and-deploy-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Log in to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Log in to Azure Container Registry
        run: az acr login --name crudregistry2026

      - name: Build and push backend Docker image
        run: |
          docker build -t ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:${{ github.sha }} ./Backend
          docker tag ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:${{ github.sha }} ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:latest
          docker push --all-tags ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}

      - name: Configure ACR credentials on Container App
        run: |
          az containerapp registry set --name crud-backend --resource-group crud-app --server ${{ env.AZURE_CONTAINER_REGISTRY }} --username ${{ secrets.AZURE_REGISTRY_USERNAME }} --password ${{ secrets.AZURE_REGISTRY_PASSWORD }}

      - name: Deploy to Azure Container Apps
        run: |
          az containerapp update --name crud-backend --resource-group crud-app --image ${{ env.AZURE_CONTAINER_REGISTRY }}/${{ env.BACKEND_IMAGE_NAME }}:${{ github.sha }}

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

**Replace** `crudregistry2026` in line 5 with your actual ACR name from step 2.6.

### 3.3 Update the GitHub Actions Workflow

We still need `VERCEL_TOKEN`, `VERCEL_ORG_ID`, and `VERCEL_PROJECT_ID`. Let's get those in Phase 4.

---

## Phase 4: Set Up Vercel (Frontend)

Vercel is a hosting platform for frontend apps. It gives you a global CDN, automatic SSL, and auto-deploys from GitHub.

### 4.1 Sign Up and Create a Project

1. Go to https://vercel.com
2. Sign up with GitHub (recommended — it makes setup automatic)
3. Click **Add New** → **Project**
4. Click **Import Git Repository**
5. Select your `crud-app` repository
6. Click **Import**
7. On the configure screen:
   - **Root Directory**: Click **Edit** → select `Frontend`
   - **Environment Variables**: Add one:
     - Name: `VITE_API_URL`
     - Value: `https://crud-backend.cleverhill-abc123.eastus.azurecontainerapps.io/api` (replace with YOUR backend URL from step 2.9)
8. Click **Deploy**

Vercel gives you a URL like: `https://crud-app.vercel.app`

### 4.2 Get Vercel Token and IDs (for GitHub Actions)

Open a new browser tab for each step:

#### 4.2.1 Vercel Token
1. Go to https://vercel.com/account/tokens
2. Click **Create**
3. Name it `GitHub Actions`
4. Scope: **Full Account**
5. Click **Create**
6. **Copy the token immediately** — it only shows once
7. Add it as a GitHub secret: repo → Settings → Secrets → `VERCEL_TOKEN`

#### 4.2.2 Vercel Org ID
1. Go to https://vercel.com/account
2. Under **General**, look for **ID** (it's a long string like `team_xxxxxxxx`)
3. Copy it and add as GitHub secret: `VERCEL_ORG_ID`

#### 4.2.3 Vercel Project ID
1. Go to your project dashboard on Vercel: `https://vercel.com/your-username/crud-app`
2. Click **Settings** (top right)
3. Scroll down to **Project ID**
4. Copy it and add as GitHub secret: `VERCEL_PROJECT_ID`

### 4.3 Test the Frontend

Open your Vercel URL in the browser (`https://crud-app.vercel.app`). You should see the login page. Try registering and logging in.

> **If API calls fail**: Open browser DevTools (F12) → Console → type `window.__API_URL__`. It should show your Azure backend URL with `/api` at the end. If it doesn't, re-check the `API_URL` environment variable in Vercel's project settings.

---

## Phase 5: Commit and Deploy

### 5.1 Push the GitHub Actions Workflow

```bash
# Create the workflows directory
mkdir -p .github/workflows

# Create the deploy.yml file (copy from section 3.2 above)
# If you created it already, skip this

# Add, commit, and push
git add -A
git commit -m "Add GitHub Actions CI/CD pipeline"
git push
```

### 5.2 Watch the Deployment

1. Go to your GitHub repo
2. Click **Actions** tab (top of the page)
3. You'll see a workflow running called "Deploy to Production"
4. Click it to watch the progress:
   - First job: **build-and-deploy-backend** (builds Docker image, pushes to ACR, updates Azure Container Apps)
   - Second job: **deploy-frontend** (triggers Vercel deployment)

### 5.3 Verify

Once both jobs show green checkmarks:
1. Refresh your frontend URL — your changes are live
2. Test everything: register, create products, edit, delete

---

## Phase 6: Add a Custom Domain (Optional but Recommended for Clients)

A custom domain makes your app look professional. Instead of `crud-app.vercel.app`, you get `https://yourapp.com`.

### 6.1 Buy a Domain

Buy a domain from any registrar (costs $8-15/year):

| Registrar | Price |
|---|---|
| Cloudflare | Cost price (cheapest) |
| Namecheap | ~$8-12/yr |
| Google Domains | ~$12/yr |

### 6.2 Set Up Cloudflare (Free)

Cloudflare provides free CDN, SSL, and DDoS protection. Most production apps use it.

1. Go to https://cloudflare.com → Sign up
2. Click **Add a Site** → enter your domain
3. Cloudflare scans your current DNS records
4. Click **Continue**
5. Choose the **Free** plan
6. Cloudflare gives you two nameservers (e.g., `jake.ns.cloudflare.com`)
7. Go to your domain registrar's website → find **Nameservers** → replace with Cloudflare's

### 6.3 Add DNS Records

In Cloudflare dashboard → your site → **DNS**:

| Type | Name | Content | Proxy |
|---|---|---|---|
| CNAME | `@` | `cname.vercel-dns.com` | Proxied (orange cloud) |
| CNAME | `www` | `cname.vercel-dns.com` | Proxied |

### 6.4 Add Domain to Vercel

1. Vercel → your project → **Settings** → **Domains**
2. Enter your domain (e.g., `yourapp.com`)
3. Click **Add**
4. Vercel automatically provisions an SSL certificate

### 6.5 Add Domain to Azure (for API)

To have `https://api.yourapp.com` point to your backend:

1. Cloudflare → DNS → Add record:
   - Type: `CNAME`
   - Name: `api`
   - Content: `crud-backend.cleverhill-abc123.eastus.azurecontainerapps.io`
   - Proxy: **DNS only** (gray cloud) — Azure manages its own SSL
2. In Azure Portal:
   - Search for **crud-backend** → **Settings** → **Custom domains**
   - Click **Add** → enter `api.yourapp.com`
   - Follow Azure's validation steps

---

## Phase 7: Monitoring (Application Insights)

Application Insights gives you dashboards showing:
- How many people are using the app
- How fast the API responds
- When errors happen
- What database queries are slow

### 7.1 Create the Resource

```bash
az monitor app-insights component create --app crud-insights --resource-group crud-app --location eastus --application-type web
```

### 7.2 Get the Connection String

```bash
az monitor app-insights component show --app crud-insights --resource-group crud-app --query connectionString
```

### 7.3 Add to Backend Environment Variables

```bash
az containerapp update --name crud-backend --resource-group crud-app --set-env-vars APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key-here"
```

Wait a few minutes, then view live data:
1. Azure Portal → **crud-insights** → **Live metrics**
2. Use the app in your browser — you'll see requests appearing in real-time

---

## Updating the App

To deploy a new version:

```bash
# Make your code changes
git add -A
git commit -m "Describe what you changed"
git push
```

That's it. GitHub Actions builds, deploys, and your clients see the update in 2-3 minutes.

---

## Costs Summary

| Service | Without Free Credits | With Azure for Students |
|---|---|---|
| Vercel (free tier) | $0 | $0 |
| Azure Container Apps | ~$10-15/mo | Covered by $100 credits |
| Azure Database for PostgreSQL | ~$15/mo | Covered by $100 credits |
| Azure Container Registry | ~$5/mo | Covered by $100 credits |
| GitHub Actions | $0 (public repo) | $0 |
| Cloudflare | $0 (free tier) | $0 |
| Domain name | $8-12/year | $8-12/year |
| **Monthly total** | **~$30/mo** | **~$0 for ~3 months** |

The $100 Azure student credits will cover all Azure costs for about 3-4 months.

---

## Troubleshooting

### Backend fails to start
```
az containerapp logs show --name crud-backend --resource-group crud-app --tail 50
```
Common causes:
- Wrong database password in the connection string
- Database server not accepting Azure connections (check firewall rules)
- JWT Key too short (must be 32+ characters)

### Frontend shows blank page or API errors
1. Open DevTools (F12) → Console
2. Type `window.__API_URL__` — should be your backend URL
3. If it's `/api`, the `API_URL` env var isn't set in Vercel
4. If it shows the URL but calls fail, the backend might not have CORS working (check `Program.cs` has `AllowAnyOrigin()`)

### GitHub Actions fails
1. Go to your repo → **Actions** → click the failed run
2. Read the red error message — it usually tells you exactly what's wrong
3. Common issues:
   - Secrets not set correctly → re-add them
   - Azure credentials expired → run the service principal command again
   - Docker build fails → test locally with `docker build -t test ./Backend`

### Domain not loading
- DNS can take 5-30 minutes to propagate
- Use `ping yourdomain.com` to check if it resolves
- Cloudflare should show "Proxied" (orange cloud) for the root domain
