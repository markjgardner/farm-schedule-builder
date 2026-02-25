// ---------------------------------------------------------------------------
// Module: Azure Service Bus
// Standard-tier namespace with a topic for schedule generation events.
// ---------------------------------------------------------------------------

@description('Base name used for resource naming.')
param baseName string

@description('Azure region for deployment.')
param location string

@description('Tags to apply to all resources.')
param tags object = {}

@description('Principal ID of the Function App managed identity for RBAC assignments. Leave empty to skip.')
param functionAppPrincipalId string = ''

resource serviceBusNamespace'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: 'sb-${baseName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

// Topic for schedule generation events
resource scheduleGeneratedTopic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: serviceBusNamespace
  name: 'schedule-generated'
  properties: {
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P14D'
  }
}

// Default subscription on the schedule-generated topic
resource defaultSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: scheduleGeneratedTopic
  name: 'default'
  properties: {
    maxDeliveryCount: 10
    defaultMessageTimeToLive: 'P14D'
    lockDuration: 'PT1M'
  }
}

// ---- RBAC Role Assignments ------------------------------------------------

// Azure Service Bus Data Sender — allows the Function App to publish messages
resource serviceBusDataSenderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionAppPrincipalId)) {
  name: guid(serviceBusNamespace.id, functionAppPrincipalId, '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('Service Bus Namespace resource ID.')
output serviceBusNamespaceId string = serviceBusNamespace.id

@description('Service Bus Namespace name.')
output serviceBusNamespaceName string = serviceBusNamespace.name

@description('Service Bus Namespace fully qualified domain name.')
output serviceBusEndpoint string = '${serviceBusNamespace.name}.servicebus.windows.net'
