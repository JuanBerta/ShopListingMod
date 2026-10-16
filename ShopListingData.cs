using Eco.Core.Controller;
using Eco.Core.Serialization;
using Eco.Core.Utils;
using Eco.Gameplay.Players;
using Eco.Gameplay.PropertyHandling;
using Eco.Gameplay.Utils;
using Eco.Shared.Networking;
using Eco.Shared.Serialization;
using Eco.Shared.Utils;
using PropertyChanged;
using System.Collections.Generic;
using System.ComponentModel;

namespace ShopListing
{
    /// <summary>Persisted per-server shopping list data.</summary>
    [Serialized]
    public class ShopListingData : Singleton<ShopListingData>, IStorage
    {
        [Serialized] public List<ShoppingEntry> Entries { get; set; } = new();
        public IPersistent? StorageHandle { get; set; }
    }

    /// <summary>A single shopping list entry: an item and the amount the player wants to buy.</summary>
    [Serialized]
    public class ShoppingEntry
    {
        [Serialized] public int ItemId { get; set; }
        [Serialized] public float Amount { get; set; }
    }
}
