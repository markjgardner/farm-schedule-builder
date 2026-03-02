// ---------------------------------------------------------------------------
// Module: Azure Function App (Flex Consumption)
// .NET 8 Isolated worker runtime on the FC1 Flex Consumption plan.
// Uses system-assigned managed identity for all Azure service access.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Application Insights connection string for telemetry.')
param appInsightsConnectionString string

@description('Name of the Storage Account used for deployment packages and runtime.')
param storageAccountName string

@description('Service Bus fully qualified namespace (e.g. sb-xxx.servicebus.windows.net).')
param serviceBusEndpoint string

@description('Key Vault URI for secret references.')
param keyVaultUri string

@description('Cosmos DB account document endpoint.')
param cosmosEndpoint string

// Flex Consumption hosting plan (FC1 SKU)
resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-${baseName}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'func-${baseName}'
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}/app-package-container'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
    }
    siteConfig: {
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'ServiceBus__fullyQualifiedNamespace'
          value: serviceBusEndpoint
        }
        {
          name: 'KeyVaultUri'
          value: keyVaultUri
        }
        {
          name: 'CosmosDbEndpoint'
          value: cosmosEndpoint
        }
      ]
    }
  }
}

@description('Function App resource ID.')
output functionAppId string = functionApp.id

@description('Function App name.')
output functionAppName string = functionApp.name

@description('Function App default hostname.')
output functionAppHostname string = functionApp.properties.defaultHostName

@description('System-assigned managed identity principal ID.')
output principalId string = functionApp.identity.principalId
