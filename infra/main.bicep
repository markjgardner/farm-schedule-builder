// ---------------------------------------------------------------------------
// Farm Schedule Builder — Main Bicep Orchestrator
//
// Composes all infrastructure modules and wires RBAC role assignments so the
// Function App managed identity can access Storage, Service Bus, and Key Vault
// without connection strings.
// ---------------------------------------------------------------------------

targetScope = 'resourceGroup'

// ---- Parameters -----------------------------------------------------------

@description('Environment name (e.g. dev, staging, prod).')
param environmentName string

@description('Primary Azure region for all resources.')
param location string = resourceGroup().location

@description('Prefix used in resource names to avoid collisions.')
param resourceNamePrefix string = 'farm'

// ---- Variables ------------------------------------------------------------

// Generate a unique base name using the prefix, environment, and resource group
var baseName = '${resourceNamePrefix}-${environmentName}-${uniqueString(resourceGroup().id)}'

// Common tags applied to every resource
var commonTags = {
  environment: environmentName
  project: 'farm-schedule-builder'
}

// ---- Modules --------------------------------------------------------------

// Application Insights + Log Analytics
module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
  }
}

// Function App (depends on App Insights and initial resource deployments for config values)
module functionApp 'modules/function-app.bicep' = {
  name: 'functionApp'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
    appInsightsConnectionString: appInsights.outputs.connectionString
    storageAccountName: storage.outputs.storageAccountName
    serviceBusEndpoint: serviceBus.outputs.serviceBusEndpoint
    keyVaultUri: keyVault.outputs.keyVaultUri
    cosmosEndpoint: cosmos.outputs.cosmosEndpoint
  }
}

// Storage Account (initial deployment without RBAC)
module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
  }
}

// Service Bus namespace, topic, and subscription (initial deployment without RBAC)
module serviceBus 'modules/service-bus.bicep' = {
  name: 'serviceBus'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
  }
}

// Key Vault (initial deployment without RBAC)
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
  }
}

// Cosmos DB (initial deployment without RBAC)
module cosmos 'modules/cosmos-db.bicep' = {
  name: 'cosmos'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
  }
}

// ---- RBAC Role Assignments ------------------------------------------------
// Re-deploy resource modules with the Function App principal ID to create
// scoped role assignments. Modules use conditional resources to apply RBAC
// only when a principalId is provided.

module storageRbac 'modules/storage.bicep' = {
  name: 'storageRbac'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
    functionAppPrincipalId: functionApp.outputs.principalId
  }
  dependsOn: [storage, functionApp]
}

module serviceBusRbac 'modules/service-bus.bicep' = {
  name: 'serviceBusRbac'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
    functionAppPrincipalId: functionApp.outputs.principalId
  }
  dependsOn: [serviceBus, functionApp]
}

module keyVaultRbac 'modules/key-vault.bicep' = {
  name: 'keyVaultRbac'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
    functionAppPrincipalId: functionApp.outputs.principalId
  }
  dependsOn: [keyVault, functionApp]
}

module cosmosRbac 'modules/cosmos-db.bicep' = {
  name: 'cosmosRbac'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
    functionAppPrincipalId: functionApp.outputs.principalId
  }
  dependsOn: [cosmos]
}

// Static Web App with linked backend to Function App
module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'staticWebApp'
  params: {
    baseName: baseName
    location: location
    tags: commonTags
    functionAppId: functionApp.outputs.functionAppId
  }
}

// ---- Outputs --------------------------------------------------------------

@description('Static Web App default hostname.')
output staticWebAppHostname string = staticWebApp.outputs.defaultHostname

@description('Static Web App resource name.')
output staticWebAppName string = staticWebApp.outputs.staticWebAppName

@description('Function App name.')
output functionAppName string = functionApp.outputs.functionAppName

@description('Storage Account name.')
output storageAccountName string = storage.outputs.storageAccountName

@description('Service Bus namespace name.')
output serviceBusNamespaceName string = serviceBus.outputs.serviceBusNamespaceName

@description('Cosmos DB Account name.')
output cosmosAccountName string = cosmos.outputs.cosmosAccountName
