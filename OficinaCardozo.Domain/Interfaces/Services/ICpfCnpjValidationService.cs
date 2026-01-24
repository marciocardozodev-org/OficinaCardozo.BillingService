namespace OficinaCardozo.Domain.Interfaces.Services;

public interface ICpfCnpjValidationService
{
    bool ValidarCpf(string cpf);
    bool ValidarCnpj(string cnpj);
    bool ValidarCpfCnpj(string cpfCnpj);
    string LimparFormatacao(string documento);
    string FormatarCpf(string cpf);
    string FormatarCnpj(string cnpj);
    string FormatarCpfCnpj(string cpfCnpj);
}