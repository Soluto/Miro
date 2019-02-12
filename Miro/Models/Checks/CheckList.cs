using System;
using System.Collections.Generic;
using MongoDB.Bson;

namespace Miro.Models.Checks
{
    public class CheckList
    {
        public ObjectId Id { get; set; }
        public string Owner { get; set; }
        public string Repo { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<string> CheckNames { get; set; } = new List<string>();
    }
}