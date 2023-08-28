using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Services
{

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles")]
    internal class ItemUniversalis
    {
        public int lastReviewTime { get; set; }
        public int pricePerUnit { get; set; }
        public int quantity { get; set; }
        public int stainID { get; set; }
        public string worldName { get; set; }
        public int worldID { get; set; }
        public string creatorName { get; set; }
        public string creatorID { get; set; }
        public bool hq { get; set; }
        public bool isCrafted { get; set; }
        public string listingID { get; set; }

        // Materia

        public bool onMannequin { get; set; }
        public int retainerCity { get; set; }
        public string retainerID { get; set; }
        public string retainerName { get; set; }
        public string sellerID { get; set; }
        public int total { get; set; }
    }
}
