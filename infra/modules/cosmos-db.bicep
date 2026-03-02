// ---------------------------------------------------------------------------
// Module: Azure Cosmos DB (Serverless NoSQL)
// Provides document storage for farm worker data, availability windows,
// barn configurations, and blackout periods.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Principal ID of the Function App managed identity for RBAC assignments. Leave empty to skip.')
param functionAppPrincipalId string = ''

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: 'cosmos-${baseName}'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'FarmScheduler'
  properties: {
    resource: {
      id: 'FarmScheduler'
    }
  }
}

// Workers container — stores farm worker records
resource workersContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'workers'
  properties: {
    resource: {
      id: 'workers'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
  }
}

// Availability container — stores worker availability windows (30-day TTL)
resource availabilityContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'availability'
  properties: {
    resource: {
      id: 'availability'
      partitionKey: {
        paths: ['/windowStart']
        kind: 'Hash'
      }
      defaultTtl: 2592000
    }
  }
}

// Barn configs container — stores barn configuration data
resource barnConfigsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'barnConfigs'
  properties: {
    resource: {
      id: 'barnConfigs'
      partitionKey: {
        paths: ['/barn']
        kind: 'Hash'
      }
    }
  }
}

// Blackouts container — stores blackout periods (per-document TTL)
resource blackoutsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'blackouts'
  properties: {
    resource: {
      id: 'blackouts'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
      defaultTtl: -1
    }
  }
}

// ---- RBAC Role Assignments ------------------------------------------------

// Cosmos DB Built-in Data Contributor — allows read/write to Cosmos DB data
resource cosmosDataContributorAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = if (!empty(functionAppPrincipalId)) {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, functionAppPrincipalId, '00000000-0000-0000-0000-000000000002')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: functionAppPrincipalId
    scope: cosmosAccount.id
  }
}

@description('Cosmos DB Account name.')
output cosmosAccountName string = cosmosAccount.name

@description('Cosmos DB Account document endpoint.')
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
