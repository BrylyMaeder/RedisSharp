﻿using RedisSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sample
{
    public class SampleModel : IAsyncModel
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        [Indexed]
        public string Username { get; set; } = "James";
        [Indexed]
        public int Number { get; set; } = 5;
        [Indexed]
        public bool Boolean { get; set; } = true;
        public string IndexName()
        {
            return "samples";
        }
    }
}
