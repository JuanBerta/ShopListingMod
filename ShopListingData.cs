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
        [Serialized, ThreadSafe] public List<ShoppingEntry> Entries { get; set; } = new();

        // 3.3.2: single lock protecting all list mutations (chat thread writes + storage thread saves).
        [ThreadSafe] public readonly object SyncRoot = new();

        public IPersistent? StorageHandle { get; set; }
    }

    /// <summary>A single shopping list entry: an item, the amount the player wants to buy,
    /// the owning user (3.2.1) and an optional target price used for price alerts (3.1.1).</summary>
    [Serialized]
    public class ShoppingEntry
    {
        [Serialized] public int OwnerUserId { get; set; }
        [Serialized] public int ItemId { get; set; }
        [Serialized] public float Amount { get; set; }
        [Serialized] public float TargetPrice { get; set; }
    }
}
