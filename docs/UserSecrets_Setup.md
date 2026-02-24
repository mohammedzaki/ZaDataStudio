# User Secrets Setup for Semantic Matching

This file shows you how to configure the OpenAI API key securely for development using .NET User Secrets.

## Why User Secrets?

✅ **Secure** - Not stored in project files  
✅ **Safe** - Won't be committed to Git  
✅ **Convenient** - Persists across restarts  
✅ **Isolated** - Per-user, per-project  

## Setup Steps

### 1. Initialize User Secrets

Open PowerShell/Terminal in the Web project directory:

```powershell
cd src\ZaDataStudio.Web
dotnet user-secrets init
```

Expected output:
```
Set UserSecretsId to 'some-guid-here' for MSBuild project 'D:\...\ZaDataStudio.Web.csproj'.
```

### 2. Set Your OpenAI API Key

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-YOUR-ACTUAL-KEY-HERE"
```

Replace `sk-proj-YOUR-ACTUAL-KEY-HERE` with your real API key from https://platform.openai.com/api-keys

Expected output:
```
Successfully saved OpenAI:ApiKey = sk-proj-YOUR-ACTUAL-KEY-HERE to the secret store.
```

### 3. (Optional) Set Other Configuration

```powershell
# Change model (default is text-embedding-3-small)
dotnet user-secrets set "OpenAI:Model" "text-embedding-3-large"

# Change similarity threshold (default is 0.75)
dotnet user-secrets set "SemanticMatching:SimilarityThreshold" "0.80"

# Disable semantic matching temporarily
dotnet user-secrets set "SemanticMatching:Enabled" "false"
```

### 4. Verify Configuration

```powershell
dotnet user-secrets list
```

Expected output:
```
OpenAI:ApiKey = sk-proj-YOUR-ACTUAL-KEY-HERE
SemanticMatching:Enabled = true
SemanticMatching:SimilarityThreshold = 0.75
```

### 5. Run the Application

```powershell
dotnet run
```

The application will automatically read from user secrets!

---

## Where Are User Secrets Stored?

User secrets are stored in your user profile directory:

**Windows**:
```
%APPDATA%\Microsoft\UserSecrets\{project-guid}\secrets.json
```

**macOS/Linux**:
```
~/.microsoft/usersecrets/{project-guid}/secrets.json
```

You can edit this file directly if needed, but using the CLI is recommended.

---

## Production Setup (Do NOT use User Secrets!)

For production/staging environments, use one of these methods:

### Option 1: Environment Variables

**Windows PowerShell**:
```powershell
$env:OpenAI__ApiKey = "sk-proj-PRODUCTION-KEY"
$env:SemanticMatching__Enabled = "true"
```

**Linux/macOS**:
```bash
export OpenAI__ApiKey="sk-proj-PRODUCTION-KEY"
export SemanticMatching__Enabled="true"
```

**Azure App Service** (Portal → Configuration → Application Settings):
```
Name: OpenAI__ApiKey
Value: sk-proj-PRODUCTION-KEY
```

### Option 2: Azure Key Vault

1. Create Key Vault in Azure Portal
2. Add secret: `OpenAI--ApiKey` = `sk-proj-PRODUCTION-KEY`
3. Update Program.cs to read from Key Vault:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-vault.vault.azure.net/"),
    new DefaultAzureCredential());
```

---

## Common Issues

### "Could not find the specified user secrets"

**Solution**: Make sure you ran `dotnet user-secrets init` first

### "Permission denied"

**Solution**: Run PowerShell/Terminal as Administrator

### "API key still showing empty"

**Check**:
1. User secrets are in the **Web** project (not Application)
2. Secret key is exactly: `OpenAI:ApiKey` (case-sensitive)
3. Run `dotnet user-secrets list` to verify

---

## Security Best Practices

1. ✅ **Never** commit API keys to Git
2. ✅ **Never** put API keys in appsettings.json (even commented out)
3. ✅ Use different API keys for dev/test/prod
4. ✅ Rotate keys every 90 days
5. ✅ Monitor usage on OpenAI dashboard
6. ✅ Set spending limits ($5-10 for development)

---

## Quick Reference

```powershell
# Initialize (first time only)
dotnet user-secrets init

# Set API key
dotnet user-secrets set "OpenAI:ApiKey" "your-key"

# View all secrets
dotnet user-secrets list

# Remove a secret
dotnet user-secrets remove "OpenAI:ApiKey"

# Clear all secrets (careful!)
dotnet user-secrets clear

# Get help
dotnet user-secrets --help
```

---

## Next Steps

1. ✅ Initialize user secrets
2. ✅ Get your OpenAI API key from https://platform.openai.com/api-keys
3. ✅ Set the key using `dotnet user-secrets set`
4. ✅ Run the application with `dotnet run`
5. ✅ Test semantic matching with your data!

For more help, see:
- `docs\SemanticMatching_QuickStart.md`
- https://learn.microsoft.com/aspnet/core/security/app-secrets

---

**Ready to start?** Run the commands above and you're good to go! 🚀
