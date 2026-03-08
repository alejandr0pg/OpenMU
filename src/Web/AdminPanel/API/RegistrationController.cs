namespace MUnique.OpenMU.Web.API;

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Public API for account self-registration, protected by HMAC signature.
/// </summary>
[Route("api/")]
public class RegistrationController : Controller
{
    private const int MaxTimestampDriftSeconds = 300;

    private readonly IPersistenceContextProvider _contextProvider;
    private readonly ILogger<RegistrationController> _logger;
    private readonly string _sharedSecret;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationController"/> class.
    /// </summary>
    public RegistrationController(
        IPersistenceContextProvider contextProvider,
        ILogger<RegistrationController> logger)
    {
        this._contextProvider = contextProvider;
        this._logger = logger;
        this._sharedSecret = Environment.GetEnvironmentVariable("REGISTRATION_SECRET") ?? string.Empty;
    }

    /// <summary>
    /// Registers a new account.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
    {
        if (string.IsNullOrEmpty(this._sharedSecret))
        {
            return this.StatusCode(503, "Registration is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return this.BadRequest("Username and password are required.");
        }

        if (request.Username.Length < 4 || request.Username.Length > 10)
        {
            return this.BadRequest("Username must be 4-10 characters.");
        }

        if (request.Password.Length < 4 || request.Password.Length > 20)
        {
            return this.BadRequest("Password must be 4-20 characters.");
        }

        if (!this.ValidateHmac(request))
        {
            this._logger.LogWarning("Invalid HMAC signature from registration attempt for '{Username}'.", request.Username);
            return this.StatusCode(403, "Invalid signature.");
        }

        try
        {
            using var context = this._contextProvider.CreateNewContext();
            var existing = await context.GetAccountByLoginNameAsync(request.Username).ConfigureAwait(false);
            if (existing is not null)
            {
                return this.Conflict("Username already exists.");
            }

            var account = context.CreateNew<Account>();
            account.LoginName = request.Username;
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            account.State = AccountState.Normal;
            account.RegistrationDate = DateTime.UtcNow;
            account.SecurityCode = string.Empty;

            if (!await context.SaveChangesAsync().ConfigureAwait(false))
            {
                this._logger.LogError("Failed to save new account '{Username}'.", request.Username);
                return this.StatusCode(500, "Failed to create account.");
            }

            this._logger.LogInformation("Account '{Username}' registered successfully.", request.Username);
            return this.Ok("Account created.");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Registration error for '{Username}'.", request.Username);
            return this.StatusCode(500, "Internal server error.");
        }
    }

    private bool ValidateHmac(RegistrationRequest request)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - request.Timestamp) > MaxTimestampDriftSeconds)
        {
            return false;
        }

        var message = $"{request.Timestamp}|{request.Nonce}|{request.Username}";
        var keyBytes = Encoding.UTF8.GetBytes(this._sharedSecret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var expected = Convert.ToHexStringLower(hmac.ComputeHash(messageBytes));
        return string.Equals(expected, request.Signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registration request payload.
    /// </summary>
    public class RegistrationRequest
    {
        /// <summary>Gets or sets the username.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Gets or sets the password.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Gets or sets the timestamp.</summary>
        public long Timestamp { get; set; }

        /// <summary>Gets or sets the nonce.</summary>
        public string Nonce { get; set; } = string.Empty;

        /// <summary>Gets or sets the HMAC signature.</summary>
        public string Signature { get; set; } = string.Empty;
    }
}
