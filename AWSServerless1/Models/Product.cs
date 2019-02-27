using System;
using System.Collections.Generic;

namespace AWSServerless1.Models
{
    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public double Price { get; set; }
        public double StarRating { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public List<string> Tags { get; set; }
        public DateTime ReleaseTimestamp { get; set; }
        public DateTime CreatedTimestamp { get; set; }
    }
}
