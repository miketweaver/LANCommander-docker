﻿using LANCommander.SDK.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Media")]
    public class Media : BaseModel
    {
        public Guid FileId { get; set; }
        [MaxLength(64)]
        public string? Name { get; set; }
        public MediaType Type { get; set; }

        [MaxLength(2048)]
        public string? SourceUrl { get; set; }

        [MaxLength(255)]
        public string? MimeType { get; set; }

        [MaxLength(8)]
        public string Crc32 { get; set; }

        public virtual Media? Thumbnail { get; set; }

        [JsonIgnore]
        public virtual Media? Parent { get; set; }

        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("Media")]
        public virtual Game? Game { get; set; }

        public Guid? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        [InverseProperty("Media")]
        public virtual User? User { get; set; }
    }
}
