# 30-recovery-platform


iOS/Android 플랫폼별 네이티브 기능을 정의한다.

- **Import**: Custom File Type Association으로 `.dvn` 파일을 게임에 연결하고, 네이티브 → Unity 파일 경로를 전달한다.
- **Export**: OS 공유 시트(Share Sheet)를 호출하여 `.dvn` 파일을 이메일 등으로 공유한다.


---


## iOS Setup


### Info.plist — CFBundleDocumentTypes

```xml
<key>CFBundleDocumentTypes</key>
<array>
  <dict>
    <key>CFBundleTypeName</key>
    <string>Devian Save Data</string>
    <key>CFBundleTypeRole</key>
    <string>Editor</string>
    <key>LSHandlerRank</key>
    <string>Owner</string>
    <key>LSItemContentTypes</key>
    <array>
      <string>com.devian.savedata</string>
    </array>
  </dict>
</array>
```


### Info.plist — UTExportedTypeDeclarations

```xml
<key>UTExportedTypeDeclarations</key>
<array>
  <dict>
    <key>UTTypeConformsTo</key>
    <array>
      <string>public.data</string>
    </array>
    <key>UTTypeDescription</key>
    <string>Devian Save Data</string>
    <key>UTTypeIdentifier</key>
    <string>com.devian.savedata</string>
    <key>UTTypeTagSpecification</key>
    <dict>
      <key>public.filename-extension</key>
      <array>
        <string>dvn</string>
      </array>
    </dict>
  </dict>
</array>
```


### iOS Native Receiver

`UnityAppController+Recovery.mm` (또는 기존 UnityAppController 수정):

```
application:openURL:options: 에서 URL.pathExtension == "dvn" 일 때
→ 파일을 Application.temporaryCachePath로 복사
→ UnitySendMessage("RecoveryManager", "OnRecoveryFileReceived", filePath)
```

- 게임이 이미 실행 중이면 `application:openURL:` 호출.
- 게임이 미실행이면 `application:didFinishLaunchingWithOptions:` → `openURL:` 순서.
- 두 경우 모두 동일한 경로로 처리한다.


---


## Android Setup


### AndroidManifest.xml — intent-filter

메인 Activity에 추가:

```xml
<intent-filter>
  <action android:name="android.intent.action.VIEW" />
  <category android:name="android.intent.category.DEFAULT" />
  <category android:name="android.intent.category.BROWSABLE" />
  <data
    android:scheme="content"
    android:mimeType="*/*"
    android:pathPattern=".*\\.dvn" />
</intent-filter>

<intent-filter>
  <action android:name="android.intent.action.VIEW" />
  <category android:name="android.intent.category.DEFAULT" />
  <category android:name="android.intent.category.BROWSABLE" />
  <data
    android:scheme="file"
    android:mimeType="*/*"
    android:pathPattern=".*\\.dvn" />
</intent-filter>
```


### Android Native Receiver

`UnityPlayerActivity` (또는 커스텀 Activity) 수정:

```
onNewIntent(Intent intent) 에서 intent.getData() 확인
→ URI에서 파일 읽기 (ContentResolver로 InputStream 획득)
→ Application.temporaryCachePath로 복사
→ UnitySendMessage("RecoveryManager", "OnRecoveryFileReceived", filePath)
```

- `content://` URI는 직접 파일 경로 접근이 불가하므로, `ContentResolver.openInputStream()`으로 읽어야 한다.
- 파일을 임시 경로에 복사한 후 Unity에 경로를 전달한다.


---


## Unity Receiver (C#)

RecoveryManager에 네이티브 콜백 수신 메서드:

```csharp
// UnitySendMessage로 호출됨 (네이티브 → C#)
public void OnRecoveryFileReceived(string filePath)
{
    // ImportDvnAsync(filePath, ct) 호출
}
```

- `UnitySendMessage`는 메인 스레드에서 호출된다.
- async 호출이 필요하므로 내부에서 Task를 시작한다.


---


## Export — DevianShare (OS 공유 시트)

RecoveryManager.ExportDvnAsync에서 .dvn 파일을 OS 공유 시트로 공유한다.


### iOS — UIActivityViewController

```objective-c
extern "C" void DevianShare_ShareFile(const char* filePath, const char* subject)
```

- `UIActivityViewController`에 `[NSURL fileURLWithPath:]`를 전달한다.
- iPad 대응: `popoverPresentationController`에 sourceView/sourceRect를 설정한다.
- 구현 파일: `DevianShare.mm`


### Android — Intent.ACTION_SEND

```java
public static void shareFile(Activity activity, String filePath, String subject)
```

- `FileProvider.getUriForFile()`로 content URI를 생성한다.
- `Intent.ACTION_SEND` + `Intent.createChooser()`로 공유 시트를 연다.
- `FLAG_GRANT_READ_URI_PERMISSION`으로 수신 앱에 읽기 권한을 부여한다.
- 구현 파일: `DevianShare.java`
- FileProvider 설정: `DevianShare.androidlib` (아래 Unity Build Integration 참조)


### C# Bridge

```csharp
// iOS
[DllImport("__Internal")]
private static extern void DevianShare_ShareFile(string filePath, string subject);

// Android
using var cls = new AndroidJavaClass("com.devian.share.DevianShare");
cls.CallStatic("shareFile", activity, filePath, subject);
```


---


## Export — DevianShare (이메일 전용)

RecoveryManager.ExportDvnViaEmailAsync에서 .dvn 파일을 이메일로 공유한다.
수신자 이메일 주소가 프리셋된 상태로 이메일 앱이 열린다.


### iOS — MFMailComposeViewController

```objective-c
extern "C" void DevianShare_SendEmail(const char* filePath, const char* recipient, const char* subject)
```

- `[MFMailComposeViewController canSendMail]`로 이메일 가능 여부를 확인한다.
- `setToRecipients:`로 수신자를 프리셋한다.
- `addAttachmentData:mimeType:fileName:`으로 .dvn 파일을 첨부한다.
- `MFMailComposeViewControllerDelegate`로 화면 dismiss를 처리한다.
- 빌드 요구사항: `MessageUI.framework` 링크 필요
- 구현 파일: `DevianShare.mm`


### Android — Intent.ACTION_SEND (message/rfc822)

```java
public static void sendEmail(Activity activity, String filePath, String recipient, String subject)
```

- `setType("message/rfc822")`로 이메일 앱만 필터한다.
- `Intent.EXTRA_EMAIL`로 수신자를 프리셋한다.
- 나머지 (FileProvider URI, EXTRA_STREAM, EXTRA_SUBJECT)는 shareFile과 동일하다.
- 구현 파일: `DevianShare.java`


### C# Bridge

```csharp
// iOS
[DllImport("__Internal")]
private static extern void DevianShare_SendEmail(string filePath, string recipient, string subject);

// Android
using var cls = new AndroidJavaClass("com.devian.share.DevianShare");
cls.CallStatic("sendEmail", activity, filePath, recipient, subject);
```


---


## Unity Build Integration

iOS와 Android 네이티브 설정은 Unity 빌드 프로세스에 통합해야 한다.

| 플랫폼 | 방식 | 비고 |
|--------|------|------|
| iOS | `PostProcessBuild` 또는 수동 Info.plist 수정 | Xcode 프로젝트에 자동 반영 권장 |
| Android | `DevianShare.androidlib` (`Assets/Plugins/Android/`) | FileProvider 선언 + 파일 경로 설정 포함 |

- Android의 `DevianShare.androidlib`는 `project.properties` + `AndroidManifest.xml` + `res/xml/devian_file_paths.xml`로 구성된다.
- `Assets/Plugins/Android/res/`는 Unity에서 지원 중단되었으므로 `.androidlib` 형식을 사용한다.
- 상세 설정은 `Plugins/Android/Recovery/SETUP.txt` 참조.


---


## Implementation Location (3-path mirror)

> 3-path mirror 정책: [devian-unity/07-samples-creation-guide](../../../07-samples-creation-guide/SKILL.md), [devian-unity/03-ssot](../../../03-ssot/SKILL.md) §UPM Packages Sync

- iOS Native Plugin:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Plugins/iOS/Recovery/`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Plugins/iOS/Recovery/`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Plugins/iOS/Recovery/`

- Android Native Plugin:
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Plugins/Android/Recovery/`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Plugins/Android/Recovery/`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Plugins/Android/Recovery/`

- DevianShare (iOS):
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Plugins/iOS/Recovery/DevianShare.mm`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Plugins/iOS/Recovery/DevianShare.mm`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Plugins/iOS/Recovery/DevianShare.mm`

- DevianShare (Android):
  - UPM (정본): `framework-cs/upm/com.devian.samples/Samples~/MobileSystem/Plugins/Android/Recovery/com/devian/share/DevianShare.java`
  - Packages (sync): `framework-cs/apps/UnityExample/Packages/com.devian.samples/Samples~/MobileSystem/Plugins/Android/Recovery/com/devian/share/DevianShare.java`
  - Assets/Samples (import): `framework-cs/apps/UnityExample/Assets/Samples/Devian Samples/{version}/MobileSystem/Plugins/Android/Recovery/com/devian/share/DevianShare.java`

- DevianShare.androidlib (프로젝트 레벨, 3-path mirror 아님):
  - `framework-cs/apps/UnityExample/Assets/Plugins/Android/DevianShare.androidlib/`
  - 설정 문서: `Plugins/Android/Recovery/SETUP.txt` (3-path mirror)

asmdef:
- `Devian.Samples.MobileSystem.asmdef` (C# 수신부)
- 네이티브 플러그인은 asmdef 밖에 위치


---


## Related

- [03-ssot](../03-ssot/SKILL.md) §C — File Identity (UTI, pathPattern)
- [10-recovery-manager](../10-recovery-manager/SKILL.md) — RecoveryManager (Export/Import 호출)
