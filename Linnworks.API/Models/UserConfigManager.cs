using LinnworksAPI.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class UserConfigManager
{
    public static UserConfig GetUser(string userKey)
    {
        var json = File.ReadAllText("appsettings.json");

        var dict = JsonConvert.DeserializeObject<
            Dictionary<string, UserConfig>
        >(json);

        if (!dict.ContainsKey(userKey))
            throw new Exception($"User '{userKey}' not found");

        var user = dict[userKey];
        user.UserKey = userKey;

        return user;
    }
}
