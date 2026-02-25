namespace Devian
{
    public sealed class GameStorageManager : CompoSingleton<GameStorageManager>
    {
        InventoryStorage _inventory => InventoryManager.Instance.Storage;
        readonly PurchaseStorage _purchase = new();

        public InventoryStorage Inventory => _inventory;
        public PurchaseStorage Purchase => _purchase;

        public string ToJson() => GameStorageJsonCodec.Serialize(_inventory, _purchase);

        public void LoadFromPayload(string payload)
        {
            var json = ComplexUtil.Decrypt_Base64(payload);
            LoadFromJson(json);
        }

        public void LoadFromJson(string json) => GameStorageJsonCodec.DeserializeInto(json, _inventory, _purchase);

        public void Clear()
        {
            _inventory.Clear();
            _purchase.ClearAll();
        }
    }
}
