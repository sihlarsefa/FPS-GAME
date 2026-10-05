variable "cloud_provider" {
  description = "Bulut sağlayıcı: gcp | aws | azure (iskelet GCP modülleri ile gelir; diğerleri README'de)"
  type        = string
  default     = "gcp"
  validation {
    condition     = contains(["gcp", "aws", "azure"], var.cloud_provider)
    error_message = "cloud_provider gcp, aws veya azure olmalı."
  }
}

variable "project_id" {
  description = "GCP proje kimliği"
  type        = string
}

variable "environment" {
  description = "Ortam adı (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "cluster_name" {
  description = "Kubernetes küme adı öneki"
  type        = string
  default     = "harekat"
}

variable "primary_region" {
  description = "Birincil bölge (varsayılan Frankfurt — GKE geniş kullanılabilirlik)"
  type        = string
  default     = "europe-west3"
}

variable "region_istanbul" {
  description = "İstanbul / Türkiye bölgesi kodu"
  type        = string
  default     = "europe-west-istanbul"
}

variable "region_frankfurt" {
  description = "Frankfurt bölge kodu"
  type        = string
  default     = "europe-west3"
}

variable "region_amsterdam" {
  description = "Amsterdam bölge kodu"
  type        = string
  default     = "europe-west4"
}

variable "network" {
  description = "VPC ağ adı"
  type        = string
  default     = "harekat-vpc"
}

variable "subnetwork" {
  description = "Alt ağ adı"
  type        = string
  default     = "harekat-subnet"
}

variable "game_machine_type" {
  description = "GameServer node makine tipi (2 vCPU / 8 GB önerilir)"
  type        = string
  default     = "e2-standard-4"
}

variable "backend_machine_type" {
  description = "Backend / API node makine tipi"
  type        = string
  default     = "e2-standard-2"
}

variable "game_node_min" {
  type    = number
  default = 2
}

variable "game_node_max" {
  type    = number
  default = 50
}

variable "spot_node_min" {
  type    = number
  default = 1
}

variable "spot_node_max" {
  type    = number
  default = 80
}

variable "backend_node_min" {
  type    = number
  default = 2
}

variable "backend_node_max" {
  type    = number
  default = 20
}

variable "labels" {
  description = "Kaynak etiketleri"
  type        = map(string)
  default     = {}
}

variable "enable_night_scale" {
  description = "Gece saatleri node pool min değerlerini düşür (Cloud Scheduler + Terraform Cloud/CI)"
  type        = bool
  default     = true
}

variable "night_spot_node_min" {
  type    = number
  default = 0
}

variable "night_game_node_min" {
  type    = number
  default = 1
}
