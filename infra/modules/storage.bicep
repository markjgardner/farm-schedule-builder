// ---------------------------------------------------------------------------
// Module: Azure Storage Account
// Provides blob storage for Function App deployment packages.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
@minLength(1)
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Principal ID of the Function App managed identity for RBAC assignments. Leave empty to skip.')
param functionAppPrincipalId string = ''

// Storage account names must be 3-24 lowercase alphanumeric characters
var storageAccountRawName = 'st${replace(baseName, '-', '')}'
var storageAccountName = substring(storageAccountRawName, 0, min(length(storageAccountRawName), 24))

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // Allow shared key access for Table data plane and Function App deployment storage.
    // Managed identity is used for application-level access via RBAC.
    allowSharedKeyAccess: true
  }
}

// Blob service — used for Function App deployment packages
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storageAccount
  name: 'default'
}

// Container for Function App deployment packages
resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobService
  name: 'app-package-container'
}

// ---- RBAC Role Assignments ------------------------------------------------

// Storage Blob Data Owner — allows the Function App to manage deployment blobs
resource storageBlobDataOwnerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionAppPrincipalId)) {
  name: guid(storageAccount.id, functionAppPrincipalId, 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('Storage Account resource ID.')
output storageAccountId string = storageAccount.id

@description('Storage Account name.')
output storageAccountName string = storageAccount.name

@description('Primary blob endpoint.')
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
