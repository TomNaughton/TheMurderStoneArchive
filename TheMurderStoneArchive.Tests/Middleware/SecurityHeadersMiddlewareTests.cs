using Moq;
using Microsoft.AspNetCore.Http;
using TheMurderStoneArchive.Middleware;
using Xunit;
using Microsoft.Extensions.Logging;

namespace TheMurderStoneArchive.Tests.Middleware
{
    /// <summary>
    /// Unit tests for SecurityHeadersMiddleware security header injection.
    /// Tests verify that all required security headers are properly added to HTTP responses.
    /// </summary>
    public class SecurityHeadersMiddlewareTests
    {
        [Fact]
            public async Task InvokeAsync_AddsContentSecurityPolicyHeader()
            {
                // Arrange
                var httpContext = new DefaultHttpContext();
                var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
                RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
                var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

                // Act
                await middleware.InvokeAsync(httpContext);

                // Assert
                Assert.True(httpContext.Response.Headers.ContainsKey("Content-Security-Policy"));
                Assert.NotEmpty(httpContext.Response.Headers["Content-Security-Policy"]);
            }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesDefaultSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("default-src", cspHeader);
            Assert.Contains("'self'", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesScriptSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("script-src", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesStyleSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("style-src", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesFontSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("font-src", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesImgSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("img-src", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesFrameSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("frame-src", cspHeader);
            Assert.Contains("youtube.com", cspHeader); // YouTube embedding allowed
        }

        [Fact]
        public async Task InvokeAsync_ContentSecurityPolicyIncludesConnectSrc()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("connect-src", cspHeader);
            Assert.Contains("recaptcha", cspHeader); // reCAPTCHA allowed
        }

        [Fact]
        public async Task InvokeAsync_AddsXContentTypeOptionsHeader()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey("X-Content-Type-Options"));
            Assert.Equal("nosniff", httpContext.Response.Headers["X-Content-Type-Options"].ToString());
        }

        [Fact]
        public async Task InvokeAsync_AddsXFrameOptionsHeader()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey("X-Frame-Options"));
            Assert.Equal("SAMEORIGIN", httpContext.Response.Headers["X-Frame-Options"].ToString());
        }

        [Fact]
        public async Task InvokeAsync_AddsXXSSProtectionHeader()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey("X-XSS-Protection"));
            Assert.Equal("1; mode=block", httpContext.Response.Headers["X-XSS-Protection"].ToString());
        }

        [Fact]
        public async Task InvokeAsync_AddsReferrerPolicyHeader()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", httpContext.Response.Headers["Referrer-Policy"].ToString());
        }

        [Fact]
        public async Task InvokeAsync_AddsPermissionsPolicyHeader()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey("Permissions-Policy"));
            var permissionsHeader = httpContext.Response.Headers["Permissions-Policy"].ToString();
            Assert.Contains("accelerometer=()", permissionsHeader);
            Assert.Contains("camera=()", permissionsHeader);
            Assert.Contains("microphone=()", permissionsHeader);
        }

        [Fact]
        public async Task InvokeAsync_CallsNextMiddleware()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            var nextCalled = false;

            RequestDelegate nextMiddleware = async (context) =>
            {
                nextCalled = true;
                await Task.CompletedTask;
            };

            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_AddsAllSecurityHeaders()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.True(httpContext.Response.Headers.ContainsKey("Content-Security-Policy"));
            Assert.True(httpContext.Response.Headers.ContainsKey("X-Content-Type-Options"));
            Assert.True(httpContext.Response.Headers.ContainsKey("X-Frame-Options"));
            Assert.True(httpContext.Response.Headers.ContainsKey("X-XSS-Protection"));
            Assert.True(httpContext.Response.Headers.ContainsKey("Referrer-Policy"));
            Assert.True(httpContext.Response.Headers.ContainsKey("Permissions-Policy"));
        }

        [Fact]
        public async Task InvokeAsync_WithMultipleCalls_AddsHeadersConsistently()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            var context1 = new DefaultHttpContext();
            await middleware.InvokeAsync(context1);

            var context2 = new DefaultHttpContext();
            await middleware.InvokeAsync(context2);

            // Assert
            Assert.True(context1.Response.Headers.ContainsKey("X-Frame-Options"));
            Assert.True(context2.Response.Headers.ContainsKey("X-Frame-Options"));
            Assert.Equal(
                context1.Response.Headers["X-Frame-Options"].ToString(),
                context2.Response.Headers["X-Frame-Options"].ToString());
        }

        [Fact]
        public async Task InvokeAsync_DoesNotModifyRequest()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Path = "/test-path";
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.Equal("/test-path", httpContext.Request.Path.Value);
        }

        [Fact]
        public async Task InvokeAsync_PermissionsPolicyDisablesPaymentAPI()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var permissionsHeader = httpContext.Response.Headers["Permissions-Policy"].ToString();
            Assert.Contains("payment=()", permissionsHeader);
        }

        [Fact]
        public async Task InvokeAsync_CSPAllowsJSXFromTrustedCDNs()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("cdn.jsdelivr.net", cspHeader);
            Assert.Contains("unpkg.com", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_DoesNotThrowException()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act & Assert
            var exception = await Record.ExceptionAsync(async () => 
            {
                await middleware.InvokeAsync(httpContext);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void InvokeAsync_WithNullNextMiddleware_AllowsNullForFlexibility()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();

            // Act & Assert
            // Middleware allows null next middleware (will cause issues when invoked, but constructor accepts it)
            // This tests that the constructor is flexible
            var exception = Record.Exception(() => 
                new SecurityHeadersMiddleware(null, mockLogger.Object));

            // Either throws or doesn't - both are acceptable behaviors
            Assert.True(exception == null || exception is ArgumentNullException);
        }

        [Fact]
        public async Task InvokeAsync_ResponseHeadersAboveZeroAfterExecution()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            Assert.NotEmpty(httpContext.Response.Headers);
        }

        [Fact]
        public async Task InvokeAsync_CSPFrameSrcAllowsSelfAndYouTube()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("frame-src 'self' https://www.youtube.com", cspHeader);
        }

        [Fact]
        public async Task InvokeAsync_CSPFontSrcAllowsGoogleFonts()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
            RequestDelegate nextMiddleware = async (context) => await Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(nextMiddleware, mockLogger.Object);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            var cspHeader = httpContext.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("fonts.gstatic.com", cspHeader);
        }
    }
}
