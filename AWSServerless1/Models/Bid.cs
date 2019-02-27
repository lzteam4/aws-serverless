using System;

namespace AWSServerless1.Models
{
    public class Bid
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string ProductId { get; set; }
        public double amount { get; set; }
        public DateTime CreatedTimestamp { get; set; }
    }
}
