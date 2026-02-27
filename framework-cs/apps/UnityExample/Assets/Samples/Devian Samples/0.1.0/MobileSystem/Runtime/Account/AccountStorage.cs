using System;

namespace Devian
{
    [Serializable]
    public sealed class AccountStorage
    {
        public LoginType loginType;
        public string socialUserId;
        public long lastUpdatedAtUtcMs;

        public AccountStorage(LoginType loginType = LoginType.NONE, string socialUserId = null, long lastUpdatedAtUtcMs = 0L)
        {
            this.loginType = loginType;
            this.socialUserId = socialUserId;
            this.lastUpdatedAtUtcMs = lastUpdatedAtUtcMs;
        }

        public void Clear()
        {
            loginType = LoginType.NONE;
            socialUserId = null;
            lastUpdatedAtUtcMs = 0L;
        }

        public void Set(LoginType loginType, string socialUserId, long lastUpdatedAtUtcMs)
        {
            this.loginType = loginType;
            this.socialUserId = string.IsNullOrWhiteSpace(socialUserId) ? null : socialUserId;
            this.lastUpdatedAtUtcMs = lastUpdatedAtUtcMs;
        }

        public AccountStorage Clone()
        {
            return new AccountStorage(loginType, socialUserId, lastUpdatedAtUtcMs);
        }
    }
}
