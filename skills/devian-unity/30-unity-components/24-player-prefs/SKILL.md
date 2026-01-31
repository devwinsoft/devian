# PlayerPrefs Wrapper (Devian)

## 0. 목적
Unity PlayerPrefs를 실전적으로 안전/일관되게 사용하기 위해,
- 키 오타/난립을 줄이고
- 기본값/자동 초기화를 통일하며
- class(복합 설정) 저장을 JSON(1-key)로 표준화한다.

본 스킬은 Devian Foundation Runtime에서 제공하는 Prefs 래퍼의 정본이다.

---

## 1. 포함 범위
- Primitive: IntPrefs / FloatPrefs / BoolPrefs / StringPrefs
- Enum(string): EnumPrefs<T>
- Class(JSON 1-key): JsonPrefs<T>

---

## 2. Key 네이밍 규약 (Hard)
- 키는 prefix를 포함한 dot-style을 사용한다.
  - 예: `devian.game.audio.bgm`
- **prefix의 SSOT는 `DevianSettings.asset`의 `playerPrefsPrefix`다.**
- 코드에서 prefix 상수를 쓰는 것은 허용하되, 그 값은 DevianSettings의 값과 동일해야 한다(불일치 금지).
  - 예: `const string Prefix = "devian.game."; // SSOT: DevianSettings.playerPrefsPrefix`

---

## 3. 기본값 및 자동 초기화 (Hard)
- 모든 Prefs 래퍼는 `HasKey(key)==false`일 때:
  - defaultValue를 즉시 SetXXX로 기록한 뒤
  - defaultValue를 반환한다.
- Delete는 `PlayerPrefs.DeleteKey(key)`만 수행한다.

---

## 4. Save 정책 (Hard)
- Prefs 래퍼의 setter는 Save()를 자동 호출하지 않는다.
- 앱 종료/백그라운드 진입 시점에서 호출한다:
  - `OnApplicationPause(true)`
  - `OnApplicationQuit()`

예외:
- JsonPrefs.Edit(..., saveNow:true)로 즉시 저장이 필요한 설정만 선택적으로 Save 가능.

---

## 5. Enum 저장 규약 (Hard)
- Enum은 반드시 문자열로 저장한다.
  - `PlayerPrefs.SetString(key, enumValue.ToString())`
- enum에 새 값이 중간 삽입/리오더 될 수 있으므로 int 저장 방식은 금지한다.

---

## 6. Class(JSON) 저장 규약 (Hard)
- JsonPrefs<T>는 1개의 key에 JSON 문자열로 저장한다.
- Load는 default 인스턴스를 만든 후 `FromJsonOverwrite`로 덮어쓴다.
  - 목적: 필드 추가 시 default 유지(부분 overwrite)
- JSON 파싱 실패 시 default로 복구하여 저장한다.

제한:
- Unity JsonUtility 제한을 따른다 (Dictionary 직렬화 불가 등).

---

## 7. 사용 예시 (짧게)

```csharp
using System;
using UnityEngine;

namespace Devian
{
    public enum QualityPreset { Low, Medium, High }

    [Serializable]
    public class UserSettings
    {
        public int version = 1;
        public float bgmVolume = 1f;
        public bool haptic = true;
        public QualityPreset quality = QualityPreset.Medium;
    }

    public static class GamePrefs
    {
        // SSOT: DevianSettings.playerPrefsPrefix
        private const string Prefix = "devian.game.";

        public static readonly FloatPrefs BgmVolume = new(Prefix + "audio.bgm", 1f);
        public static readonly BoolPrefs Haptic = new(Prefix + "haptic.enabled", true);
        public static readonly EnumPrefs<QualityPreset> Quality = new(Prefix + "gfx.quality", QualityPreset.Medium);

        public static readonly JsonPrefs<UserSettings> Settings = new(Prefix + "settings", new UserSettings());
    }

    public sealed class PrefsExample : MonoBehaviour
    {
        private void Start()
        {
            GamePrefs.BgmVolume.Value = 0.7f;
            GamePrefs.Haptic.Value = false;
            GamePrefs.Quality.Value = QualityPreset.High;

            GamePrefs.Settings.Edit(s =>
            {
                s.version = 2;
                s.bgmVolume = GamePrefs.BgmVolume.Value;
                s.haptic = GamePrefs.Haptic.Value;
                s.quality = GamePrefs.Quality.Value;
            }, saveNow: true);
        }
    }
}
```

---

## 8. 파일 구조

```
Runtime/Unity/Prefs/
  PrefsValue.cs       # 추상 베이스 클래스
  IntPrefs.cs         # int 래퍼
  FloatPrefs.cs       # float 래퍼
  BoolPrefs.cs        # bool 래퍼 (int 0/1 저장)
  StringPrefs.cs      # string 래퍼
  EnumPrefs.cs        # enum 래퍼 (string 저장)
  JsonPrefs.cs        # class JSON 래퍼
```

---

## 9. API 요약

### PrefsValue<T> (추상 베이스)
```csharp
public abstract class PrefsValue<T>
{
    public bool HasKey { get; }
    public abstract T Value { get; set; }
    public void Delete();
}
```

### IntPrefs / FloatPrefs / BoolPrefs / StringPrefs
```csharp
public sealed class IntPrefs : PrefsValue<int>
{
    public IntPrefs(string key, int defaultValue);
}
// FloatPrefs, BoolPrefs, StringPrefs 동일 패턴
```

### EnumPrefs<T>
```csharp
public sealed class EnumPrefs<T> : PrefsValue<T> where T : struct, Enum
{
    public EnumPrefs(string key, T defaultValue);
}
```

### JsonPrefs<T>
```csharp
public sealed class JsonPrefs<T> where T : class, new()
{
    public JsonPrefs(string key, T defaultValue = null);
    public T Value { get; }
    public void Edit(Action<T> mutator, bool saveNow = true);
    public void Save(bool saveNow = false);
    public void Delete();
}
```
