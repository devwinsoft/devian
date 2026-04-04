using System;
using Devian.Domain.Common;
using Devian.Domain.Game;
using UnityEngine;

namespace Devian
{
    public sealed class InventoryManager : CompoSingleton<InventoryManager>
    {
        const long DefaultRentalDurationMs = 30L * 24L * 60L * 60L * 1000L;

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

        // ── Apply API ──

        public GameResult ApplyCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            if (amount < 0L)
            {
                return GameResult.Failure(
                    GAME_ERROR_TYPE.GAME_INVALID_ARGUMENT,
                    $"InventoryManager.ApplyCurrency: amount is negative: {amount}");
            }

            _storage.Wallet.TryAdd(currencyType, amount);
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

                _storage.AddEquip(itemUid, create.Value);
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
            }
            else
            {
                var create = AbilityItemFactory.CreateCard(itemId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddCard(itemId, ability);
                ability.AddAmount(amount);
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
            }
            else
            {
                var create = AbilityItemFactory.CreateMaterial(itemId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddMaterial(itemId, ability);
                ability.AddAmount(amount);
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
            }
            else
            {
                var create = AbilityItemFactory.CreateHero(heroId);
                if (create.IsFailure)
                    return GameResult.Failure(create.Error!);

                var ability = create.Value;
                _storage.AddHero(heroId, ability);
                ability.AddAmount(amount);
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
            _storage.SetRental(itemId, baseUtcMs + DefaultRentalDurationMs);
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

            _storage.AddTreasure(gradeType, amount);
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
                _messageTrigger.Notify(INVENTORY_MESSAGE_TYPE.PASS_CHANGED, itemId, owned);

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
                _messageTrigger.Notify(INVENTORY_MESSAGE_TYPE.PASS_CHANGED, itemId, false);

            return GameResult.Ok();
        }

        // ── Revoke API ──

        public void RevokeCurrency(CURRENCY_TYPE currencyType, long amount)
        {
            _storage.Wallet.TryAdd(currencyType, -amount);
        }

        public void RevokeEquip(string itemId, int amount)
        {
            var equips = _storage.GetEquipsByItemId(itemId);
            for (var j = 0; j < amount && j < equips.Count; j++)
            {
                if (equips[j] != null && !string.IsNullOrEmpty(equips[j].ItemUid))
                    _storage.RemoveEquip(equips[j].ItemUid);
            }
        }

        public void RevokeCard(string itemId, int amount)
        {
            var card = _storage.GetCard(itemId);
            if (card != null)
                card.AddAmount(-amount);
        }

        public void RevokeMaterial(string itemId, int amount)
        {
            var material = _storage.GetMaterial(itemId);
            if (material != null)
                material.AddAmount(-amount);
        }

        public void RevokeHero(string heroId, int amount)
        {
            var hero = _storage.GetHero(heroId);
            if (hero != null)
                hero.AddAmount(-amount);
        }

        public void RevokeRental(string itemId)
        {
            _storage.RemoveRental(itemId);
        }

        public void RevokeTreasure(TREASURE_GRADE_TYPE gradeType, int amount)
        {
            var current = _storage.GetTreasureCount(gradeType);
            _storage.SetTreasureCount(gradeType, current - amount);
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

        // ── Stamina ──

        public void LoadSettings() => _staminaController.LoadSettings();

        public void RecoverStamina() => _staminaController.RecoverStamina(_storage);
    }
}
