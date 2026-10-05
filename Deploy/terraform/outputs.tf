output "cluster_name" {
  description = "Oluşturulan Kubernetes küme adı"
  value       = module.primary_cluster.name
}

output "cluster_location" {
  value = module.primary_cluster.location
}

output "cluster_endpoint" {
  description = "Küme API uç noktası"
  value       = module.primary_cluster.endpoint
  sensitive   = true
}

output "ca_certificate" {
  value     = module.primary_cluster.ca_certificate
  sensitive = true
}

output "node_pools" {
  value = {
    games_ondemand = module.game_nodes_ondemand.name
    games_spot     = module.game_nodes_spot.name
    backend        = module.backend_nodes.name
  }
}

output "multi_region_map" {
  description = "Ping routing için bölge eşlemesi"
  value = {
    eu-istanbul  = var.region_istanbul
    eu-frankfurt = var.region_frankfurt
    eu-amsterdam = var.region_amsterdam
  }
}

output "estimated_capacity" {
  description = "Kabaca eşzamanlı oyuncu kapasitesi (spot max * 2 sunucu/node * 50 oyuncu)"
  value = {
    max_gameservers_spot     = var.spot_node_max * 2
    max_ccu_estimate         = var.spot_node_max * 2 * 50
    players_per_match        = "40-60"
    squad_size               = 10
  }
}
