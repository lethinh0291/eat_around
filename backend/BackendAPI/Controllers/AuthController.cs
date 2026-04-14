using BackendAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLib.Models;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "seller",
        "customer"
    };

    private readonly AppDbContext _dbContext;

    public AuthController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] User request)
    {
        var name = request.Name.Trim();
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var password = request.Password;
        var role = string.IsNullOrWhiteSpace(request.Role) ? "customer" : request.Role.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Vui lòng nhập đủ thông tin bắt buộc." });
        }

        if (!AllowedRoles.Contains(role))
        {
            return BadRequest(new { message = "Role không hợp lệ. Chỉ chấp nhận admin, seller, customer." });
        }

        var usernameExists = await _dbContext.Users.AnyAsync(user => user.Username == username);
        if (usernameExists)
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        var emailExists = await _dbContext.Users.AnyAsync(user => user.Email == email);
        if (emailExists)
        {
            return Conflict(new { message = "Email đã được sử dụng." });
        }

        _dbContext.Users.Add(new User
        {
            Name = name,
            Username = username,
            Email = email,
            Password = password,
            AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
            Role = role
        });

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đăng ký thành công." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var identifier = request.Username.Trim().ToLowerInvariant();
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "Vui lòng nhập tên đăng nhập/email và mật khẩu." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(item =>
            item.Username.ToLower() == identifier || item.Email.ToLower() == identifier);

        if (user is null || user.Password != password)
        {
            return Unauthorized(new { message = "Sai thông tin đăng nhập." });
        }

        return Ok(new
        {
            message = "Đăng nhập thành công.",
            user = new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Username,
                user.AvatarUrl,
                user.Role
            }
        });
    }

    [HttpPut("users/{userId:int}/role")]
    public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] UpdateRoleRequest request)
    {
        var role = request.Role.Trim().ToLowerInvariant();
        if (!AllowedRoles.Contains(role))
        {
            return BadRequest(new { message = "Role không hợp lệ. Chỉ chấp nhận admin, seller, customer." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        user.Role = role;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Cập nhật role thành công." });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _dbContext.Users
            .OrderBy(user => user.Id)
            .Select(user => new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Username,
                user.AvatarUrl,
                user.Role
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] UpsertUserRequest request)
    {
        var validationError = ValidateUpsertUserRequest(request, out var role);
        if (validationError is not null)
        {
            return validationError;
        }

        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        var usernameExists = await _dbContext.Users.AnyAsync(user => user.Username == username);
        if (usernameExists)
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        var emailExists = await _dbContext.Users.AnyAsync(user => user.Email == email);
        if (emailExists)
        {
            return Conflict(new { message = "Email đã được sử dụng." });
        }

        _dbContext.Users.Add(new User
        {
            Name = request.Name.Trim(),
            Username = username,
            Email = email,
            Password = request.Password,
            AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
            Role = role
        });

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã tạo người dùng mới." });
    }

    [HttpPut("users/{userId:int}")]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpsertUserRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        var validationError = ValidateUpsertUserRequest(request, out var role);
        if (validationError is not null)
        {
            return validationError;
        }

        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        var usernameExists = await _dbContext.Users.AnyAsync(item => item.Id != userId && item.Username == username);
        if (usernameExists)
        {
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });
        }

        var emailExists = await _dbContext.Users.AnyAsync(item => item.Id != userId && item.Email == email);
        if (emailExists)
        {
            return Conflict(new { message = "Email đã được sử dụng." });
        }

        user.Name = request.Name.Trim();
        user.Username = username;
        user.Email = email;
        user.Password = request.Password;
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? user.AvatarUrl : request.AvatarUrl.Trim();
        user.Role = role;

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Cập nhật người dùng thành công." });
    }

    [HttpPut("users/{userId:int}/avatar")]
    public async Task<IActionResult> UpdateUserAvatar(int userId, [FromBody] UpdateAvatarRequest request)
    {
        var avatarUrl = request.AvatarUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(avatarUrl) || !Uri.TryCreate(avatarUrl, UriKind.Absolute, out _))
        {
            return BadRequest(new { message = "URL ảnh đại diện không hợp lệ." });
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        user.AvatarUrl = avatarUrl;
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Cập nhật ảnh đại diện thành công.", avatarUrl });
    }

    [HttpDelete("users/{userId:int}")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId);
        if (user is null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng." });
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã xóa người dùng." });
    }

    public sealed class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public sealed class UpsertUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = "customer";
    }

    public sealed class UpdateAvatarRequest
    {
        public string AvatarUrl { get; set; } = string.Empty;
    }

    private IActionResult? ValidateUpsertUserRequest(UpsertUserRequest request, out string role)
    {
        role = (request.Role ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Vui lòng nhập đủ thông tin bắt buộc." });
        }

        if (!AllowedRoles.Contains(role))
        {
            return BadRequest(new { message = "Role không hợp lệ. Chỉ chấp nhận admin, seller, customer." });
        }

        return null;
    }
}