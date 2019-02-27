using System.Collections.Generic;

namespace AWSServerless1.Helpers
{
    public class HeaderHelper
    {
        public static Dictionary<string, string> GetHeaderAttributes()
        {
            var headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
            headers.Add("Access-Control-Allow-Origin", "*");

            return headers;
        }
    }
}
