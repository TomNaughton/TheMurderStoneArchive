using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;

namespace TheMurderStoneArchive.Pages
{
    [Authorize]
    public class MyApiKeysModel : PageModel
    {
        private readonly IApiAuthenticationService _authService;
        private readonly ILogger<MyApiKeysModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _environment;

        public MyApiKeysModel(
            IApiAuthenticationService authService,
            ILogger<MyApiKeysModel> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment environment)
        {
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _environment = environment;
        }

        public List<ApiKey>? ApiKeys { get; set; }
        public string ReCaptchaSiteKey => _configuration[AppConstants.ReCaptchaSiteKeyKey] ?? string.Empty;

        [TempData] public string? GeneratedKeyMessage { get; set; }
        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
                ApiKeys = (await _authService.GetUserApiKeysAsync(userId)).ToList();
        }

        // ── JSON endpoints for fetch-based UI ──────────────────────────────

        public async Task<IActionResult> OnPostGenerateKeyJsonAsync([FromBody] GenerateKeyRequest? body)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return new JsonResult(new { success = false, error = "Not authenticated" }) { StatusCode = 401 };

            if (!_environment.IsDevelopment())
            {
                if (string.IsNullOrEmpty(body?.RecaptchaToken) || !await VerifyReCaptchaAsync(body.RecaptchaToken, "generate_api_key"))
                    return new JsonResult(new { success = false, error = "Captcha verification failed. Please try again." }) { StatusCode = 400 };
            }

            var keyName = string.IsNullOrWhiteSpace(body?.KeyName) ? "My API Key" : body.KeyName.Trim();
            if (keyName.Length > 100) keyName = keyName[..100];

            try
            {
                var (rawKey, dbEntity) = await _authService.GenerateApiKeyAsync(userId, keyName, ApiKeyTier.Free);
                _logger.LogInformation("User {UserId} generated new API key: {KeyId}", userId, dbEntity.Id);
                return new JsonResult(new
                {
                    success = true,
                    rawKey,
                    key = new
                    {
                        dbEntity.Id,
                        dbEntity.Name,
                        keyPrefix = dbEntity.KeyPrefix,
                        tier = dbEntity.Tier.ToString(),
                        dbEntity.IsRevoked,
                        dbEntity.RequestsThisMonth,
                        limit = dbEntity.Tier == ApiKeyTier.Free ? 100 : 10000,
                        lastUsed = (string?)null,
                        created = dbEntity.CreatedAtUtc.ToString("yyyy-MM-dd")
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return new JsonResult(new { success = false, error = ex.Message }) { StatusCode = 400 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating API key for user {UserId}", userId);
                return new JsonResult(new { success = false, error = "An error occurred. Please try again." }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostRevokeKeyJsonAsync([FromBody] RevokeKeyRequest? body)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return new JsonResult(new { success = false, error = "Not authenticated" }) { StatusCode = 401 };

            if (!_environment.IsDevelopment())
            {
                if (string.IsNullOrEmpty(body?.RecaptchaToken) || !await VerifyReCaptchaAsync(body.RecaptchaToken, "revoke_api_key"))
                    return new JsonResult(new { success = false, error = "Captcha verification failed. Please try again." }) { StatusCode = 400 };
            }

            if (body?.ApiKeyId == null)
                return new JsonResult(new { success = false, error = "Missing key ID" }) { StatusCode = 400 };

            try
            {
                var revoked = await _authService.RevokeApiKeyAsync(body.ApiKeyId);
                if (revoked == null)
                    return new JsonResult(new { success = false, error = "Could not find or revoke the API key." }) { StatusCode = 404 };

                _logger.LogInformation("User {UserId} revoked API key: {KeyId}", userId, body.ApiKeyId);

                // If the revoked key had an active premium subscription, automatically issue a replacement
                // so the user is never locked out of access they paid for.
                if (revoked.Tier == ApiKeyTier.Premium &&
                    revoked.SubscriptionExpiresAtUtc.HasValue &&
                    revoked.SubscriptionExpiresAtUtc.Value > DateTime.UtcNow)
                {
                    var replacementName = revoked.Name.EndsWith(" (replaced)") ? revoked.Name : revoked.Name + " (replaced)";
                    var (rawKey, newKey) = await _authService.GenerateApiKeyAsync(userId, replacementName, ApiKeyTier.Premium);
                    await _authService.UpgradeToPremiumpAsync(newKey, revoked.SubscriptionId ?? 0, revoked.SubscriptionExpiresAtUtc.Value);
                    _logger.LogInformation("Auto-reissued premium key {NewKeyId} after revoke of {OldKeyId} for user {UserId}", newKey.Id, body.ApiKeyId, userId);
                    return new JsonResult(new
                    {
                        success = true,
                        replacementIssued = true,
                        rawKey,
                        message = "Your premium key was revoked and a new one has been issued automatically — copy it now, you won't see it again.",
                        key = new
                        {
                            newKey.Id,
                            newKey.Name,
                            tier = newKey.Tier.ToString(),
                            newKey.IsRevoked,
                            newKey.RequestsThisMonth,
                            limit = 10000,
                            lastUsed = (string?)null,
                            created = newKey.CreatedAtUtc.ToString("yyyy-MM-dd")
                        }
                    });
                }

                return new JsonResult(new { success = true, replacementIssued = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key {KeyId} for user {UserId}", body.ApiKeyId, userId);
                return new JsonResult(new { success = false, error = "An error occurred. Please try again." }) { StatusCode = 500 };
            }
        }

        // ── Fallback PRG handlers (non-JS) ─────────────────────────────────

        public async Task<IActionResult> OnPostGenerateKeyAsync(string? keyName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(keyName)) keyName = "My API Key";
            keyName = keyName.Trim();
            if (keyName.Length > 100) keyName = keyName[..100];

            try
            {
                var (rawKey, dbEntity) = await _authService.GenerateApiKeyAsync(userId, keyName, ApiKeyTier.Free);
                GeneratedKeyMessage = rawKey;
                SuccessMessage = "API key generated successfully. Make sure to copy and save it — you won't see it again!";
                _logger.LogInformation("User {UserId} generated new API key: {KeyId}", userId, dbEntity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating API key for user {UserId}", userId);
                ErrorMessage = "An error occurred while generating the API key. Please try again.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRevokeKeyAsync(int apiKeyId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            try
            {
                var revoked = await _authService.RevokeApiKeyAsync(apiKeyId);
                if (revoked == null)
                {
                    ErrorMessage = "Could not find or revoke the API key.";
                }
                else
                {
                    _logger.LogInformation("User {UserId} revoked API key: {KeyId}", userId, apiKeyId);

                    // Auto-reissue if premium subscription is still active
                    if (revoked.Tier == ApiKeyTier.Premium &&
                        revoked.SubscriptionExpiresAtUtc.HasValue &&
                        revoked.SubscriptionExpiresAtUtc.Value > DateTime.UtcNow)
                    {
                        var replacementName = revoked.Name.EndsWith(" (replaced)") ? revoked.Name : revoked.Name + " (replaced)";
                        var (rawKey, newKey) = await _authService.GenerateApiKeyAsync(userId, replacementName, ApiKeyTier.Premium);
                        await _authService.UpgradeToPremiumpAsync(newKey, revoked.SubscriptionId ?? 0, revoked.SubscriptionExpiresAtUtc.Value);
                        _logger.LogInformation("Auto-reissued premium key {NewKeyId} after revoke of {OldKeyId} for user {UserId}", newKey.Id, apiKeyId, userId);
                        GeneratedKeyMessage = rawKey;
                        SuccessMessage = "Premium key revoked and a new one has been issued — copy it now, you won't see it again!";
                    }
                    else
                    {
                        SuccessMessage = "API key revoked successfully.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key {KeyId} for user {UserId}", apiKeyId, userId);
                ErrorMessage = "An error occurred while revoking the API key.";
            }

            return RedirectToPage();
        }

        // ── reCAPTCHA helper ───────────────────────────────────────────────────

        private async Task<bool> VerifyReCaptchaAsync(string token, string? expectedAction = null, double minScore = AppConstants.ReCaptchaDefaultMinScore)
        {
            try
            {
                var secret = _configuration[AppConstants.ReCaptchaSecretKeyKey];
                if (string.IsNullOrEmpty(secret)) return false;
                var client = _httpClientFactory.CreateClient();
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "secret",   secret },
                    { "response", token  }
                });
                var resp = await client.PostAsync(AppConstants.ReCaptchaVerifyUrl, content);
                if (!resp.IsSuccessStatusCode) return false;
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                    return false;
                if (doc.RootElement.TryGetProperty("score", out var scoreElem) &&
                    scoreElem.ValueKind == System.Text.Json.JsonValueKind.Number &&
                    scoreElem.GetDouble() < minScore)
                    return false;
                if (!string.IsNullOrEmpty(expectedAction) &&
                    doc.RootElement.TryGetProperty("action", out var actionElem) &&
                    !string.Equals(actionElem.GetString(), expectedAction, StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class GenerateKeyRequest
    {
        public string? KeyName { get; set; }
        public string? RecaptchaToken { get; set; }
    }

    public class RevokeKeyRequest
    {
        public int ApiKeyId { get; set; }
        public string? RecaptchaToken { get; set; }
    }
}
