namespace Gateway.Auth;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    // Публичные маршруты — не требуют токена
    private static readonly string[] PublicPaths =
    [
        "/api/auth/login",
        "/swagger",
        "/hubs"
    ];

    public JwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IJwtService jwtService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Пропускаем публичные маршруты
        if (PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var token = ExtractToken(context);

        if (token is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Token required" });
            return;
        }

        var principal = jwtService.ValidateToken(token);

        if (principal is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token" });
            return;
        }

        // Прокидываем userId в заголовке к downstream сервисам
        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (userId != null)
            context.Request.Headers["X-User-Id"] = userId;
        if (role != null)
            context.Request.Headers["X-User-Role"] = role;

        await _next(context);
    }

    private static string? ExtractToken(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader?.StartsWith("Bearer ") == true)
            return authHeader["Bearer ".Length..].Trim();

        return null;
    }
}