namespace TheMurderStoneArchive.Middleware
{
    /// <summary>
    /// Middleware for adding security headers to HTTP responses.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityHeadersMiddleware> _logger;

        public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Content Security Policy - restrict resources to same origin + necessary CDNs for maps and libraries
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://unpkg.com https://www.google.com https://www.gstatic.com https://cdnjs.cloudflare.com; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://unpkg.com https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data: https: blob:; " +
                "frame-src 'self' https://www.youtube.com https://www.google.com https://recaptcha.google.com; " +
                "connect-src 'self' https://www.google.com/recaptcha https://www.gstatic.com https://tile.openstreetmap.org https://*.tile.openstreetmap.org; " +
                "tile-layer data: https:;");

            // X-Content-Type-Options - prevent MIME type sniffing
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

            // X-Frame-Options - prevent clickjacking
            context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");

            // X-XSS-Protection - enable XSS filter
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            // Referrer-Policy - control referrer information
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            // Permissions-Policy - control browser features
            context.Response.Headers.Append("Permissions-Policy",
                "accelerometer=(), " +
                "camera=(), " +
                "geolocation=(), " +
                "gyroscope=(), " +
                "magnetometer=(), " +
                "microphone=(), " +
                "payment=(), " +
                "usb=()");

            // Strict-Transport-Security is handled by app.UseHsts() in Program.cs
            // but we add it here for clarity
            if (context.Request.IsHttps)
            {
                context.Response.Headers.Append("Strict-Transport-Security",
                    "max-age=31536000; includeSubDomains; preload");
            }

            await _next(context);
        }
    }
}
