# 타일 어트리뷰트 시각화 계획

타일에 속성이 부여되었을 때 3D 오브젝트나 지속 VFX를 타일 위에 표시하기 위한 설계.

---

## 핵심 아이디어

`TileAttribute` 자신이 시각 오브젝트를 소유하고 생명주기를 관리한다.  
별도 VisualManager 없이 Attach/Detach 훅으로 처리.

---

## 구현 계획

### 1. TileAttribute에 훅 추가

```csharp
// TileAttribute.cs
public virtual void OnAttached(TileData tile) { }
public virtual void OnDetached()              { }
```

### 2. TileData에서 훅 호출

```csharp
// TileData.AddAttribute()
attribue.SetOwner(this);
attributes.Add(attribue.Type, attribue);
attribue.OnAttached(this);              // 추가

// TileData.RemoveAttribute()
attribute.OnDetached();                 // 추가 (SetOwner(null) 전에)
attribute.SetOwner(null);
```

### 3. 서브클래스 구현 패턴

```csharp
public class TurretTileAttribute : TileAttribute
{
    private GameObject _visual;

    public override void OnAttached(TileData tile)
    {
        var prefab = Resources.Load<GameObject>("Tiles/Visuals/Turret");
        _visual = Object.Instantiate(prefab, tile.transform);
        _visual.transform.localPosition = Vector3.up * 0.5f;
    }

    public override void OnDetached()
    {
        if (_visual != null) Object.Destroy(_visual);
    }
}

// 연막 (지속 파티클)
public class SmokeScreenTileAttribute : TileAttribute
{
    private GameObject _vfx;

    public override void OnAttached(TileData tile)
    {
        var prefab = Resources.Load<GameObject>("Tiles/VFX/SmokeScreen");
        _vfx = Object.Instantiate(prefab, tile.transform);
    }

    public override void OnDetached()
    {
        if (_vfx != null) Object.Destroy(_vfx);
    }
}
```

---

## 프리팹 위치 규칙

| 종류 | 경로 |
|---|---|
| 3D 오브젝트 (포탑 등) | `Resources/Tiles/Visuals/` |
| 지속 VFX (파티클) | `Resources/Tiles/VFX/` |

---

## 관련 파일

- `TileAttribute.cs` — 베이스 클래스 (훅 추가 대상)
- `TileData.cs` — AddAttribute / RemoveAttribute (훅 호출 대상)
- [vfx_system_design.md](vfx_system_design.md) — 전투 VFX 전반 설계
