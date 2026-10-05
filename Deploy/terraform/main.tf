# HAREKÂT bulut iskeleti — sağlayıcı seçimi README'de
# Desteklenen: gcp (varsayılan), aws, azure

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.0"
    }
  }
}

provider "google" {
  project = var.project_id
  region  = var.primary_region
}

locals {
  regions = {
    istanbul  = var.region_istanbul
    frankfurt = var.region_frankfurt
    amsterdam = var.region_amsterdam
  }
  labels = merge(var.labels, {
    project     = "harekat"
    managed-by  = "terraform"
    environment = var.environment
  })
}

module "primary_cluster" {
  source = "./modules/gke-cluster"

  project_id   = var.project_id
  name         = "${var.cluster_name}-${var.environment}"
  region       = var.primary_region
  network      = var.network
  subnetwork   = var.subnetwork
  environment  = var.environment
  labels       = local.labels
}

module "game_nodes_ondemand" {
  source = "./modules/node-pool"

  project_id       = var.project_id
  cluster_name     = module.primary_cluster.name
  location         = module.primary_cluster.location
  name             = "games-ondemand"
  machine_type     = var.game_machine_type
  min_count        = var.game_node_min
  max_count        = var.game_node_max
  spot             = false
  labels           = merge(local.labels, { workload = "gameserver" })
  taints           = []
  disk_size_gb     = 100
}

module "game_nodes_spot" {
  source = "./modules/node-pool"

  project_id       = var.project_id
  cluster_name     = module.primary_cluster.name
  location         = module.primary_cluster.location
  name             = "games-spot"
  machine_type     = var.game_machine_type
  min_count        = var.spot_node_min
  max_count        = var.spot_node_max
  spot             = true
  labels           = merge(local.labels, { workload = "gameserver", capacity = "spot" })
  taints = [{
    key    = "cloud.google.com/gke-spot"
    value  = "true"
    effect = "NO_SCHEDULE"
  }]
  disk_size_gb = 100
}

module "backend_nodes" {
  source = "./modules/node-pool"

  project_id       = var.project_id
  cluster_name     = module.primary_cluster.name
  location         = module.primary_cluster.location
  name             = "backend"
  machine_type     = var.backend_machine_type
  min_count        = var.backend_node_min
  max_count        = var.backend_node_max
  spot             = false
  labels           = merge(local.labels, { workload = "backend" })
  taints           = []
  disk_size_gb     = 50
}

output "cluster_name" {
  value = module.primary_cluster.name
}

output "cluster_endpoint" {
  value     = module.primary_cluster.endpoint
  sensitive = true
}

output "regions" {
  value = local.regions
}

output "spot_pool" {
  value = module.game_nodes_spot.name
}
