terraform {
  backend "s3" {
    bucket  = "oficina-cardozo-terraform-state-sp"
    key     = "billingservice/prod/terraform.tfstate"
    region  = "sa-east-1"
    encrypt = true
  }

  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}
