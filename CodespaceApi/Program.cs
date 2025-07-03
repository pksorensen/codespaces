using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddLogging();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// API Endpoints
app.MapPost("/api/users", async ([FromBody] CreateUserRequest request, IUserService userService) =>
{
    try
    {
        var result = await userService.CreateUserAsync(request.Username);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateUser")
.WithOpenApi();

app.MapGet("/api/users/{username}", async (string username, IUserService userService) =>
{
    try
    {
        var result = await userService.GetUserInfoAsync(username);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
})
.WithName("GetUser")
.WithOpenApi();

app.MapDelete("/api/users/{username}", async (string username, IUserService userService) =>
{
    try
    {
        await userService.DeleteUserAsync(username);
        return Results.Ok(new { message = "User deleted successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("DeleteUser")
.WithOpenApi();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
.WithName("HealthCheck")
.WithOpenApi();

app.Run();

// Models
public record CreateUserRequest(string Username);

public record UserInfo(string Username, string HomeDirectory, string SshPublicKey, bool IsActive);

public record CreateUserResponse(string Username, string TempPassword, string HomeDirectory, string SshPublicKey);

// Services
public interface IUserService
{
    Task<CreateUserResponse> CreateUserAsync(string username);
    Task<UserInfo> GetUserInfoAsync(string username);
    Task DeleteUserAsync(string username);
}

public class UserService : IUserService
{
    private readonly ILogger<UserService> _logger;
    private const string BaseDirectory = "/data/codespaces";

    public UserService(ILogger<UserService> logger)
    {
        _logger = logger;
    }

    public async Task<CreateUserResponse> CreateUserAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty");

        if (username.Length < 3 || username.Length > 32)
            throw new ArgumentException("Username must be between 3 and 32 characters");

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$"))
            throw new ArgumentException("Username can only contain letters, numbers, hyphens, and underscores");

        var userDirectory = Path.Combine(BaseDirectory, username);
        var tempPassword = GeneratePassword();

        try
        {
            // Check if user already exists
            if (await UserExistsAsync(username))
                throw new InvalidOperationException($"User '{username}' already exists");

            // Create user directory
            await CreateUserDirectoryAsync(userDirectory);

            // Create Linux user
            await CreateLinuxUserAsync(username, tempPassword, userDirectory);

            // Generate SSH key pair
            var sshPublicKey = await GenerateSshKeyAsync(username, userDirectory);

            // Set permissions
            await SetDirectoryPermissionsAsync(username, userDirectory);

            _logger.LogInformation($"Successfully created user: {username}");

            return new CreateUserResponse(username, tempPassword, userDirectory, sshPublicKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to create user: {username}");
            // Cleanup on failure
            await CleanupUserAsync(username, userDirectory);
            throw;
        }
    }

    public async Task<UserInfo> GetUserInfoAsync(string username)
    {
        if (!await UserExistsAsync(username))
            throw new InvalidOperationException($"User '{username}' does not exist");

        var userDirectory = Path.Combine(BaseDirectory, username);
        var sshKeyPath = Path.Combine(userDirectory, ".ssh", "id_rsa.pub");
        var sshPublicKey = "";

        if (File.Exists(sshKeyPath))
        {
            sshPublicKey = await File.ReadAllTextAsync(sshKeyPath);
        }

        var isActive = await IsUserActiveAsync(username);

        return new UserInfo(username, userDirectory, sshPublicKey.Trim(), isActive);
    }

    public async Task DeleteUserAsync(string username)
    {
        if (!await UserExistsAsync(username))
            throw new InvalidOperationException($"User '{username}' does not exist");

        var userDirectory = Path.Combine(BaseDirectory, username);

        try
        {
            // Delete Linux user
            await DeleteLinuxUserAsync(username);

            // Remove user directory
            if (Directory.Exists(userDirectory))
            {
                Directory.Delete(userDirectory, true);
            }

            _logger.LogInformation($"Successfully deleted user: {username}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete user: {username}");
            throw;
        }
    }

    private async Task<bool> UserExistsAsync(string username)
    {
        var result = await ExecuteCommandAsync("id", username);
        return result.ExitCode == 0;
    }

    private async Task<bool> IsUserActiveAsync(string username)
    {
        var result = await ExecuteCommandAsync("passwd", $"-S {username}");
        return result.ExitCode == 0 && !result.Output.Contains("L");
    }

    private async Task CreateUserDirectoryAsync(string userDirectory)
    {
        if (!Directory.Exists(BaseDirectory))
        {
            Directory.CreateDirectory(BaseDirectory);
        }

        if (!Directory.Exists(userDirectory))
        {
            Directory.CreateDirectory(userDirectory);
        }

        // Create .ssh directory
        var sshDirectory = Path.Combine(userDirectory, ".ssh");
        if (!Directory.Exists(sshDirectory))
        {
            Directory.CreateDirectory(sshDirectory);
        }
    }

    private async Task CreateLinuxUserAsync(string username, string password, string homeDirectory)
    {
        // Create user with specific shell and home directory
        var result = await ExecuteCommandAsync("useradd", 
            $"-m -d {homeDirectory} -s /bin/bash {username}");

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create user: {result.Error}");

        // Set password
        var passwordResult = await ExecuteCommandAsync("bash", 
            $"-c \"echo '{username}:{password}' | chpasswd\"");

        if (passwordResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to set password: {passwordResult.Error}");
    }

    private async Task DeleteLinuxUserAsync(string username)
    {
        var result = await ExecuteCommandAsync("userdel", $"-r {username}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to delete user: {result.Error}");
    }

    private async Task<string> GenerateSshKeyAsync(string username, string userDirectory)
    {
        var sshDirectory = Path.Combine(userDirectory, ".ssh");
        var keyPath = Path.Combine(sshDirectory, "id_rsa");
        var publicKeyPath = Path.Combine(sshDirectory, "id_rsa.pub");

        // Generate SSH key pair
        var result = await ExecuteCommandAsync("ssh-keygen", 
            $"-t rsa -b 4096 -f {keyPath} -N '' -C '{username}@codespace'");

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to generate SSH key: {result.Error}");

        // Add public key to authorized_keys
        var authorizedKeysPath = Path.Combine(sshDirectory, "authorized_keys");
        var publicKey = await File.ReadAllTextAsync(publicKeyPath);
        await File.WriteAllTextAsync(authorizedKeysPath, publicKey);

        return publicKey.Trim();
    }

    private async Task SetDirectoryPermissionsAsync(string username, string userDirectory)
    {
        // Set ownership
        var chownResult = await ExecuteCommandAsync("chown", $"-R {username}:{username} {userDirectory}");
        if (chownResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to set ownership: {chownResult.Error}");

        // Set permissions
        var chmodResult = await ExecuteCommandAsync("chmod", $"700 {userDirectory}");
        if (chmodResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to set directory permissions: {chmodResult.Error}");

        // Set SSH directory permissions
        var sshDirectory = Path.Combine(userDirectory, ".ssh");
        var sshChmodResult = await ExecuteCommandAsync("chmod", $"700 {sshDirectory}");
        if (sshChmodResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to set SSH directory permissions: {sshChmodResult.Error}");

        // Set SSH key permissions
        var keyPath = Path.Combine(sshDirectory, "id_rsa");
        var keyChmodResult = await ExecuteCommandAsync("chmod", $"600 {keyPath}");
        if (keyChmodResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to set SSH key permissions: {keyChmodResult.Error}");

        var authorizedKeysPath = Path.Combine(sshDirectory, "authorized_keys");
        var authKeysChmodResult = await ExecuteCommandAsync("chmod", $"600 {authorizedKeysPath}");
        if (authKeysChmodResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to set authorized_keys permissions: {authKeysChmodResult.Error}");
    }

    private async Task CleanupUserAsync(string username, string userDirectory)
    {
        try
        {
            // Try to delete user if it was created
            if (await UserExistsAsync(username))
            {
                await DeleteLinuxUserAsync(username);
            }

            // Remove directory if it exists
            if (Directory.Exists(userDirectory))
            {
                Directory.Delete(userDirectory, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to cleanup user: {username}");
        }
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        var password = new StringBuilder();

        for (int i = 0; i < 16; i++)
        {
            password.Append(chars[random.Next(chars.Length)]);
        }

        return password.ToString();
    }

    private async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null)
            throw new InvalidOperationException($"Failed to start process: {command}");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }
}