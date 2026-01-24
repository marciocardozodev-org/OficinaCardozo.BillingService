using OficinaCardozo.Domain.Exceptions;
using OficinaCardozo.App.OficinaCardozo.Domain.Interfaces.Services;

namespace OficinaCardozo.Domain.ValueObjects;

public record CpfCnpj
{
    public string Valor { get; }
    public string ValorFormatado { get; }

    public CpfCnpj(string valor, ICpfCnpjValidationService validationService)
    {
        ArgumentNullException.ThrowIfNull(validationService);

        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("CPF/CNPJ n�o pode estar vazio", nameof(valor));

        var valorLimpo = validationService.LimparFormatacao(valor);

        if (!validationService.ValidarCpfCnpj(valorLimpo))
            throw new CpfCnpjInvalidoException(valor);

        Valor = valorLimpo;
        ValorFormatado = validationService.FormatarCpfCnpj(valorLimpo);
    }

    public bool EhCpf() => Valor.Length == 11;
    public bool EhCnpj() => Valor.Length == 14;

    public override string ToString() => ValorFormatado;

    public static implicit operator string(CpfCnpj cpfCnpj) => cpfCnpj.Valor;
}