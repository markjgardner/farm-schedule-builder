// ---------------------------------------------------------------------------
// Module: Azure Static Web App
// Standard SKU hosting the React frontend with Easy Auth providers
// (Google, Microsoft, Facebook) and a linked backend to the Function App.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Resource ID of the Function App to link as backend.')
param functionAppId string

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'swa-${baseName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    // Easy Auth identity providers are configured at the app level via
    // staticwebapp.config.json (Google, Microsoft, Facebook).
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
  }
}

// Link the Function App as the backend API for the Static Web App.
// This enables /api/* requests to be proxied to the Function App.
resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2024-04-01' = {
  parent: staticWebApp
  name: 'backend'
  properties: {
    backendResourceId: functionAppId
    region: location
  }
}

@description('Static Web App default hostname.')
output defaultHostname string = staticWebApp.properties.defaultHostname

@description('Static Web App resource ID.')
output staticWebAppId string = staticWebApp.id

@description('Static Web App name.')
output staticWebAppName string = staticWebApp.name
