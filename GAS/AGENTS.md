# AGENTS.md

## 文档目标

本目录用于规划一套 Unity GAS-inspired Ability System。

这套系统的目标不是做一个教学 Demo，也不是先写一个简化技能系统再慢慢补功能，而是从设计阶段就对标 Unreal GAS 的能力边界，整理出一套能够支撑大型项目、复杂技能、长期维护、多人协作和后续扩展的落地方案。

核心目标：

```text
能支撑大型项目的技能需求。
能支撑复杂 Ability / Effect / Tag / Attribute / Cue / Task 工作流。
能支撑长期维护和复杂设计。
能让后续联网、AI、调试、编辑器、回放、ECS 后端等方向有明确接口。
```

## 总体原则

后续所有文档都应遵循：

```text
落地版本 -> 优化
```

而不是：

```text
简化版本 -> 迭代补全
```

原因：

```text
GAS 这类系统的基础设施一旦做薄，后续 Ability、GameplayEffect、GameplayTag、Attribute、Cue、AI、UI、编辑器、存档、网络都会依赖这个薄版本。
后续再补完整能力时，会牵动大量资产、接口、序列化结构和运行时流程。
```

因此，文档应优先描述完整设计，再说明哪些部分可以暂时空实现、延迟接业务、或者只预留接口。

不能把“暂时不接业务”写成“第一版不设计”。

## 对标标准

文档设计应尽量对标 Unreal GAS 的能力边界，而不是一比一复刻 UE C++ 实现。

应复刻：

```text
能力模型
工作流
数据流
生命周期
扩展点
编辑器配置思路
调试思路
网络预留边界
```

不需要复刻：

```text
UE 的 UObject 体系
UE 宏系统
UE 反射实现
UE Replication 具体实现
UE Editor API
UE FName 完整引擎级实现
```

Unity 版本应该使用 Unity/C# 生态适合的实现方式，但能力边界不能因为实现平台不同而过度简化。

## 文档写作要求

后续新增或修改文档时，应遵守以下要求。

### 1. 先写完整能力边界

每个子系统文档都应先回答：

```text
这个模块在 UE/GAS 同级系统中完整应该支持什么？
大型项目会依赖它解决什么问题？
哪些能力如果后补会导致大规模返工？
```

然后再说明：

```text
哪些功能第一版完整实现。
哪些功能第一版只预留字段和接口。
哪些功能可以后期优化。
```

### 2. 不写“简化版作为主方案”

可以写简化示例帮助理解，但不能把简化版作为正式设计路线。

不推荐表达：

```text
第一版先用 string，后面再换 TagId。
第一版先只做简单 All/Any/None Query，后面再换表达式树。
第一版先不做 Spec，后面复杂了再加。
第一版先不用引用计数，后面 Buff 多了再补。
```

推荐表达：

```text
正式结构第一版就按完整模型设计。
暂时可以只接最小业务路径。
复杂 UI / 网络 / 优化可延后，但字段、接口、序列化边界要保留。
```

### 3. 每个模块都要写依赖和实现顺序

文档应尽量适合边看边做。

每个模块应说明：

```text
前置依赖
自身负责什么
不负责什么
被哪些模块依赖
建议实现步骤
验收标准
后续优化点
```

例如：

```text
GameplayTag 不依赖 AbilitySystemComponent。
GameplayEffect 依赖 GameplayTag 和 Attribute。
Ability 依赖 GameplayEffect、TargetData、AbilityTask。
AbilitySystemComponent 是总装入口，应在基础模块之后实现。
```

### 4. 明确原生 GAS 与 Unity 强化版边界

文档中如果引入非原生 GAS 概念，必须注明。

例如：

```text
Command Pattern 不是原生 GAS 核心概念，是 Unity 强化版用于输入、AI、回放、联网预留的工程增强。
Gameplay Message Bus 不是原生 GAS 核心概念，是跨系统玩法消息总线。
ECS / DOTS 不是原生 GAS 概念，是可选运行时后端。
Debug Timeline 不是原生 GAS 核心概念，是调试工具增强。
```

但这些概念可以作为大型 Unity 项目的推荐增强，只要不和原生 GAS 边界混淆。

### 5. 优先保证长期稳定性

设计时优先考虑：

```text
资产序列化稳定
运行时数据结构稳定
Id 稳定
重命名和迁移机制
调试可解释
扩展点明确
多来源数据合并
未来网络和回放预留
```

不要为了当前实现方便牺牲长期维护。

## 具体文档规划

建议后续按以下专题继续补齐。

### 已有文档

```text
Unity-GAS-Architecture.md
  主架构文档，按实现依赖从基础到总装组织。

Unity-GAS-Advanced-Concepts.md
  现代架构扩展概念说明。

Unity-GAS-Complex-Skill-Testcases.md
  复杂技能压力测试用例。

Unity-GAS-GameplayTag-Implementation.md
  GameplayTag 完整实现设计。

Unity-GAS-NameId-Implementation.md
  对标 UE FName 思想的 Unity NameId / StableName 基础设施设计。
```

### 建议新增文档

```text
Unity-GAS-Attribute-Implementation.md
  AttributeId、AttributeSet、Aggregator、Modifier Algebra、Meta Attribute、Clamp、事件、快照。

Unity-GAS-GameplayEffect-Implementation.md
  GameplayEffectAsset、Spec、Modifier、Execution、Duration、Period、Stacking、GrantedTags、ApplicationRequirements。

Unity-GAS-ActiveEffect-Implementation.md
  ActiveGameplayEffectContainer、生命周期、叠层、周期 Tick、RemoveReason、Ongoing Requirements、Effect Query。

Unity-GAS-Ability-Implementation.md
  GameplayAbilityAsset、AbilitySpec、AbilityInstance、Commit、Cancel、Block、ActivationGroup、InstancingPolicy。

Unity-GAS-AbilityTask-Implementation.md
  AbilityTask 生命周期、取消、Tick、动画事件、输入等待、TargetData、Projectile、Graph 预留。

Unity-GAS-Targeting-Implementation.md
  TargetData、TargetActor 思路、目标筛选、锁定、区域、射线、预测和序列化预留。

Unity-GAS-GameplayCue-Implementation.md
  CueTag、CueContext、CueManager、表现生命周期、对象池、Addressables、预测表现预留。

Unity-GAS-ASC-Implementation.md
  AbilitySystemComponent 总装、初始化、Ability/Effect/Tag/Attribute 容器协作、事件总线、调试接口。

Unity-GAS-Editor-Tooling.md
  TagPicker、EffectEditor、AbilityEditor、RuntimeDebugger、DebugTimeline、资产校验、代码生成。

Unity-GAS-Networking-Design.md
  当前不实现联网，但设计 PredictionKey、Command、Snapshot、NetworkAdapter、Replication 边界。

Unity-GAS-ECS-DOTS-Backend.md
  ECS 作为可选后端时的数据布局、Baking、System 分层、Hybrid 表现层。
```

## 子系统文档推荐结构

每份实现文档建议使用统一结构：

```text
1. 设计目标
2. UE/GAS 对标能力边界
3. Unity 实现取舍
4. 核心数据结构
5. 生命周期 / 数据流
6. 编辑器与资产序列化
7. 运行时 API
8. 调试与事件
9. 网络 / 回放 / ECS 预留
10. 第一版必须实现清单
11. 可延后但必须预留的内容
12. 常见坑
13. 验收用例
```

## 术语使用

推荐统一使用以下术语：

```text
NameId / StableName
NameRegistry
GameplayTag
GameplayTagContainer
GameplayTagQuery
GameplayAttribute
AttributeSet
AttributeAggregator
GameplayEffect
GameplayEffectSpec
ActiveGameplayEffect
GameplayAbility
AbilitySpec
AbilityInstance
AbilityTask
TargetData
GameplayCue
AbilitySystemComponent
PredictionKey
GameplayCommand
GameplayMessage
GameplayEventRecord
```

如果引入 Unity 自定义概念，应说明它是否属于：

```text
原生 GAS 核心
Unity 强化版建议
项目形态适配层
```

## 最终判断标准

文档和系统设计应始终服务于这个判断：

```text
未来能否稳定实现复杂技能？
未来能否让策划和程序长期协作？
未来能否调试清楚一个技能为什么没生效？
未来能否支持大量 Buff、Debuff、被动、装备、AI、Boss 机制？
未来能否接网络、回放、ECS、编辑器工具，而不是推倒重来？
```

如果某个设计为了省事导致这些问题未来难以回答，就不应该作为正式路线。

## 一句话原则

```text
这套 Unity GAS 文档不是写一个能跑的简化技能系统。
它是在设计一套能长期承载大型项目复杂玩法的 Ability System。
第一版就应该按落地版本设计，再按性能、编辑器体验和项目需求优化。
```



