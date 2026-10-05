project_id  = "harekat-dev"
environment = "dev"
cluster_name = "harekat"

primary_region = "europe-west3"

game_machine_type    = "e2-standard-4"
backend_machine_type = "e2-standard-2"

game_node_min = 1
game_node_max = 10
spot_node_min = 0
spot_node_max = 15
backend_node_min = 1
backend_node_max = 5

labels = {
  team = "platform"
}
