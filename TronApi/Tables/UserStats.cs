﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TronApi
{
    public class UserStats
    {
        [Key]
        public int StatId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User? User { get; set; }
        public int Strength { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public int Dexterity { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int Life { get; set; }
        public int Money { get; set; }
        public int Energy { get; set; }

        public UserStats(int userId)
        {
            UserId = userId;
            Strength = 10;
            Defense = 10;
            Speed = 10;
            Dexterity = 10;
            Level = 1;
            Experience = 0;
            Life = 100;
            Money = 100;
            Energy = 6;
        }
    }
}
