using System;

namespace AWSServerless1.Models
{
    public class User
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public string FcmToken { get; set; }
        public DateTime CreatedTimestamp { get; set; }
    }
}
