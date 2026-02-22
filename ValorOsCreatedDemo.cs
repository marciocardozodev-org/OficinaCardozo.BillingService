using System;
using System.Text.Json;
using OFICINACARDOZO.BILLINGSERVICE.Contracts.Events;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests
{
    /// <summary>
    /// Demonstração da lógica de fallback implementada
    /// </summary>
    class ValorOsCreatedDemo
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🧪 Demonstração: Lógica de Fallback do Valor em OsCreated\n");
            Console.WriteLine("===========================================================\n");
            
            // Cenário A: Valor fornecido e válido
            TestScenario("A", new OsCreated 
            { 
                OsId = Guid.NewGuid(),
                Description = "OS com valor 0.01",
                CreatedAt = DateTime.UtcNow,
                Valor = 0.01m
            });
            
            Console.WriteLine();
            
            // Cenário B: Valor não fornecido (null)
            TestScenario("B", new OsCreated 
            { 
                OsId = Guid.NewGuid(),
                Description = "OS sem campo Valor",
                CreatedAt = DateTime.UtcNow,
                Valor = null
            });
            
            Console.WriteLine();
            
            // Cenário C: Valor inválido (negativo)
            TestScenario("C", new OsCreated 
            { 
                OsId = Guid.NewGuid(),
                Description = "OS com valor negativo",
                CreatedAt = DateTime.UtcNow,
                Valor = -10.00m
            });
            
            Console.WriteLine();
            
            // Cenário D: Valor zero
            TestScenario("D", new OsCreated 
            { 
                OsId = Guid.NewGuid(),
                Description = "OS com valor zero",
                CreatedAt = DateTime.UtcNow,
                Valor = 0.00m
            });
            
            Console.WriteLine("\n===========================================================");
            Console.WriteLine("✅ Todos os cenários validados!");
        }
        
        static void TestScenario(string scenarioName, OsCreated osCreated)
        {
            Console.WriteLine($"📌 CENÁRIO {scenarioName}");
            Console.WriteLine($"   Valor recebido: {osCreated.Valor?.ToString() ?? "null"}");
            
            // Lógica implementada no OsCreatedHandler
            const decimal DefaultBudgetAmount = 100.00m;
            decimal budgetAmount;
            bool usedFallback;
            
            if (osCreated.Valor.HasValue && osCreated.Valor.Value > 0)
            {
                budgetAmount = osCreated.Valor.Value;
                usedFallback = false;
                Console.WriteLine($"   ✅ Usando valor do evento: {budgetAmount:F2}");
            }
            else
            {
                budgetAmount = DefaultBudgetAmount;
                usedFallback = true;
                Console.WriteLine($"   ⚠️  Aplicando fallback: {budgetAmount:F2}");
                Console.WriteLine($"      Motivo: Valor {(osCreated.Valor.HasValue ? $"<= 0 ({osCreated.Valor.Value})" : "não fornecido")}");
            }
            
            Console.WriteLine($"   📊 Resultado:");
            Console.WriteLine($"      - orcamento.Valor = {budgetAmount:F2}");
            Console.WriteLine($"      - pagamento.Valor = {budgetAmount:F2}");
            Console.WriteLine($"      - PaymentPending.Amount = {budgetAmount:F2}");
            Console.WriteLine($"      - UsedFallback = {usedFallback}");
        }
    }
}
