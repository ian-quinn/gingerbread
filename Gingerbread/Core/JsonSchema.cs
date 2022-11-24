using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Gingerbread.Core
{
    public class JsonSchema
    {
        public class UV
        {
            public double coordU { get; set; }
            public double coordV { get; set; }
        }
        public class Poly
        {
            public string name { get; set; }
            public IList<UV> vertice { get; set; }
            // other custom attributes here
        }
        public class Seg
        {
            public string name { get; set; }
            public UV start { get; set; }
            public UV end { get; set; }
        }
        public class Level
        {
            public string name { get; set; }
            public double elevation { get; set; }
            public double height { get; set; }
            public IList<Poly> rooms { get; set; }
            public IList<Seg> walls { get; set; }
        }
        public class Building
        {
            public string name { get; set; }
            public Poly canvas { get; set; }
            public IList<Level> levels { get; set; }
        }
    }
}
