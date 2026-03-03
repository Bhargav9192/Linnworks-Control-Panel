using System;
using System.Collections.Generic;
using System.Text;

namespace LinnworksAPI.Models
{
    public class UserConfigList
    {
        public List<UserConfig> Users { get; set; }
    }

    public class UserConfig
    {
        public string ApplicationName { get; set; }
        public Guid ApplicationId { get; set; }
        public Guid ApplicationSecret { get; set; }
        public Guid Token { get; set; }  // User's unique token
        public string UserKey { get; set; }
    }

}
