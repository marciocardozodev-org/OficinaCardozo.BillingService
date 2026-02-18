variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "sa-east-1"
}

variable "billingservice_rds_instance_id" {
  description = "Identificador único da instância RDS do BillingService."
  type        = string
  default     = "billingservice-rds"
}

variable "billingservice_app_name" {
  description = "Prefixo para nomear recursos do BillingService."
  type        = string
  default     = "billingservice"
}

variable "billingservice_db_name" {
  description = "Nome do banco de dados do BillingService."
  type        = string
  default     = "billingservice"
}

variable "billingservice_db_username" {
  description = "Usuário administrador do banco do BillingService."
  type        = string
  default     = "billingadmin"
  sensitive   = true
}

variable "billingservice_db_password" {
  description = "Senha do banco do BillingService."
  type        = string
  sensitive   = true
  default     = ""
  
  validation {
    condition     = length(var.billingservice_db_password) == 0 || length(var.billingservice_db_password) >= 8
    error_message = "Senha inválida. Requisitos: vazio (para testes) ou mínimo 8 caracteres."
  }
}

variable "billingservice_db_subnet_ids" {
  description = "Lista de subnets privadas para o RDS do BillingService."
  type        = list(string)
  default     = []
}

variable "billingservice_db_security_group_ids" {
  description = "Security Groups para o RDS do BillingService."
  type        = list(string)
  default     = []
}

variable "enable_db" {
  description = "Se true, cria os recursos de banco gerenciado (RDS)."
  type        = bool
  default     = false
}
