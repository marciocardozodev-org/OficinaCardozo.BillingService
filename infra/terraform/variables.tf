variable "aws_region" {
  description = "Região AWS padrão"
  type        = string
  default     = "sa-east-1"
}

variable "billingservice_db_username" {
  description = "Usuário do banco de dados do BillingService"
  type        = string
  default     = "postgres"
  sensitive   = true
}

variable "billingservice_db_password" {
  description = "Senha do banco de dados do BillingService"
  type        = string
  sensitive   = true
}

variable "billingservice_db_name" {
  description = "Nome do banco de dados do BillingService"
  type        = string
  default     = "billingservice"
}

variable "enable_db" {
  description = "Habilitar provisionamento do banco de dados RDS"
  type        = bool
  default     = true
}
