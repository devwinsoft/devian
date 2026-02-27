using Newtonsoft.Json.Linq;

namespace Devian
{
    internal static class GameStorageJsonCodecAccount
    {
        public static JObject Serialize(AccountStorage account)
        {
            if (account == null)
                account = new AccountStorage();
            return new JObject
            {
                ["loginType"] = (int)account.loginType,
                ["socialUserId"] = account.socialUserId,
                ["lastUpdatedAtUtcMs"] = account.lastUpdatedAtUtcMs,
            };
        }

        public static void DeserializeInto(JObject accountObj, AccountStorage account)
        {
            if (account == null)
                return;

            if (accountObj == null)
            {
                account.Clear();
                return;
            }

            var rawLoginType = accountObj.Value<int?>("loginType") ?? (int)LoginType.NONE;
            account.loginType = System.Enum.IsDefined(typeof(LoginType), rawLoginType)
                ? (LoginType)rawLoginType
                : LoginType.NONE;
            account.socialUserId = accountObj.Value<string>("socialUserId");
            account.lastUpdatedAtUtcMs = accountObj.Value<long?>("lastUpdatedAtUtcMs") ?? 0L;
        }
    }
}
