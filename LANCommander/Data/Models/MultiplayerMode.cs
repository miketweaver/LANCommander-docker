﻿using LANCommander.Data.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("MultiplayerModes")]
    public class MultiplayerMode : BaseModel
    {
        public MultiplayerType Type { get; set; }
        public NetworkProtocol NetworkProtocol { get; set; }
        public string Description { get; set; }
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int Spectators { get; set; }

        public virtual Game Game { get; set; }
    }
}
