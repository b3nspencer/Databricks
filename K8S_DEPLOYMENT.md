# Kubernetes Deployment Guide

Complete guide for deploying the Databricks C# Service to AKS with managed identity authentication.

## Architecture

```
┌─────────────────────────────────────────────┐
│         AKS Cluster (Azure)                 │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │  Namespace: default                 │   │
│  │                                     │   │
│  │  ┌──────────────────────────────┐   │   │
│  │  │ Pod (Databricks Service)     │   │   │
│  │  │  + Managed Identity          │   │   │
│  │  │  + ConfigMap (config)        │   │   │
│  │  │  + ServiceAccount            │   │   │
│  │  └──────────────────────────────┘   │   │
│  │           ↓ (HTTPS)                  │   │
│  │  ┌──────────────────────────────┐   │   │
│  │  │ Service (ClusterIP)          │   │   │
│  │  │ Port 8080                    │   │   │
│  │  └──────────────────────────────┘   │   │
│  └─────────────────────────────────────┘   │
│           ↓ (HTTPS)                        │
└──────────┬────────────────────────────────┘
           │
    ┌──────▼───────┐
    │ Azure AD     │
    │ (Workload    │
    │  Identity)   │
    └──────┬───────┘
           │
    ┌──────▼──────────────────────┐
    │ Azure Databricks API         │
    │ (SQL Warehouse)              │
    └─────────────────────────────┘
```

## Prerequisites

### Azure Resources

1. **AKS Cluster** with workload identity enabled
   ```bash
   az aks create \
     --resource-group myRG \
     --name myCluster \
     --enable-oidc-issuer \
     --enable-workload-identity
   ```

2. **Databricks Workspace** with SQL Warehouse
   - Note your workspace URL and warehouse ID

3. **User-Assigned Managed Identity**
   ```bash
   az identity create \
     --resource-group myRG \
     --name databricks-service-identity
   ```

### Local Tools

- `kubectl` (1.24+)
- `docker` (for building image)
- `az` CLI
- Access to Azure Container Registry

## Step 1: Prepare Databricks Credentials

### Option A: Databricks Service Principal (Recommended)

```bash
# Create service principal in Databricks
# In Databricks workspace, go to Admin Console → Service Principals
# Create new service principal and get the token

# Store in Key Vault
az keyvault secret set \
  --vault-name myKeyVault \
  --name databricks-pat \
  --value "dapi1234567890abcdef"
```

### Option B: Personal Access Token

```bash
# In Databricks workspace, generate PAT from profile
# Store in Key Vault
az keyvault secret set \
  --vault-name myKeyVault \
  --name databricks-pat \
  --value "dapi1234567890abcdef"
```

## Step 2: Create Managed Identity

```bash
# Create managed identity
az identity create \
  --resource-group myRG \
  --name databricks-service

# Get client ID and subscription ID
MANAGED_IDENTITY_CLIENT_ID=$(az identity show \
  --resource-group myRG \
  --name databricks-service \
  --query 'clientId' -o tsv)

SUBSCRIPTION_ID=$(az account show --query 'id' -o tsv)
IDENTITY_RESOURCE_ID="/subscriptions/${SUBSCRIPTION_ID}/resourcegroups/myRG/providers/Microsoft.ManagedIdentity/userAssignedIdentities/databricks-service"
```

## Step 3: Configure Workload Identity Federation

```bash
# Get AKS OIDC issuer URL
OIDC_ISSUER=$(az aks show \
  --resource-group myRG \
  --name myCluster \
  --query 'oidcIssuerProfile.issuerUrl' -o tsv)

# Create federated identity credential
az identity federated-credential create \
  --name databricks-service-fed \
  --identity-name databricks-service \
  --resource-group myRG \
  --issuer "${OIDC_ISSUER}" \
  --subject "system:serviceaccount:default:databricks-service"
```

## Step 4: Grant Key Vault Permissions

```bash
# Grant managed identity access to Key Vault secrets
az keyvault set-policy \
  --name myKeyVault \
  --object-id $(az identity show \
    --resource-group myRG \
    --name databricks-service \
    --query 'principalId' -o tsv) \
  --secret-permissions get list
```

## Step 5: Build and Push Docker Image

```bash
# Get registry login server
REGISTRY=$(az acr show \
  --resource-group myRG \
  --name myRegistry \
  --query 'loginServer' -o tsv)

# Build image
docker build -t ${REGISTRY}/databricks-service:latest .

# Push to registry
az acr login --name myRegistry
docker push ${REGISTRY}/databricks-service:latest
```

## Step 6: Update Kubernetes Manifests

### Update ConfigMap (k8s/configmap.yaml)

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: databricks-config
  namespace: default
data:
  workspace-url: "https://adb-12345678.azuredatabricks.net"
  warehouse-id: "abc123def456"
  keyvault-url: "https://mykeyvault.vault.azure.net/"
  token-secret-name: "databricks-pat"
```

### Update ServiceAccount (k8s/serviceaccount.yaml)

Update the client ID:

```yaml
annotations:
  azure.workload.identity/client-id: "${MANAGED_IDENTITY_CLIENT_ID}"
```

### Update Deployment (k8s/deployment.yaml)

Update the image:

```yaml
image: ${REGISTRY}/databricks-service:latest
```

## Step 7: Deploy to Kubernetes

```bash
# Get AKS credentials
az aks get-credentials \
  --resource-group myRG \
  --name myCluster

# Create namespace (optional)
kubectl create namespace default

# Apply manifests
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/serviceaccount.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

## Step 8: Verify Deployment

```bash
# Check pod status
kubectl get pods -l app=databricks-service
kubectl describe pod <pod-name>

# Check logs
kubectl logs -f -l app=databricks-service

# Check events
kubectl get events --sort-by='.lastTimestamp'

# Test health check
kubectl port-forward svc/databricks-service 8080:8080
curl http://localhost:8080/health
```

## Step 9: Test the Service

### Option A: Port Forward and Local Test

```bash
# Port forward
kubectl port-forward svc/databricks-service 8080:8080

# In another terminal, test
curl http://localhost:8080/health
```

### Option B: Create Test Pod

```bash
kubectl run -it --rm debug --image=mcr.microsoft.com/dotnet/sdk:6.0 -- bash

# Inside pod
curl http://databricks-service:8080/health
```

## Monitoring and Troubleshooting

### View Logs

```bash
# Real-time logs
kubectl logs -f deployment/databricks-service

# Logs from specific pod
kubectl logs pod/<pod-name>

# Previous pod logs (if crashed)
kubectl logs <pod-name> --previous
```

### Check Health Status

```bash
# Liveness probe failures
kubectl get pods -l app=databricks-service -o wide
kubectl describe pod <pod-name> | grep -A 5 "Liveness"

# Readiness probe failures
kubectl describe pod <pod-name> | grep -A 5 "Readiness"
```

### Debug Identity Issues

```bash
# Check pod labels
kubectl get pods --show-labels -l app=databricks-service

# Check service account annotation
kubectl get serviceaccount databricks-service -o yaml

# Verify workload identity webhook injected token
kubectl exec -it <pod-name> -- env | grep AZURE_FEDERATED_TOKEN
```

### Common Issues

#### "No valid authentication method available"

**Problem:** Pod can't authenticate with Databricks

**Solutions:**
1. Verify workload identity is properly configured:
   ```bash
   kubectl describe pod <pod-name> | grep -i identity
   ```

2. Check federated credential:
   ```bash
   az identity federated-credential show \
     --name databricks-service-fed \
     --identity-name databricks-service \
     --resource-group myRG
   ```

3. Verify Key Vault permissions:
   ```bash
   az keyvault list-deleted-secrets --vault-name myKeyVault
   ```

#### "Query timeout"

**Problem:** Databricks queries are timing out

**Solutions:**
1. Check SQL warehouse is running
2. Increase timeout in configmap:
   ```yaml
   DATABRICKS_TIMEOUT_SECONDS: "1200"  # 20 minutes
   ```

3. Check network connectivity from AKS to Databricks

#### "Permission denied" errors

**Problem:** Managed identity doesn't have Databricks permissions

**Solutions:**
1. Verify service principal has SQL warehouse access
2. Check workspace permissions for the identity
3. Verify Key Vault secret exists and is accessible

## Scaling and Performance

### Horizontal Pod Autoscaling

Already configured in `service.yaml`:

```yaml
minReplicas: 2
maxReplicas: 10
metrics:
  - cpu: 70%
  - memory: 80%
```

Monitor scaling:
```bash
kubectl get hpa databricks-service-hpa -w
```

### Resource Limits

Adjust in `deployment.yaml` based on your workload:

```yaml
resources:
  requests:
    cpu: 250m      # Increase for CPU-intensive queries
    memory: 512Mi  # Increase for large result sets
  limits:
    cpu: 500m
    memory: 1Gi
```

## Security Best Practices

### 1. Network Policies

Restrict traffic to only necessary destinations:

```bash
kubectl apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: databricks-service-netpol
spec:
  podSelector:
    matchLabels:
      app: databricks-service
  policyTypes:
  - Ingress
  - Egress
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: kube-system
    ports:
    - protocol: TCP
      port: 53     # DNS
  - to:
    - podSelector: {}  # Allow internal pods
    ports:
    - protocol: TCP
      port: 8080
EOF
```

### 2. Pod Security Policy

```bash
# Verify pod security policy is enabled
kubectl get psp
```

### 3. RBAC

Limit service account permissions:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: databricks-service
rules: []  # No Kubernetes API access needed
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: databricks-service
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: databricks-service
subjects:
- kind: ServiceAccount
  name: databricks-service
```

### 4. Audit Logging

Enable Azure audit logs for managed identity usage:

```bash
# View Azure Activity Log
az monitor activity-log list \
  --resource-group myRG \
  --query "[].{Time:eventTimestamp, Resource:resourceId, Action:operationName.value, Status:status.value}"
```

## Updating and Rollback

### Rolling Update

```bash
# Update image
kubectl set image deployment/databricks-service \
  databricks-service=${REGISTRY}/databricks-service:v2.0

# Monitor rollout
kubectl rollout status deployment/databricks-service
```

### Rollback

```bash
# View rollout history
kubectl rollout history deployment/databricks-service

# Rollback to previous version
kubectl rollout undo deployment/databricks-service
```

## Cleanup

```bash
# Delete Kubernetes resources
kubectl delete -f k8s/

# Delete Azure resources
az aks delete --resource-group myRG --name myCluster
az identity delete --resource-group myRG --name databricks-service
az acr delete --resource-group myRG --name myRegistry
```

## Additional Resources

- [Azure Workload Identity](https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview)
- [AKS Documentation](https://learn.microsoft.com/en-us/azure/aks/)
- [Databricks API](https://docs.databricks.com/api/)
- [Kubernetes Best Practices](https://kubernetes.io/docs/concepts/security/)
