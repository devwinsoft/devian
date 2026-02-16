#import <Foundation/Foundation.h>
#import <Security/Security.h>

/**
 * iOS Keychain-based device-bound key store.
 * Stores 48-byte DEK (key 32B + iv 16B) with kSecAttrAccessibleWhenUnlockedThisDeviceOnly.
 * "ThisDeviceOnly" ensures the item is NOT included in device backups/transfers.
 *
 * IMPORTANT: This is a minimal implementation stub.
 * Production use requires proper error handling and testing.
 */

static NSString *const kServiceName = @"com.devian.crypto.LocalDEK";
static NSString *const kAccountName = @"DevianLocalDEK";

static NSData* _Nullable DevianKeychain_Load(void) {
    NSDictionary *query = @{
        (__bridge id)kSecClass:            (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService:      kServiceName,
        (__bridge id)kSecAttrAccount:      kAccountName,
        (__bridge id)kSecReturnData:       @YES,
        (__bridge id)kSecMatchLimit:       (__bridge id)kSecMatchLimitOne,
    };

    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);

    if (status == errSecSuccess && result != NULL) {
        return (__bridge_transfer NSData *)result;
    }
    return nil;
}

static BOOL DevianKeychain_Store(NSData *data) {
    // Delete existing item first (idempotent)
    NSDictionary *deleteQuery = @{
        (__bridge id)kSecClass:       (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kServiceName,
        (__bridge id)kSecAttrAccount: kAccountName,
    };
    SecItemDelete((__bridge CFDictionaryRef)deleteQuery);

    NSDictionary *addQuery = @{
        (__bridge id)kSecClass:            (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService:      kServiceName,
        (__bridge id)kSecAttrAccount:      kAccountName,
        (__bridge id)kSecValueData:        data,
        (__bridge id)kSecAttrAccessible:   (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly,
    };

    OSStatus status = SecItemAdd((__bridge CFDictionaryRef)addQuery, NULL);
    return (status == errSecSuccess);
}

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Returns 48 if successful, 0 on failure.
 * Caller must call DevianKeychain_Free on the returned pointer.
 */
int DevianKeychain_GetOrCreateSecret48(void **outPtr) {
    *outPtr = NULL;

    NSData *existing = DevianKeychain_Load();
    if (existing != nil && existing.length == 48) {
        void *buf = malloc(48);
        memcpy(buf, existing.bytes, 48);
        *outPtr = buf;
        return 48;
    }

    // Generate new 48-byte secret
    NSMutableData *secret = [NSMutableData dataWithLength:48];
    int result = SecRandomCopyBytes(kSecRandomDefault, 48, secret.mutableBytes);
    if (result != errSecSuccess) {
        return 0;
    }

    if (!DevianKeychain_Store(secret)) {
        return 0;
    }

    void *buf = malloc(48);
    memcpy(buf, secret.bytes, 48);
    *outPtr = buf;
    return 48;
}

void DevianKeychain_Free(void *ptr) {
    if (ptr != NULL) {
        free(ptr);
    }
}

#ifdef __cplusplus
}
#endif
