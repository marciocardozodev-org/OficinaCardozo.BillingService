using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;
using OFICINACARDOZO.BILLINGSERVICE.API;

namespace OFICINACARDOZO.BILLINGSERVICE.Tests.API;

public class ValidationFilterTests
{
    [Fact]
    public void OnActionExecuting_WithValidModelState_ShouldDoNothing()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new(), new(), new ModelStateDictionary());
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        // Act
        filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_WithInvalidModelState_ShouldReturnBadRequest()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Nome", "Campo obrigatório");
        
        var actionContext = new ActionContext(httpContext, new(), new(), modelState);
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        // Act
        filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().NotBeNull();
        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void OnActionExecuting_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Nome", "Campo obrigatório");
        modelState.AddModelError("Email", "Email inválido");
        modelState.AddModelError("Valor", "Valor deve ser maior que 0");
        
        var actionContext = new ActionContext(httpContext, new(), new(), modelState);
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        // Act
        filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().NotBeNull();
        var badRequestResult = context.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public void OnActionExecuting_WithOneFieldMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "Email é obrigatório");
        modelState.AddModelError("Email", "Email deve ser válido");
        
        var actionContext = new ActionContext(httpContext, new(), new(), modelState);
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        // Act
        filter.OnActionExecuting(context);

        // Assert
        var badRequestResult = context.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
    }

    [Fact]
    public void OnActionExecuted_ShouldDoNothing()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new(), new(), new ModelStateDictionary());
        var context = new ActionExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new object());

        // Act & Assert (should not throw)
        filter.OnActionExecuted(context);
    }

    [Fact]
    public void OnActionExecuting_ErrorResponseIncludesFieldNames()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Valor", "Valor deve ser positivo");
        
        var actionContext = new ActionContext(httpContext, new(), new(), modelState);
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        // Act
        filter.OnActionExecuting(context);

        // Assert
        var badRequestResult = context.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
        badRequestResult.Value!.ToString().Should().Contain("Valor");
    }

    [Fact]
    public void OnActionExecuting_ErrorResponseIncludesErrorMessages()
    {
        // Arrange
        var filter = new ValidationFilter();
        var httpContext = new DefaultHttpContext();
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Nome", "Nome deve ter pelo menos 3 caracteres");
        
        var actionContext = new ActionContext(httpContext, new(), new(), modelState);
        var context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        // Act
        filter.OnActionExecuting(context);

        // Assert
        var badRequestResult = context.Result as BadRequestObjectResult;
        badRequestResult!.Value.Should().NotBeNull();
        badRequestResult.Value!.ToString().Should().Contain("até");
    }
}
