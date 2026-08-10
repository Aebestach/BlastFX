# BlastFX

为 KSP 1.12.5 提供共享的火球爆炸特效与部件摧毁 API，供其他模组直接调用，无需各自再带一套爆炸 shader。

可与 [雷劫 / Thunderbolt](https://github.com/Aebestach/Thunderbolt) 等依赖 BlastFX 的模组一起使用。

## 安装

将 `GameData/BlastFX` 放入游戏的 `GameData`。

需要 **Harmony**（`GameData/000_Harmony`，一般已随 Community Fixes 安装）。

## 难度设置

**替换原版爆炸效果**（默认**关闭**）：开启后，`Part.explode()` 会走 BlastFX 火球。会影响碰撞、引擎、作弊菜单及其他模组——除非你明确想全局替换，否则保持关闭。

## API（给模组作者）

命名空间 `BlastFX`，入口类 `Blast`：

```csharp
Blast.Spawn(worldPos, size: 40f);
Blast.DestroyPart(part);
Blast.SpawnAtPart(part, destroy: true);
Blast.SpawnAtPoint(hitPoint, part, destroy: true, plasma: boltColor);
```

软依赖可用反射查找 `BlastFX.Blast`，无需编译期引用。
