using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartsBoxStickers
{
    
    public class Part
    {
        public string description { get; set; }
        public long totalStock { get; set; }
        public string name { get; set; }
        public object created { get; set; }
        public string footprint { get; set; }
        public object lastAccessed { get; set; }
        public double? averagePrice { get; set; }
        public string id { get; set; }
        public List<Stock> stock { get; set; }
        public string notes { get; set; }
    }

    public class Stock {
        public string comments { get; set; }
        public float price { get; set; }
        public long quantity { get; set; }
        public string storage { get; set; }
        public ulong timestamp { get; set; }
    }

    public class Storage
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class PartsBox
    {
        public List<Part> parts { get; set; }
        public List<Storage> storage { get; set; }
    }
}
