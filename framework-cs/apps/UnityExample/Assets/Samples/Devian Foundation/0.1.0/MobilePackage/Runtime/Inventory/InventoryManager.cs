using System;
using System.Collections.Generic;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class InventoryManager : CompoSingleton<InventoryManager>
    {
        const long DefaultRentalDurationMs = 30L * 24L * 60L * 60L * 1000L;

        public readonly struct CurrencySpendReceipt
        {
            public CurrencySpendReceipt(
                CURRENCY_TYPE currencyType,
                long deductJewelFree,
                long deductJewelPaid,
                long deductAmount)
            {
                CurrencyType = currencyType;
                DeductJewelFree = deductJewelFree;
                DeductJewelPaid = deductJewelPaid;
                DeductAmount = deductAmount;
            }

            public CURRENCY_TYPE CurrencyType { get; }
            public long DeductJewelFree { get; }
            public long DeductJewelPaid { get; }
            public long DeductAmount { get; }
            public bool HasDeduction => DeductJewelFree > 0L || DeductJewelPaid > 0L || DeductAmount > 0L;
        }

        readonly InventoryStorage _storage = new();
        readonly InventoryMessageTrigger _messageTrigger = new();
        readonly InventoryStaminaController _staminaController = new();
        readonly List<AbilityItemEquip> _equippedItems = new();
        readonly List<AbilityItemEquip> _unequippedItems = new();
        readonly List<ITEM_EQUIP> _unownedEquipItems = new();

        public int MaxStamina => _staminaController.MaxStamina;
        public IReadOnlyList<AbilityItemEquip> EquippedItems => _equippedItems;
        public IReadOnlyList<AbilityItemEquip> UnequippedItems => _unequippedItems;
        public IReadOnlyList<ITEM_EQUIP> UnownedEquipItems => _unownedEquipItems;
        public string SelectedHeroId
        {
            get => _storage.SelectedHeroId;
            set => _storage.SelectedHeroId = normalizeSelectedHeroId(value);
        }
        public AbilityItemHero SelectedHero => _storage.GetHero(_storage.SelectedHeroId);

        protected override void onInitAwake()
        {
            refreshEquipViews();
        }

        // ── Message API ──

        public void Subcribe(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, BaseTrigger<EntityId, INVENTORY_MESSAGE_TYPE>.Handler handler)
        {
            _messageTrigger.Subcribe(ownerKey, msgType, handler);
        }

        public void SubcribeOnce(EntityId ownerKey, INVENTORY_MESSAGE_TYPE msgType, Action<object[]> handler)
        {
            _messageTrigger.SubcribeOnce(ownerKey, msgType, handler);
        }

        public void UnSubcribe(EntityId ownerKey)
        {
            _messageTrigger.UnSubcribe(ownerKey);
        }

        public bool HasSufficientCurrency(CURRENCY_TYPE currencyType, int amount)
        {
            if (amount < 0)
                return false;

            if (amount == 0)
                return true;

            if (currencyType == CURRENCY_TYPE.FREE || currencyType == CURRENCY_TYPE.ADS)
                return false;

            return GetCurrencyAmount(currencyType) >= amount;
        }

        public bool TrySpendCurrency(CURRENCY_TYPE currencyType, int amount, out CurrencySpendReceipt receipt)
        {
            receipt = default;

            if (amount < 0)
                return false;

            if (amount == 0)
                return true;

            if (currencyType == CURRENCY_TYPE.FREE || currencyType == CURRENCY_TYPE.ADS)
                return false;

            if (currencyType == CURRENCY_TYPE.JEWEL)
            {
                var free = GetCurrencyAmount(CURRENCY_TYPE.JEWEL_FREE);
                var paid = GetCurrencyAmount(CURRENCY_TYPE.JEWEL_PAID);
                var total = free + paid;
                if (total < amount)
                    return false;

                var useFree = Math.Min((long)amount, free);
                var usePaid = amount - useFree;

                if (useFree > 0L)
                {
                    var revokeFree = RevokeCurrency(CURRENCY_TYPE.JEWEL_FREE, useFree);
                    if (revokeFree.IsFailure)
                        return false;
                }

                if (usePaid > 0L)
                {
                    var revokePaid = RevokeCurrency(CURRENCY_TYPE.JEWEL_PAID, usePaid);
                    if (revokePaid.IsFailure)
                    {
                        if (useFree > 0L)
                            ApplyCurrency(CURRENCY_TYPE.JEWEL_FREE, useFree);
                        return false;
                    }
                }

                receipt = new CurrencySpendReceipt(CURRENCY_TYPE.JEWEL, useFree, usePaid, 0L);
                return true;
            }

            if (!HasSufficientCurrency(currencyType, amount))
                return false;

            var revoke = RevokeCurrency(currencyType, amount);
            if (revoke.IsFailure)
                return false;

            receipt = new CurrencySpendReceipt(currencyType, 0L, 0L, amount);
            return true;
        }

        public void RollbackCurrencySpend(CurrencySpendReceipt receipt)
        {
            if (!receipt.HasDeduction)
                return;

            if (receipt.CurrencyType == CURRENCY_TYPE.JEWEL)
            {
                if (receipt.DeductJewelFree > 0L)
                    ApplyCurrency(CURRENCY_TYPE.JEWEL_FREE, receipt.DeductJewelFree);

                if (receipt.DeductJewelPaid > 0L)
                    ApplyCurrency(CURRENCY_TYPE.JEWEL_PAID, receipt.DeductJewelPaid);

                return;
            }

            if (receipt.DeductAmount > 0L)
                ApplyCurrency(receipt.CurrencyType, receipt.DeductAmount);
        }

        public InventorySnapshot CreateSnapshot()
        {
            var snapshot = new InventorySnapshot();

            foreach (var kv in _storage.EnumerateCurrencyBalancesForSave())
                snapshot.CurrencyBalances[kv.Key] = kv.Value;

            foreach (var kv in _storage.Equipments)
            {
                var equip = kv.Value;
                snapshot.Equipments[kv.Key] = new InventorySnapshotEquipment
                {
                    ItemId = equip.ItemId,
                    ItemUid = equip.ItemUid,
                    ItemLevel = equip.ItemLevel,
                };
            }

            foreach (var kv in _storage.Cards)
            {
                var card = kv.Value;
                snapshot.Cards[kv.Key] = new InventorySnapshotCard
                {
                    ItemId = card.ItemId,
                    ItemLevel = card.ItemLevel,
                    Amount = card.Amount,
                };
            }

            foreach (var kv in _storage.Materials)
            {
                var material = kv.Value;
                snapshot.Materials[kv.Key] = new InventorySnapshotMaterial
                {
                    ItemId = material.ItemId,
                    Amount = material.Amount,
                };
            }

            foreach (var kv in _storage.Heroes)
            {
                var hero = kv.Value;
                var heroSnapshot = new InventorySnapshotHero
                {
                    ItemId = hero.ItemId,
                    ItemLevel = hero.ItemLevel,
                    Amount = hero.Amount,
                };

                foreach (var equip in hero.Equips)
                    heroSnapshot.Equips[equip.Key] = equip.Value.ItemUid;

                snapshot.Heroes[kv.Key] = heroSnapshot;
            }

            snapshot.SelectedHeroId = _storage.SelectedHeroId;

            foreach (var kv in _storage.Rentals)
                snapshot.Rentals[kv.Key] = kv.Value;

            foreach (var kv in _storage.Passes)
                snapshot.Passes[kv.Key] = kv.Value;

            foreach (var kv in _storage.TreasureCounts)
                snapshot.TreasureCounts[kv.Key] = kv.Value;

            snapshot.TreasureCurrentLevel = _storage.TreasureCurrent.Level;
            snapshot.TreasureCurrentExp = _storage.TreasureCurrent.Exp;
            snapshot.LastStaminaUpdateUtcMs = _storage.LastStaminaUpdateUtcMs;
            return snapshot;
        }

        public void ClearState(INVENTORY_SNAPSHOT_CHANGE_REASON reason)
        {
            _storage.Clear();
            refreshEquipViews();
            notifySnapshotChanged(reason);
        }

        public GameResult ReplaceState(InventorySnapshot snapshot, INVENTORY_SNAPSHOT_CHANGE_REASON reason)
        {
            var nextStorage = createStorageFromSnapshot(snapshot);
            if (nextStorage.IsFailure)
                return GameResult.Failure(nextStorage.Error!);

            _storage.CopyFrom(nextStorage.Value);
            refreshEquipViews();
            notifySnapshotChanged(reason);
            return GameResult.Ok();
        }

        // ── Apply API ──

        public GameResult ApplyCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            if (amount < 0L)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyCurrency: amount is negative: {amount}");
            }

            if (amount == 0L)
                return GameResult.Ok();

            if (!_storage.TryAddCurrency(currencyType, amount))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyCurrency: unsupported currencyType={currencyType}");
            }

            notifyCurrencyChanged(currencyType, amount);

            return GameResult.Ok();
        }

        public GameResult ApplyEquip(string itemId, int amount)
        {
            if (amount < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyEquip: amount is negative: {amount}");
            }

            if (amount == 0)
                return GameResult.Ok();

            for (var i = 0; i < amount; i++)
            {
                var itemUid = Guid.NewGuid().ToString("N");
                var create = AbilityItemFactory.CreateEquip(itemId, itemUid);
                if (create.IsFailure)
                {
                    refreshEquipViews();
                    return GameResult.Failure(create.Error!);
                }

                var equip = create.Value;
                _storage.AddEquip(itemUid, equip);
                notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE.ADD, itemUid, itemId, equip);
            }

            refreshEquipViews();
            return GameResult.Ok();
        }

        public GameResult ApplyCard(string itemId, int amount)
        {
            if (amount < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyCard: amount is negative: {amount}");
            }

            return AddCardAmount(itemId, amount);
        }

        public GameResult AddCardAmount(string itemId, int delta)
        {
            if (delta == 0)
                return GameResult.Ok();

            if (delta < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.AddCardAmount: delta must be >= 0. itemId={itemId}, delta={delta}");
            }

            var existing = _storage.GetCard(itemId);
            if (existing != null)
            {
                existing.AddAmount(delta);
                notifyCardChanged(itemId, existing);
            }
            else
            {
                var create = AbilityItemFactory.CreateCard(itemId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddCard(itemId, ability);
                ability.AddAmount(delta);
                notifyCardListChanged(INVENTORY_LIST_CHANGE_TYPE.ADD, itemId, ability);
            }

            return GameResult.Ok();
        }

        public GameResult ApplyMaterial(string itemId, int amount)
        {
            if (amount < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyMaterial: amount is negative: {amount}");
            }

            return AddMaterialAmount(itemId, amount);
        }

        public GameResult AddMaterialAmount(string itemId, int delta)
        {
            if (delta == 0)
                return GameResult.Ok();

            if (delta < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.AddMaterialAmount: delta must be >= 0. itemId={itemId}, delta={delta}");
            }

            var existing = _storage.GetMaterial(itemId);
            if (existing != null)
            {
                existing.AddAmount(delta);
                notifyMaterialChanged(itemId, existing);
            }
            else
            {
                var create = AbilityItemFactory.CreateMaterial(itemId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddMaterial(itemId, ability);
                ability.AddAmount(delta);
                notifyMaterialListChanged(INVENTORY_LIST_CHANGE_TYPE.ADD, itemId, ability);
            }

            return GameResult.Ok();
        }

        public GameResult ApplyHero(string heroId, int amount)
        {
            if (amount < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyHero: amount is negative: {amount}");
            }

            return AddHeroAmount(heroId, amount);
        }

        public GameResult AddHeroAmount(string heroId, int delta)
        {
            if (delta == 0)
                return GameResult.Ok();

            if (delta < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.AddHeroAmount: delta must be >= 0. heroId={heroId}, delta={delta}");
            }

            var existing = _storage.GetHero(heroId);
            if (existing != null)
            {
                existing.AddAmount(delta);
                notifyHeroChanged(heroId, existing);
            }
            else
            {
                var create = AbilityItemFactory.CreateHero(heroId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddHero(heroId, ability);
                ability.AddAmount(delta);
                notifyHeroListChanged(INVENTORY_LIST_CHANGE_TYPE.ADD, heroId, ability);
            }

            return GameResult.Ok();
        }

        public GameResult LevelUpCard(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.LevelUpCard: itemId is null or empty.");
            }

            var card = _storage.GetCard(itemId);
            if (card == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.LevelUpCard: card runtime not found. itemId={itemId}");
            }

            var levelUpCost = card.ResolveLevelUpCost();
            if (levelUpCost.IsFailure)
                return GameResult.Failure(levelUpCost.Error!);

            var currencyCost = card.ResolveLevelUpCurrencyCost();
            if (currencyCost.IsFailure)
                return GameResult.Failure(currencyCost.Error!);

            if (card.Amount < levelUpCost.Value)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_CARD_LEVELUP_COUNT_INSUFFICIENT,
                    $"InventoryManager.LevelUpCard: insufficient card count. itemId={itemId}, need={levelUpCost.Value}, current={card.Amount}, itemLevel={card.ItemLevel}");
            }

            var currencySpend = default(CurrencySpendReceipt);
            if (currencyCost.Value.Amount > 0
                && !TrySpendCurrency(currencyCost.Value.CurrencyType, currencyCost.Value.Amount, out currencySpend))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_ITEM_LEVELUP_CURRENCY_INSUFFICIENT,
                    $"InventoryManager.LevelUpCard: insufficient level-up currency. itemId={itemId}, currencyType={currencyCost.Value.CurrencyType}, need={currencyCost.Value.Amount}, current={GetCurrencyAmount(currencyCost.Value.CurrencyType)}, itemLevel={card.ItemLevel}");
            }

            card.AddAmount(-levelUpCost.Value);
            var levelUp = card._LevelUp();
            if (levelUp.IsFailure)
            {
                card.AddAmount(levelUpCost.Value);
                RollbackCurrencySpend(currencySpend);
                return GameResult.Failure(levelUp.Error!);
            }

            notifyCardChanged(itemId, card);
            return GameResult.Ok();
        }

        public GameResult LevelUpHero(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.LevelUpHero: heroId is null or empty.");
            }

            var hero = _storage.GetHero(heroId);
            if (hero == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.LevelUpHero: hero runtime not found. heroId={heroId}");
            }

            var levelUpCost = hero.ResolveLevelUpCost();
            if (levelUpCost.IsFailure)
                return GameResult.Failure(levelUpCost.Error!);

            var currencyCost = hero.ResolveLevelUpCurrencyCost();
            if (currencyCost.IsFailure)
                return GameResult.Failure(currencyCost.Error!);

            if (hero.Amount < levelUpCost.Value)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_HERO_LEVELUP_COUNT_INSUFFICIENT,
                    $"InventoryManager.LevelUpHero: insufficient hero count. heroId={heroId}, need={levelUpCost.Value}, current={hero.Amount}, itemLevel={hero.ItemLevel}");
            }

            var currencySpend = default(CurrencySpendReceipt);
            if (currencyCost.Value.Amount > 0
                && !TrySpendCurrency(currencyCost.Value.CurrencyType, currencyCost.Value.Amount, out currencySpend))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_ITEM_LEVELUP_CURRENCY_INSUFFICIENT,
                    $"InventoryManager.LevelUpHero: insufficient level-up currency. heroId={heroId}, currencyType={currencyCost.Value.CurrencyType}, need={currencyCost.Value.Amount}, current={GetCurrencyAmount(currencyCost.Value.CurrencyType)}, itemLevel={hero.ItemLevel}");
            }

            hero.AddAmount(-levelUpCost.Value);
            var levelUp = hero._LevelUp();
            if (levelUp.IsFailure)
            {
                hero.AddAmount(levelUpCost.Value);
                RollbackCurrencySpend(currencySpend);
                return GameResult.Failure(levelUp.Error!);
            }

            notifyHeroChanged(heroId, hero);
            return GameResult.Ok();
        }

        public GameResult LevelUpEquip(string itemUid)
        {
            if (string.IsNullOrWhiteSpace(itemUid))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.LevelUpEquip: itemUid is null or empty.");
            }

            var equip = _storage.GetEquip(itemUid);
            if (equip == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.LevelUpEquip: equip runtime not found. itemUid={itemUid}");
            }

            var levelUpCost = equip.ResolveLevelUpMaterialCost();
            if (levelUpCost.IsFailure)
                return GameResult.Failure(levelUpCost.Error!);

            var currencyCost = equip.ResolveLevelUpCurrencyCost();
            if (currencyCost.IsFailure)
                return GameResult.Failure(currencyCost.Error!);

            AbilityItemMaterial material = null;
            var materialRemoved = false;
            if (levelUpCost.Value.Amount > 0)
            {
                material = _storage.GetMaterial(levelUpCost.Value.MaterialItemId);
                if (material == null || material.Amount < levelUpCost.Value.Amount)
                {
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.INVENTORY_EQUIP_LEVELUP_MATERIAL_INSUFFICIENT,
                        $"InventoryManager.LevelUpEquip: insufficient level-up material. itemUid={itemUid}, equipItemId={equip.ItemId}, materialItemId={levelUpCost.Value.MaterialItemId}, need={levelUpCost.Value.Amount}, current={material?.Amount ?? 0}, itemLevel={equip.ItemLevel}");
                }
            }

            var currencySpend = default(CurrencySpendReceipt);
            if (currencyCost.Value.Amount > 0
                && !TrySpendCurrency(currencyCost.Value.CurrencyType, currencyCost.Value.Amount, out currencySpend))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_ITEM_LEVELUP_CURRENCY_INSUFFICIENT,
                    $"InventoryManager.LevelUpEquip: insufficient level-up currency. itemUid={itemUid}, equipItemId={equip.ItemId}, currencyType={currencyCost.Value.CurrencyType}, need={currencyCost.Value.Amount}, current={GetCurrencyAmount(currencyCost.Value.CurrencyType)}, itemLevel={equip.ItemLevel}");
            }

            if (levelUpCost.Value.Amount > 0)
            {
                material.AddAmount(-levelUpCost.Value.Amount);
                if (material.Amount == 0)
                {
                    _storage.RemoveMaterial(levelUpCost.Value.MaterialItemId);
                    materialRemoved = true;
                }
            }

            var levelUp = equip._LevelUp();
            if (levelUp.IsFailure)
            {
                if (material != null)
                {
                    if (materialRemoved)
                        _storage.AddMaterial(levelUpCost.Value.MaterialItemId, material);

                    material.AddAmount(levelUpCost.Value.Amount);
                }

                RollbackCurrencySpend(currencySpend);
                return GameResult.Failure(levelUp.Error!);
            }

            if (material != null)
            {
                if (materialRemoved)
                    notifyMaterialListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, levelUpCost.Value.MaterialItemId, null);
                else
                    notifyMaterialChanged(levelUpCost.Value.MaterialItemId, material);
            }

            notifyEquipChanged(itemUid, equip.ItemId, equip);
            return GameResult.Ok();
        }

        public GameResult<AbilityItemEquip> UpgradeEquipItem(string baseItemUid, string materialItemUid0, string materialItemUid1)
        {
            if (string.IsNullOrWhiteSpace(baseItemUid))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.UpgradeEquipItem: baseItemUid is null or empty.");
            }

            if (string.IsNullOrWhiteSpace(materialItemUid0) || string.IsNullOrWhiteSpace(materialItemUid1))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.UpgradeEquipItem: material itemUid is null or empty.");
            }

            if (baseItemUid == materialItemUid0
                || baseItemUid == materialItemUid1
                || materialItemUid0 == materialItemUid1)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.UpgradeEquipItem: source itemUids must be distinct. base={baseItemUid}, material0={materialItemUid0}, material1={materialItemUid1}");
            }

            var baseEquip = _storage.GetEquip(baseItemUid);
            var materialEquip0 = _storage.GetEquip(materialItemUid0);
            var materialEquip1 = _storage.GetEquip(materialItemUid1);
            if (baseEquip == null || materialEquip0 == null || materialEquip1 == null)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.UpgradeEquipItem: source equip runtime not found. base={baseItemUid}, material0={materialItemUid0}, material1={materialItemUid1}");
            }

            if (baseEquip.IsEquipped || materialEquip0.IsEquipped || materialEquip1.IsEquipped)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.UpgradeEquipItem: equipped item cannot be used as upgrade source. base={baseItemUid}, material0={materialItemUid0}, material1={materialItemUid1}");
            }

            var sourceItemId = baseEquip.ItemId;
            if (string.IsNullOrWhiteSpace(sourceItemId)
                || materialEquip0.ItemId != sourceItemId
                || materialEquip1.ItemId != sourceItemId)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.UpgradeEquipItem: all source equips must share the same itemId. base={baseEquip.ItemId}, material0={materialEquip0.ItemId}, material1={materialEquip1.ItemId}");
            }

            var baseTable = TB_ITEM_EQUIP.Get(sourceItemId);
            if (baseTable == null)
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.ABILITY_ITEM_TABLE_NOT_FOUND,
                    $"InventoryManager.UpgradeEquipItem: ITEM_EQUIP not found. itemId={sourceItemId}");
            }

            if (string.IsNullOrWhiteSpace(baseTable.upgrade_id))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.UpgradeEquipItem: upgrade target is empty. itemId={sourceItemId}");
            }

            var nextItemUid = Guid.NewGuid().ToString("N");
            var createUpgrade = AbilityItemFactory.CreateEquip(
                baseTable.upgrade_id,
                nextItemUid,
                baseEquip.ItemLevel);
            if (createUpgrade.IsFailure)
                return GameResult<AbilityItemEquip>.Failure(createUpgrade.Error!);

            if (!_storage.RemoveEquip(materialItemUid0)
                || !_storage.RemoveEquip(materialItemUid1)
                || !_storage.RemoveEquip(baseItemUid))
            {
                return GameResult<AbilityItemEquip>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.UpgradeEquipItem: failed to remove source equips. base={baseItemUid}, material0={materialItemUid0}, material1={materialItemUid1}");
            }

            notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, materialItemUid0, sourceItemId, null);
            notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, materialItemUid1, sourceItemId, null);
            notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, baseItemUid, sourceItemId, null);

            var nextEquip = createUpgrade.Value;
            _storage.AddEquip(nextItemUid, nextEquip);
            notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE.ADD, nextItemUid, nextEquip.ItemId, nextEquip);

            refreshEquipViews();
            return GameResult<AbilityItemEquip>.Success(nextEquip);
        }

        public GameResult SetHeroEquip(string heroId, EQUIP_SLOT_TYPE slotType, string equipUid)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.SetHeroEquip: heroId is null or empty.");
            }

            if (slotType == EQUIP_SLOT_TYPE.NONE)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.SetHeroEquip: slotType must not be NONE.");
            }

            if (string.IsNullOrWhiteSpace(equipUid))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.SetHeroEquip: equipUid is null or empty.");
            }

            var hero = _storage.GetHero(heroId);
            if (hero == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.SetHeroEquip: hero runtime not found. heroId={heroId}");
            }

            var equip = _storage.GetEquip(equipUid);
            if (equip == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.SetHeroEquip: equip runtime not found. equipUid={equipUid}");
            }

            var resolveRule = resolveEquipSlotRule(equip);
            if (resolveRule.IsFailure)
                return GameResult.Failure(resolveRule.Error!);

            var rule = resolveRule.Value;
            switch (AbilityEquipSlotPolicy.GetPlacementFailure(rule, slotType, hero.Equips))
            {
                case AbilityEquipPlacementFailure.None:
                    break;
                case AbilityEquipPlacementFailure.SlotNotAllowed:
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.SetHeroEquip: slot is not allowed. heroId={heroId}, equipUid={equipUid}, equipType={equip.EquipType}, slotType={slotType}");
                case AbilityEquipPlacementFailure.HandSubBlockedByTwoHandedMain:
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.SetHeroEquip: HAND_SUB is blocked by two-handed main weapon. heroId={heroId}, equipUid={equipUid}");
                default:
                    return GameResult.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.SetHeroEquip: unsupported equip placement failure. heroId={heroId}, equipUid={equipUid}, slotType={slotType}");
            }

            var targetPrev = hero.GetEquip(slotType);
            var prevOwnerHeroId = equip.OwnerUnitId;
            var prevOwnerHero = !string.IsNullOrWhiteSpace(prevOwnerHeroId) && prevOwnerHeroId != heroId
                ? _storage.GetHero(prevOwnerHeroId)
                : null;
            var autoUnequippedSubHand = slotType == EQUIP_SLOT_TYPE.HAND_MAIN && AbilityEquipSlotPolicy.IsTwoHanded(rule)
                ? hero.GetEquip(EQUIP_SLOT_TYPE.HAND_SUB)
                : null;

            if (!_storage.Equip(heroId, slotType, equipUid))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.SetHeroEquip: equip operation failed. heroId={heroId}, slotType={slotType}, equipUid={equipUid}");
            }

            refreshEquipViews();
            notifyHeroChanged(heroId, hero);
            if (prevOwnerHero != null)
                notifyHeroChanged(prevOwnerHeroId, prevOwnerHero);

            notifyEquipChanged(equip.ItemUid, equip.ItemId, equip);
            if (targetPrev != null && !AbilityItemEquip.IsSame(targetPrev, equip))
                notifyEquipChanged(targetPrev.ItemUid, targetPrev.ItemId, targetPrev);

            if (autoUnequippedSubHand != null
                && !AbilityItemEquip.IsSame(autoUnequippedSubHand, equip)
                && !AbilityItemEquip.IsSame(autoUnequippedSubHand, targetPrev))
            {
                notifyEquipChanged(autoUnequippedSubHand.ItemUid, autoUnequippedSubHand.ItemId, autoUnequippedSubHand);
            }

            return GameResult.Ok();
        }

        public GameResult RemoveHeroEquip(string heroId, EQUIP_SLOT_TYPE slotType)
        {
            if (string.IsNullOrWhiteSpace(heroId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.RemoveHeroEquip: heroId is null or empty.");
            }

            if (slotType == EQUIP_SLOT_TYPE.NONE)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.RemoveHeroEquip: slotType must not be NONE.");
            }

            var hero = _storage.GetHero(heroId);
            if (hero == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.RemoveHeroEquip: hero runtime not found. heroId={heroId}");
            }

            var equip = hero.GetEquip(slotType);
            if (equip == null)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.RemoveHeroEquip: no equipped item at slot. heroId={heroId}, slotType={slotType}");
            }

            if (!_storage.Unequip(heroId, slotType))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.RemoveHeroEquip: unequip failed. heroId={heroId}, slotType={slotType}");
            }

            refreshEquipViews();
            notifyEquipChanged(equip.ItemUid, equip.ItemId, equip);
            notifyHeroChanged(heroId, hero);
            return GameResult.Ok();
        }

        public GameResult ApplyRental(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.ApplyRental: item_id is null or empty.");
            }

            var nowUtcMs = RemoteDataManager.ServerNowUtcMs;
            var currentExpiryUtcMs = _storage.GetRentalExpiry(itemId);
            var baseUtcMs = currentExpiryUtcMs > nowUtcMs ? currentExpiryUtcMs : nowUtcMs;
            var nextExpiryUtcMs = baseUtcMs + DefaultRentalDurationMs;
            _storage.SetRental(itemId, nextExpiryUtcMs);
            notifyRentalChanged(itemId, nextExpiryUtcMs, true);
            return GameResult.Ok();
        }

        public GameResult ApplyTreasure(ITEM_GRADE_TYPE gradeType, int amount)
        {
            if (gradeType == ITEM_GRADE_TYPE.NONE)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.ApplyTreasure: gradeType is NONE.");
            }

            if (amount < 0)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyTreasure: amount is negative: {amount}");
            }

            if (amount == 0)
                return GameResult.Ok();

            _storage.AddTreasure(gradeType, amount);
            notifyTreasureStateChanged(gradeType, amount);
            return GameResult.Ok();
        }

        public GameResult SetPassOwnership(string itemId, bool owned)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.SetPassOwnership: item_id is null or empty.");
            }

            if (_storage.SetPass(itemId, owned))
                notifyPassOwnershipChanged(itemId, owned);

            return GameResult.Ok();
        }

        public GameResult RemovePassOwnership(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.RemovePassOwnership: item_id is null or empty.");
            }

            if (_storage.RemovePass(itemId))
                notifyPassOwnershipChanged(itemId, false);

            return GameResult.Ok();
        }

        // ── Revoke API ──

        public GameResult RevokeCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            if (amount <= 0L)
                return GameResult.Ok();

            if (currencyType == CURRENCY_TYPE.FREE || currencyType == CURRENCY_TYPE.ADS || currencyType == CURRENCY_TYPE.JEWEL)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.RevokeCurrency: unsupported currencyType={currencyType}");
            }

            var currentAmount = GetCurrencyAmount(currencyType);
            if (currentAmount < amount)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeCurrency: insufficient currency. currencyType={currencyType}, amount={amount}, current={currentAmount}");
            }

            if (_storage.TryAddCurrency(currencyType, -amount))
                notifyCurrencyChanged(currencyType, -amount);

            return GameResult.Ok();
        }

        public GameResult RevokeEquip(string itemId, int amount)
        {
            if (amount <= 0)
                return GameResult.Ok();

            var equips = _storage.GetEquipsByItemId(itemId);
            if (equips.Count < amount)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeEquip: insufficient equip count. itemId={itemId}, amount={amount}, current={equips.Count}");
            }

            for (var j = 0; j < amount && j < equips.Count; j++)
            {
                if (equips[j] != null && !string.IsNullOrEmpty(equips[j].ItemUid))
                {
                    var itemUid = equips[j].ItemUid;
                    _storage.RemoveEquip(itemUid);
                    notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, itemUid, itemId, null);
                }
            }

            refreshEquipViews();
            return GameResult.Ok();
        }

        public GameResult RevokeCard(string itemId, int amount)
        {
            if (amount <= 0)
                return GameResult.Ok();

            var card = _storage.GetCard(itemId);
            if (card == null || card.Amount < amount)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeCard: insufficient card amount. itemId={itemId}, amount={amount}, current={card?.Amount ?? 0}");
            }

            card.AddAmount(-amount);
            if (card.Amount > 0)
            {
                notifyCardChanged(itemId, card);
            }
            else
            {
                _storage.RemoveCard(itemId);
                notifyCardListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, itemId, null);
            }

            return GameResult.Ok();
        }

        public GameResult RevokeMaterial(string itemId, int amount)
        {
            if (amount <= 0)
                return GameResult.Ok();

            var material = _storage.GetMaterial(itemId);
            if (material == null || material.Amount < amount)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeMaterial: insufficient material amount. itemId={itemId}, amount={amount}, current={material?.Amount ?? 0}");
            }

            material.AddAmount(-amount);
            if (material.Amount > 0)
            {
                notifyMaterialChanged(itemId, material);
            }
            else
            {
                _storage.RemoveMaterial(itemId);
                notifyMaterialListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, itemId, null);
            }

            return GameResult.Ok();
        }

        public GameResult RevokeHero(string heroId, int amount)
        {
            if (amount <= 0)
                return GameResult.Ok();

            var hero = _storage.GetHero(heroId);
            if (hero == null || hero.Amount < amount)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeHero: insufficient hero amount. heroId={heroId}, amount={amount}, current={hero?.Amount ?? 0}");
            }

            hero.AddAmount(-amount);
            if (hero.Amount > 0)
            {
                notifyHeroChanged(heroId, hero);
            }
            else
            {
                _storage.RemoveHero(heroId);
                refreshEquipViews();
                notifyHeroListChanged(INVENTORY_LIST_CHANGE_TYPE.REMOVE, heroId, null);
            }

            return GameResult.Ok();
        }

        public GameResult RevokeRental(string itemId)
        {
            var currentExpiryUtcMs = _storage.GetRentalExpiry(itemId);
            if (currentExpiryUtcMs <= 0L)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeRental: rental not found. itemId={itemId}");
            }

            _storage.RemoveRental(itemId);
            notifyRentalChanged(itemId, 0L, false);
            return GameResult.Ok();
        }

        public GameResult RevokeTreasure(ITEM_GRADE_TYPE gradeType, int amount)
        {
            if (amount <= 0)
                return GameResult.Ok();

            var current = _storage.GetTreasureCount(gradeType);
            if (current < amount)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.INVENTORY_REFUND_INSUFFICIENT,
                    $"InventoryManager.RevokeTreasure: insufficient treasure count. gradeType={gradeType}, amount={amount}, current={current}");
            }

            _storage.SetTreasureCount(gradeType, current - amount);
            var next = _storage.GetTreasureCount(gradeType);
            var delta = next - current;
            if (delta != 0)
                notifyTreasureStateChanged(gradeType, delta);

            return GameResult.Ok();
        }

        // ── Query API ──

        public long GetCurrencyAmount(CURRENCY_TYPE currencyType)
        {
            return _storage.GetCurrencyAmount(currencyType);
        }

        public IReadOnlyDictionary<string, AbilityItemEquip> GetEquipments()
        {
            return _storage.Equipments;
        }

        public AbilityItemEquip GetEquip(string itemUid)
        {
            return _storage.GetEquip(itemUid);
        }

        public IReadOnlyList<AbilityItemEquip> GetEquipsByItemId(string itemId)
        {
            return _storage.GetEquipsByItemId(itemId);
        }

        public int GetEquipCount(string itemId)
        {
            return _storage.GetEquipsByItemId(itemId).Count;
        }

        public IReadOnlyDictionary<string, AbilityItemCard> GetCards()
        {
            return _storage.Cards;
        }

        public AbilityItemCard GetCard(string itemId)
        {
            return _storage.GetCard(itemId);
        }

        public long GetCardAmount(string itemId)
        {
            var card = GetCard(itemId);
            return card != null ? card.Amount : 0L;
        }

        public IReadOnlyDictionary<string, AbilityItemMaterial> GetMaterials()
        {
            return _storage.Materials;
        }

        public AbilityItemMaterial GetMaterial(string itemId)
        {
            return _storage.GetMaterial(itemId);
        }

        public long GetMaterialAmount(string itemId)
        {
            var material = GetMaterial(itemId);
            return material != null ? material.Amount : 0L;
        }

        public IReadOnlyDictionary<string, AbilityItemHero> GetHeroes()
        {
            return _storage.Heroes;
        }

        public AbilityItemHero GetHero(string heroId)
        {
            return _storage.GetHero(heroId);
        }

        public long GetHeroAmount(string heroId)
        {
            var hero = GetHero(heroId);
            return hero != null ? hero.Amount : 0L;
        }

        public IReadOnlyDictionary<string, long> GetRentals()
        {
            return _storage.Rentals;
        }

        public bool HasActiveRental(string itemId)
        {
            return _storage.HasActiveRental(itemId);
        }

        public long GetRentalRemainingMs(string itemId)
        {
            return _storage.GetRentalRemainingMs(itemId);
        }

        public IReadOnlyDictionary<string, bool> GetPasses()
        {
            return _storage.Passes;
        }

        public bool HasPass(string itemId)
        {
            return _storage.HasPass(itemId);
        }

        public int GetTreasureCount(ITEM_GRADE_TYPE gradeType)
        {
            return _storage.GetTreasureCount(gradeType);
        }

        public int GetTreasureCurrentLevel()
        {
            return _storage.TreasureCurrent.Level;
        }

        public int GetTreasureCurrentExp()
        {
            return _storage.TreasureCurrent.Exp;
        }

        public void SetTreasureCurrentState(int level, int exp)
        {
            var normalizedLevel = level < 1 ? 1 : level;
            var normalizedExp = exp < 0 ? 0 : exp;
            if (_storage.TreasureCurrent.Level == normalizedLevel
                && _storage.TreasureCurrent.Exp == normalizedExp)
                return;

            _storage.SetTreasureCurrentState(normalizedLevel, normalizedExp);
            notifyTreasureStateChanged(ITEM_GRADE_TYPE.NONE, 0);
        }

        // ── Stamina ──

        public void LoadSettings() => _staminaController.LoadSettings();

        public void RecoverStamina()
        {
            var recovery = _staminaController.CalculateRecovery(
                GetCurrencyAmount(CURRENCY_TYPE.STAMINA),
                _storage.LastStaminaUpdateUtcMs,
                RemoteDataManager.ServerNowUtcMs);

            _storage.LastStaminaUpdateUtcMs = recovery.NextLastUpdateUtcMs;

            if (recovery.RecoveredAmount > 0L)
            {
                var apply = ApplyCurrency(CURRENCY_TYPE.STAMINA, recovery.RecoveredAmount);
                if (apply.IsFailure)
                    Debug.LogError($"[InventoryManager] RecoverStamina ApplyCurrency failed: {apply.Error}");
            }
        }

        static GameResult<InventoryStorage> createStorageFromSnapshot(InventorySnapshot snapshot)
        {
            var nextStorage = new InventoryStorage();
            if (snapshot == null)
                return GameResult<InventoryStorage>.Success(nextStorage);

            foreach (var kv in snapshot.CurrencyBalances)
            {
                if (kv.Value == 0L)
                    continue;

                if (kv.Value < 0L)
                {
                    return GameResult<InventoryStorage>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.ReplaceState: negative currency amount. currencyType={kv.Key}, amount={kv.Value}");
                }

                if (!nextStorage.TryAddCurrency(kv.Key, kv.Value))
                {
                    return GameResult<InventoryStorage>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.ReplaceState: unsupported currency type in snapshot. currencyType={kv.Key}");
                }
            }

            foreach (var kv in snapshot.Equipments)
            {
                var equipSnapshot = kv.Value;
                if (equipSnapshot == null)
                    continue;

                var itemUid = string.IsNullOrWhiteSpace(equipSnapshot.ItemUid) ? kv.Key : equipSnapshot.ItemUid;
                var createEquip = AbilityItemFactory.CreateEquip(
                    equipSnapshot.ItemId,
                    itemUid,
                    equipSnapshot.ItemLevel);
                if (createEquip.IsFailure)
                    return GameResult<InventoryStorage>.Failure(createEquip.Error!);

                nextStorage.AddEquip(itemUid, createEquip.Value);
            }

            foreach (var kv in snapshot.Cards)
            {
                var cardSnapshot = kv.Value;
                if (cardSnapshot == null)
                    continue;

                if (cardSnapshot.Amount < 0)
                {
                    return GameResult<InventoryStorage>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.ReplaceState: negative card amount. itemId={cardSnapshot.ItemId}, amount={cardSnapshot.Amount}");
                }

                if (cardSnapshot.Amount == 0
                    && cardSnapshot.ItemLevel == 1)
                    continue;

                var createCard = AbilityItemFactory.CreateCard(cardSnapshot.ItemId, cardSnapshot.ItemLevel);
                if (createCard.IsFailure)
                    return GameResult<InventoryStorage>.Failure(createCard.Error!);

                createCard.Value.AddAmount(cardSnapshot.Amount);
                nextStorage.AddCard(cardSnapshot.ItemId, createCard.Value);
            }

            foreach (var kv in snapshot.Materials)
            {
                var materialSnapshot = kv.Value;
                if (materialSnapshot == null)
                    continue;

                if (materialSnapshot.Amount < 0)
                {
                    return GameResult<InventoryStorage>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.ReplaceState: negative material amount. itemId={materialSnapshot.ItemId}, amount={materialSnapshot.Amount}");
                }

                if (materialSnapshot.Amount == 0)
                    continue;

                var createMaterial = AbilityItemFactory.CreateMaterial(materialSnapshot.ItemId);
                if (createMaterial.IsFailure)
                    return GameResult<InventoryStorage>.Failure(createMaterial.Error!);

                createMaterial.Value.AddAmount(materialSnapshot.Amount);
                nextStorage.AddMaterial(materialSnapshot.ItemId, createMaterial.Value);
            }

            foreach (var kv in snapshot.Heroes)
            {
                var heroSnapshot = kv.Value;
                if (heroSnapshot == null)
                    continue;

                if (heroSnapshot.Amount < 0)
                {
                    return GameResult<InventoryStorage>.Failure(
                        GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                        $"InventoryManager.ReplaceState: negative hero amount. itemId={heroSnapshot.ItemId}, amount={heroSnapshot.Amount}");
                }

                if (heroSnapshot.Amount == 0
                    && heroSnapshot.ItemLevel == 0
                    && heroSnapshot.Equips.Count == 0)
                    continue;

                var createHero = AbilityItemFactory.CreateHero(heroSnapshot.ItemId, heroSnapshot.ItemLevel);
                if (createHero.IsFailure)
                    return GameResult<InventoryStorage>.Failure(createHero.Error!);

                createHero.Value.AddAmount(heroSnapshot.Amount);
                nextStorage.AddHero(heroSnapshot.ItemId, createHero.Value);
            }

            foreach (var kv in snapshot.Heroes)
            {
                var heroSnapshot = kv.Value;
                if (heroSnapshot == null)
                    continue;

                if (heroSnapshot.Amount == 0
                    && heroSnapshot.ItemLevel == 0
                    && heroSnapshot.Equips.Count == 0)
                    continue;

                foreach (var equip in heroSnapshot.Equips)
                {
                    if (equip.Key == EQUIP_SLOT_TYPE.NONE || string.IsNullOrWhiteSpace(equip.Value))
                    {
                        return GameResult<InventoryStorage>.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                            $"InventoryManager.ReplaceState: invalid hero equip mapping. itemId={heroSnapshot.ItemId}, slot={equip.Key}, equipUid={equip.Value}");
                    }

                    if (!nextStorage.Equip(heroSnapshot.ItemId, equip.Key, equip.Value))
                    {
                        return GameResult<InventoryStorage>.Failure(
                            GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                            $"InventoryManager.ReplaceState: failed to restore hero equip. itemId={heroSnapshot.ItemId}, slot={equip.Key}, equipUid={equip.Value}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(snapshot.SelectedHeroId)
                && nextStorage.GetHero(snapshot.SelectedHeroId) != null)
            {
                nextStorage.SelectedHeroId = snapshot.SelectedHeroId;
            }

            foreach (var kv in snapshot.Rentals)
            {
                if (!string.IsNullOrWhiteSpace(kv.Key))
                    nextStorage.SetRental(kv.Key, kv.Value);
            }

            foreach (var kv in snapshot.Passes)
            {
                if (!string.IsNullOrWhiteSpace(kv.Key))
                    nextStorage.SetPass(kv.Key, kv.Value);
            }

            foreach (var kv in snapshot.TreasureCounts)
            {
                if (kv.Key == ITEM_GRADE_TYPE.NONE)
                    continue;

                if (kv.Value <= 0)
                    continue;

                nextStorage.SetTreasureCount(kv.Key, kv.Value);
            }

            nextStorage.SetTreasureCurrentState(snapshot.TreasureCurrentLevel, snapshot.TreasureCurrentExp);
            nextStorage.LastStaminaUpdateUtcMs = snapshot.LastStaminaUpdateUtcMs;
            return GameResult<InventoryStorage>.Success(nextStorage);
        }

        static GameResult<EQUIP_SLOT> resolveEquipSlotRule(AbilityItemEquip equip)
        {
            if (equip == null)
            {
                return GameResult<EQUIP_SLOT>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    "InventoryManager.resolveEquipSlotRule: equip is null.");
            }

            var rule = AbilityEquipSlotPolicy.GetRule(equip.EquipType);
            if (rule == null)
            {
                return GameResult<EQUIP_SLOT>.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.resolveEquipSlotRule: EQUIP_SLOT not found. equipType={equip.EquipType}, itemId={equip.ItemId}, itemUid={equip.ItemUid}");
            }

            return GameResult<EQUIP_SLOT>.Success(rule);
        }

        string normalizeSelectedHeroId(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                return string.Empty;

            return _storage.GetHero(heroId) != null ? heroId : string.Empty;
        }

        void notifyCurrencyChanged(CURRENCY_TYPE currencyType, long delta)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.CURRENCY_CHANGED,
                currencyType,
                delta,
                _storage.GetCurrencyAmount(currencyType));
        }

        void notifyEquipChanged(string itemUid, string itemId, AbilityItemEquip runtime)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_EQUIP_CHANGED,
                itemUid,
                itemId,
                runtime);
        }

        void notifyEquipListChanged(INVENTORY_LIST_CHANGE_TYPE action, string itemUid, string itemId, AbilityItemEquip runtimeOrNull)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_EQUIP_LIST_CHANGED,
                action,
                itemUid,
                itemId,
                runtimeOrNull);
        }

        void notifyCardChanged(string itemId, AbilityItemCard runtime)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_CARD_CHANGED,
                itemId,
                runtime);
        }

        void notifyCardListChanged(INVENTORY_LIST_CHANGE_TYPE action, string itemId, AbilityItemCard runtimeOrNull)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_CARD_LIST_CHANGED,
                action,
                itemId,
                runtimeOrNull);
        }

        void notifyMaterialChanged(string itemId, AbilityItemMaterial runtime)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_MATERIAL_CHANGED,
                itemId,
                runtime);
        }

        void notifyMaterialListChanged(INVENTORY_LIST_CHANGE_TYPE action, string itemId, AbilityItemMaterial runtimeOrNull)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_MATERIAL_LIST_CHANGED,
                action,
                itemId,
                runtimeOrNull);
        }

        void notifyHeroChanged(string itemId, AbilityItemHero runtime)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_HERO_CHANGED,
                itemId,
                runtime);
        }

        void notifyHeroListChanged(INVENTORY_LIST_CHANGE_TYPE action, string itemId, AbilityItemHero runtimeOrNull)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_HERO_LIST_CHANGED,
                action,
                itemId,
                runtimeOrNull);
        }

        void notifyRentalChanged(string itemId, long expiresAtClientUtcMs, bool active)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.RENTAL_CHANGED,
                itemId,
                expiresAtClientUtcMs,
                active);
        }

        void notifyPassOwnershipChanged(string itemId, bool owned)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.PASS_OWNERSHIP_CHANGED,
                itemId,
                owned);
        }

        void notifyTreasureStateChanged(ITEM_GRADE_TYPE gradeType, int deltaCount)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.TREASURE_STATE_CHANGED,
                gradeType,
                deltaCount,
                _storage.GetTreasureCount(gradeType),
                _storage.TreasureCurrent.Level,
                _storage.TreasureCurrent.Exp);
        }

        void notifySnapshotChanged(INVENTORY_SNAPSHOT_CHANGE_REASON reason)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.INVENTORY_SNAPSHOT_CHANGED,
                reason);
        }

        void refreshEquipViews()
        {
            _equippedItems.Clear();
            _unequippedItems.Clear();
            _unownedEquipItems.Clear();
            var ownedEquipItemIds = new HashSet<string>();

            foreach (var equip in _storage.Equipments.Values)
            {
                if (equip == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(equip.ItemId))
                    ownedEquipItemIds.Add(equip.ItemId);

                if (equip.IsEquipped)
                    _equippedItems.Add(equip);
                else
                    _unequippedItems.Add(equip);
            }

            foreach (var table in TB_ITEM_EQUIP.GetAll())
            {
                if (table == null)
                    continue;

                if (ownedEquipItemIds.Contains(table.item_id))
                    continue;

                _unownedEquipItems.Add(table);
            }
        }
    }
}
