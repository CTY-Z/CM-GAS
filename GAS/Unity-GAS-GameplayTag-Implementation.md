# Unity GAS GameplayTag 完整实现设计

本文档专门说明 Unity GAS-inspired 系统中 `GameplayTag` 的完整实现方案。

本版本的目标不是 MVP 简化版，而是：

```text
第一版就实现接近 Unreal GameplayTags / GAS 使用习惯的完整 Tag 逻辑。
```

原因是 `GameplayTag` 是整个 Ability System 的地基。一旦早期做得太薄，后续 Ability、Effect、Cue、AI、UI、联网、回放、编辑器、数据表都会依赖这个薄弱版本，后补时会牵动全系统。

本文档仍然不要求一比一复刻 Unreal Engine 源码，但会尽量复刻其能力边界和工作流。

---

## 1. 设计结论

Unity 版 GameplayTag 第一版建议完整实现这些能力：

```text
GameplayTag
GameplayTagContainer
GameplayTagQuery
GameplayTagManager / Registry
GameplayTagDatabase
GameplayTagSource
GameplayTagRedirect
Restricted GameplayTag
Native GameplayTag
Parent Tag Cache
Tag Tree
Tag Count Container
Tag Change Event
Tag Query Expression Tree
Tag Picker Editor
Tag Validation
Tag Serialization
Tag Database Version / Hash
Network Tag Index 预留
```

关于 UE 的 `FName` 与本项目的 `NameId`：

```text
不需要完整复刻 UE 的 FName 系统。
Unity 应复刻 FName 的名字驻留、快速比较和可反查调试价值。
本项目通过 `Unity-GAS-NameId-Implementation.md` 中的 NameId / StableName / NameRegistry 承担这层职责。
GameplayTag 是强类型玩法语义句柄，内部持有 NameId；Path 由 GameplayTagRegistry / TagDatabase 管理。
GameplayTagManager 可以维护树、父级缓存和独立 NetworkIndex，但这些都不是 GameplayTag 的身份字段。
```

推荐核心原则：

```text
编辑器友好：用 Path 展示和编辑。
运行时高效：NameId / NameId 比较；不反复比较字符串内容。
数据稳定：资产保存 id + path snapshot；NameId 由 NameRegistry 稳定管理，并通过 Redirect 迁移。
可维护：支持 Redirect、Deprecated、Restricted。
可扩展：预留网络同步、回放、ECS、热更新 Tag Source。
```

---

## 2. GameplayTag 解决什么问题

GameplayTag 是项目级玩法语义标识。

它用于表达：

```text
状态：
  State.Alive
  State.Dead
  State.Stunned
  State.Silenced
  State.Invincible

技能：
  Ability.Magic.Fireball
  Ability.Attack.Combo
  Ability.Passive.ThermalShock

冷却：
  Cooldown.Fireball
  Cooldown.Dodge

效果：
  Effect.Buff.Haste
  Effect.Debuff.Poison
  Effect.Debuff.CrowdControl.Stun

伤害：
  Damage.Fire
  Damage.Ice
  Damage.Physical.Slash

事件：
  Event.Animation.Hit
  Event.Combat.DamageDealt

表现：
  GameplayCue.Fireball.Impact
  GameplayCue.Buff.Haste.Add
```

Tag 的核心价值不是“代替字符串”，而是：

```text
统一语义空间。
支持层级匹配。
支持编辑器选择和校验。
支持跨系统低耦合查询。
支持 Ability / Effect / Cue / AI / UI 共用规则。
支持调试、回放、网络、存档时稳定识别。
```

---

## 3. 和 UE GameplayTags 的能力边界

### 3.1 UE 中相关核心概念

Unreal GameplayTags 系统一般包含这些概念：

```text
FGameplayTag
FGameplayTagContainer
FGameplayTagQuery
UGameplayTagsManager
Gameplay Tag Dictionary / Tree
Native Gameplay Tags
Config / DataTable Tag Sources
Restricted Gameplay Tags
Tag Redirects
Parent Tags
NetIndex / Fast Replication
Tag Count Container usage in ASC
```

Unity 版不复制 UE 类名和底层宏系统，但建议复刻对应能力：

| UE 概念 | Unity 对应设计 | 第一版是否实现 |
| --- | --- | --- |
| `FGameplayTag` | `GameplayTag` struct，内部持有 `NameId`，对齐 UE 中持有 `FName TagName` 的思想 | 是 |
| `FGameplayTagContainer` | `GameplayTagContainer` | 是 |
| `FGameplayTagQuery` | `GameplayTagQuery` 表达式树 | 是 |
| `UGameplayTagsManager` | `GameplayTagManager` / `Registry` | 是 |
| Tag Tree | `GameplayTagNode` 树 | 是 |
| Native Gameplay Tags | C# 静态注册 Tag | 是 |
| Config Tag Sources | `GameplayTagDatabase` / 多 Source 合并 | 是 |
| Restricted Tags | 受限 Tag Source / 权限标记 | 是 |
| Redirects | `GameplayTagRedirect` | 是 |
| Parent Tags | Parent Cache | 是 |
| NetIndex | `NetworkIndex` 预留 | 是，先不接网络 |
| Tag Count Container | `GameplayTagCountContainer` | 是 |

### 3.2 不需要复刻的 UE 细节

不需要照搬：

```text
UObject 生命周期。
UE Config 系统。
UE DataTable 格式。
UE 宏声明 Native Tag 的方式。
UE NetSerialize 的具体实现。
UE Editor Details Panel 的具体 API。
```

Unity 版应使用：

```text
ScriptableObject
AssetDatabase
PropertyDrawer
EditorWindow
SerializedProperty
Addressables optional
C# source-generated constants optional
```

---

## 4. FName 是否需要实现

### 4.1 UE `FName` 的真实运行时结构

UE 的 `FGameplayTag` 内部字段就是一个 `FName TagName`。在 UE5 中，`FName` 的核心布局可概括为：

```cpp
struct FName
{
    FNameEntryId ComparisonIndex; // 全局 FNamePool 中用于比较的条目索引
#if WITH_CASE_PRESERVING_NAME
    FNameEntryId DisplayIndex;    // 保留原始显示大小写；不参与通常的相等语义
#endif
    uint32 Number;                // 名称实例编号
};

struct FGameplayTag
{
private:
    FName TagName;
};
```

`FNameEntryId` 是 UE 全局 `FNamePool` 的进程内句柄；Pool 的 Entry 持有实际字符数据和编码信息。它不是持久化 TagId，也不是 GameplayTag 专属表。

相等的核心语义是：

```text
ComparisonIndex 相同，且 Number 相同 => 相等。
DisplayIndex 只服务显示大小写，不改变通常的名称相等结果。
```

普通 GameplayTag 不应使用名称编号，因此 `Number` 必须为零；`State.Stunned` 的匹配不需要任何字符串逐字符比较。

### 4.2 UE GameplayTagsManager 的边界

`UGameplayTagsManager` 不拥有另一套 FName 池。它以 `FName / FGameplayTag` 为键维护 GameplayTag 特有数据：

```text
Tag Dictionary / TagTree
父子关系与匹配查询
Tag Source、Restricted Tag、Redirect、Deprecated
Native Tag 注册
独立 GameplayTag NetIndex 表与快速网络序列化
```

因此 `FGameplayTag` 的相等只比较 `TagName`；父子匹配、合法性校验和 NetIndex 查找均委托给 `UGameplayTagsManager`。

### 4.3 Unity 适配设计

UE 的关键能力是“轻量值、快速比较、名称可追溯、Tag 语义由 Manager 管理”，不是要求 Unity 复刻完整 `FNamePool`。

Unity 版的正确适配是：

```text
NameId / StableName:
  对标 FName 的名字地基，负责名字驻留、稳定 Id、快速比较、反查调试、Redirect、Hash、NetworkIndex 预留。

GameplayTag:
  对标 FGameplayTag 的强类型值对象，内部只持有 NameId。

GameplayTagRegistry:
  对标 UGameplayTagsManager 的 Tag 语义管理器，负责 Path、TagTree、ParentCache、Restricted、Redirect、TagQuery 支持和 NetworkIndex 映射。
```

推荐结构：

```csharp
[Serializable]
public readonly struct GameplayTag : IEquatable<GameplayTag>, IComparable<GameplayTag>
{
    public static readonly GameplayTag None = default;

    // 对齐 UE FGameplayTag 内部持有 FName TagName 的思想；Unity 版持有 NameId。
    private readonly NameId nameId;

    public NameId NameId => nameId;
    public bool IsValid => nameId.IsValid && GameplayTagRegistry.IsValid(this);

    internal GameplayTag(NameId nameId)
    {
        this.nameId = nameId;
    }

    public bool Equals(GameplayTag other) => nameId.Equals(other.nameId);
    public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
    public override int GetHashCode() => nameId.GetHashCode();
    public int CompareTo(GameplayTag other) => nameId.CompareTo(other.nameId);

    public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
    public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);

    public override string ToString() => GameplayTagRegistry.GetPath(this) ?? "InvalidTag";
}
```

```text
NameRegistry:
  NameNamespace.GameplayTag + Path -> NameId
  NameId -> NameEntry / DebugName / Source / Deprecated / Redirect

GameplayTagRegistry:
  Path -> GameplayTag(NameId)
  NameId -> GameplayTagEntry / TagNode / ParentCache
  NetworkIndex 为独立网络压缩映射。

SerializedGameplayTag:
  Unity 资产和存档保存 SerializedNameId + path snapshot + databaseVersion / databaseHash。
  加载后由 NameRegistry 和 GameplayTagRegistry Resolve 为 GameplayTag。
```

这复刻的是 UE FName 在 GameplayTag 中的实际作用：名称文本集中保存，运行时 Tag 只保存轻量名字句柄。区别是本项目中的 NameId 设计为项目级稳定 Id；NetworkIndex 才是可按数据库版本重建的网络压缩索引。

最终结论：

```text
FName 的能力边界要对齐，字段布局要按 Unity 的序列化和生命周期重设计。
GameplayTag = 强类型玩法语义句柄，内部持有 NameId。
NameRegistry = 稳定名字地基。
GameplayTagRegistry = Path / NameId / TagTree / ParentCache / TagQuery 的玩法语义管理边界。
NetworkIndex 是独立传输优化，不进入 GameplayTag。
```
---

## 5. 总体架构

```text
GameplayTagDatabase
  ScriptableObject 资产，保存项目 Tag、Redirect、Restricted、Source 信息。

GameplayTagManager
  运行时和编辑器共享的管理器，负责加载 Source、构建 TagTree、分配查询。

GameplayTagRegistry
  运行时只读注册表，负责 Path / NameId 映射、TagNode、ParentCache、NetIndex。

GameplayTag
  轻量值类型，只保存运行时 NameId。

GameplayTagContainer
  显式 Tag 集合，支持层级匹配。

GameplayTagCountContainer
  带引用计数的 Tag 集合，用于 ASC OwnedTags。

GameplayTagQuery
  可序列化表达式树，支持 All / Any / None 嵌套组合。

GameplayTagPicker
  编辑器选择器。
```

推荐依赖方向：

```text
TagEntry / TagSource / Redirect
  -> GameplayTagDatabase
  -> GameplayTagManager / Registry
  -> GameplayTag / Container / Query
  -> Ability / Effect / Cue / ASC
```

`GameplayTag` 系统不应该依赖 Ability、Effect、ASC。它必须是底层基础模块。

---

## 6. 核心数据结构

### 6.1 GameplayTag

```csharp
[Serializable]
public readonly struct GameplayTag : IEquatable<GameplayTag>, IComparable<GameplayTag>
{
    public static readonly GameplayTag None = default;

    // FName-like 稳定名字句柄；不直接序列化为唯一资产边界。
    private readonly NameId nameId;

    public NameId NameId => nameId;
    public bool IsValid => nameId.IsValid && GameplayTagRegistry.IsValid(this);

    internal GameplayTag(NameId nameId)
    {
        this.nameId = nameId;
    }

    public bool Equals(GameplayTag other) => nameId.Equals(other.nameId);
    public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
    public override int GetHashCode() => nameId.GetHashCode();
    public int CompareTo(GameplayTag other) => nameId.CompareTo(other.nameId);

    public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
    public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);

    public override string ToString() => GameplayTagRegistry.GetPath(this) ?? "InvalidTag";
}
```

设计要求：

```text
GameplayTag 是值类型。
GameplayTag 的唯一身份字段是 NameId，作用对齐 UE FGameplayTag 的 FName TagName。
None 使用无效 NameId。
Equals / HashCode 只比较 NameId。
GameplayTagManager 负责 Registry 有效性、Path、层级关系、父级缓存和 NetworkIndex；这些状态不进入 GameplayTag 值对象。
```

### 6.2 NameId 关系

`NameId` 的完整实现见 `Unity-GAS-NameId-Implementation.md`。

在 GameplayTag 系统中：

```text
NameRegistry 负责 NameNamespace.GameplayTag + Path -> NameId。
GameplayTag 只持有 NameId。
GameplayTagRegistry 负责 NameId -> GameplayTagEntry / TagNode / ParentCache。
业务代码不能取得或比较裸 int。
任意 NameId 不能自动当作 GameplayTag；必须通过 GameplayTagRegistry 校验其 Namespace 和 TagDatabase 合法性。
```

### 6.3 GameplayTagEntry

```csharp
[Serializable]
public sealed class GameplayTagEntry
{
    public string path;
    public string description;
    public string sourceName;
    public bool restricted;
    public bool deprecated;
    public string replacedByPath;
    public int networkIndex = -1;
}
```

字段说明：

```text
path:
  Tag 的规范名称，例如 State.Stunned；用于编辑、日志、Redirect 和文本输入解析。


description:
  给编辑器和策划看的说明。

sourceName:
  来源名，例如 Core、Combat、Item、DLC_01。

restricted:
  是否是受限 Tag。

deprecated:
  是否废弃。

replacedByPath:
  废弃后推荐替代 Tag。

networkIndex:
  未来快速网络同步用索引。
```

### 6.4 GameplayTagNode

TagTree 节点：

```csharp
public sealed class GameplayTagNode
{
    public GameplayTag Tag;
    public string Name;       // 当前段名，例如 Fire
    public string FullPath;   // 完整路径，例如 Damage.Fire
    public GameplayTagNode Parent;
    public List<GameplayTagNode> Children = new();
}
```

用途：

```text
编辑器树形展示。
Parent / Child 查询。
Tag Picker 搜索。
构建 Parent Cache。
```

### 6.5 GameplayTagRedirect

```csharp
[Serializable]
public sealed class GameplayTagRedirect
{
    public string oldPath;
    public string newPath;
}
```

用途：

```text
Tag 重命名后旧资产仍能加载。
导入旧配置时自动迁移。
编辑器提示旧 Tag 已替换。
```

示例：

```text
Old: State.Stuned
New: State.Stunned
```

### 6.6 GameplayTagSource

```csharp
[Serializable]
public sealed class GameplayTagSource
{
    public string name;
    public GameplayTagSourceType type;
    public bool restricted;
    public List<GameplayTagEntry> tags;
}

public enum GameplayTagSourceType
{
    Native,
    ScriptableObject,
    Generated,
    ExternalTable,
    RuntimeExtension
}
```

为什么需要 Source：

```text
区分核心 Tag、战斗 Tag、装备 Tag、DLC Tag。
支持受限 Tag 权限。
支持生成 Tag 常量。
支持热更新或扩展包。
支持调试时知道 Tag 来自哪里。
```

---

## 7. GameplayTagDatabase

### 7.1 职责

`GameplayTagDatabase` 是编辑器资产，保存 Tag 数据。

```csharp
[CreateAssetMenu(menuName = "UGAS/Gameplay Tag Database")]
public sealed class GameplayTagDatabase : ScriptableObject
{
    [SerializeField] private List<GameplayTagSource> sources;
    [SerializeField] private List<GameplayTagRedirect> redirects;
    [SerializeField] private int databaseVersion;
    [SerializeField] private string databaseHash;
}
```

### 7.2 第一版必须支持多 Source

不要只做一个 `List<string>`。

第一版就建议支持：

```text
Core Source:
  State.*
  Event.*
  GameplayCue.*

Combat Source:
  Damage.*
  Effect.*
  Ability.*

Item Source:
  Weapon.*
  Item.*
  Equipment.*

Restricted Source:
  Boss.*
  Internal.*
  Debug.*
```

这样后续系统扩展时不会把所有 Tag 混在一个大列表里。

### 7.3 规范名称与 NetworkIndex 策略

第一版必须实现稳定的规范名称；NetworkIndex 是由 GameplayTagManager 生成的独立传输优化，不能与 Tag 身份混淆。

不要使用：

```text
List index 作为唯一 Tag 身份
string.GetHashCode 作为持久 Tag 身份
序列化 NetworkIndex 或任意运行时缓存索引
```

推荐：

```text
新增 Tag 时登记唯一的规范 Path；NameRegistry 为其维护稳定 NameId。
启动 Manager 时按 TagDatabase 构建独立的 NetworkIndex；它只在双方数据库版本一致时用于压缩。
重命名 Tag 时添加 OldPath -> NewPath Redirect；旧资产和存档依其保存的名称解析并迁移。
```

例如：

```text
NameId(1001) / State.Stunned -> NetworkIndex 1
NameId(1002) / State.Silenced -> NetworkIndex 2
NameId(1003) / Damage.Fire   -> NetworkIndex 3
```

NameId 是项目级稳定名字 Id；NetworkIndex 只对相同数据库版本的通信有效。资产建议保存 id + path snapshot，兼容性由 Path、Redirect、DatabaseHash / Version 共同保障。

### 7.4 Database Hash

第一版建议生成 Hash。

Hash 输入：

```text
所有规范 Path + Redirect + Source + Restricted 信息，以及确定性 NetworkIndex 映射（如有）
```

用途：

```text
启动时校验客户端和服务器 Tag 表一致。
回放文件记录使用的 TagDatabase 版本。
调试日志能知道当时的 Tag 表。
```

即使暂不联网，也建议保留。

---

## 8. GameplayTagManager / Registry

### 8.1 职责

`GameplayTagManager` 负责构建和管理 Tag 系统。

```text
加载 GameplayTagDatabase。
合并 Native Tags。
处理 Redirect。
校验重复和非法路径。
构建 Path -> GameplayTag(NameId)（含 Redirect）。
构建 NameId -> Entry / TagNode。
构建 TagTree。
构建 ParentCache。
构建 NetworkIndex。
提供 RequestTag / TryRequestTag。
```

可以拆分为：

```text
GameplayTagManager:
  编辑器和初始化逻辑较多。

GameplayTagRegistry:
  运行时只读查询接口。
```

第一版也可以合并成一个类，但职责要清楚。

### 8.2 推荐接口

```csharp
public static class GameplayTagRegistry
{
    public static bool IsInitialized { get; }
    public static int DatabaseVersion { get; }
    public static string DatabaseHash { get; }

    public static void Initialize(GameplayTagDatabase database, IReadOnlyList<NativeGameplayTagDefinition> nativeTags);

    public static GameplayTag RequestTag(string path, bool errorIfNotFound = true);
    public static bool TryRequestTag(string path, out GameplayTag tag);

    public static string GetPath(GameplayTag tag);
    public static GameplayTagEntry GetEntry(GameplayTag tag);
    public static bool IsValid(GameplayTag tag);

    public static bool IsChildOf(GameplayTag child, GameplayTag parent);
    public static IReadOnlyList<GameplayTag> GetParents(GameplayTag tag);
    public static IReadOnlyList<GameplayTag> GetChildren(GameplayTag tag, bool recursive);

    public static GameplayTag RedirectTagPath(string path, out bool wasRedirected);

    public static int GetNetworkIndex(GameplayTag tag);
    public static GameplayTag FromNetworkIndex(int networkIndex);
}
```

### 8.3 RequestTag 规则

`RequestTag` 必须严格。

```text
找不到 Tag：
  开发期抛异常或报错。

找到 Deprecated Tag：
  返回 Tag，但输出警告，并提示 replacedByPath。

命中 Redirect：
  返回新 Tag，并记录 wasRedirected。
```

不要运行时静默创建 Tag。

---

## 9. Tag 路径规范与校验

### 9.1 合法字符

推荐路径规则：

```text
只允许：A-Z a-z 0-9 _ .
不能以 . 开头。
不能以 . 结尾。
不能包含连续 ..。
每段不能为空。
建议首字母大写或使用项目统一规范。
```

示例合法：

```text
State.Stunned
Damage.Physical.Slash
GameplayCue.Fireball.Impact
```

示例非法：

```text
.State.Stunned
State..Stunned
State.Stunned.
State Stunned
State/ Stunned
```

### 9.2 父节点策略

如果声明：

```text
Damage.Fire.Burn
```

父节点：

```text
Damage
Damage.Fire
```

第一版建议自动生成父节点，但要标记为隐式节点：

```csharp
public bool explicitTag;
```

区别：

```text
Explicit Tag:
  用户明确声明，可被配置引用。

Implicit Parent Tag:
  为层级树自动生成，通常用于匹配和展示。
```

也可以要求父节点必须显式存在，但编辑器体验会更重。大型项目推荐：

```text
自动生成父节点，同时允许将父节点提升为显式 Tag。
```

---

## 10. Parent Cache 与层级匹配

### 10.1 匹配语义

Tag：

```text
Damage.Fire.Burn
```

应该匹配：

```text
Damage.Fire.Burn
Damage.Fire
Damage
```

但不匹配：

```text
Damage.Ice
Effect.Debuff.Burn
```

### 10.2 ParentCache

初始化时预计算：

```csharp
private static Dictionary<GameplayTag, GameplayTag[]> parentsByTag;
private static Dictionary<GameplayTag, GameplayTag[]> childrenByTag;
```

`Damage.Fire.Burn` 的 ParentCache：

```text
[Damage.Fire, Damage]
```

### 10.3 IsChildOf

```csharp
public static bool IsChildOf(GameplayTag child, GameplayTag parent, bool includeSelf = true)
{
    if (!child.IsValid || !parent.IsValid)
    {
        return false;
    }

    if (includeSelf && child == parent)
    {
        return true;
    }

    return parentsByTag.TryGetValue(child, out var parents)
        && Array.IndexOf(parents, parent) >= 0;
}
```

---

## 11. GameplayTagContainer

### 11.1 设计目标

`GameplayTagContainer` 表示一组显式 Tag。

用于：

```text
AbilityTags
ActivationRequiredTags
ActivationBlockedTags
GrantedTags
SourceTags
TargetTags
EffectTags
CueTags
```

### 11.2 显式 Tag 与展开 Parent Tag

UE 的 TagContainer 有显式 Tag 和 Parent Tag 相关概念。Unity 版也建议实现类似能力。

推荐内部：

```csharp
public sealed class GameplayTagContainer
{
    private readonly HashSet<GameplayTag> explicitTags = new();
    private readonly HashSet<GameplayTag> expandedTags = new();
}
```

含义：

```text
explicitTags:
  用户真正添加的 Tag，例如 Damage.Fire.Burn。

expandedTags:
  显式 Tag + 所有父 Tag，例如 Damage.Fire.Burn、Damage.Fire、Damage。
```

这样：

```text
HasTag(Damage.Fire) 可以 O(1) 查询 expandedTags。
```

### 11.3 推荐接口

```csharp
public sealed class GameplayTagContainer
{
    public IReadOnlyCollection<GameplayTag> ExplicitTags => explicitTags;
    public IReadOnlyCollection<GameplayTag> ExpandedTags => expandedTags;

    public bool IsEmpty => explicitTags.Count == 0;
    public int Count => explicitTags.Count;

    public void AddTag(GameplayTag tag);
    public bool RemoveTag(GameplayTag tag);
    public void Reset();

    public bool HasTag(GameplayTag tag, GameplayTagMatchType matchType = GameplayTagMatchType.IncludeParentTags);
    public bool HasAny(GameplayTagContainer other, GameplayTagMatchType matchType = GameplayTagMatchType.IncludeParentTags);
    public bool HasAll(GameplayTagContainer other, GameplayTagMatchType matchType = GameplayTagMatchType.IncludeParentTags);

    public bool MatchesQuery(GameplayTagQuery query);

    public GameplayTagContainer Filter(GameplayTagContainer filterTags, GameplayTagMatchType matchType);
    public void AppendTags(GameplayTagContainer other);
    public void RemoveTags(GameplayTagContainer other);
}
```

### 11.4 AddTag

```csharp
public void AddTag(GameplayTag tag)
{
    if (!tag.IsValid)
    {
        return;
    }

    if (!explicitTags.Add(tag))
    {
        return;
    }

    expandedTags.Add(tag);

    foreach (var parent in GameplayTagRegistry.GetParents(tag))
    {
        expandedTags.Add(parent);
    }
}
```

### 11.5 RemoveTag

移除显式 Tag 后，需要重建 `expandedTags`。

原因：

```text
Damage.Fire.Burn 和 Damage.Fire.Explosion 都贡献 Damage.Fire。
移除 Burn 后，Damage.Fire 仍可能来自 Explosion。
```

简单可靠做法：

```csharp
public bool RemoveTag(GameplayTag tag)
{
    if (!explicitTags.Remove(tag))
    {
        return false;
    }

    RebuildExpandedTags();
    return true;
}
```

### 11.6 MatchType

```csharp
public enum GameplayTagMatchType
{
    Explicit,
    IncludeParentTags
}
```

含义：

```text
Explicit:
  只查显式添加的 Tag。

IncludeParentTags:
  查显式 Tag 和父 Tag 展开集合。
```

---

## 12. GameplayTagCountContainer

### 12.1 为什么必须实现

ASC 的 OwnedTags 不是普通集合，而是引用计数集合。

示例：

```text
GE_Stun_A 授予 State.Stunned。
GE_Stun_B 也授予 State.Stunned。
GE_Stun_A 结束。
```

此时角色仍然应该有：

```text
State.Stunned count = 1
```

不能直接 Remove。

### 12.2 推荐结构

```csharp
public sealed class GameplayTagCountContainer
{
    private readonly Dictionary<GameplayTag, int> explicitTagCounts = new();
    private readonly Dictionary<GameplayTag, int> expandedTagCounts = new();

    public event Action<GameplayTagChangedEvent> TagChanged;

    public void AddTag(GameplayTag tag, int count = 1);
    public void RemoveTag(GameplayTag tag, int count = 1);
    public int GetTagCount(GameplayTag tag, GameplayTagMatchType matchType = GameplayTagMatchType.IncludeParentTags);
    public bool HasTag(GameplayTag tag, GameplayTagMatchType matchType = GameplayTagMatchType.IncludeParentTags);
}
```

### 12.3 父 Tag 计数也要维护

添加：

```text
Damage.Fire.Burn count +1
```

同时 expanded：

```text
Damage.Fire.Burn +1
Damage.Fire +1
Damage +1
```

移除时也对应减少。

这样层级 HasTag 是 O(1)。

### 12.4 事件触发规则

```text
count 0 -> 1:
  Tag Added

count 1 -> 0:
  Tag Removed

count 1 -> 2:
  引用数变化，可记录 Debug，不一定通知普通监听者
```

推荐事件：

```csharp
public readonly struct GameplayTagChangedEvent
{
    public readonly object Owner;
    public readonly GameplayTag Tag;
    public readonly int OldCount;
    public readonly int NewCount;
    public readonly GameplayTagChangeReason Reason;
    public readonly object Source;

    public bool WasAdded => OldCount == 0 && NewCount > 0;
    public bool WasRemoved => OldCount > 0 && NewCount == 0;
}
```

`Source` 可用于调试，例如哪个 Effect 授予了这个 Tag。

---

## 13. GameplayTagQuery 完整表达式树

### 13.1 为什么不能只做 All / Any / None

简单 All / Any / None 能覆盖早期需求，但复杂 GAS 项目会很快遇到：

```text
需要嵌套逻辑。
需要表达任意组合。
需要把 Query 序列化到资产。
需要编辑器可视化。
```

例如：

```text
目标必须：
  拥有 Team.Enemy
  且没有 State.Dead
  且满足：
    拥有 State.Burning
    或拥有 Effect.Debuff.Oil
  且不能拥有：
    State.Invincible
    State.PhaseShift
```

### 13.2 Query 表达式类型

推荐实现类似 UE 的表达式树：

```csharp
public enum GameplayTagQueryExprType
{
    AnyTagsMatch,
    AllTagsMatch,
    NoTagsMatch,
    AnyExprMatch,
    AllExprMatch,
    NoExprMatch
}
```

含义：

```text
AnyTagsMatch:
  容器拥有任意一个 Tag。

AllTagsMatch:
  容器拥有全部 Tag。

NoTagsMatch:
  容器不能拥有任何一个 Tag。

AnyExprMatch:
  子表达式任意一个为 true。

AllExprMatch:
  子表达式全部为 true。

NoExprMatch:
  子表达式全部不能为 true。
```

### 13.3 Query Expression

```csharp
[Serializable]
public sealed class GameplayTagQueryExpression
{
    public GameplayTagQueryExprType Type;
    public GameplayTagContainer Tags;
    public List<GameplayTagQueryExpression> Children;

    public bool Evaluate(GameplayTagContainer container)
    {
        switch (Type)
        {
            case GameplayTagQueryExprType.AnyTagsMatch:
                return container.HasAny(Tags);

            case GameplayTagQueryExprType.AllTagsMatch:
                return container.HasAll(Tags);

            case GameplayTagQueryExprType.NoTagsMatch:
                return !container.HasAny(Tags);

            case GameplayTagQueryExprType.AnyExprMatch:
                return Children.Any(child => child.Evaluate(container));

            case GameplayTagQueryExprType.AllExprMatch:
                return Children.All(child => child.Evaluate(container));

            case GameplayTagQueryExprType.NoExprMatch:
                return !Children.Any(child => child.Evaluate(container));

            default:
                return false;
        }
    }
}
```

### 13.4 GameplayTagQuery

```csharp
[Serializable]
public sealed class GameplayTagQuery
{
    public GameplayTagQueryExpression RootExpression;
    public string UserDescription;
    public string AutoDescription;

    public bool IsEmpty => RootExpression == null;

    public bool Matches(GameplayTagContainer container)
    {
        return RootExpression == null || RootExpression.Evaluate(container);
    }
}
```

### 13.5 为什么还保留 All / Any / None 快捷形式

完整 Query 表达力强，但配置较重。

建议提供快捷 Builder：

```csharp
public static GameplayTagQuery All(params GameplayTag[] tags);
public static GameplayTagQuery Any(params GameplayTag[] tags);
public static GameplayTagQuery None(params GameplayTag[] tags);
```

编辑器上也可以提供“简单模式”和“高级模式”：

```text
简单模式：All / Any / None
高级模式：表达式树
```

但底层第一版就应使用表达式树，避免后续迁移。

---

## 14. Native Gameplay Tags

### 14.1 为什么需要 Native Tag

有些核心 Tag 会在代码中高频使用：

```text
State.Dead
State.Stunned
Cooldown.Global
Damage.Fire
Event.Combat.DamageDealt
```

如果每次都 `RequestTag("State.Stunned")`，容易写错，也不方便跳转。

### 14.2 推荐实现

```csharp
public static class NativeGameplayTags
{
    public static GameplayTag State_Dead { get; private set; }
    public static GameplayTag State_Stunned { get; private set; }
    public static GameplayTag Damage_Fire { get; private set; }

    public static void Register()
    {
        State_Dead = GameplayTagRegistry.RequestTag("State.Dead");
        State_Stunned = GameplayTagRegistry.RequestTag("State.Stunned");
        Damage_Fire = GameplayTagRegistry.RequestTag("Damage.Fire");
    }
}
```

### 14.3 代码生成

中后期建议由 TagDatabase 生成：

```text
GameplayTags.Generated.cs
```

示例：

```csharp
public static partial class Tags
{
    public static readonly GameplayTag State_Stunned = GameplayTagRegistry.RequestTag("State.Stunned");
    public static readonly GameplayTag Damage_Fire = GameplayTagRegistry.RequestTag("Damage.Fire");
}
```

注意：

```text
如果 Registry 运行时初始化顺序复杂，生成代码里不要静态初始化 RequestTag。
可以生成 Path 常量和 Lazy Tag。
```

更安全：

```csharp
public static class TagPaths
{
    public const string State_Stunned = "State.Stunned";
}
```

---

## 15. Restricted Gameplay Tags

### 15.1 什么是 Restricted Tag

Restricted Tag 是受限标签。

用途：

```text
Boss 机制专用 Tag。
内部系统 Tag。
不允许普通技能随意引用的 Tag。
需要负责人审批的 Tag。
```

示例：

```text
Boss.Phase.Hidden
Internal.Network.Predicted
Debug.GodMode
```

### 15.2 Unity 版实现

在 `GameplayTagEntry` 或 `GameplayTagSource` 上标记：

```csharp
public bool restricted;
public string owner;
public string comment;
```

编辑器行为：

```text
普通 TagPicker 默认隐藏 Restricted Tag。
开启高级模式才显示。
引用 Restricted Tag 时给出警告。
可按 Source 或 Owner 限制编辑权限。
```

第一版不一定要做权限系统，但必须保存 restricted 标记，并在编辑器显示出来。

---

## 16. Tag Redirect 与 Deprecated

### 16.1 Redirect

Tag 重命名时不要直接改所有资产。

推荐流程：

```text
State.Stuned -> State.Stunned
添加 Redirect。
旧资产加载时自动映射到新 Tag。
编辑器提示可以迁移。
```

### 16.2 Deprecated

Deprecated 用于废弃但暂时保留：

```text
Effect.OldPoison deprecated -> Effect.Debuff.Poison
```

编辑器：

```text
显示黄色警告。
禁止新资产选择 Deprecated Tag。
旧资产仍可加载。
提供一键迁移。
```

### 16.3 第一版必须做吗

建议第一版就做。

原因：

```text
Tag 一旦被大量资产引用，重命名和废弃是必然发生的。
后补 Redirect 会导致旧资产已经散落全项目，很难迁移。
```

---

## 17. Tag 序列化

### 17.1 资产序列化

Unity Inspector 中推荐保存：

```text
SerializedNameId + path snapshot + databaseVersion / databaseHash
```

结构：

```csharp
[Serializable]
public struct SerializedGameplayTag
{
    public SerializedNameId name;
    public string pathSnapshot;
    public int tagDatabaseVersion;
    public string tagDatabaseHash;
}
```

加载时：

```text
1. 优先用 SerializedNameId 中的 namespace + id 查 NameRegistry。
2. 校验该 NameId 属于 NameNamespace.GameplayTag。
3. 通过 GameplayTagRegistry 校验它仍是合法 GameplayTag。
4. 如果 id 无效或数据库 Hash 不匹配，使用 pathSnapshot 经 Redirect 解析。
5. 如果 pathSnapshot 命中 Redirect，更新 name 与 pathSnapshot，并提示可迁移资产。
6. 如果 id 与 pathSnapshot 指向不同 Tag，输出明确诊断，不静默吞掉。
```

### 17.2 为什么不只存 NetworkIndex 或任意运行时索引

只存 NetworkIndex 或运行时索引的问题：

```text
开发期查看资产不直观。
TagDatabase 损坏时难恢复。
多人协作冲突时难判断。
不同数据库版本或加载顺序可能没有同一映射。
无法自然承接 Rename Redirect。
```

### 17.3 为什么不只存 Path

只存 Path 的问题：

```text
每次加载都必须字符串解析。
重命名和冲突诊断缺少稳定 Id 参照。
大型资产库中难以做快速校验和批量分析。
网络、回放、存档无法直接关联稳定 NameId。
```

所以建议：

```text
资产/存档存 SerializedNameId + Path Snapshot；Redirect 保障重命名后的旧数据迁移。
运行时 GameplayTag 保存 NameId；Registry / Container 使用 TagNode 与 ParentCache 完成高频查询。
编辑器显示并编辑 pathSnapshot，但只允许 TagPicker 或 Registry 写入有效 Tag。
```
---

## 18. 网络同步预留

虽然当前不实现联网，但 Tag 系统第一版要预留网络能力。

### 18.1 NetworkIndex

GameplayTag 内部的 NameId 是项目级稳定名字 Id，但不应直接作为跨端压缩协议编码；Manager 生成的 NetworkIndex 才是网络压缩边界。

可以额外生成：

```text
NetworkIndex
```

特点：

```text
连续、小范围、适合压缩。
由 TagDatabase 构建时生成。
客户端和服务器必须一致。
```

### 18.2 Hash 校验

联网启动时应校验：

```text
TagDatabaseHash
```

如果不一致：

```text
拒绝连接。
或者降级到 Path 同步并报错。
```

### 18.3 同步内容

未来同步 Tag 时可传：

```text
NetworkIndex
Count
ChangeReason
PredictionKey optional
```

---

## 19. 与 ASC 的关系

ASC 应使用 `GameplayTagCountContainer` 管理 OwnedTags。

Tag 来源：

```text
Loose Tags
GameplayEffect Granted Tags
Ability Activation Tags
Equipment Tags
Passive Tags
```

第一版建议内部保留 Source 信息：

```csharp
public readonly struct GameplayTagStackSource
{
    public GameplayTagStackSourceType Type;
    public int SourceHandle;
    public string DebugName;
}
```

Source 类型：

```text
Loose
GameplayEffect
Ability
Equipment
Passive
System
```

这样 Debugger 可以显示：

```text
State.Stunned count 2
  GE_Stun_FromTrap
  GE_Stun_FromMonster
```

这对大型系统非常重要。

---

## 20. 编辑器工具要求

第一版 Tag 系统如果目标完整，编辑器也不能太弱。

必须有：

```text
GameplayTagDatabase Inspector
Tag Tree View
Tag Picker PropertyDrawer
搜索
新增 / 重命名 / 删除
Redirect 生成
Deprecated 标记
Restricted 标记
Source 筛选
重复校验
非法字符校验
父节点可视化
复制完整 Path
生成代码常量
导出 / 导入 JSON 可选
```

### 20.1 Tag Picker

选择体验：

```text
支持搜索 State.Stunned。
支持搜索 Stunned。
支持按 Source 过滤。
支持隐藏 Deprecated。
支持隐藏 Restricted。
显示 Description。
显示完整 Path。
```

### 20.2 重命名流程

重命名 `OldPath` 到 `NewPath`：

```text
1. 校验 NewPath 合法。
2. 添加 Redirect OldPath -> NewPath，使旧序列化名称可解析。
3. 修改 Entry path；旧 Path 通过 Redirect 在加载时解析为新 Tag。
4. 标记数据库 dirty。
5. 重新生成 Hash。
6. 提示是否扫描并迁移资产中的 path snapshot。
```

---

## 21. 推荐第一版实现清单

既然目标是完整地基，第一版应实现：

```text
GameplayTag struct
SerializedGameplayTag
SerializedNameId dependency
GameplayTagEntry
GameplayTagSource
GameplayTagRedirect
GameplayTagDatabase
GameplayTagManager / Registry
NameId
Path -> GameplayTag(NameId) / TagNode
NameId -> GameplayTagEntry / TagNode
Redirect resolution
Deprecated validation
Restricted flag
Tag Tree
ParentCache
ChildCache
GameplayTagContainer explicitTags + expandedTags
GameplayTagCountContainer explicit counts + expanded counts
GameplayTagChangedEvent
GameplayTagQuery expression tree
Native Tag registration
Generated constants optional but recommended
DatabaseVersion
DatabaseHash
NetworkIndex placeholder
Tag Picker
Tag Database Inspector
Validation tools
```

可以不在第一版实现完整功能，但必须预留字段和接口：

```text
真正的联网压缩传输
多人权限系统
Mod / DLC 动态 Source 合并
ECS BitSet 优化
复杂资产批量迁移 UI
```

注意这里的“不实现”指业务流程不接，不是数据结构完全不留。

---

## 22. 推荐目录结构

```text
Runtime/
  Tags/
    GameplayTag.cs
    SerializedGameplayTag.cs
    GameplayTagEntry.cs
    GameplayTagSource.cs
    GameplayTagSourceType.cs
    GameplayTagRedirect.cs
    GameplayTagDatabase.cs
    GameplayTagRegistry.cs
    GameplayTagNode.cs
    GameplayTagContainer.cs
    GameplayTagCountContainer.cs
    GameplayTagChangedEvent.cs
    GameplayTagQuery.cs
    GameplayTagQueryExpression.cs
    GameplayTagQueryExprType.cs
    GameplayTagMatchType.cs
    NativeGameplayTagDefinition.cs

Editor/
  Tags/
    GameplayTagDatabaseEditor.cs
    GameplayTagTreeView.cs
    GameplayTagPickerWindow.cs
    GameplayTagPropertyDrawer.cs
    GameplayTagContainerDrawer.cs
    GameplayTagQueryDrawer.cs
    GameplayTagCodeGenerator.cs
    GameplayTagValidationUtility.cs
```

---

## 23. 常见坑

### 23.1 第一版只做 string

这是最危险的做法。

后续所有资产、Effect、Ability、Cue、AI 都会依赖字符串，迁移到 Id 会很痛。

### 23.2 第一版只做 All / Any / None Query

如果底层数据结构只支持简单 Query，后面改表达式树会影响所有资产序列化。

建议第一版底层就是表达式树，编辑器可以先提供简单 UI。

### 23.3 不做 Redirect

Tag 一定会重命名。

不做 Redirect，旧资产要么断引用，要么只能全项目搜索替换。

### 23.4 把 NetworkIndex 或缓存索引当作持久化身份

NetworkIndex 和任何缓存索引都只在其构建条件成立时有效，不能被资产、存档或跨版本协议当作唯一身份。

排序、插入、删除、DLC 合并后应通过规范 Path、Redirect、DatabaseVersion / Hash 保证兼容性；如需固定网络压缩表，使用独立 `NetworkIndex` 映射。

### 23.5 Parent Tag 不缓存

运行时频繁拆字符串做父子匹配，会很浪费。

ParentCache 第一版就应该有。

### 23.6 Tag 和 bool 混用

不要同时维护：

```csharp
bool IsStunned;
GameplayTag State.Stunned;
```

推荐：

```text
业务事实以 Tag 为准。
高频系统如移动组件可以监听 TagChangedEvent 缓存状态。
```

---

## 24. 最终建议

如果目标是长期维护的大型 Unity GAS，那么 GameplayTag 第一版就应该当成基础设施，而不是临时工具类。

最终建议：

```text
不使用裸 string 作为运行时 Tag。
不使用 enum 作为主 Tag 系统。
不完整复刻 UE FName。
实现底层 NameId / StableName，并让 GameplayTag 持有 NameId；由 GameplayTagRegistry 管理其到 Path / TagNode 的映射。
实现由 Manager 管理的独立 NetworkIndex，而非把网络或缓存索引塞进 GameplayTag。
实现 GameplayTagManager / Registry。
实现多 Source TagDatabase。
实现 Redirect / Deprecated / Restricted。
实现 TagTree / ParentCache。
实现 Container 的 explicitTags + expandedTags。
实现 CountContainer 的引用计数和事件。
实现完整 GameplayTagQuery 表达式树。
实现编辑器 TagPicker 和校验。
预留 NetworkIndex 和 DatabaseHash。
```

一句话：

```text
GameplayTag 是 GAS 的语言系统。
语言系统一开始就要设计完整，否则后面的 Ability、Effect、AI、UI 都会说方言。
```


