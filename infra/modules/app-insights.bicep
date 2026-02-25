// ---------------------------------------------------------------------------
// Module: Application Insights + Log Analytics Workspace
// Provides monitoring and diagnostics for the Function App and Static Web App.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

// Log Analytics Workspace — central log sink
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${baseName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights — telemetry collection
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${baseName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

@description('Application Insights resource ID.')
output appInsightsId string = appInsights.id

@description('Application Insights instrumentation key.')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights connection string.')
output connectionString string = appInsights.properties.ConnectionString

@description('Application Insights resource name.')
output appInsightsName string = appInsights.name

@description('Log Analytics Workspace resource ID.')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
