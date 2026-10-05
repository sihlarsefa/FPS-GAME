# Terraform sağlayıcı seçimi
#
# Varsayılan iskelet **GCP (GKE)** içindir (`modules/gke-cluster`, `modules/node-pool`).
#
# ## GCP (önerilen)
# ```bash
# cd Deploy/terraform
# terraform init
# terraform plan -var-file=environments/prod/terraform.tfvars
# terraform apply -var-file=environments/prod/terraform.tfvars
# ```
#
# ## AWS (EKS)
# - `cloud_provider = "aws"` ayarlayın.
# - EKS + managed node group + Karpenter spot pool için ayrı modül ekleyin
#   (`terraform-aws-modules/eks/aws` önerilir).
# - Bölgeler: `eu-central-1` (Frankfurt), `eu-west-1` / NL eşdeğeri, Türkiye için
#   en yakın: `eu-central-1` + İstanbul edge.
#
# ## Azure (AKS)
# - `cloud_provider = "azure"` ayarlayın.
# - AKS + Virtual Machine Scale Sets + Spot node pool.
# - Bölgeler: `germanywestcentral`, `westeurope`, Türkiye: `turkeycentral` (varsa).
#
# Agones kurulumu apply sonrası:
# ```bash
# helm repo add agones https://agones.dev/chart/stable
# helm install agones agones/agones --namespace agones-system --create-namespace
# ```
