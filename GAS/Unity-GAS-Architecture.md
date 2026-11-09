# Unity GAS-Inspired Ability System Architecture

本文档描述一套在 Unity 中复刻 Unreal GAS 工作流的完整能力系统架构。

目标不是一比一复制 Unreal Engine 的 C++、UObject、Replication、GameplayTask、Montage 等底层实现，而是复刻 GAS 的核心设计思想：

- 角色能力由统一的 Ability System Component 管理。
- 技能、Buff、Debuff、伤害、治疗、冷却、消耗都通过统一的 Gameplay Effect 管线表达。
- 属性变化不直接散落在角色脚本里，而是通过 Attribute、Modifier、Execution Calculation 统一计算。
- 状态判断、技能阻塞、技能取消、免疫、UI 查询都依赖 Gameplay Tag。
- 技能释放过程可以拆成可组合的任务与事件。
- 表现层通过 Gameplay Cue 解耦，不污染战斗逻辑。
- 当前暂不实现联网，但核心数据结构和流程预留服务端权威、预测、同步、回滚的扩展位置。

---

## 1. 阅读与实现标注

为了边实现边阅读，本文会在相关章节直接标注概念来源：

```text
原生 GAS 核心：
  Unreal GAS 工作流中本来就非常核心，Unity 版应优先复刻。

Unity 强化版建议：
  不是原生 GAS 必备概念，而是为了 Unity 项目的长期演进、调试、性能、AI、联网或工具链而预留。

第一版分寸：
  说明当前阶段应该完整实现、轻量预留，还是暂时只理解概念。
```

实现时可以按这个原则判断优先级：

```text
先把原生 GAS 语义跑通。
再把 Unity 强化版的入口和数据边界留好。
最后根据项目规模逐步实现强化能力。
```
---

## 2. 总体目标

### 2.1 要实现的体验

这套系统最终应该让 Unity 项目的战斗玩法开发变成一种数据驱动工作流：

1. 策划在编辑器中创建属性、标签、技能、效果、冷却、消耗和表现配置。
2. 程序提供通用的运行时框架、公式扩展点和调试工具。
3. 角色只需要挂载 `AbilitySystemComponent`，再配置初始属性、初始技能、初始效果。
4. 技能释放统一走 ASC 的激活流程。
5. Buff、Debuff、伤害、治疗、冷却、消耗都表现为 Gameplay Effect。
6. UI 通过监听属性、标签、冷却和 Active Effect 变化更新。
7. 后续如需联网，可以把 ASC 的命令、属性、标签、Active Effect、预测状态接到网络层。

### 2.2 非目标

第一版不追求：

- 完全复刻 Unreal GAS 源码结构。
- 依赖 Unreal 的反射、UObject 生命周期、ActorComponent、GameplayTask 原实现。
- 第一阶段就支持完整客户端预测。
- 第一阶段就支持复杂编辑器图形化编排。
- 第一阶段就支持所有叠层、免疫、溢出、捕获快照规则。

### 2.3 推荐定位

推荐把它定义为：

```text
Unity Gameplay Ability Framework
```

也可以简称：

```text
UGAS
```

它是一套 GAS-inspired 框架，而不是 Unreal GAS 的移植。

---

## 3. 从基础到总装的实现顺序

本文后续章节按“实现依赖从低到高”排列，目标是边看边做时尽量不需要来回翻。

推荐实现顺序：

```text
1. NameId / StableName / NameRegistry
2. GameplayTag / GameplayTagContainer / GameplayTagQuery
3. AttributeId / AttributeSet / AttributeAggregator
4. GameplayEffectAsset / Modifier / ExecutionCalculation
5. GameplayEffectSpec / ActiveGameplayEffect / Stacking / EffectQuery
6. Cost / Cooldown
7. TargetData / GameplayEvent / GameplayCue
8. AbilityTask / AbilityBlackboard
9. GameplayAbilityAsset / AbilitySpec / AbilityInstance
10. Ability 阻塞、取消、激活条件
11. AbilitySystemComponent 总装
12. Input Router / Command Processor
13. Editor / Debugger / NetworkAdapter / ECS 后端等扩展
```

阅读时可以这样理解依赖关系：

```text
NameId 是底层名字基础设施，对标 UE FName 的工程价值，但不是原生 GAS 玩法概念。
Tag 是语义基础，应建立在稳定名字/Id 思想之上。
Attribute 依赖 AttributeId，不应依赖 Ability。
Effect 依赖 Tag 和 Attribute。
ActiveEffect 依赖 EffectSpec 和时间推进。
Ability 依赖 Effect、Tag、TargetData、Task。
ASC 依赖以上几乎所有模块，所以应作为总装入口放到后面实现。
Input 依赖 ASC，所以最后再接入。
```

如果某章出现“Unity 强化版建议”，它表示这不是原生 GAS 必备概念，可以先预留接口，后续再补完整实现。

## 4. 高层架构

### 4.1 模块分层

推荐将系统拆成以下模块：

```text
Game.AbilitySystem.Core
Game.AbilitySystem.Tags
Game.AbilitySystem.Attributes
Game.AbilitySystem.Effects
Game.AbilitySystem.Abilities
Game.AbilitySystem.Tasks
Game.AbilitySystem.Targeting
Game.AbilitySystem.Cues
Game.AbilitySystem.Input
Game.AbilitySystem.Debugging
Game.AbilitySystem.Editor
Game.AbilitySystem.Networking
```

各模块职责：

| 模块 | 职责 |
| --- | --- |
| Core | ASC、句柄、上下文、通用事件、生命周期 |
| Tags | Gameplay Tag 注册、容器、查询、编辑器 |
| Attributes | Attribute Set、聚合器、属性事件、Clamp |
| Effects | Gameplay Effect、Spec、Active Effect、Modifier、Execution |
| Abilities | Gameplay Ability、Ability Spec、激活、取消、提交 |
| Tasks | 等待输入、动画、事件、时间、目标、碰撞等异步任务 |
| Targeting | Target Data、目标选择、射线、范围、筛选 |
| Cues | 逻辑到表现的事件桥接 |
| Input | 输入 ID、Input Action 到 Ability 的绑定 |
| Debugging | 运行时检查器、日志、可视化 |
| Editor | ScriptableObject 资产编辑、Tag 面板、Effect 调试 |
| Networking | 暂不实现，但保留接口、序列化、预测扩展点 |

### 4.1.1 架构边界提示

原生 GAS 核心：

```text
ASC、Ability、Effect、Attribute、Tag、Cue、Task、TargetData 是 GAS 工作流的主干。
```

Unity 强化版建议：

```text
Command、Message Bus、Event Log、Simulation Tick、ECS Backend、Asset Baking 不是原生 GAS 必备概念。
它们是为了让 Unity 版能力系统更容易接 AI、联网、回放、调试和大规模单位。
```

第一版分寸：

```text
必须先实现 GAS 主干模块。
强化模块可以先做接口和空实现，不要一开始把 ECS、Graph、Replay、完整网络预测全部做完。
```
### 4.2 运行时核心对象

```text
AbilitySystemComponent
  - AttributeSetCollection
  - GameplayTagContainer
  - AbilitySpecContainer
  - ActiveGameplayEffectContainer
  - GameplayCueDispatcher
  - AbilityEventBus
  - PredictionAdapter placeholder
```

ASC 是所有能力系统逻辑的中枢。

角色、怪物、召唤物、机关、可破坏物都可以拥有 ASC。只要一个对象需要属性、状态、Buff、技能或者受到效果影响，就应该通过 ASC 接入系统。

### 4.3 数据资产

Unity 中应优先用 `ScriptableObject` 承载设计时配置：

```text
GameplayTagDatabase
AttributeSetDefinition
GameplayAbilityAsset
GameplayEffectAsset
GameplayCueAsset
AbilityInputBindingAsset
TargetingProfileAsset
EffectCalculationAsset
```

运行时不要直接修改这些资产，而是从资产创建运行时实例：

```text
GameplayAbilityAsset -> AbilitySpec -> AbilityInstance
GameplayEffectAsset  -> GameplayEffectSpec -> ActiveGameplayEffect
```

---

## 5. Unreal GAS 概念到 Unity 的映射

| Unreal GAS 概念 | Unity 对应设计 | 说明 |
| --- | --- | --- |
| AbilitySystemComponent | `AbilitySystemComponent : MonoBehaviour` | 核心运行时组件 |
| GameplayAbility | `GameplayAbilityAsset : ScriptableObject` + `AbilityInstance` | 配置与运行时状态分离 |
| GameplayEffect | `GameplayEffectAsset : ScriptableObject` | 伤害、治疗、Buff、冷却、消耗 |
| GameplayEffectSpec | `GameplayEffectSpec` | 一次应用的上下文实例 |
| ActiveGameplayEffect | `ActiveGameplayEffect` | 持续效果运行时实例 |
| AttributeSet | `AttributeSetInstance` / `AttributeSetDefinition` | 属性组 |
| GameplayAttribute | `GameplayAttributeId` + `AttributeValue` | 属性标识与数值 |
| GameplayTag | `GameplayTag` / `GameplayTagContainer` | 层级状态标签 |
| GameplayCue | `GameplayCueAsset` + `GameplayCueDispatcher` | 表现层事件 |
| AbilityTask | `AbilityTask` / `IAbilityTask` | 技能异步节点 |
| TargetData | `TargetData` | 目标数据抽象 |
| PredictionKey | `PredictionKey` placeholder | 后续联网预测 |
| Replication | `IAbilityNetworkAdapter` placeholder | 后续网络同步 |

### 5.1 边界速查

本表中的 Unreal GAS 概念是复刻工作流的基础。后续文档中出现的 `Command Pattern`、`Gameplay Message Bus`、`Event Sourcing`、`ECS / DOTS`、`Ability Graph`、`Statechart`、`Utility AI` 等，属于 Unity 强化版扩展，不是原生 GAS 的必要组成。

推荐实现时把它们分成三层：

```text
GAS 核心层：
  Ability / Effect / Attribute / Tag / Cue / Task / TargetData

Unity 工程增强层：
  Command / MessageBus / EventLog / SimulationTick / Blackboard / Snapshot

项目形态适配层：
  ECS / AbilityGraph / Statechart / UtilityAI / Replay / NetworkPrediction
```
---

## 6. Gameplay Tag 系统

### 6.1 Tag 的意义

Gameplay Tag 是系统中最重要的低耦合判断工具。

它可以表达：

- 状态：`State.Stunned`
- 行为：`Ability.Attack.Light`
- 冷却：`Cooldown.Fireball`
- 伤害类型：`Damage.Fire`
- 元素：`Element.Ice`
- 阵营：`Team.Player`
- 武器：`Weapon.Sword`
- 免疫：`Immune.CrowdControl`
- 事件：`Event.Animation.Hit`

Tag 的优势是：

- 比 enum 更易扩展。
- 比字符串更可控。
- 支持层级匹配。
- 适合编辑器选择。
- 能把技能、Buff、UI、AI、表现事件连接起来。

### 6.1.1 Unity 强化版提示：Gameplay Ontology

原生 GAS 核心：

```text
GameplayTag 是 GAS 的核心概念，用来表达状态、条件、分类、阻塞、冷却和事件。
```

Unity 强化版建议：

```text
Gameplay Ontology 不是原生 GAS 概念。
它是项目级“玩法语义词汇表”，用于规定 Tag 应该如何命名和归类。
```

例如要提前约定：

```text
Damage.Fire 是伤害类型。
Element.Fire 是元素属性。
Effect.Debuff.Burn 是持续负面效果。
State.Burning 是角色当前处于燃烧状态。
Immune.CrowdControl 是否能免疫 Knockback，要在语义词汇表中说明。
```

这样做不是为了多一层概念，而是为了避免项目后期出现 `FireDamage`、`Damage.Fire`、`Element.Fire`、`Burning`、`Debuff.Burn` 混用，导致技能、UI、AI、驱散、免疫规则互相不认识。
### 6.2 Tag 层级匹配

一个完整 Tag：

```text
Damage.Fire.Burn
```

应该能够匹配：

```text
Damage
Damage.Fire
Damage.Fire.Burn
```

但 `Damage.Fire` 不应该匹配 `Damage.Ice`。

### 6.3 Tag 数据结构

```csharp
public readonly struct GameplayTag
{
    public readonly int Id;
    public readonly string Path;
}

public sealed class GameplayTagContainer
{
    public bool HasTag(GameplayTag tag, TagMatchType matchType = TagMatchType.ExactOrParent);
    public bool HasAny(GameplayTagContainer other);
    public bool HasAll(GameplayTagContainer other);
    public void AddTag(GameplayTag tag);
    public void RemoveTag(GameplayTag tag);
}
```

### 6.4 Tag 来源

ASC 的最终 Owned Tags 应该来自多个来源：

```text
Loose Tags
  手动添加的运行时标签，比如剧情状态、交互状态。

Ability Activation Tags
  技能激活期间临时授予的标签。

Gameplay Effect Granted Tags
  Buff/Debuff/冷却等持续效果授予的标签。

Equipment Tags
  装备、武器、套装授予的标签。

Passive Tags
  被动能力或天赋授予的标签。
```

内部可用引用计数管理：

```csharp
public sealed class GameplayTagCountContainer
{
    public void AddTag(GameplayTag tag, object source);
    public void RemoveTag(GameplayTag tag, object source);
    public int GetTagCount(GameplayTag tag);
}
```

同一个 Tag 可能由多个 Effect 同时授予，只有所有来源都移除后，Tag 才应真正消失。

### 6.5 Tag Query

简单容器只能表达 HasAny / HasAll。

复杂条件建议用 `GameplayTagQuery`：

```text
All:
  State.Alive
  Weapon.Sword

None:
  State.Stunned
  State.Silenced

Any:
  Stance.Ground
  Stance.Air
```

推荐结构：

```csharp
public sealed class GameplayTagQuery
{
    public GameplayTagContainer AllTags;
    public GameplayTagContainer AnyTags;
    public GameplayTagContainer NoneTags;

    public bool Matches(GameplayTagContainer container);
}
```

---

## 7. Attribute 系统

### 7.1 属性类型

常见属性：

```text
Health
MaxHealth
Mana
MaxMana
Stamina
MaxStamina
AttackPower
AbilityPower
Defense
MoveSpeed
AttackSpeed
CriticalChance
CriticalDamage
CooldownReduction
DamageReduction
Shield
Poise
Tenacity
```

属性可分为：

| 类型 | 示例 | 说明 |
| --- | --- | --- |
| Primary | Strength, Intelligence | 一级属性 |
| Secondary | AttackPower, MaxHealth | 由一级属性推导或受装备影响 |
| Vital | Health, Mana, Stamina | 当前资源 |
| Meta | IncomingDamage, IncomingHeal | 临时计算属性，不长期显示 |

### 7.2 BaseValue 与 CurrentValue

每个属性建议拥有：

```text
BaseValue
CurrentValue / EvaluatedValue
```

两者含义：

- `BaseValue`：基础值，可被升级、装备、永久成长改变。
- `CurrentValue`：聚合 Modifier 后的最终值。

对 `Health` 这类资源属性可以更细分：

```text
MaxHealth = 可聚合属性
Health = 当前资源值
```

`MaxHealth` 变化时，`Health` 是否等比例调整，应由 Attribute Hook 决定。

### 7.3 Attribute Aggregator

持续 Buff 不应该反复直接改属性值，而应该挂载 Modifier，由聚合器计算最终值。

```csharp
public sealed class AttributeAggregator
{
    public float BaseValue { get; private set; }
    public IReadOnlyList<AttributeModifier> Modifiers { get; }

    public float Evaluate(GameplayEvaluationContext context);
}
```

推荐计算顺序：

```text
Final = Override ?? (((Base + Additive) * Multiplicative) / Division)
```

Modifier 操作类型：

```csharp
public enum AttributeModifierOp
{
    Add,
    Multiply,
    Divide,
    Override
}
```

### 7.3.1 Unity 强化版提示：Modifier Algebra

原生 GAS 核心：

```text
属性由 BaseValue、CurrentValue、Modifier 和 Aggregator 共同计算。
```

Unity 强化版建议：

```text
Modifier Algebra 不是原生 GAS 核心概念。
它是为了让 Unity 项目的数值乘区、加区、覆盖、Clamp 规则更清楚。
```

如果只使用 `Add / Multiply / Override`，后期很容易产生歧义：

```text
两个 +20% 是相加成 +40%，还是连续乘成 1.44？
装备攻击力 +20% 和 Buff 攻击力 +20% 是否同乘区？
火伤提高 20% 和最终伤害提高 20% 谁先算？
```

推荐早期明确数值通道：

```text
BaseAdd
PercentAdd
PercentMultiply
FinalAdd
FinalMultiply
Override
ClampMin
ClampMax
```

第一版可以仍然只实现少量通道，但文档和调试器中要把规则写死，避免后期数值体系变成口头约定。
### 7.4 属性变化 Hook

Attribute Set 应支持以下钩子：

```csharp
public interface IAttributeChangeHook
{
    void PreAttributeBaseChange(AttributeChangeContext context);
    void PostAttributeBaseChange(AttributeChangeContext context);
    void PreAttributeCurrentChange(AttributeChangeContext context);
    void PostAttributeCurrentChange(AttributeChangeContext context);
}
```

典型用途：

- Clamp `Health` 到 `[0, MaxHealth]`。
- Clamp `Mana` 到 `[0, MaxMana]`。
- Health 归零时添加 `State.Dead`。
- MaxHealth 增加时按比例补血。
- MoveSpeed 变化时通知移动组件。

### 7.5 属性变更事件

UI 和表现层不应该轮询属性。

推荐事件：

```csharp
public readonly struct AttributeChangedEvent
{
    public GameplayAttributeId AttributeId;
    public float OldValue;
    public float NewValue;
    public AbilitySystemComponent Source;
    public GameplayEffectSpec SourceEffectSpec;
}
```

ASC 对外暴露：

```csharp
public event Action<AttributeChangedEvent> AttributeChanged;
```

---

## 8. Gameplay Effect

### 8.1 Gameplay Effect 的定位

Gameplay Effect 是系统中统一表达数值和状态变化的对象。

它可以表示：

```text
Instant Damage
Instant Heal
Mana Cost
Cooldown
Haste Buff
Poison Debuff
Stun
Shield
Aura
Equipment Passive
Periodic Damage
Temporary Ability Grant
```

### 8.2 持续策略

```csharp
public enum EffectDurationPolicy
{
    Instant,
    Duration,
    Infinite
}
```

含义：

| 策略 | 说明 | 示例 |
| --- | --- | --- |
| Instant | 立即执行，不进入 Active Effect 列表 | 伤害、治疗、资源消耗 |
| Duration | 持续一段时间 | 眩晕、加速、冷却 |
| Infinite | 无限持续，直到主动移除 | 装备光环、被动 |

### 8.3 GameplayEffectAsset

```csharp
public sealed class GameplayEffectAsset : ScriptableObject
{
    public EffectDurationPolicy DurationPolicy;
    public ScalableFloat Duration;
    public ScalableFloat Period;

    public List<AttributeModifierDefinition> Modifiers;
    public List<GameplayEffectExecutionAsset> Executions;

    public GameplayTagContainer GrantedTags;
    public GameplayTagQuery ApplicationTagRequirements;
    public GameplayTagContainer OngoingRequiredTags;
    public GameplayTagContainer RemoveGameplayEffectsWithTags;

    public EffectStackingPolicy StackingPolicy;
    public GameplayCueContainer GameplayCues;
}
```

### 8.4 GameplayEffectSpec

Spec 是一次 Effect 应用的运行时上下文。

```csharp
public sealed class GameplayEffectSpec
{
    public GameplayEffectAsset Definition;
    public AbilitySystemComponent SourceASC;
    public AbilitySystemComponent TargetASC;
    public int Level;

    public GameplayTagContainer CapturedSourceTags;
    public GameplayTagContainer CapturedTargetTags;
    public Dictionary<string, float> SetByCallerMagnitudes;
    public TargetData TargetData;
    public PredictionKey PredictionKey;
}
```

为什么必须有 Spec：

- 同一个 Effect 资产会被不同角色使用。
- 同一个技能等级可能不同。
- 本次伤害倍率可能由蓄力、距离、暴击决定。
- Source 和 Target 的标签会影响公式。
- 后续联网预测需要追踪这一次应用。

### 8.4.1 Unity 强化版提示：Authoring / Runtime 分离

原生 GAS 核心：

```text
GameplayEffect 和 GameplayEffectSpec 分离，是 GAS 工作流中非常关键的思想。
```

Unity 强化版建议：

```text
Authoring / Runtime 分离不是 GAS 独有概念，但在 Unity 中必须更严格执行。
ScriptableObject 只表示配置，运行时状态必须进入 Spec、Instance、Handle、Container。
```

不要在 `GameplayEffectAsset` 上保存：

```text
当前剩余时间
当前层数
本次施法者
本次目标
本次暴击结果
```

这些应保存在：

```text
GameplayEffectSpec
ActiveGameplayEffect
GameplayEffectHandle
GameplayEventRecord
```

第一版分寸：

```text
Effect Asset 可以先简单。
但 Spec 和 ActiveEffect 必须存在。
这是未来接叠层、回放、调试、联网、ECS Baking 的基础。
```
### 8.5 Modifier

Modifier 表达对某个属性的修改。

```csharp
public sealed class AttributeModifierDefinition
{
    public GameplayAttributeId Attribute;
    public AttributeModifierOp Operation;
    public ModifierMagnitude Magnitude;
    public GameplayTagQuery SourceTagRequirements;
    public GameplayTagQuery TargetTagRequirements;
}
```

Magnitude 来源：

```csharp
public enum ModifierMagnitudeType
{
    Constant,
    CurveByLevel,
    AttributeBased,
    SetByCaller,
    CustomCalculation
}
```

### 8.6 Execution Calculation

复杂公式应该放到 Execution 中，而不是塞进 Modifier 配置。

示例：伤害计算。

```text
Damage = AttackPower * SkillMultiplier
Damage *= CriticalMultiplier
Damage *= ElementMultiplier
Damage *= VulnerabilityMultiplier
Damage -= DefenseMitigation
Damage = Max(Damage, MinDamage)
```

推荐接口：

```csharp
public abstract class GameplayEffectExecutionAsset : ScriptableObject
{
    public abstract void Execute(GameplayEffectExecutionContext context);
}
```

Context 提供：

```csharp
public sealed class GameplayEffectExecutionContext
{
    public GameplayEffectSpec Spec;
    public AbilitySystemComponent SourceASC;
    public AbilitySystemComponent TargetASC;

    public float GetSourceAttribute(GameplayAttributeId id);
    public float GetTargetAttribute(GameplayAttributeId id);
    public float GetSetByCaller(string key, float defaultValue = 0);

    public void AddOutputModifier(GameplayAttributeId attribute, AttributeModifierOp op, float magnitude);
    public void AddGameplayCue(GameplayTag cueTag);
}
```

### 8.7 Periodic Effect

周期效果如中毒、燃烧、持续治疗：

```text
Duration = 6s
Period = 1s
每 1s 执行一次 Instant 部分
```

Active Effect 需要保存：

```csharp
public sealed class ActiveGameplayEffect
{
    public GameplayEffectHandle Handle;
    public GameplayEffectSpec Spec;
    public float StartTime;
    public float Duration;
    public float Period;
    public float NextTickTime;
    public int StackCount;
}
```

---

## 9. Active Gameplay Effect Container

### 9.1 职责

`ActiveGameplayEffectContainer` 负责管理目标身上的所有持续效果。

职责：

- 添加 Duration / Infinite Effect。
- 移除 Effect。
- Tick Duration。
- Tick Period。
- 处理叠层。
- 更新 Attribute Aggregator。
- 授予与移除 Granted Tags。
- 触发 Gameplay Cue OnAdd / OnRemove / WhileActive。
- 广播 UI 所需事件。

### 9.1.1 Unity 强化版提示：ECS 可选后端

原生 GAS 核心：

```text
ActiveGameplayEffectContainer 负责持续效果、周期 Tick、持续 Modifier、Granted Tags 的生命周期。
```

Unity 强化版建议：

```text
ECS / DOTS 不是原生 GAS 概念。
但 ActiveEffect Tick、Cooldown、周期伤害、属性聚合非常适合未来迁移到 ECS System 批处理。
```

第一版可以仍然用普通 C# 容器和 MonoBehaviour ASC，但运行时数据最好保持可序列化、可快照、少 UnityEngine.Object 依赖：

```text
ActiveEffectHandle
EffectAssetId
SourceEntityId
StartTick
DurationTicks
PeriodTicks
StackCount
```

这样后续从：

```text
MonoBehaviour ASC Tick ActiveEffectContainer
```

迁移到：

```text
ECS GameplayEffectTickSystem 批量处理 ActiveEffectBuffer
```

会轻很多。
### 9.2 添加效果流程

```text
ApplyGameplayEffect
  -> 检查 ApplicationTagRequirements
  -> 处理 RemoveGameplayEffectsWithTags
  -> 判断 Instant / Duration / Infinite
  -> Instant: 执行 Modifier 和 Execution 后结束
  -> Duration/Infinite: 创建 ActiveGameplayEffect
  -> 处理 Stacking
  -> 注册 GrantedTags
  -> 注册持续 Modifier 到 AttributeAggregator
  -> 触发 GameplayCue OnAdd
  -> 广播 ActiveEffectAdded
```

### 9.3 移除效果流程

```text
RemoveActiveEffect
  -> 找到 ActiveGameplayEffect
  -> 注销持续 Modifier
  -> 移除 GrantedTags 引用
  -> 触发 GameplayCue OnRemove
  -> 广播 ActiveEffectRemoved
```

### 9.4 Ongoing Tag Requirements

某些效果只有在满足标签条件时才持续生效。

例如：

```text
燃烧光环要求目标仍在 Aura.Range 内。
格挡 Buff 要求角色拥有 State.Blocking。
潜行 Buff 要求角色没有 State.Attacking。
```

实现方式：

- Active Effect 不一定被移除。
- 当 Ongoing 条件不满足时，暂停 Modifier 和 Granted Tags。
- 条件恢复后重新启用。

---

## 10. 叠层系统

### 10.1 基础叠层策略

推荐第一版支持：

```csharp
public sealed class EffectStackingPolicy
{
    public bool Stackable;
    public int StackLimit;
    public StackDurationRefreshPolicy DurationRefreshPolicy;
    public StackPeriodResetPolicy PeriodResetPolicy;
    public StackExpirationPolicy ExpirationPolicy;
}
```

### 10.2 常见规则

```text
不叠层，重复应用刷新时间
不叠层，重复应用无效
可叠层，重复应用刷新总时间
可叠层，重复应用只增加层数不刷新时间
可叠层，每层独立计时
到达上限后触发溢出效果
持续时间结束时移除一层
持续时间结束时全部移除
```

### 10.3 推荐第一版规则

为了可控，第一版只实现：

```text
StackLimit
RefreshDurationOnSuccessfulApplication
RemoveSingleStackAndRefreshDurationOnExpiration
RemoveAllStacksOnExpiration
```

高级规则可以在第二阶段补。

---

## 11. 免疫、驱散与效果查询

### 11.1 免疫

免疫可以用 Tag Query 实现。

例如：

```text
StunEffect:
  EffectTags:
    Effect.CrowdControl.Stun

Target Has:
  Immune.CrowdControl
```

应用时检查：

```text
如果目标拥有 Immune.CrowdControl，则拒绝应用 StunEffect。
```

### 11.2 驱散

驱散本质是查询并移除 Active Effect。

```csharp
public int RemoveActiveEffects(GameplayEffectQuery query);
```

Query 可包含：

```text
EffectTags
GrantedTags
SourceASC
DurationPolicy
IsDebuff
IsBuff
```

示例：

```text
Cleanse:
  Remove effects with tags:
    Effect.Debuff
    Effect.Poison
```

### 11.3 Effect Query

```csharp
public sealed class GameplayEffectQuery
{
    public GameplayTagQuery EffectTagQuery;
    public GameplayTagQuery GrantedTagQuery;
    public AbilitySystemComponent SourceASC;
    public EffectDurationPolicy? DurationPolicy;
}
```

---

## 12. Cost 与 Cooldown

### 12.1 统一用 Gameplay Effect 表达

Cost 和 Cooldown 都应该是 Gameplay Effect。

火球术示例：

```text
CostEffect:
  Instant
  Mana Add -20

CooldownEffect:
  Duration 5s
  GrantedTags:
    Cooldown.Fireball
```

CanActivate 检查：

```text
Mana >= 20
没有 Cooldown.Fireball
没有 State.Silenced
```

### 12.2 Cooldown 查询

UI 需要查询冷却剩余时间：

```csharp
public bool TryGetCooldown(GameplayTag cooldownTag, out CooldownInfo info);

public readonly struct CooldownInfo
{
    public float Duration;
    public float RemainingTime;
    public float Normalized;
}
```

### 12.3 Cost 检查

CostEffect 不能只在 Commit 时扣资源，也要能在 CanActivate 时预检查。

推荐给 GameplayEffectAsset 提供：

```csharp
public bool CanApplyAsCost(GameplayEffectSpec spec);
```

或者独立实现：

```csharp
public interface IAbilityCostEvaluator
{
    bool CanPayCost(AbilityActivationContext context);
    void PayCost(AbilityActivationContext context);
}
```

但为了保持 GAS 风格，第一版推荐 Cost 仍使用 Gameplay Effect。

---

## 13. Targeting 与 TargetData

### 13.1 TargetData 的意义

Ability 不应该关心目标来自鼠标、锁定、AI、碰撞还是网络包。

统一用 TargetData 表达：

```csharp
public abstract class TargetData {}

public sealed class ActorTargetData : TargetData
{
    public IReadOnlyList<AbilitySystemComponent> Targets;
}

public sealed class LocationTargetData : TargetData
{
    public Vector3 Position;
    public Vector3 Direction;
}

public sealed class HitTargetData : TargetData
{
    public RaycastHit Hit;
    public AbilitySystemComponent HitASC;
}

public sealed class AreaTargetData : TargetData
{
    public Vector3 Center;
    public float Radius;
    public IReadOnlyList<AbilitySystemComponent> Targets;
}
```

### 13.2 Targeting Profile

目标选择建议数据化：

```csharp
public sealed class TargetingProfileAsset : ScriptableObject
{
    public TargetingShape Shape;
    public LayerMask LayerMask;
    public float Range;
    public float Radius;
    public GameplayTagQuery RequiredTargetTags;
    public GameplayTagQuery BlockedTargetTags;
    public TargetSortPolicy SortPolicy;
    public int MaxTargets;
}
```

### 13.3 目标筛选流程

```text
收集候选目标
  -> 距离过滤
  -> Layer 过滤
  -> 阵营过滤
  -> Tag Query 过滤
  -> 视线过滤
  -> 排序
  -> 截取 MaxTargets
  -> 输出 TargetData
```

---

## 14. Gameplay Event

### 14.1 用途

Gameplay Event 是跨模块通信的轻量事件。

可用于：

- 动画事件通知 Ability。
- 投射物命中通知 Ability。
- 受到攻击触发被动。
- 进入范围触发光环。
- 输入确认 / 取消。
- AI 行为驱动技能。

### 14.1.1 Unity 强化版提示：结构化事件日志

原生 GAS 核心：

```text
Gameplay Event 可用于驱动 Ability 内部流程。
```

Unity 强化版建议：

```text
Event Sourcing / 结构化 Event Log 不是原生 GAS 核心概念。
但它非常适合记录 GAS 运行过程，后续能直接支撑 Debug Timeline、自动测试、回放和线上问题分析。
```

建议记录事实，而不是只打文本日志：

```text
CommandReceived
AbilityActivated
CostCommitted
EffectApplied
AttributeChanged
TagAdded
TagRemoved
CueExecuted
AbilityEnded
```

关键区别：

```text
Command 是请求，可能失败。
Event 是事实，表示已经发生。
Debug.Log 是给人看的文本。
EventRecord 是给人和系统一起用的结构化数据。
```
### 14.2 事件结构

```csharp
public readonly struct GameplayEventData
{
    public GameplayTag EventTag;
    public AbilitySystemComponent InstigatorASC;
    public AbilitySystemComponent TargetASC;
    public TargetData TargetData;
    public GameplayEffectSpec EffectSpec;
    public float Magnitude;
    public object Payload;
}
```

ASC 提供：

```csharp
public void SendGameplayEvent(GameplayEventData eventData);
public IDisposable ListenGameplayEvent(GameplayTag eventTag, Action<GameplayEventData> callback);
```

---

## 15. Gameplay Cue

### 15.1 Cue 的定位

Gameplay Cue 是逻辑层和表现层之间的桥。

逻辑层只发事件：

```text
GameplayCue.Damage.Fire.Impact
GameplayCue.Buff.Haste.Add
GameplayCue.Buff.Haste.Remove
GameplayCue.Ability.Fireball.Cast
```

表现层决定：

- 播放哪个 VFX。
- 播放哪个 SFX。
- 是否显示伤害数字。
- 是否震屏。
- 是否显示 Buff 图标。
- 是否生成地面贴花。

### 15.1.1 Unity 强化版提示：Cue、Event、Message Bus 的区别

原生 GAS 核心：

```text
GameplayCue 是 GAS 中用于表现层反馈的概念。
```

Unity 强化版建议：

```text
Gameplay Message Bus 不是原生 GAS 核心概念。
它是跨系统玩法消息总线，可以让 UI、AI、任务、音频、数据分析监听同一套玩法事实。
```

建议区分：

```text
GameplayCue:
  面向表现，比如命中特效、音效、震屏、Buff 挂点。

GameplayEvent:
  面向 Ability 内部流程，比如等待动画事件、等待目标数据、等待输入释放。

GameplayMessage:
  面向跨系统广播，比如造成伤害、击杀、技能成功激活、任务进度变化。
```

它和普通消息机制的区别不在底层技术，而在约束：消息名使用 GameplayTag，Payload 强类型，关键消息进入 Event Log 和 Debug Timeline。
### 15.2 Cue 事件类型

```csharp
public enum GameplayCueEventType
{
    Execute,
    Add,
    Remove,
    WhileActive
}
```

含义：

| 类型 | 用途 |
| --- | --- |
| Execute | 一次性表现，如命中特效 |
| Add | 持续效果开始，如 Buff 上身 |
| Remove | 持续效果结束 |
| WhileActive | 持续效果存在期间 |

### 15.3 Cue Context

```csharp
public sealed class GameplayCueContext
{
    public GameplayTag CueTag;
    public GameplayCueEventType EventType;
    public AbilitySystemComponent SourceASC;
    public AbilitySystemComponent TargetASC;
    public GameplayEffectSpec EffectSpec;
    public TargetData TargetData;
    public Vector3 Location;
    public Quaternion Rotation;
    public float Magnitude;
}
```

### 15.4 Cue Dispatcher

```csharp
public interface IGameplayCueDispatcher
{
    void HandleCue(GameplayCueContext context);
}
```

第一版可以简单做成：

- CueTag 到 Prefab 的映射。
- Execute 时 Instantiate。
- Add 时 Attach 到 Target。
- Remove 时 Destroy。

后续可接：

- Addressables
- Object Pool
- VFX Graph
- Audio Mixer
- Camera Shake
- Damage Number System

---

## 16. Ability Task

### 16.1 为什么需要 Task

技能很少是一帧完成。

常见流程：

```text
播放动画
等待动画事件
生成判定盒
等待命中
等待输入释放
等待目标选择
等待位移结束
等待投射物命中
等待 Gameplay Event
```

如果所有逻辑都写在 Ability 的 `Update` 或 Coroutine 里，会很快失控。

Ability Task 用于将异步步骤变成可组合模块。

### 16.1.1 Unity 强化版提示：AbilityTask 与 Coroutine 的边界

原生 GAS 核心：

```text
UE GAS 原生就有 AbilityTask，用来表达技能中的异步等待和回调。
```

Unity 强化版建议：

```text
Unity 可以用 Coroutine 或 async/await 实现等待，但上层仍建议保留 AbilityTask 语义。
```

原因是 AbilityTask 更适合能力系统：

```text
Ability 结束时统一 Cancel。
调试器能显示当前 Task。
未来 AbilityGraph 可以复用 Task。
Simulation Tick 可以统一推进 Task。
网络预测和回滚可以知道技能卡在哪一步。
```

第一版可以让 AbilityTask 底层用 Coroutine、UniTask 或手动 Tick，但不要让技能流程直接散落在多个 Coroutine 里。
### 16.2 推荐接口

```csharp
public abstract class AbilityTask
{
    public AbilityInstance Ability { get; private set; }
    public bool IsFinished { get; protected set; }

    public virtual void Start() {}
    public virtual void Tick(float deltaTime) {}
    public virtual void Cancel() {}
}
```

AbilityInstance 持有任务列表：

```csharp
public sealed class AbilityTaskRunner
{
    public T StartTask<T>(T task) where T : AbilityTask;
    public void Tick(float deltaTime);
    public void CancelAll();
}
```

### 16.3 常用 Task

```text
WaitDelayTask
WaitInputPressTask
WaitInputReleaseTask
WaitAnimationEventTask
PlayAnimationTask
WaitGameplayEventTask
WaitTargetDataTask
SpawnProjectileTask
MoveToLocationTask
ApplyRootMotionTask
WaitCollisionHitTask
WaitConfirmCancelTask
```

### 16.4 动画事件

Unity 中可通过以下方式接入：

- Animation Event
- Animator StateMachineBehaviour
- Timeline Signal
- 自定义 Animation Event Router

推荐统一转换成 Gameplay Event：

```text
Event.Animation.CastStart
Event.Animation.Hit
Event.Animation.CastEnd
```

Ability Task 等待这些事件，而不是直接绑定某个动画片段。

---

## 17. Gameplay Ability

### 17.1 Ability 的定位

Gameplay Ability 表示一个角色可以主动或被动执行的能力。

它可以是：

```text
普通攻击
火球术
冲刺
格挡
蓄力攻击
连招
复活
被动天赋
装备主动技能
交互技能
AI 技能
```

Ability 负责流程，Effect 负责结果。

错误倾向：

```text
Ability 里直接 target.Health -= damage
```

推荐倾向：

```text
Ability 选择目标、播放动画、等待命中帧，然后 Apply GameplayEffectSpec
```

### 17.2 配置与实例分离

设计时资产：

```csharp
public abstract class GameplayAbilityAsset : ScriptableObject
{
    public GameplayTagContainer AbilityTags;
    public GameplayTagQuery ActivationRequirements;
    public GameplayTagContainer ActivationBlockedTags;

    public GameplayTagContainer GrantedTagsWhileActive;
    public GameplayTagContainer CancelAbilitiesWithTags;
    public GameplayTagContainer BlockAbilitiesWithTags;

    public GameplayEffectAsset CostEffect;
    public GameplayEffectAsset CooldownEffect;

    public AbilityInstancingPolicy InstancingPolicy;

    public abstract AbilityInstance CreateInstance(AbilitySpec spec, AbilityActivationContext context);
}
```

### 17.2.1 Unity 强化版提示：Blackboard 与 Ability Graph

原生 GAS 核心：

```text
GameplayAbility 负责编排技能流程，AbilityInstance 保存一次释放的运行时状态。
```

Unity 强化版建议：

```text
AbilityBlackboard 和 AbilityGraph 不是原生 GAS 核心概念。
Blackboard 用来让 AbilityTask 之间共享临时数据。
AbilityGraph 是未来把技能流程可视化的工具层。
```

例如一次蓄力火球可能在 Blackboard 中保存：

```text
Target
TargetPoint
ChargeTime
DamageMultiplier
ProjectileId
HitTargets
```

第一版分寸：

```text
建议 AbilityInstance 从一开始就带轻量 Blackboard。
AbilityGraph 暂时不要做，先用 C# Ability + AbilityTask 跑通流程。
```
运行时实例：

```csharp
public abstract class AbilityInstance
{
    public AbilitySpec Spec { get; }
    public AbilitySystemComponent OwnerASC { get; }
    public AbilityActivationContext Context { get; }

    public virtual bool CanActivate();
    public virtual void Activate();
    public virtual bool Commit();
    public virtual void Cancel();
    public virtual void End();
}
```

### 17.3 AbilitySpec

角色拥有的不是 Ability Asset 本身，而是 Ability Spec。

```csharp
public sealed class AbilitySpec
{
    public AbilityHandle Handle;
    public GameplayAbilityAsset Ability;
    public int Level;
    public int InputId;
    public GameplayTagContainer DynamicAbilityTags;
    public List<AbilityInstance> ActiveInstances;
    public object SourceObject;
}
```

用途：

- 同一个 Ability Asset 可在不同角色上以不同等级存在。
- 装备、Buff、天赋可以临时授予技能。
- 技能可以有运行时动态标签。
- 输入绑定不污染技能资产。

### 17.4 Ability 生命周期

```text
Input / AI / Script 请求激活
  -> ASC 查找 AbilitySpec
  -> CanActivateAbility
  -> 检查 Owner Tags
  -> 检查技能阻塞
  -> 检查 Cost
  -> 检查 Cooldown
  -> 创建 AbilityInstance
  -> 应用 GrantedTagsWhileActive
  -> CancelAbilitiesWithTags
  -> BlockAbilitiesWithTags 生效
  -> Activate
  -> Commit Cost / Cooldown
  -> 执行 AbilityTask
  -> Apply GameplayEffect
  -> End 或 Cancel
  -> 移除 GrantedTagsWhileActive
```

### 17.5 Commit

Commit 表示正式提交技能。

一般包含：

```text
CommitCost
CommitCooldown
CommitResources
```

为什么需要 Commit：

- 有些技能按下时开始瞄准，确认释放时才消耗。
- 有些技能蓄力过程中可取消，释放瞬间才进入冷却。
- 预测时需要知道哪些效果是本地预提交的。

### 17.6 Ability Instancing Policy

```csharp
public enum AbilityInstancingPolicy
{
    NonInstanced,
    InstancedPerActor,
    InstancedPerExecution
}
```

推荐 Unity 第一版只支持：

```text
InstancedPerExecution
```

原因：

- 最安全。
- 每次释放状态独立。
- 易于处理异步任务。
- 后续优化时再引入 NonInstanced。

---

## 18. 技能阻塞、取消与状态控制

### 18.1 激活要求

Ability Asset 上应有：

```text
ActivationRequiredTags
ActivationBlockedTags
SourceRequiredTags
SourceBlockedTags
TargetRequiredTags
TargetBlockedTags
```

例如：

```text
Fireball:
  Required: State.Alive
  Blocked: State.Silenced, State.Stunned, Cooldown.Fireball
```

### 18.2 激活期间授予标签

技能释放时可临时授予：

```text
State.Casting
Ability.Active.Fireball
```

用途：

- 阻塞移动。
- 阻塞其他技能。
- 让 AI 或 UI 知道角色正在施法。

### 18.3 取消其他技能

技能激活时可取消已有技能：

```text
Dodge:
  CancelAbilitiesWithTags:
    Ability.Attack
    Ability.Cast
```

眩晕效果应用时也可以取消技能：

```text
StunEffect:
  GrantedTags:
    State.Stunned
  CancelAbilitiesWithTags:
    Ability.Interruptible
```

### 18.4 阻塞其他技能

技能激活期间可以阻塞其他技能：

```text
HeavyAttack:
  BlockAbilitiesWithTags:
    Ability.Dodge
    Ability.Cast
```

内部可实现为 ASC 中的 Blocked Ability Tag Count。

---

## 19. AbilitySystemComponent

### 19.1 职责

`AbilitySystemComponent` 是系统的门面对象。外部系统应尽量只与 ASC 交互，而不是直接操作能力、效果或属性容器。

ASC 负责：

- 初始化 Attribute Set。
- 授予、移除、查询 Ability。
- 激活、取消、结束 Ability。
- 接收输入事件并路由到 Ability。
- 应用、移除、Tick Gameplay Effect。
- 聚合 Gameplay Tag。
- 查询技能释放条件。
- 提交技能消耗与冷却。
- 广播属性变化、标签变化、效果变化。
- 分发表现事件 Gameplay Cue。
- 为联网层提供命令入口和状态快照。

### 19.1.1 Unity 强化版接入点：Command 与 Simulation Tick

原生 GAS 核心：

```text
外部请求通过 ASC 激活 Ability、应用 Effect、查询 Attribute 和 Tag。
```

Unity 强化版建议：

```text
不要让输入、AI、测试、网络层到处直接调用 ASC。
可以在 ASC 前面加一层 Command Processor。
```

推荐入口：

```text
Input / AI / Script / Network
  -> GameplayCommand
  -> CommandProcessor
  -> AbilitySystemComponent
```

`Command Pattern` 不是原生 GAS 核心概念。它的价值是把“请求做某事”和“已经发生某事”分开。比如 `ActivateAbilityCommand` 表示请求释放技能，它可能因为冷却、资源、状态失败；而 `AbilityActivatedEvent` 表示技能已经成功激活，是事实记录。

第一版分寸：

```text
可以先实现 LocalCommandProcessor，收到命令后立即调用 ASC。
但方法签名和调试日志里最好已经能看到 CommandId、Tick、Instigator、PredictionKey 这些预留信息。
```
### 19.2 推荐接口

```csharp
public sealed class AbilitySystemComponent : MonoBehaviour
{
    public AttributeSetCollection Attributes { get; }
    public GameplayTagContainer OwnedTags { get; }
    public AbilitySpecContainer Abilities { get; }
    public ActiveGameplayEffectContainer ActiveEffects { get; }

    public AbilityHandle GiveAbility(GameplayAbilityAsset ability, int level, int inputId);
    public bool ClearAbility(AbilityHandle handle);

    public bool TryActivateAbility(AbilityHandle handle);
    public bool TryActivateAbilityByInput(int inputId);
    public void CancelAbility(AbilityHandle handle);
    public void CancelAbilities(GameplayTagQuery query);

    public GameplayEffectHandle ApplyGameplayEffectToSelf(GameplayEffectSpec spec);
    public GameplayEffectHandle ApplyGameplayEffectToTarget(GameplayEffectSpec spec, AbilitySystemComponent target);
    public bool RemoveActiveEffect(GameplayEffectHandle handle);

    public float GetAttribute(GameplayAttributeId id);
    public bool TryGetAttribute(GameplayAttributeId id, out float value);

    public bool HasTag(GameplayTag tag);
    public bool HasAnyTags(GameplayTagContainer tags);
    public bool HasAllTags(GameplayTagContainer tags);
}
```

### 19.3 ASC 的依赖原则

ASC 可以依赖：

- Tags
- Attributes
- Effects
- Abilities
- Cues
- Targeting
- Input
- Networking abstraction

但业务角色脚本不应该直接越过 ASC 修改属性或 Buff。

错误示例：

```csharp
enemy.Health -= 50;
enemy.IsStunned = true;
```

推荐示例：

```csharp
var spec = GameplayEffectSpec.Create(damageEffect, sourceASC, targetASC);
spec.SetByCaller("Damage", 50);
sourceASC.ApplyGameplayEffectToTarget(spec, targetASC);
```

---

## 20. 输入系统

### 20.1 输入不直接调用具体技能

不推荐：

```csharp
if (Input.GetKeyDown(KeyCode.Q))
{
    fireball.Cast();
}
```

推荐：

```text
InputAction Ability1 Pressed
  -> ASC.AbilityInputPressed(InputId.Ability1)
  -> 找到绑定 InputId 的 AbilitySpec
  -> TryActivateAbility
```

### 20.1.1 Unity 强化版提示：输入先变成 Command

原生 GAS 核心：

```text
输入通过 ASC 路由到 AbilitySpec，而不是直接调用具体技能。
```

Unity 强化版建议：

```text
输入事件可以先变成 InputPressedCommand / InputReleasedCommand，再交给 CommandProcessor。
```

这样玩家、AI、自动测试、回放和未来网络层都可以走同一条入口：

```text
InputAction
  -> InputCommand
  -> CommandProcessor
  -> ASC.AbilityInputPressed
  -> TryActivateAbility
```

第一版可以立即执行 Command，不需要复杂队列；但保留 Command 概念会让后续输入缓冲、固定 Tick、联网验证和 Replay 容易很多。
### 20.2 输入接口

```csharp
public interface IAbilityInputRouter
{
    void AbilityInputPressed(int inputId);
    void AbilityInputReleased(int inputId);
    void AbilityInputHeld(int inputId, float heldTime);
}
```

ASC 接收：

```csharp
public void OnAbilityInputPressed(int inputId);
public void OnAbilityInputReleased(int inputId);
```

### 20.3 支持的玩法

输入层应支持：

```text
按下释放
松开释放
长按蓄力
按住持续引导
输入缓冲
连招窗口
确认 / 取消
目标选择模式
```

---

## 21. 编辑器工作流

### 21.1 必备资产

第一版建议提供这些资产菜单：

```text
Create/UGAS/Gameplay Tag Database
Create/UGAS/Attribute Set Definition
Create/UGAS/Gameplay Effect
Create/UGAS/Gameplay Ability
Create/UGAS/Gameplay Cue
Create/UGAS/Targeting Profile
Create/UGAS/Input Binding
```

### 21.2 Gameplay Tag 编辑器

功能：

- 树形展示 Tag。
- 新增、重命名、删除 Tag。
- 检查重复。
- 检查非法字符。
- 支持搜索。
- 支持右键复制完整路径。
- 支持导出代码常量。

Tag 示例：

```text
State.Alive
State.Dead
State.Stunned
State.Silenced
State.Rooted
State.Invincible
Ability.Attack
Ability.Cast
Ability.Dodge
Cooldown.Fireball
Damage.Physical
Damage.Fire
Effect.Buff
Effect.Debuff
GameplayCue.Damage.Hit
Event.Animation.Hit
```

### 21.3 Gameplay Effect 编辑器

应展示：

- Duration Policy。
- Duration / Period。
- Modifiers 列表。
- Executions 列表。
- Granted Tags。
- Application Requirements。
- Remove Effects With Tags。
- Stacking Policy。
- Gameplay Cues。

体验重点：

- Modifier 选择 Attribute 时用下拉，不手写字符串。
- Tag 选择用 Tag Picker。
- SetByCaller Key 可提示命名规范。
- Duration 为 Instant 时隐藏持续相关字段。
- Stacking 仅对 Duration / Infinite 显示。

### 21.4 Gameplay Ability 编辑器

应展示：

- Ability Tags。
- 激活条件。
- 阻塞条件。
- Cost Effect。
- Cooldown Effect。
- 激活期间授予 Tags。
- 取消其他技能 Tags。
- 阻塞其他技能 Tags。
- Targeting Profile。
- Ability Task 流程配置。
- Gameplay Cue。

第一版如果不做可视化流程图，可以先通过派生 C# Ability 类编写流程，资产只配置通用参数。

### 21.5 运行时调试器

强烈建议做一个 Runtime Debug Window。

选中一个 ASC 时显示：

```text
Attributes:
  Health: 80 / 100
  Mana: 30 / 50
  AttackPower: 24

Owned Tags:
  State.Alive
  State.Casting
  Cooldown.Fireball

Abilities:
  Fireball Lv1 Input1 Active=false
  Dodge Lv1 Input2 Active=false

Active Effects:
  GE_Cooldown_Fireball Remaining=3.2s
  GE_Buff_Haste Remaining=4.8s Stack=2

Recent Events:
  ApplyEffect FireballDamage
  AttributeChanged Health 100 -> 80
  Cue GameplayCue.Damage.Fire.Impact
```

### 21.5.1 Unity 强化版提示：Debug Timeline 与 Ability Graph

原生 GAS 核心：

```text
GAS 本身提供强大的调试思路，但 Debug Timeline 和 Ability Graph 不是原生 GAS 核心概念。
```

Unity 强化版建议：

```text
Runtime Debugger 看“当前状态”。
Debug Timeline 看“状态是怎么变成这样的”。
Ability Graph 用于未来把 AbilityTask 流程可视化。
```

推荐顺序：

```text
先做 Runtime ASC Debugger。
再做结构化 Event Log。
再做 Debug Timeline。
最后在 AbilityTask 稳定后做 Ability Graph。
```

不要一开始就做完整可视化技能编辑器，否则底层 AbilityTask、Blackboard、TargetData、EffectSpec 还没稳定时会反复返工。
这个工具对调技能非常关键。

---

## 22. 联网预留设计

当前暂不实现联网，但从第一版开始预留边界，避免未来大改。

### 22.1 网络模型建议

后续如果是动作 RPG、MOBA、MMO、联机 PvE，推荐：

```text
Server authoritative
Client prediction for local player
Server reconciliation
Replicated attributes
Replicated tags
Replicated active effects
Replicated ability activation
```

如果是帧同步项目，则需要完全确定性改造：

```text
Fixed tick
Deterministic math
Deterministic random
No nondeterministic Unity Physics in gameplay authority
Input command sync
State hash validation
```

### 22.1.1 Unity 强化版提示：网络预留与确定性边界

原生 GAS 核心：

```text
UE GAS 深度结合 Unreal 的 Replication、PredictionKey 和服务端权威模型。
```

Unity 强化版建议：

```text
Unity 不应照搬 UE 网络层，而是预留 IAbilityNetworkAdapter、PredictionKey、Command、Snapshot、Serializable TargetData。
```

当前暂不实现联网，但需要提前避免这些设计：

```text
技能入口散落在直接方法调用中。
TargetData 只保存 Unity 对象引用，无法序列化。
Effect 运行时状态存在 ScriptableObject 上。
战斗逻辑依赖渲染帧 Update。
随机数没有统一入口。
```

如果未来是服务端权威：

```text
Command 发送到服务端验证。
属性、Tag、ActiveEffect 由服务端同步。
客户端可做本地表现预测。
```

如果未来是帧同步或回滚：

```text
需要固定 Tick、确定性随机、稳定排序、可快照状态、尽量避免非确定性 Unity Physics 参与权威逻辑。
```
### 22.2 从第一版就应保留的接口

```csharp
public interface IAbilityNetworkAdapter
{
    bool IsAuthority { get; }
    bool IsLocalPredictingClient { get; }

    PredictionKey CreatePredictionKey();

    void SendTryActivateAbility(AbilityHandle handle, PredictionKey predictionKey);
    void SendCancelAbility(AbilityHandle handle, PredictionKey predictionKey);
    void SendTargetData(AbilityHandle handle, TargetData targetData, PredictionKey predictionKey);

    void ReplicateAttributeChange(AttributeChangedEvent evt);
    void ReplicateTagChange(GameplayTag tag, int count);
    void ReplicateActiveEffect(ActiveGameplayEffectSnapshot snapshot);
}
```

单机版实现：

```csharp
public sealed class LocalAbilityNetworkAdapter : IAbilityNetworkAdapter
{
    public bool IsAuthority => true;
    public bool IsLocalPredictingClient => false;
}
```

### 22.3 PredictionKey

即使第一版不用，也建议在上下文里带着：

```csharp
public readonly struct PredictionKey
{
    public readonly int Value;
    public bool IsValid => Value != 0;
}
```

相关结构中预留：

```text
AbilityActivationContext.PredictionKey
GameplayEffectSpec.PredictionKey
GameplayEventData.PredictionKey
TargetData.PredictionKey
```

### 22.4 状态快照

后续网络同步不一定要同步完整对象，可定义快照：

```csharp
public readonly struct AttributeSnapshot
{
    public GameplayAttributeId AttributeId;
    public float BaseValue;
    public float CurrentValue;
}

public readonly struct ActiveGameplayEffectSnapshot
{
    public int Handle;
    public int EffectAssetId;
    public int SourceEntityId;
    public float StartTime;
    public float Duration;
    public float Period;
    public int StackCount;
}
```

### 22.5 当前阶段不要做的事

为了避免复杂度爆炸，第一阶段不做：

- 客户端预测回滚。
- 服务端拒绝后的本地状态修正。
- 属性压缩同步。
- Active Effect delta replication。
- 跨客户端 Gameplay Cue 可靠性策略。

但保留接口后，未来可以逐步加。

---

## 23. 推荐目录结构

Unity 项目中推荐：

```text
Assets/
  Game/
    AbilitySystem/
      Runtime/
        Core/
        Tags/
        Attributes/
        Effects/
        Abilities/
        Tasks/
        Targeting/
        Cues/
        Input/
        Networking/
      Editor/
        Tags/
        Effects/
        Abilities/
        Debugging/
      Samples/
        Abilities/
        Effects/
        Characters/
        UI/
      Tests/
        EditMode/
        PlayMode/
```

Assembly Definition：

```text
Game.AbilitySystem.Runtime.asmdef
Game.AbilitySystem.Editor.asmdef
Game.AbilitySystem.Tests.asmdef
```

---

## 24. 第一版 MVP 范围

### 24.1 必须实现

```text
AbilitySystemComponent
GameplayTag
GameplayTagContainer
GameplayTagQuery
AttributeSetCollection
AttributeAggregator
GameplayEffectAsset
GameplayEffectSpec
ActiveGameplayEffect
ActiveGameplayEffectContainer
GameplayAbilityAsset
AbilitySpec
AbilityInstance
Cost as GameplayEffect
Cooldown as GameplayEffect
GameplayCueDispatcher
InputId 到 AbilitySpec 的绑定
```

### 24.2 可以延后

```text
复杂编辑器
可视化 Ability Graph
复杂叠层
复杂免疫
客户端预测
网络同步
Effect Granted Ability
属性捕获快照高级规则
```

### 24.3 MVP 能支持的技能

MVP 完成后应能实现：

```text
普通攻击
消耗魔法的火球术
5 秒冷却技能
持续 3 秒加速 Buff
持续 2 秒眩晕 Debuff
每秒扣血的中毒
治疗术
带 Tag 阻塞的沉默
通过 UI 显示冷却剩余
通过 Runtime Debugger 查看当前 Buff 和属性
```

---

## 25. 示例：火球术完整流程

### 25.1 数据配置

Ability：

```text
GA_Fireball
AbilityTags:
  Ability.Magic.Fireball

ActivationRequiredTags:
  State.Alive

ActivationBlockedTags:
  State.Stunned
  State.Silenced
  Cooldown.Fireball

CostEffect:
  GE_Cost_Fireball

CooldownEffect:
  GE_Cooldown_Fireball

GrantedTagsWhileActive:
  State.Casting
```

Cost Effect：

```text
GE_Cost_Fireball
DurationPolicy: Instant
Modifier:
  Mana Add -20
```

Cooldown Effect：

```text
GE_Cooldown_Fireball
DurationPolicy: Duration
Duration: 5
GrantedTags:
  Cooldown.Fireball
```

Damage Effect：

```text
GE_Damage_Fireball
DurationPolicy: Instant
SetByCaller:
  DamageMultiplier
Execution:
  DamageExecution
GameplayCue:
  GameplayCue.Damage.Fire.Impact
```

### 25.2 运行流程

```text
玩家按下 Ability1
  -> InputRouter.OnAbilityInputPressed(1)
  -> ASC.TryActivateAbilityByInput(1)
  -> 找到 GA_Fireball AbilitySpec
  -> 检查 State.Alive
  -> 检查没有 State.Stunned / State.Silenced / Cooldown.Fireball
  -> 检查 Mana >= 20
  -> 创建 FireballAbilityInstance
  -> 添加 State.Casting
  -> Commit:
      Apply GE_Cost_Fireball to self
      Apply GE_Cooldown_Fireball to self
  -> 播放施法动画
  -> WaitAnimationEvent Event.Animation.Fire
  -> 生成投射物
  -> 投射物命中目标
  -> 创建 GE_Damage_Fireball Spec
  -> SetByCaller DamageMultiplier = 1.5
  -> ApplyGameplayEffectToTarget
  -> DamageExecution 计算最终伤害
  -> 修改目标 Health
  -> 触发 GameplayCue.Damage.Fire.Impact
  -> EndAbility
  -> 移除 State.Casting
```

---

## 26. 示例：眩晕

Stun Effect：

```text
GE_Stun
DurationPolicy: Duration
Duration: 2
GrantedTags:
  State.Stunned
RemoveGameplayEffectsWithTags:
  Effect.Channeling
GameplayCue:
  GameplayCue.Debuff.Stun.Add
  GameplayCue.Debuff.Stun.Remove
```

当 `State.Stunned` 被授予：

```text
ASC 取消 Ability.Interruptible
ASC 阻塞 Ability.Attack
ASC 阻塞 Ability.Cast
移动组件收到标签变化后停止移动
UI 显示眩晕图标
```

---

## 27. 示例：加速 Buff

Haste Effect：

```text
GE_Haste
DurationPolicy: Duration
Duration: 5
Modifiers:
  MoveSpeed Multiply 1.3
GrantedTags:
  Buff.Haste
GameplayCue:
  GameplayCue.Buff.Haste.Add
  GameplayCue.Buff.Haste.Remove
Stacking:
  Stackable: true
  StackLimit: 3
  RefreshDurationOnSuccessfulApplication: true
```

运行时：

```text
第一次应用:
  Stack = 1
  MoveSpeed * 1.3

第二次应用:
  Stack = 2
  MoveSpeed * 1.6 或按规则叠乘
  Duration 刷新
```

叠层数值规则需要明确：

```text
Additive per stack:
  每层 +30%，三层 = +90%

Multiplicative per stack:
  每层 *1.3，三层 = *2.197
```

推荐默认使用 Additive per stack，更容易控制平衡。

---

## 28. 实现阶段计划

### 28.1 实现优先级边界

为了避免把“原生 GAS 工作流”和“Unity 强化版扩展”混在一起，建议按三层推进：

```text
第一层：GAS 核心层
  必须实现，决定系统是不是 GAS-inspired。

第二层：Unity 工程增强层
  早期预留，逐步实现，决定系统能不能长期扩展。

第三层：项目形态适配层
  按需求选择，比如 ECS、联网、AI、回放、可视化编辑器。
```

对应关系：

```text
Phase 1:
  GAS 核心语义 + Authoring/Runtime 分离 + 基础 Simulation Tick

Phase 2:
  AbilityTask + GameplayCue + TargetData + MessageBus + EventLog

Phase 3:
  Rule Engine + Modifier Algebra + Debug Timeline + 更完整编辑器

Phase 4:
  ECS 后端 / AI 接入 / Replay / NetworkAdapter 实装

Phase 5:
  Ability Graph / 完整预测 / 确定性模拟
```
### Phase 1：核心模型

目标：跑通属性、标签、效果、技能。

实现：

```text
GameplayTag
GameplayTagContainer
GameplayTagQuery
GameplayAttributeId
AttributeSetCollection
AttributeAggregator
AttributeModifier
GameplayEffectAsset
GameplayEffectSpec
ActiveGameplayEffect
ActiveGameplayEffectContainer
AbilitySystemComponent
GameplayAbilityAsset
AbilitySpec
AbilityInstance
```

验收：

```text
可以给角色加技能
可以按输入释放技能
可以扣 Mana
可以进入 Cooldown
可以对目标造成伤害
可以添加持续 Buff
Buff 结束后属性恢复
Tag 能阻塞技能释放
```

### Phase 2：技能流程

目标：支持真实战斗技能。

实现：

```text
AbilityTask
WaitDelay
WaitGameplayEvent
WaitAnimationEvent
PlayAnimation
TargetData
TargetingProfile
Projectile hit callback
GameplayCueDispatcher
```

验收：

```text
火球术等待动画事件后发射
投射物命中后造成伤害
冲刺技能持续期间阻塞攻击
眩晕能打断引导技能
表现事件不写在数值逻辑里
```

### Phase 3：高级 Effect

目标：支持复杂数值和 Buff。

实现：

```text
ExecutionCalculation
SetByCaller
AttributeBased Magnitude
Stacking
Periodic Effect
Effect Query
Dispel
Immunity
Ongoing Tag Requirements
```

验收：

```text
伤害公式可扩展
中毒每秒扣血
Buff 可叠层
净化可移除 Debuff
免疫可阻止眩晕
```

### Phase 4：编辑器与调试

目标：让系统真正可用。

实现：

```text
Tag Editor
GameplayEffect Inspector
GameplayAbility Inspector
Runtime ASC Debugger
Attribute Change Log
Active Effect Viewer
Cue Preview
```

验收：

```text
策划能创建技能和效果资产
运行时能看清属性、Tag、Buff、冷却
错误配置有提示
```

### Phase 5：联网预备

目标：接入网络前整理边界。

实现：

```text
IAbilityNetworkAdapter
PredictionKey
Serializable TargetData
AttributeSnapshot
ActiveEffectSnapshot
Ability command DTO
Effect application DTO
```

验收：

```text
单机 LocalAdapter 正常工作
核心流程都经过可替换的网络适配层
没有直接依赖具体网络库
```

---

## 29. 测试策略

### 29.1 EditMode Tests

适合测试：

```text
Tag 匹配
Tag Query
Attribute Modifier 聚合
GameplayEffect Instant 应用
Duration Effect 添加和移除
Stacking
Cooldown 查询
Cost 检查
Effect Query
```

### 29.2 PlayMode Tests

适合测试：

```text
ASC 初始化
Ability 激活流程
Ability Task Tick
动画事件模拟
Projectile 命中模拟
Gameplay Cue 分发
Buff 到期
UI 监听事件
```

### 29.3 回归测试重点

```text
Buff 移除后属性必须恢复
多来源授予同一 Tag 时引用计数正确
冷却结束后 Cooldown Tag 必须消失
技能 Cancel 后 GrantedTagsWhileActive 必须移除
Instant Effect 不能残留 ActiveEffect
Duration Effect 的周期 Tick 次数正确
叠层刷新时间行为符合配置
```

---

## 30. 常见设计陷阱

### 30.1 直接在角色脚本里改属性

这会绕过：

- Modifier。
- Attribute Hook。
- UI 事件。
- 网络同步。
- 调试日志。

所有战斗属性变化都应通过 ASC。

### 30.2 Tag 当字符串随便写

后果：

- 拼写错误。
- 查询不稳定。
- 编辑器不可控。
- 重构困难。

应使用 Tag Database 和 Tag Picker。

### 30.3 GameplayEffect 没有 Spec

没有 Spec 会导致：

- 无法区分本次施法来源。
- 无法传 SetByCaller。
- 无法捕获 Source/Target Tags。
- 无法做预测和同步。

### 30.4 技能直接做数值计算

Ability 应负责编排，Effect / Execution 应负责结果。

技能直接算伤害会让公式复用困难。

### 30.5 过早做联网预测

预测会引入：

- 本地临时效果。
- 服务端确认。
- 拒绝回滚。
- 时间同步。
- TargetData 验证。

建议等单机模型稳定后再接。

---

## 31. 推荐命名规范

资产名前缀：

```text
GA_  Gameplay Ability
GE_  Gameplay Effect
GC_  Gameplay Cue
AS_  Attribute Set
TG_  Targeting Profile
```

示例：

```text
GA_Fireball
GE_Fireball_Damage
GE_Fireball_Cost
GE_Fireball_Cooldown
GC_Fireball_Impact
TG_Fireball_Targeting
```

Tag 命名：

```text
State.Stunned
State.Silenced
Ability.Magic.Fireball
Cooldown.Fireball
Damage.Fire
Effect.Buff.Haste
GameplayCue.Damage.Fire.Impact
Event.Animation.Hit
```

SetByCaller Key 命名：

```text
Data.Damage
Data.DamageMultiplier
Data.Heal
Data.Duration
Data.Radius
Data.StackBonus
```

---

## 32. 最终推荐方案

这套系统建议按以下原则落地：

1. 以 `AbilitySystemComponent` 为唯一能力系统入口。
2. 以 `GameplayTag` 管理状态、条件、分类、事件和冷却。
3. 以 `GameplayEffect` 统一表达伤害、治疗、Buff、Debuff、冷却、消耗。
4. 以 `GameplayEffectSpec` 承载每次应用的上下文。
5. 以 `AttributeAggregator` 处理持续 Modifier。
6. 以 `GameplayAbility` 编排技能流程，而不是直接写数值结果。
7. 以 `AbilityTask` 处理动画、输入、目标、延迟、命中等异步步骤。
8. 以 `GameplayCue` 解耦表现。
9. 以 `IAbilityNetworkAdapter` 和 `PredictionKey` 预留联网能力。
10. 先做单机核心，再做高级效果，再做编辑器，再接联网。

如果按这个架构推进，Unity 中可以实现一套非常接近 GAS 工作流体验的能力系统，并且不会被 Unreal 的底层实现细节绑住。

补充边界：

```text
原生 GAS 提供 Ability、Effect、Attribute、Tag、Cue 这套玩法语义框架。
Unity 强化版在这套语义框架外，补上 Command、MessageBus、EventLog、SimulationTick、ECS、Graph、AI、Replay 等工程能力。
```

实现时不要把强化概念当成原生 GAS 必须项，也不要为了追求“纯正 GAS”而拒绝适合 Unity 的工程增强。

比较稳的姿势是：

```text
核心像 GAS。
边界像现代 Unity 架构。
实现按项目需求逐步加深。
```




