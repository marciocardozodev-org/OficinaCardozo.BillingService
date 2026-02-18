# =====================================
# AWS Provider Configuration
# =====================================
provider "aws" {
  region = var.aws_region
}

# =====================================
# Data Sources
# =====================================
# Importa outputs do Terraform do EKS para usar subnets e security group da mesma VPC
data "terraform_remote_state" "eks" {
  backend = "s3"
  config = {
    bucket = "oficina-cardozo-terraform-state-sp"
    key    = "eks/prod/terraform.tfstate"
    region = "sa-east-1"
  }
}

# =====================================
# Outputs
# =====================================
output "billingservice_rds_host" {
  value       = aws_db_instance.billingservice.endpoint
  description = "Endpoint do RDS PostgreSQL do BillingService"
}

output "billingservice_rds_user" {
  value       = var.billingservice_db_username
  description = "Usuário do RDS do BillingService"
  sensitive   = true
}

output "billingservice_rds_password" {
  value       = var.billingservice_db_password
  description = "Senha do RDS do BillingService"
  sensitive   = true
}

output "billingservice_rds_db_name" {
  value       = var.billingservice_db_name
  description = "Nome do banco do BillingService no RDS"
}

output "billingservice_db_subnet_ids" {
  value       = try(data.terraform_remote_state.eks.outputs.private_subnet_ids, [])
  description = "Subnets privadas usadas pelo RDS do BillingService (propagadas do EKS)"
}

output "billingservice_db_security_group_ids" {
  value       = try(data.terraform_remote_state.eks.outputs.eks_security_group_ids, [])
  description = "Security Groups usados pelo RDS do BillingService (propagados do EKS)"
}