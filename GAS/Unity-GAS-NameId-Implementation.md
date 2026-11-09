# Unity GAS NameId / StableName 完整实现设计

本文档专门说明 Unity GAS-inspired 系统中的 `NameId` / `StableName` 基础设施设计。

它的设计目标是吸收 Unreal `FName` 在 GAS 和引擎系统中的核心价值：

```text
字符串集中注册。
运行时使用稳定 Id。
比较时不比较字符串内容。
可通过 Id 反查调试名称。
支持序列化、重命名、迁移、网络和回放。
```

但它不是一比一复刻 UE 的完整 `FName`。

Unity 版应该实现的是一套服务于 GAS 大型项目的轻量名字基础设施：

```text
NameId / StableName
NameRegistry
NameSource
NameRedirect
SerializedNameId
NameRegistryHash
```

---

## 1. 设计目标

第一版就应该实现完整地基，而不是先用 `string`，后面再替换。

原因：

```text
GameplayTag、AttributeId、SetByCallerKey、BlackboardKey、GameplayMessage、GameplayCue、AnimationEventKey 等都会依赖“名字标识”。
如果第一版大量使用裸 string，后续迁移到 Id 会影响资产、序列化、调试、运行时 API 和网络边界。
```

NameId 的目标：

```text
统一名字注册。
提供稳定运行时 Id。
提供 Path / DebugName 反查。
支持重命名 Redirect。
支持 Deprecated。
支持 Source。
支持资产序列化 id + name snapshot。
支持数据库版本和 Hash。
为网络同步和回放预留压缩索引。
```

---

## 2. UE / GAS 对标能力边界

### 2.1 FName 在 UE 中的作用

UE 的 `FName` 是引擎级名字系统。

它广泛用于：

```text
对象名
属性名
函数名
反射查找
Animation Notify 名
Montage Section 名
DataTable Row 名
GameplayTag 路径内部表示
SetByCaller FName Key
GameplayAttribute 相关反射字段
GameplayCue 路径和查找
```

在 GAS 中，开发者看到的高层概念通常是：

```text
GameplayTag
GameplayAttribute
GameplayEffect
GameplayAbility
GameplayCue
```

但这些概念背后的命名、查找、反射、事件、配置和参数传递大量受益于 `FName`。

### 2.2 Unity 版不复刻什么

Unity 版不需要复刻：

```text
UE 全局 Name Table 的完整实现。
UE UObject / Reflection 的 FName 绑定。
UE Package / Object Path 命名体系。
UE 内部大小写规则细节。
UE 宏系统和静态初始化方式。
```

### 2.3 Unity 版必须吸收什么

Unity 版必须吸收：

```text
名字驻留。
稳定 Id。
快速比较。
可调试反查。
序列化稳定。
重命名 Redirect。
来源 Source。
版本 Hash。
网络索引预留。
```

一句话：

```text
不搬 FName 的引擎实现。
要搬 FName 的工程价值。
```

---

## 3. Unity 实现取舍

### 3.1 NameId 是底层基础设施，不是玩法语义

`NameId` 本身不表达玩法含义。

例如：

```text
NameId(1001) -> "State.Stunned"
NameId(2001) -> "Health"
NameId(3001) -> "Data.Damage"
```

它只表示“一个稳定名字”。

真正的玩法语义由上层类型表达：

```text
GameplayTag
GameplayAttributeId
SetByCallerKey
BlackboardKey
GameplayMessageKey
GameplayCueKey
AnimationEventKey
```

不要让所有系统都直接裸用 `NameId`，否则会丢失类型边界。

推荐关系：

```text
NameId:
  底层稳定名字 Id。

GameplayTag:
  使用 TagRegistry 管理层级、父子关系、TagQuery。

GameplayAttributeId:
  使用 AttributeRegistry 管理 AttributeSet、Clamp、聚合规则。

SetByCallerKey:
  使用 NameId 或独立 Key 包装，表示运行时传参 Key。
```

### 3.2 是否所有 Key 都共用一个 Registry

有两种方案。

方案 A：全局 NameRegistry。

```text
所有名字都进入一个表。
优点：实现统一，反查简单。
缺点：不同语义空间可能混在一起。
```

方案 B：分 Namespace 的 NameRegistry。

```text
Tag / Attribute / Data / Event / Blackboard 分命名空间。
优点：类型隔离更清楚。
缺点：实现略复杂。
```

推荐第一版使用：

```text
全局 NameRegistry + Namespace 字段
```

也就是同一套底层表，但每个名字带命名空间：

```text
GameplayTag: State.Stunned
Attribute: Health
SetByCaller: Data.Damage
Blackboard: Target
GameplayEvent: Event.Animation.Hit
```

这样既能统一管理，又能避免语义混淆。

---

## 4. 核心数据结构

### 4.1 NameId

```csharp
[Serializable]
public readonly struct NameId : IEquatable<NameId>, IComparable<NameId>
{
    public static readonly NameId None = new NameId(0);

    public readonly int Id;

    public bool IsValid => Id != 0;

    public NameId(int id)
    {
        Id = id;
    }

    public bool Equals(NameId other) => Id == other.Id;
    public override bool Equals(object obj) => obj is NameId other && Equals(other);
    public override int GetHashCode() => Id;
    public int CompareTo(NameId other) => Id.CompareTo(other.Id);

    public static bool operator ==(NameId a, NameId b) => a.Id == b.Id;
    public static bool operator !=(NameId a, NameId b) => a.Id != b.Id;

    public override string ToString()
    {
        return NameRegistry.IsValid(this)
            ? NameRegistry.GetName(this)
            : $"InvalidName({Id})";
    }
}
```

设计要求：

```text
NameId 是值类型。
只保存 int Id。
Id 0 表示 None。
Equals / HashCode 只看 Id。
调试字符串通过 NameRegistry 反查。
```

### 4.2 NameNamespace

```csharp
public enum NameNamespace
{
    None = 0,
    GameplayTag,
    Attribute,
    SetByCaller,
    Blackboard,
    GameplayEvent,
    GameplayCue,
    AnimationEvent,
    Asset,
    Debug
}
```

用途：

```text
隔离不同语义空间。
避免 Attribute.Health 和 Blackboard.Health 误用。
编辑器筛选。
生成不同类型的强类型 Key。
```

### 4.3 NameEntry

```csharp
[Serializable]
public sealed class NameEntry
{
    public int id;
    public string name;
    public NameNamespace nameNamespace;
    public string sourceName;
    public string description;
    public bool deprecated;
    public string replacedByName;
    public int networkIndex = -1;
}
```

字段说明：

```text
id:
  稳定 Id，不随排序变化。

name:
  原始名字，例如 State.Stunned、Data.Damage、Health。

nameNamespace:
  名字所属语义空间。

sourceName:
  来源，例如 Core、Combat、Generated、DLC_01。

description:
  编辑器说明。

deprecated:
  是否废弃。

replacedByName:
  废弃后的替代名。

networkIndex:
  未来网络压缩同步用。
```

### 4.4 SerializedNameId

资产中不要只保存 Id，也不要只保存字符串。

推荐：

```csharp
[Serializable]
public struct SerializedNameId
{
    public int id;
    public string name;
    public NameNamespace nameNamespace;
}
```

加载规则：

```text
优先使用 id。
id 无效时用 namespace + name 查找。
命中 Redirect 时自动迁移。
id 与 name 不一致时输出警告。
```

---

## 5. NameRegistry

### 5.1 职责

`NameRegistry` 是底层名字注册中心。

它负责：

```text
注册名字。
校验名字。
Name + Namespace -> NameId。
NameId -> NameEntry。
Redirect。
Deprecated。
Source。
DatabaseVersion / Hash。
NetworkIndex。
```

### 5.2 推荐接口

```csharp
public static class NameRegistry
{
    public static bool IsInitialized { get; }
    public static int Version { get; }
    public static string Hash { get; }

    public static void Initialize(NameRegistryDatabase database);

    public static NameId RequestName(NameNamespace ns, string name, bool errorIfNotFound = true);
    public static bool TryRequestName(NameNamespace ns, string name, out NameId id);

    public static bool IsValid(NameId id);
    public static string GetName(NameId id);
    public static NameNamespace GetNamespace(NameId id);
    public static NameEntry GetEntry(NameId id);

    public static NameId ResolveRedirect(NameNamespace ns, string oldName, out bool wasRedirected);

    public static int GetNetworkIndex(NameId id);
    public static NameId FromNetworkIndex(NameNamespace ns, int networkIndex);
}
```

### 5.3 严禁运行时静默注册

不要允许：

```text
RequestName 找不到时自动创建。
```

原因：

```text
拼写错误会变成真实名字。
客户端和服务器 Name 表不一致。
回放和存档不可复现。
```

推荐：

```text
编辑器允许创建。
初始化阶段允许合并 Source。
运行时默认只读。
```

---

## 6. NameRegistryDatabase

### 6.1 职责

`NameRegistryDatabase` 是 ScriptableObject 资产。

```csharp
[CreateAssetMenu(menuName = "UGAS/Name Registry Database")]
public sealed class NameRegistryDatabase : ScriptableObject
{
    [SerializeField] private List<NameSource> sources;
    [SerializeField] private List<NameRedirect> redirects;
    [SerializeField] private int version;
    [SerializeField] private string hash;
}
```

### 6.2 NameSource

```csharp
[Serializable]
public sealed class NameSource
{
    public string name;
    public NameSourceType type;
    public List<NameEntry> entries;
}

public enum NameSourceType
{
    Native,
    ScriptableObject,
    Generated,
    ExternalTable,
    RuntimeExtension
}
```

### 6.3 NameRedirect

```csharp
[Serializable]
public sealed class NameRedirect
{
    public NameNamespace nameNamespace;
    public string oldName;
    public string newName;
}
```

用于：

```text
SetByCaller Key 重命名。
Attribute 名重命名。
Blackboard Key 重命名。
Event Key 重命名。
```

GameplayTag 可以有自己的 Redirect 系统，但底层也可以复用 NameRedirect。

---

## 7. 和 GameplayTag 的关系

### 7.1 GameplayTag 不应该只是 NameId

虽然 GameplayTag 可以内部持有 `NameId` 或 `int Id`，但它不等同于 NameId。

因为 GameplayTag 有额外规则：

```text
层级路径。
父子匹配。
TagTree。
TagQuery。
Restricted Tag。
GameplayTagContainer。
GameplayTagCountContainer。
GameplayTag Picker。
```

NameId 只负责名字稳定性。

GameplayTag 负责玩法语义。

### 7.2 推荐结构

可以这样设计：

```csharp
public readonly struct GameplayTag
{
    public readonly NameId NameId;
}
```

也可以这样设计：

```csharp
public readonly struct GameplayTag
{
    public readonly int Id;
}
```

如果使用独立 `int Id`，TagRegistry 内部也应把 Path 注册进 NameRegistry。

推荐第一版：

```text
GameplayTag 对外是独立类型。
内部可以使用 NameId。
不要让外部把任意 NameId 当 GameplayTag 使用。
```

### 7.3 示例

```text
NameRegistry:
  GameplayTag / State.Stunned -> NameId 1001

GameplayTagRegistry:
  NameId 1001 -> GameplayTag State.Stunned
  ParentCache: State.Stunned -> State
```

---

## 8. 推荐使用 NameId 的系统

### 8.1 GameplayTag

用途：

```text
Tag Path 底层 Id。
```

但仍通过 GameplayTagRegistry 做层级和 Query。

### 8.2 GameplayAttributeId

属性标识可以使用 NameId。

```csharp
public readonly struct GameplayAttributeId
{
    public readonly NameId NameId;
}
```

示例：

```text
Attribute / Health
Attribute / MaxHealth
Attribute / AttackPower
```

### 8.3 SetByCallerKey

SetByCaller 不建议长期用裸 string。

```csharp
public readonly struct SetByCallerKey
{
    public readonly NameId NameId;
}
```

示例：

```text
SetByCaller / Data.Damage
SetByCaller / Data.Duration
SetByCaller / Data.Radius
```

### 8.4 BlackboardKey

AbilityBlackboard 可使用强类型 Key。

```csharp
public readonly struct BlackboardKey<T>
{
    public readonly NameId NameId;
}
```

示例：

```text
Blackboard / Target
Blackboard / ChargeTime
Blackboard / HitLocation
```

### 8.5 GameplayMessage / GameplayEvent

如果事件使用 GameplayTag，则可以不单独用 NameId。

但如果有非层级事件 Key，也可以用：

```text
GameplayEvent / Animation.Hit
GameplayMessage / Combat.DamageDealt
```

建议：

```text
玩法语义事件优先使用 GameplayTag。
局部技术事件可使用 NameId。
```

### 8.6 AnimationEventKey

Unity 动画事件通常是字符串。

如果 GAS AbilityTask 要等待动画事件，建议统一为：

```text
AnimationEvent / Hit
AnimationEvent / Fire
AnimationEvent / ComboWindowOpen
```

运行时转换为 NameId，避免散落字符串。

---

## 9. 类型安全包装

不要到处传 `NameId`，否则会出现：

```text
把 AttributeId 传给 BlackboardKey。
把 SetByCallerKey 传给 GameplayCue。
```

推荐为不同语义创建强类型包装：

```csharp
public readonly struct GameplayAttributeId
{
    public readonly NameId NameId;
}

public readonly struct SetByCallerKey
{
    public readonly NameId NameId;
}

public readonly struct GameplayCueKey
{
    public readonly NameId NameId;
}
```

这样底层共享 NameRegistry，外部保持类型安全。

---

## 10. 编辑器支持

第一版应提供：

```text
NameRegistryDatabase Inspector
Name Picker
按 Namespace 过滤
重复校验
非法字符校验
Redirect 编辑
Deprecated 标记
Source 筛选
生成 C# 常量
显示 NameId、NetworkIndex、Source
```

但注意：GameplayTag 仍应使用专门 TagPicker，因为它有层级树和 Restricted Tag 语义。

NamePicker 更适合：

```text
SetByCallerKey
BlackboardKey
AnimationEventKey
AttributeId
```

---

## 11. 序列化、存档、网络、回放

### 11.1 资产序列化

推荐保存：

```text
id
name
namespace
```

### 11.2 存档

开发期可保存：

```text
namespace + name
```

上线稳定后可保存：

```text
id + registryHash
```

### 11.3 网络

未来同步时可使用：

```text
networkIndex
```

前提：

```text
客户端和服务器 NameRegistryHash 一致。
```

### 11.4 回放

回放文件应记录：

```text
NameRegistryVersion
NameRegistryHash
```

否则回放里的 Id 可能无法解释。

---

## 12. 生命周期 / 数据流

初始化流程：

```text
加载 NameRegistryDatabase
加载 Native Name Definitions
合并 NameSource
应用 Redirect
校验重复
校验非法字符
构建 Name -> Id
构建 Id -> Entry
生成 NetworkIndex
生成 RegistryHash
冻结 Registry 为只读
```

运行时使用：

```text
配置资产反序列化 SerializedNameId
  -> NameRegistry.Resolve
  -> 得到 NameId
  -> 上层包装类型使用 NameId
```

---

## 13. 第一版必须实现清单

既然目标是大型项目落地版本，第一版必须实现：

```text
NameId struct
SerializedNameId
NameNamespace
NameEntry
NameSource
NameRedirect
NameRegistryDatabase
NameRegistry
稳定 Id
Namespace + Name -> Id
Id -> Entry
Redirect
Deprecated
Source
Version
Hash
NetworkIndex placeholder
NamePicker 基础编辑器
类型安全包装设计
```

可以延后接业务，但必须预留：

```text
真正网络压缩同步
完整资产批量迁移工具
外部表热加载
Mod / DLC 动态 Source 合并
ECS BlobAsset Baking
```

---

## 14. 常见坑

### 14.1 NameId 变成万能 ObjectId

不要让 `NameId` 什么都表示。

它只表示名字，不表示实体、资产实例、Effect 实例、Ability 实例。

这些应使用：

```text
EntityId
AssetGuid
GameplayEffectHandle
AbilityHandle
```

### 14.2 不做 Namespace

如果没有 Namespace，容易出现：

```text
Health 是 Attribute？
Health 是 BlackboardKey？
Health 是 UI Message？
```

### 14.3 运行时自动注册

不要运行时静默注册名字。

这会破坏网络、回放和调试一致性。

### 14.4 只存 Id 不存 name snapshot

资产中只存 Id 会让开发期调试和冲突排查非常痛苦。

### 14.5 直接把 NameId 当 GameplayTag

GameplayTag 需要层级、Query、Container、CountContainer。

NameId 不提供这些。

---

## 15. 验收用例

### 15.1 基础查找

```text
注册 Attribute / Health。
RequestName(Attribute, Health) 返回稳定 NameId。
GetName(NameId) 返回 Health。
```

### 15.2 Namespace 隔离

```text
Attribute / Health
Blackboard / Health
```

两者名字相同，但 NameId 不同，不能互相误用。

### 15.3 Redirect

```text
SetByCaller / Data.Dmg -> SetByCaller / Data.Damage
```

旧资产加载时能自动解析到新 Key，并提示迁移。

### 15.4 Deprecated

废弃 `Data.OldDamage` 后，新资产不能选择它，旧资产仍能加载并给出警告。

### 15.5 RegistryHash

修改 NameEntry 后 Hash 改变。

未来网络或回放可用 Hash 校验一致性。

---

## 16. 最终建议

Unity GAS 如果要支撑大型项目，应该从第一版就有类似 UE `FName` 思想的底层名字系统。

但这套系统应叫： 

```text
NameId / StableName
```

而不是直接复刻完整 `FName`。

最终结构：

```text
NameId:
  底层稳定名字 Id。

NameRegistry:
  负责名字注册、反查、Redirect、Hash、NetworkIndex。

GameplayTag:
  建立在 NameId 思想之上，但保留独立玩法语义系统。

AttributeId / SetByCallerKey / BlackboardKey:
  使用 NameId 做底层稳定标识，并用强类型包装保持边界。
```

一句话：

```text
NameId 是 GAS 的名字地基。
GameplayTag 是 GAS 的玩法语义语言。
两者应该协作，但不能混为一谈。
```

