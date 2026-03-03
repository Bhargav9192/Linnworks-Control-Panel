using LinnworksAPI;
using System;

internal class StoredSession
{
    public Guid Token { get; set; }
    public string Server { get; set; }
    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public BaseSession ToBaseSession()
    {
        return new BaseSession
        {
            Token = Token,
            Server = Server,
            Id = UserId
        };
    }
}
