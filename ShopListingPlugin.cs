using Eco.Core.Plugins;
using Eco.Core.Plugins.Interfaces;
using Eco.Shared.Localization;
using Eco.Shared.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace ShopListing
{
    internal class ShopListingPlugin : Singleton<ShopListingPlugin>, IModKitPlugin, ISaveablePlugin
    {
        [NotNull] private readonly ShopListingData data;

        public ShopListingPlugin()
        {
            try { data = StorageManager.LoadOrCreate<ShopListingData>("ShopListing"); }
            catch
            {
                foreach (var fileName in StorageManager.GetFiles("ShopListing"))
                    StorageManager.Delete(fileName);

                data = StorageManager.LoadOrCreate<ShopListingData>("ShopListing");
            }
        }

        public string GetCategory() => Localizer.DoStr("Mods");
        public string GetStatus() => string.Empty;

        public void SaveAll()
        {
            StorageManager.Obj.MarkDirty(data);
        }

        public Task ShutdownAsync() => Task.CompletedTask;
    }
}
