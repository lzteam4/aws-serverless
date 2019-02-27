using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWSServerless1.Models
{
   public class FcmMessage
    {
        public string to { get; set; }
        public string[] registration_ids { get; set; }
        public FcmNotification notification { get; set; }
        public object data { get; set; }
    }

    public class FcmNotification
    {
        public string title { get; set; }
        public string body { get; set; }
        public string icon { get; set; }
    }
}
