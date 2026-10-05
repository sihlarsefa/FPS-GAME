project_id  = "harekat-prod"
environment = "prod"
cluster_name = "harekat"

primary_region    = "europe-west3"
region_istanbul   = "europe-west-istanbul"
region_frankfurt  = "europe-west3"
region_amsterdam  = "europe-west4"

game_machine_type    = "e2-standard-4"
backend_machine_type = "e2-standard-4"

game_node_min = 3
game_node_max = 40
spot_node_min = 2
spot_node_max = 80
backend_node_min = 3
backend_node_max = 20

enable_night_scale  = true
night_spot_node_min = 0
night_game_node_min = 2

labels = {
  team        = "platform"
  cost-center = "harekat-online"
}
