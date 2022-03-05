using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using MongoDB.Bson;

namespace streamcmdproxy2.Data.Models
{
    /// <summary>
    /// Provides a base properties to all models
    /// </summary>
    public abstract class BaseModel
    {
        /// <summary>
        /// Entry Id
        /// </summary>
        [JsonProperty("id"), Required]
        public string DocumentId { get; set; }

        /// <summary>
        /// MongoDB Object Id
        /// </summary>
        [JsonIgnore]
        public ObjectId _id { get; set; }
    }
}