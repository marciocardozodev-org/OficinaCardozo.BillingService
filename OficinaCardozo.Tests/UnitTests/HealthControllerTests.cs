using OficinaCardozo.API.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using OficinaCardozo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace OficinaCardozo.Tests.UnitTests
{
    public class HealthControllerTests
    {
        [Fact]
        public void Live_ReturnsOk()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<OficinaDbContext>().Options;
            var mockDbContext = new Mock<OficinaDbContext>(options);
            var controller = new HealthController(mockDbContext.Object);

            // Act
            var result = controller.Live();

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);
            Assert.Equal(200, okResult.StatusCode ?? 200);
        }
    }
}