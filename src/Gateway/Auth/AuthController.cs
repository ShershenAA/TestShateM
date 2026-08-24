using Microsoft.AspNetCore.Mvc;

namespace Gateway.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IJwtService _jwt;

    // Простые тестовые пользователи — в реальном проекте это БД
    private static readonly Dictionary<string, (string Password, string Role, string Id)> Users = new()
    {
        ["dealer1"] = ("password123", "Dealer", "3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["dealer2"] = ("password123", "Dealer", "4fa85f64-5717-4562-b3fc-2c963f66afa7"),
        ["admin"]   = ("admin123",    "Admin",  "5fa85f64-5717-4562-b3fc-2c963f66afa8")
    };

    public AuthController(IJwtService jwt)
    {
        _jwt = jwt;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        if (!Users.TryGetValue(dto.Username, out var user))
            return Unauthorized("Пользователь не найден");

        if (user.Password != dto.Password)
            return Unauthorized("Неверный пароль");

        var token = _jwt.GenerateToken(user.Id, user.Role);

        return Ok(new
        {
            token,
            userId = user.Id,
            role = user.Role,
            expiresIn = 3600
        });
    }
}

public record LoginDto(string Username, string Password);