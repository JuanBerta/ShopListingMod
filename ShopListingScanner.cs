using Eco.Gameplay.Components;
using Eco.Gameplay.Components.Store;
using Eco.Gameplay.Items;
using Eco.Gameplay.Objects;
using Eco.Gameplay.Players;
using Eco.Gameplay.Systems.TextLinks;
using Eco.Shared;
using Eco.Shared.Items;
using Eco.Shared.Localization;
using Eco.Shared.Math;
using Eco.Shared.Utils;
using Eco.Shared.Voxel;
using System.Collections.Generic;
using System.Linq;

namespace ShopListing
{
    /// <summary>One store offer found while scanning the world for an item.</summary>
    public sealed class SellerInfo
    {
        public Item Item { get; init; } = null!;
        public LocString StoreLink { get; init; }
        public string StoreName { get; init; } = string.Empty;
        public float Price { get; init; }
        public float Quantity { get; init; }
        public float Distance { get; init; }
    }

    /// <summary>
    /// Scans every player store in the world and returns the stores currently selling a given item,
    /// together with the price, available stock and the distance from the requesting player.
    /// </summary>
    public static class ShopListingScanner
    {
        public static List<SellerInfo> FindSellers(User user, int itemId)
        {
            var item = Item.Get(itemId);
            if (item == null)
                return new List<SellerInfo>();

            var playerPos = user.Player.WorldPos();
            return WorldObjectUtil.AllObjsWithComponent<StoreComponent>()
                .Where(s => s.StoreData != null)
                .SelectMany(s => s.StoreData.SellOffers
                    .Where(o => MatchesItem(o, itemId) && (o.Stack?.Quantity ?? 0) > 0)
                    .Select(o => new SellerInfo
                    {
                        Item = item,
                        StoreName = s.Parent.Name,
                        StoreLink = s.Parent.UILink(),
                        Price = o.Price,
                        Quantity = o.Stack?.Quantity ?? 0,
                        Distance = World.WrappedDistance(playerPos, s.Parent.WorldPos())
                    }))
                .OrderBy(x => x.Price)
                .ThenBy(x => x.Distance)
                .ToList();
        }

        private static bool MatchesItem(TradeOffer offer, int itemId)
        {
            if (offer.Stack?.Item != null)
                return offer.Stack.Item.TypeID == itemId;

            return offer.MeetsSpecialRequirements(Item.Get(itemId));
        }
    }
}
