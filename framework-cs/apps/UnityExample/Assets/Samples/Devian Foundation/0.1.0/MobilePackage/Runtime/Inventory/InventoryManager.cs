using System;
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

        public InventoryStorage Storage => _storage;
        public int MaxStamina => _staminaController.MaxStamina;

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
                    RevokeCurrency(CURRENCY_TYPE.JEWEL_FREE, useFree);

                if (usePaid > 0L)
                    RevokeCurrency(CURRENCY_TYPE.JEWEL_PAID, usePaid);

                receipt = new CurrencySpendReceipt(CURRENCY_TYPE.JEWEL, useFree, usePaid, 0L);
                return true;
            }

            if (!HasSufficientCurrency(currencyType, amount))
                return false;

            RevokeCurrency(currencyType, amount);
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

        public void ClearState(INVENTORY_SNAPSHOT_CHANGE_REASON reason)
        {
            _storage.Clear();
            notifySnapshotChanged(reason);
        }

        public void ReplaceState(InventoryStorage snapshot, INVENTORY_SNAPSHOT_CHANGE_REASON reason)
        {
            _storage.CopyFrom(snapshot);
            notifySnapshotChanged(reason);
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

            if (_storage.Wallet.TryAdd(currencyType, amount))
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
                    return GameResult.Failure(create.Error!);

                var equip = create.Value;
                _storage.AddEquip(itemUid, equip);
                notifyEquipChanged(itemUid, itemId, equip, 1);
            }

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

            if (amount == 0)
                return GameResult.Ok();

            var existing = _storage.GetCard(itemId);
            if (existing != null)
            {
                existing.AddAmount(amount);
                notifyCardChanged(itemId, existing, amount);
            }
            else
            {
                var create = AbilityItemFactory.CreateCard(itemId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddCard(itemId, ability);
                ability.AddAmount(amount);
                notifyCardChanged(itemId, ability, amount);
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

            if (amount == 0)
                return GameResult.Ok();

            var existing = _storage.GetMaterial(itemId);
            if (existing != null)
            {
                existing.AddAmount(amount);
                notifyMaterialChanged(itemId, existing, amount);
            }
            else
            {
                var create = AbilityItemFactory.CreateMaterial(itemId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddMaterial(itemId, ability);
                ability.AddAmount(amount);
                notifyMaterialChanged(itemId, ability, amount);
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

            if (amount == 0)
                return GameResult.Ok();

            var existing = _storage.GetHero(heroId);
            if (existing != null)
            {
                existing.AddAmount(amount);
                notifyHeroChanged(heroId, existing, amount);
            }
            else
            {
                var create = AbilityItemFactory.CreateHero(heroId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddHero(heroId, ability);
                ability.AddAmount(amount);
                notifyHeroChanged(heroId, ability, amount);
            }

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

        public GameResult ApplyTreasure(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            if (gradeType == TREASURE_GRADE_TYPE.NONE)
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

        public void RevokeCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            if (amount <= 0L)
                return;

            if (_storage.Wallet.TryAdd(currencyType, -amount))
                notifyCurrencyChanged(currencyType, -amount);
        }

        public void RevokeEquip(string itemId, int amount)
        {
            if (amount <= 0)
                return;

            var equips = _storage.GetEquipsByItemId(itemId);
            for (var j = 0; j < amount && j < equips.Count; j++)
            {
                if (equips[j] != null && !string.IsNullOrEmpty(equips[j].ItemUid))
                {
                    var itemUid = equips[j].ItemUid;
                    _storage.RemoveEquip(itemUid);
                    notifyEquipChanged(itemUid, itemId, null, -1);
                }
            }
        }

        public void RevokeCard(string itemId, int amount)
        {
            if (amount <= 0)
                return;

            var card = _storage.GetCard(itemId);
            if (card != null)
            {
                card.AddAmount(-amount);
                notifyCardChanged(itemId, card, -amount);
            }
        }

        public void RevokeMaterial(string itemId, int amount)
        {
            if (amount <= 0)
                return;

            var material = _storage.GetMaterial(itemId);
            if (material != null)
            {
                material.AddAmount(-amount);
                notifyMaterialChanged(itemId, material, -amount);
            }
        }

        public void RevokeHero(string heroId, int amount)
        {
            if (amount <= 0)
                return;

            var hero = _storage.GetHero(heroId);
            if (hero != null)
            {
                hero.AddAmount(-amount);
                notifyHeroChanged(heroId, hero, -amount);
            }
        }

        public void RevokeRental(string itemId)
        {
            var currentExpiryUtcMs = _storage.GetRentalExpiry(itemId);
            _storage.RemoveRental(itemId);
            if (currentExpiryUtcMs > 0L)
                notifyRentalChanged(itemId, 0L, false);
        }

        public void RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            if (amount <= 0)
                return;

            var current = _storage.GetTreasureCount(gradeType);
            _storage.SetTreasureCount(gradeType, current - amount);
            var next = _storage.GetTreasureCount(gradeType);
            var delta = next - current;
            if (delta != 0)
                notifyTreasureStateChanged(gradeType, delta);
        }

        // ── Query API ──

        public long GetCurrencyAmount(CURRENCY_TYPE currencyType)
        {
            return _storage.Wallet.Get(currencyType);
        }

        public int GetEquipCount(string itemId)
        {
            return _storage.GetEquipsByItemId(itemId).Count;
        }

        public long GetCardAmount(string itemId)
        {
            var card = _storage.GetCard(itemId);
            return card != null ? card.Amount : 0L;
        }

        public long GetMaterialAmount(string itemId)
        {
            var material = _storage.GetMaterial(itemId);
            return material != null ? material.Amount : 0L;
        }

        public long GetHeroAmount(string heroId)
        {
            var hero = _storage.GetHero(heroId);
            return hero != null ? hero.Amount : 0L;
        }

        public bool HasActiveRental(string itemId)
        {
            return _storage.HasActiveRental(itemId);
        }

        public bool HasPass(string itemId)
        {
            return _storage.HasPass(itemId);
        }

        public int GetTreasureCount(TREASURE_GRADE_TYPE gradeType)
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
            notifyTreasureStateChanged(TREASURE_GRADE_TYPE.NONE, 0);
        }

        // ── Stamina ──

        public void LoadSettings() => _staminaController.LoadSettings();

        public void RecoverStamina()
        {
            var recovered = _staminaController.RecoverStamina(_storage);
            if (recovered > 0L)
                notifyCurrencyChanged(CURRENCY_TYPE.STAMINA, recovered);
        }

        void notifyCurrencyChanged(CURRENCY_TYPE currencyType, long delta)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.CURRENCY_CHANGED,
                currencyType,
                delta,
                _storage.Wallet.Get(currencyType));
        }

        void notifyEquipChanged(string itemUid, string itemId, AbilityItemEquip runtimeOrNull, int delta)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_EQUIP_CHANGED,
                itemUid,
                itemId,
                runtimeOrNull,
                delta);
        }

        void notifyCardChanged(string itemId, AbilityItemCard runtimeOrNull, int delta)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_CARD_CHANGED,
                itemId,
                runtimeOrNull,
                delta);
        }

        void notifyMaterialChanged(string itemId, AbilityItemMaterial runtimeOrNull, int delta)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_MATERIAL_CHANGED,
                itemId,
                runtimeOrNull,
                delta);
        }

        void notifyHeroChanged(string itemId, AbilityItemHero runtimeOrNull, int delta)
        {
            _messageTrigger.Notify(
                INVENTORY_MESSAGE_TYPE.ITEM_HERO_CHANGED,
                itemId,
                runtimeOrNull,
                delta);
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

        void notifyTreasureStateChanged(TREASURE_GRADE_TYPE gradeType, int deltaCount)
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
    }
}
