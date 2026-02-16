output "osservice_rds_host" {
  value       = aws_db_instance.osservice.endpoint
  description = "Endpoint do RDS PostgreSQL do OSService"
}

output "osservice_rds_user" {
  value       = var.osservice_db_username
  description = "Usuário do RDS do OSService"
}

output "osservice_rds_password" {
  value       = var.osservice_db_password
  description = "Senha do RDS do OSService"
  sensitive   = true
}

output "osservice_rds_db_name" {
  value       = var.osservice_db_name
  description = "Nome do banco do OSService no RDS"
}
output "osservice_db_subnet_ids" {
  value       = try(data.terraform_remote_state.eks.outputs.private_subnet_ids, [])
  description = "Subnets privadas usadas pelo RDS do OSService (propagadas do EKS)"
}

output "osservice_db_security_group_ids" {
  value       = try(data.terraform_remote_state.eks.outputs.eks_security_group_ids, [])
  description = "Security Groups usados pelo RDS do OSService (propagados do EKS)"
}
# Executa migrations EF Core após o RDS estar disponível
terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# Importa outputs do Terraform do EKS para usar subnets e security group da mesma VPC
data "terraform_remote_state" "eks" {
  backend = "s3"
  config = {
    bucket = "oficina-cardozo-terraform-state-sp"
    key    = "eks/prod/terraform.tfstate"
    region = "sa-east-1"
  }
}

provider "aws" {
  region = var.aws_region
}

  description = "AWS region"
  type        = string
  default     = "sa-east-1"
}

variable "osservice_app_name" {
  description = "Prefixo para nomear recursos do OSService."
  type        = string
  default     = "osservice"
}

variable "osservice_db_name" {
  description = "Nome do banco de dados do OSService."
  type        = string
  default     = "oficinacardozo"
}

variable "osservice_db_username" {
  description = "Usuário administrador do banco do OSService."
  type        = string
  default     = "osadmin"
}

variable "osservice_db_password" {
  description = "Senha do banco do OSService."
  type        = string
  sensitive   = true
  default     = ""
  validation {
    condition     = length(var.osservice_db_password) == 0 || length(var.osservice_db_password) >= 8
    error_message = "Senha inválida. Requisitos: vazio (para testes) ou mínimo 8 caracteres."
  }
}

variable "osservice_db_subnet_ids" {
  description = "Lista de subnets privadas para o RDS do OSService."
  type        = list(string)
  default     = []
}

variable "osservice_db_security_group_ids" {
  description = "Security Groups para o RDS do OSService."
  type        = list(string)
  default     = []
}

variable "enable_db" {
  description = "Se true, cria os recursos de banco gerenciado (RDS)."
  type        = bool
  default     = false
}

variable "app_name" {
  description = "Prefixo para nomear recursos de banco (ex.: oficina-cardozo)."
  type        = string
  default     = "oficina-cardozo"
}

variable "db_username" {
  description = "Usuário administrador do banco."
  type        = string
  default     = "dbadmin"
}

variable "db_password" {
  description = "Senha do banco (min 8 chars). Usar secret/tfvars em produção."
  type        = string
  sensitive   = true
  default     = ""

  validation {
    condition     = length(var.db_password) == 0 || length(var.db_password) >= 8
    error_message = "Senha inválida. Requisitos: vazio (para testes) ou mínimo 8 caracteres."
  }
}

variable "db_subnet_ids" {
  description = "Lista de subnets privadas onde o RDS será criado."
  type        = list(string)
  default     = []
  # Não pode usar data source como default. Defina via tfvars ou CLI se necessário.
}

variable "db_name" {
  description = "Nome do banco de dados a ser criado no RDS."
  type        = string
  default     = "oficinacardozo"
}

variable "db_security_group_ids" {
  description = "Security Groups que controlam o acesso ao RDS."
  type        = list(string)
  default     = []
  # Não pode usar data source como default. Defina via tfvars ou CLI se necessário.
}