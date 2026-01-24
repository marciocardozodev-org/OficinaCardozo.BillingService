using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Repositories;
using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Services;

namespace OficinaCardozo.Application.Services;

public class CpfCnpjValidationService : ICpfCnpjValidationService
{
    public bool ValidarCpfCnpj(string cpfCnpj)
    {
        if (string.IsNullOrWhiteSpace(cpfCnpj))
            return false;

        var documentoLimpo = LimparFormatacao(cpfCnpj);

        return documentoLimpo.Length switch
        {
            11 => ValidarCpf(documentoLimpo),
            14 => ValidarCnpj(documentoLimpo),
            _ => false
        };
    }

    public bool ValidarCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var cpfLimpo = LimparFormatacao(cpf);

        if (cpfLimpo.Length != 11)
            return false;

        // Verifica se todos os d�gitos s�o iguais
        if (cpfLimpo.All(c => c == cpfLimpo[0]))
            return false;

        var digitos = cpfLimpo.Select(c => int.Parse(c.ToString())).ToArray();

        // Valida primeiro d�gito verificador
        var soma = 0;
        for (int i = 0; i < 9; i++)
            soma += digitos[i] * (10 - i);

        var resto = soma % 11;
        var digitoVerificador1 = resto < 2 ? 0 : 11 - resto;

        if (digitos[9] != digitoVerificador1)
            return false;

        // Valida segundo d�gito verificador
        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += digitos[i] * (11 - i);

        resto = soma % 11;
        var digitoVerificador2 = resto < 2 ? 0 : 11 - resto;

        return digitos[10] == digitoVerificador2;
    }

    public bool ValidarCnpj(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return false;

        var cnpjLimpo = LimparFormatacao(cnpj);

        if (cnpjLimpo.Length != 14)
            return false;

        // Verifica se todos os d�gitos s�o iguais
        if (cnpjLimpo.All(c => c == cnpjLimpo[0]))
            return false;

        var digitos = cnpjLimpo.Select(c => int.Parse(c.ToString())).ToArray();

        // Multiplicadores para valida��o
        var multiplicadores1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var multiplicadores2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        // Valida primeiro d�gito verificador
        var soma = 0;
        for (int i = 0; i < 12; i++)
            soma += digitos[i] * multiplicadores1[i];

        var resto = soma % 11;
        var digitoVerificador1 = resto < 2 ? 0 : 11 - resto;

        if (digitos[12] != digitoVerificador1)
            return false;

        // Valida segundo d�gito verificador
        soma = 0;
        for (int i = 0; i < 13; i++)
            soma += digitos[i] * multiplicadores2[i];

        resto = soma % 11;
        var digitoVerificador2 = resto < 2 ? 0 : 11 - resto;

        return digitos[13] == digitoVerificador2;
    }

    public string LimparFormatacao(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return string.Empty;

        return new string(documento.Where(char.IsDigit).ToArray());
    }

    public string FormatarCpfCnpj(string cpfCnpj)
    {
        if (string.IsNullOrWhiteSpace(cpfCnpj))
            return string.Empty;

        var documentoLimpo = LimparFormatacao(cpfCnpj);

        return documentoLimpo.Length switch
        {
            11 => FormatarCpf(documentoLimpo),
            14 => FormatarCnpj(documentoLimpo),
            _ => cpfCnpj
        };
    }

    public string FormatarCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return string.Empty;

        var cpfLimpo = LimparFormatacao(cpf);

        if (cpfLimpo.Length != 11)
            return cpf;

        return $"{cpfLimpo.Substring(0, 3)}.{cpfLimpo.Substring(3, 3)}.{cpfLimpo.Substring(6, 3)}-{cpfLimpo.Substring(9, 2)}";
    }

    public string FormatarCnpj(string cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return string.Empty;

        var cnpjLimpo = LimparFormatacao(cnpj);

        if (cnpjLimpo.Length != 14)
            return cnpj;

        return $"{cnpjLimpo.Substring(0, 2)}.{cnpjLimpo.Substring(2, 3)}.{cnpjLimpo.Substring(5, 3)}/{cnpjLimpo.Substring(8, 4)}-{cnpjLimpo.Substring(12, 2)}";
    }
}