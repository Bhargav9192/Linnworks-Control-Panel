using LinnworksAPI;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

public class LinnworksAuthService
{
    private const int TOKEN_VALIDITY_MINUTES = 55;

    private readonly Guid _applicationId;
    private readonly Guid _applicationSecret;
    private readonly Guid _token;
    private readonly string _userKey; 

    public LinnworksAuthService(Guid applicationId,Guid applicationSecret,Guid token, string userKey)
    {
        _applicationId = applicationId;
        _applicationSecret = applicationSecret;
        _token = token;
        _userKey = userKey;  

    }

    // =========================
    // PUBLIC ENTRY
    // =========================
    public async Task<BaseSession> GetValidSessionAsync(bool forceRefresh = false)
    {
        var stored = LoadSession();

        if (!forceRefresh && stored != null && stored.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
            return stored.ToBaseSession();

        var newSession = AuthorizeViaSdk(); // SDK call
        SaveSession(newSession);

        return newSession.ToBaseSession();
    }
    private string GetSessionFile(string userKey)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            $"session_{userKey}.json"
        );
    }
    public async Task<T> ExecuteApiWithRetryAsync<T>(
     Func<BaseSession, Task<T>> apiCall,
     int maxRetries = 2)
    {
        int attempt = 0;

        while (true)
        {
            attempt++;

            var session = await GetValidSessionAsync(forceRefresh: false);

            try
            {
                return await apiCall(session);
            }
            catch (Exception ex) when (IsUnauthorized(ex) && attempt <= maxRetries)
            {
                // Refresh token and retry
                await GetValidSessionAsync(forceRefresh: true);

                if (attempt > maxRetries)
                    throw;

                continue;
            }
        }
    }
    private bool IsUnauthorized(Exception ex)
    {
        return ex.Message.Contains("401") ||
               ex.Message.Contains("Unauthorized");
    }
    // =========================
    // SDK AUTH CALL
    // =========================
    private StoredSession AuthorizeViaSdk()
    {
        var controller = new AuthController(
            new ApiContext("https://api.linnworks.net")
        );

        var baseSession = controller.AuthorizeByApplication(
            new AuthorizeByApplicationRequest
            {
                ApplicationId = _applicationId,
                ApplicationSecret = _applicationSecret,
                Token = _token
            });

        return new StoredSession
        {
            Token = baseSession.Token,
            Server = baseSession.Server,
            UserId = baseSession.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(TOKEN_VALIDITY_MINUTES)
        };
    }

    // =========================
    // FILE HANDLING
    // =========================
    private StoredSession LoadSession()
    {
        var file = GetSessionFile(_userKey);

        if (!File.Exists(file))
            return null;

        return JsonConvert.DeserializeObject<StoredSession>(
            File.ReadAllText(file)
        );
    }

    private void SaveSession(StoredSession session)
    {
        var file = GetSessionFile(_userKey);

        File.WriteAllText(
            file,
            JsonConvert.SerializeObject(session, Formatting.Indented)
        );
    }
}
