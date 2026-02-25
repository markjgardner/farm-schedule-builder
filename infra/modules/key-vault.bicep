// ---------------------------------------------------------------------------
// Module: Azure Key Vault
// Standard vault with RBAC authorization and purge protection enabled.
// Used for centralised secret management.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Principal ID of the Function App managed identity for RBAC assignments. Leave empty to skip.')
param functionAppPrincipalId string = ''

var keyVaultName = take('kv-${baseName}', 24)

resource keyVault'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

// ---- RBAC Role Assignments ------------------------------------------------

// Key Vault Secrets User — allows the Function App to read secrets
resource keyVaultSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionAppPrincipalId)) {
  name: guid(keyVault.id, functionAppPrincipalId, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('Key Vault resource ID.')
output keyVaultId string = keyVault.id

@description('Key Vault name.')
output keyVaultName string = keyVault.name

@description('Key Vault URI.')
output keyVaultUri string = keyVault.properties.vaultUri
