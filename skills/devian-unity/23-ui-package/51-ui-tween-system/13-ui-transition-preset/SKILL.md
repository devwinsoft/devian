# 13-ui-transition-preset

Status: ACTIVE
AppliesTo: v1
Type: Data Specification

## Purpose

`UITransitionPreset`은 UI 연출 데이터를 정의한다.
toast show/hide, panel show/hide, item add reaction 같은 전이를 preset으로 표현한다.

## Target Code Path

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITransitionPreset.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UITransitionPresetAsset.cs
```

```text
framework-cs/upm/com.devian.foundation/Samples~/UIPackage/Runtime/Tween/UI_TRANSITION_PRESET_ID.cs
```

## Data Shape

```csharp
[Serializable]
public sealed class UITransitionPreset
{
    public float Duration;
    public float Delay;
    public UITweenEase Ease;

    public bool UseAlpha;
    public float FromAlpha;
    public float ToAlpha;

    public bool UseAnchoredPosition;
    public Vector2 FromAnchoredPosition;
    public Vector2 ToAnchoredPosition;

    public bool UseScale;
    public Vector3 FromScale;
    public Vector3 ToScale;
}
```

## Rules

- `UITransitionPreset`은 순수 `Serializable` data payload다.
- editor authoring과 inspector 선택은 `UITransitionPresetAsset : ScriptableObject`를 사용한다.
- asset이 `AssetId`를 상속하지는 않는다. 기존 패턴대로 asset과 `UI_TRANSITION_PRESET_ID` wrapper를 분리한다.
- runtime field는 `UI_TRANSITION_PRESET_ID`를 우선 사용하고, 필요시 direct asset ref를 보조적으로 허용한다.
- show preset과 hide preset은 별도 preset으로 가진다.
- alpha / anchoredPosition / scale은 independent channel이다.
- 채널별 `Use*` flag가 꺼져 있으면 해당 속성은 변경하지 않는다.
- `UseAnchoredPosition`이 켜진 경우 `FromAnchoredPosition` / `ToAnchoredPosition`은 절대 좌표가 아니라 play group 시작 시점의 현재 `anchoredPosition` 기준 offset으로 해석한다.

## Asset Pattern

권장 구조:

```csharp
[CreateAssetMenu(...)]
public sealed class UITransitionPresetAsset : ScriptableObject
{
    public UITransitionPreset Preset;
}
```

```csharp
[Serializable]
public sealed class UI_TRANSITION_PRESET_ID
{
    public string Value;
}
```

- `UITransitionPresetAsset`은 `CreateAssetMenu`로 authoring asset을 만든다
- runtime lookup은 `AssetManager.GetAsset<UITransitionPresetAsset>(id.Value)` 경로를 사용한다

## Easing

권장 easing:

```csharp
public enum UITweenEase
{
    Linear,
    InQuad,
    OutQuad,
    InOutQuad
}
```
