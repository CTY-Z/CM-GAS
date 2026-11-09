# Unity GAS 复杂技能压力测试用例

本文档用于在 Unity GAS-inspired 能力系统实现后，验证系统深度是否足够。

这些技能不是第一版必须全部实现的内容，而是未来的“压力测试样例”。如果系统能比较优雅地实现其中 3 到 4 个，就说明这套能力系统已经不只是普通技能系统，而是具备接近 GAS 工作流的表达能力。

每个技能都尽量覆盖不同系统能力：

- Ability 流程编排
- GameplayEffect
- GameplayEffectSpec
- ActiveGameplayEffect
- GameplayTag
- Attribute Aggregation
- AbilityTask
- TargetData
- GameplayCue
- GameplayEvent
- GameplayMessageBus
- Command
- Blackboard
- Effect Query
- Snapshot
- Debug Timeline
- 后续联网预留

---

## 1. 使用方式

建议未来实现 GAS 框架后按以下顺序复刻：

```text
1. 蓄力火球
2. 连招剑技
3. 毒雾领域
4. 护盾反击
5. 被动天赋
6. 召唤物共享能力
7. Boss 标记机制
8. 时间回溯
```

这个顺序大致从“技能流程复杂”逐步走向“系统边界复杂”。

每个测试用例应至少产出：

```text
GameplayAbilityAsset
GameplayEffectAsset
GameplayTag 配置
必要的 AbilityTask
必要的 ExecutionCalculation
GameplayCue 配置
运行时 Debug Timeline 记录
```

---

## 2. 技能一：蓄力火球

### 2.1 需求描述

玩家按住技能键开始蓄力，松开后释放火球。

蓄力时间影响：

```text
伤害倍率
爆炸半径
投射物飞行速度
燃烧持续时间
```

蓄力期间：

```text
角色进入 State.Casting。
角色移速降低 50%。
蓄力超过 2 秒后获得 State.SuperArmor。
蓄力最多 3 秒。
```

取消规则：

```text
如果被 State.Stunned 打断，技能取消，不进入冷却，不扣完整资源。
如果玩家主动取消，返还 50% Mana。
如果蓄力成功释放，正式扣 Mana 并进入冷却。
```

命中规则：

```text
火球命中第一个目标或地面后爆炸。
爆炸范围内敌人受到火焰伤害。
目标获得燃烧 Debuff。
如果目标拥有 State.Frozen，则移除 Frozen，造成额外蒸发伤害，但不施加燃烧。
```

### 2.2 涉及模块

```text
AbilitySystemComponent
GameplayAbility
AbilitySpec
AbilityInstance
GameplayEffect
GameplayEffectSpec
ActiveGameplayEffect
GameplayTag
AbilityTask
TargetData
GameplayCue
Blackboard
ExecutionCalculation
Effect Query
```

### 2.3 推荐标签

```text
Ability.Magic.Fireball
Ability.Magic.Fireball.Charging
State.Casting
State.SuperArmor
State.Stunned
State.Frozen
Damage.Fire
Damage.Fire.Vaporize
Effect.Debuff.Burn
Cooldown.Fireball
GameplayCue.Fireball.Charge
GameplayCue.Fireball.Launch
GameplayCue.Fireball.Impact
GameplayCue.Fireball.Vaporize
Event.Animation.Fire
```

### 2.4 推荐 Effect

```text
GE_Fireball_Cost
GE_Fireball_Cooldown
GE_Fireball_ChargingSlow
GE_Fireball_SuperArmor
GE_Fireball_Damage
GE_Fireball_Burn
GE_Fireball_VaporizeDamage
```

### 2.5 推荐 AbilityTask

```text
WaitInputReleaseTask
WaitDelayTask
PlayAnimationTask
WaitAnimationEventTask
SpawnProjectileTask
WaitProjectileHitTask
SelectTargetsInRadiusTask
ApplyGameplayEffectTask
```

### 2.6 Blackboard 数据

```text
ChargeStartTick
ChargeTime
DamageMultiplier
ExplosionRadius
ProjectileSpeed
BurnDuration
HitLocation
HitTargets
CancelReason
```

### 2.7 关键实现点

```text
Commit 不应在按下瞬间完成，而是在确认释放时完成。
取消原因会影响是否扣资源、是否进入冷却、是否返还 Mana。
蓄力期间的减速和霸体应通过 GameplayEffect 授予。
火球命中后应创建 GameplayEffectSpec，并通过 SetByCaller 写入蓄力倍率。
冻结目标的蒸发逻辑应通过 Tag 和 ExecutionCalculation 判断。
表现通过 GameplayCue 触发，不写在伤害计算里。
```

### 2.8 验收点

```text
短按可以释放小火球。
蓄力越久，伤害和半径越大。
蓄力期间角色移速降低。
蓄力超过 2 秒获得霸体 Tag。
被眩晕打断时不会进入冷却。
主动取消时返还 Mana。
Frozen 目标触发 Vaporize，不再获得 Burn。
Debug Timeline 能看到 Charge、Commit、ProjectileHit、Damage、Cue、End。
```

---

## 3. 技能二：连招剑技

### 3.1 需求描述

玩家连续点击普攻，执行三段连击。

每段攻击拥有：

```text
不同动画
不同伤害倍率
不同判定窗口
不同硬直
不同可取消窗口
```

分支规则：

```text
第二段攻击期间按下重击，派生上挑。
目标处于 State.Airborne 时，第三段变成空中追击。
装备 Weapon.Sword.Katana 时，第三段追加居合斩。
拥有 Buff.LightningInfusion 时，所有剑技附加雷伤。
```

取消规则：

```text
命中后 0.15 秒内按闪避，可以取消后摇。
被眩晕会打断当前连招。
切换武器会中断连招。
```

### 3.2 涉及模块

```text
Input Command
Input Buffer
GameplayAbility
AbilityTask
GameplayTag
TargetData
Hitbox
Animation Event
Blackboard
Cancel/Block Ability
GameplayEffect
GameplayCue
```

### 3.3 推荐标签

```text
Ability.Attack.Light
Ability.Attack.Heavy
Ability.Attack.Combo
Ability.Attack.Launcher
Ability.Attack.AirChase
Ability.Dodge
State.Attacking
State.ComboWindow
State.CancelWindow
State.Airborne
State.Stunned
Weapon.Sword
Weapon.Sword.Katana
Buff.LightningInfusion
Damage.Physical.Slash
Damage.Lightning
Event.Animation.HitboxOpen
Event.Animation.HitboxClose
Event.Animation.ComboWindowOpen
Event.Animation.ComboWindowClose
```

### 3.4 推荐 Effect

```text
GE_ComboSlash_Stage1_Damage
GE_ComboSlash_Stage2_Damage
GE_ComboSlash_Stage3_Damage
GE_ComboSlash_Launcher_Damage
GE_ComboSlash_AirChase_Damage
GE_Katana_Iai_Damage
GE_LightningInfusion_ExtraDamage
```

### 3.5 推荐 AbilityTask

```text
PlayAnimationTask
WaitAnimationEventTask
OpenComboWindowTask
WaitComboInputTask
SpawnHitboxTask
WaitHitboxResultTask
OpenCancelWindowTask
WaitCancelInputTask
BranchAbilityTask
```

### 3.6 Blackboard 数据

```text
ComboIndex
BufferedInput
CurrentAnimation
HitTargets
CancelWindowOpen
ComboWindowOpen
BranchAbility
WeaponTagsAtStart
```

### 3.7 关键实现点

```text
输入不能直接触发技能函数，应进入输入缓冲。
动画事件决定判定盒开启和关闭。
连招阶段可通过 Blackboard 或 AbilitySpec runtime state 保存。
装备 Tag 和 Buff Tag 会影响分支。
LightningInfusion 不应写死在剑技里，应通过 Effect 或被动监听附加伤害。
取消窗口是 GameplayTag 或 AbilityTask 状态，不是动画脚本里的临时 bool。
```

### 3.8 验收点

```text
普攻可以连续三段。
第二段可派生重击上挑。
空中目标触发空中追击。
装备 Katana 后第三段追加居合斩。
LightningInfusion 能附加雷伤。
命中后闪避可以取消后摇。
被眩晕会清理 State.Attacking 和当前 Task。
Debug Timeline 能显示 ComboStage、InputBuffer、HitboxOpen、Hit、CancelWindow。
```

---

## 4. 技能三：毒雾领域

### 4.1 需求描述

法师在地面生成毒雾区域，持续 10 秒。

区域规则：

```text
敌人进入毒雾后，每秒受到毒伤。
敌人在毒雾内会叠加 PoisonStack，最多 5 层。
离开毒雾后，中毒效果保留 3 秒。
毒雾内敌人受到治疗降低 50%。
```

环境交互：

```text
毒雾接触火焰区域后被点燃。
点燃瞬间对区域内敌人造成爆炸伤害。
点燃后，后续周期毒伤转为火毒混合伤害。
```

来源绑定：

```text
施法者死亡时，毒雾提前消失。
毒雾造成的伤害归属施法者。
```

### 4.2 涉及模块

```text
Area Entity
Aura Effect
Periodic GameplayEffect
Stacking
Effect Query
GameplayEvent
GameplayMessage
SourceASC tracking
Target enter/exit
GameplayCue
```

### 4.3 推荐标签

```text
Ability.Magic.PoisonCloud
Area.PoisonCloud
Area.Fire
Area.PoisonCloud.Ignited
Damage.Poison
Damage.Fire
Damage.FirePoison
Effect.Debuff.Poison
Effect.Debuff.HealingReduction
State.Dead
Event.Area.Entered
Event.Area.Exited
Event.Area.OverlappedFire
Event.Source.Died
GameplayCue.PoisonCloud.Active
GameplayCue.PoisonCloud.Ignite
GameplayCue.PoisonCloud.Explosion
```

### 4.4 推荐 Effect

```text
GE_PoisonCloud_Aura
GE_Poison_DOT
GE_Poison_Stack
GE_HealingReduction
GE_IgnitedPoisonExplosion
GE_FirePoison_DOT
```

### 4.5 关键实现点

```text
区域本身可以是一个拥有 SourceASC 引用的运行时对象。
进入区域时给目标应用 DOT 和治疗降低。
离开区域时移除 Aura 部分，但保留短时间 Poison DOT。
周期伤害必须由 ActiveEffectContainer 稳定 Tick。
PoisonStack 需要明确叠层刷新规则。
被火焰点燃应替换区域状态，并替换后续 Effect。
施法者死亡事件应能移除由该 Source 产生的区域和效果。
```

### 4.6 验收点

```text
敌人进入毒雾后开始周期扣血。
离开毒雾后仍中毒 3 秒。
毒雾中治疗降低生效。
PoisonStack 最多 5 层。
火焰区域点燃毒雾，触发爆炸。
点燃后 DOT 类型变为 FirePoison。
施法者死亡后毒雾消失。
Debug Timeline 能显示 AreaEntered、PeriodicTick、StackChanged、Ignited、AreaRemoved。
```

---

## 5. 技能四：护盾反击

### 5.1 需求描述

角色获得护盾，持续 6 秒，最多吸收 500 点伤害。

护盾期间：

```text
角色拥有 State.Guarded。
受到近战攻击时，吸收 70% 伤害。
受到远程攻击时，吸收 40% 伤害。
近战吸收值的 40% 反弹给攻击者。
```

护盾结束规则：

```text
护盾被打破时，触发范围震荡并击退敌人。
护盾自然结束时，不触发震荡。
护盾期间成功格挡 3 次，刷新持续时间并获得一次免费反击技能。
```

### 5.2 涉及模块

```text
IncomingDamage Meta Attribute
ExecutionCalculation
Damage Pipeline
ActiveEffect
Remove Reason
GameplayEvent
GameplayMessage
GameplayTag
Ability Grant
Targeting
GameplayCue
```

### 5.3 推荐标签

```text
Ability.Defense.ShieldCounter
Ability.Counter
Ability.Counter.Free
State.Guarded
Damage.Melee
Damage.Ranged
Event.Shield.Blocked
Event.Shield.Broken
Event.Shield.Expired
Effect.Buff.Shield
GameplayCue.Shield.Add
GameplayCue.Shield.Block
GameplayCue.Shield.Break
GameplayCue.Shield.Reflect
```

### 5.4 推荐 Effect

```text
GE_ShieldCounter_Shield
GE_ShieldCounter_ReflectDamage
GE_ShieldCounter_BreakShockwave
GE_ShieldCounter_GrantFreeCounter
GE_ShieldCounter_InvulnerableFrames
```

### 5.5 推荐 Attribute / Meta Attribute

```text
Health
ShieldValue
IncomingDamage
AbsorbedDamage
ReflectedDamage
BlockCount
```

### 5.6 关键实现点

```text
伤害应进入 Damage Execution，而不是直接扣 Health。
护盾 Effect 需要拦截 IncomingDamage。
Damage Source Tags 决定近战/远程吸收比例。
反弹伤害要保留原始攻击者作为 Target。
Effect 移除原因必须区分 Broken 和 Expired。
格挡次数可以通过 Event Log 或 ActiveEffect runtime state 记录。
免费反击可以通过 Granted Ability 或临时 Tag 实现。
```

### 5.7 验收点

```text
护盾能吸收伤害。
近战和远程吸收比例不同。
近战吸收会反弹伤害。
护盾被打破触发震荡。
自然结束不触发震荡。
格挡 3 次刷新持续时间。
格挡 3 次后下一次反击不消耗资源。
Debug Timeline 能显示 IncomingDamage、AbsorbedDamage、ReflectDamage、ShieldBroken。
```

---

## 6. 技能五：被动天赋：元素热冲击

### 6.1 需求描述

角色拥有一个被动天赋。

触发规则：

```text
每当角色对燃烧目标造成冰伤害时，触发 ThermalShock。
ThermalShock 造成额外伤害并清除目标燃烧状态。
如果 5 秒内触发 3 次，获得 Buff.ElementalOverload。
ElementalOverload 使下一个元素技能不消耗 Mana，但伤害降低 20%。
```

被动特征：

```text
不需要输入。
获得天赋后自动激活。
移除天赋后停止监听。
```

### 6.2 涉及模块

```text
Passive Ability
GameplayMessageBus
GameplayEvent
GameplayTag
Effect Query
GameplayEffect
Cost Modification
Damage Modification
Time Window Counter
```

### 6.3 推荐标签

```text
Ability.Passive.ThermalShock
Ability.Elemental
Damage.Ice
Damage.Fire
State.Burning
Effect.Debuff.Burn
Buff.ElementalOverload
Event.Combat.DamageDealt
Event.Effect.Removed
GameplayCue.ThermalShock.Trigger
GameplayCue.ElementalOverload.Add
```

### 6.4 推荐 Effect

```text
GE_ThermalShock_Damage
GE_RemoveBurn
GE_ElementalOverload
GE_FreeNextElementalCast
GE_ElementalOverload_DamagePenalty
```

### 6.5 关键实现点

```text
Passive Ability 需要 OnGranted 自动激活。
监听 DamageDealt 消息时要判断 Source 是自己。
目标必须拥有 Burning。
本次 Damage Spec 必须携带 Damage.Ice。
触发后通过 Effect Query 移除 Burn。
5 秒内计数需要 Simulation Tick 或时间窗口数据。
下一次元素技能 Cost 修改不能写死在具体技能里，应通过 Cost Pipeline 或 Effect 修改。
```

### 6.6 验收点

```text
被动无需按键即可工作。
冰伤命中 Burning 目标触发 ThermalShock。
ThermalShock 会移除 Burning。
5 秒内触发 3 次获得 ElementalOverload。
下一个元素技能不消耗 Mana。
下一个元素技能伤害降低 20%。
被动移除后不再监听 DamageDealt。
Debug Timeline 能显示 PassiveActivated、MessageReceived、ThermalShockTriggered、OverloadGranted。
```

---

## 7. 技能六：镜像分身

### 7.1 需求描述

角色召唤一个镜像分身，持续 15 秒。

分身继承：

```text
主人 40% 攻击力。
主人 100% 暴击率。
主人 50% 最大生命。
主人当前装备授予的部分技能。
不继承终极技能。
```

联动规则：

```text
主人释放火球时，分身延迟 0.3 秒复制释放一个弱化火球。
分身造成的伤害归属主人。
分身造成击杀时，任务进度和吸血归主人。
主人死亡时，所有分身消失。
分身死亡时给主人返还 10 Mana。
```

### 7.2 涉及模块

```text
ASC on Summon
Ability Grant
AbilitySpec Copy
Attribute Capture
Ownership / Instigator
GameplayMessageBus
GameplayEffect
GameplayTag
SourceASC / InstigatorASC / EffectCauser
Lifetime Effect
```

### 7.3 推荐标签

```text
Ability.Summon.Clone
Ability.Magic.Fireball
Ability.Ultimate
Summon.Clone
Source.Proxy
Owner.Dead
Event.Ability.OwnerActivated
Event.Summon.Died
Event.Combat.DamageDealtByProxy
GameplayCue.Clone.Spawn
GameplayCue.Clone.Death
```

### 7.4 推荐 Effect

```text
GE_Clone_Lifetime
GE_Clone_InheritedAttributes
GE_Clone_WeakFireballDamage
GE_Clone_ReturnManaOnDeath
```

### 7.5 关键实现点

```text
分身必须拥有自己的 ASC。
复制 AbilitySpec 时要过滤 Ability.Ultimate。
继承属性应通过 Attribute Capture 或初始化 Effect 完成。
SourceASC、InstigatorASC、EffectCauser 要分清。
分身实际造成伤害，但归属主人。
主人释放火球的消息要能被分身监听。
主人死亡时通过 Source/Owner 查询清理分身。
```

### 7.6 验收点

```text
分身生成后有独立 ASC。
分身属性按主人属性比例继承。
分身拥有部分主人技能。
分身不会继承 Ultimate。
主人释放火球后分身延迟复制弱化火球。
分身伤害能触发主人吸血。
分身击杀能算给主人。
主人死亡后分身消失。
分身死亡返还 Mana。
Debug Timeline 能区分 Source、Instigator、EffectCauser。
```

---

## 8. 技能七：Boss 多目标标记机制

### 8.1 需求描述

Boss 随机给多名玩家施加标记。

普通阶段：

```text
选择 3 名存活玩家。
标记持续 10 秒。
标记结束时爆炸。
被标记玩家受到治疗降低 30%。
标记可以被驱散。
驱散会立刻触发小爆炸。
```

二阶段：

```text
Boss 血量低于 50% 后进入 Phase Two。
标记数量变为 5 个。
爆炸后留下火焰区域。
```

狂暴规则：

```text
Boss 拥有 State.Enraged 时，标记无法被净化。
```

站位规则：

```text
标记爆炸时，如果被标记玩家彼此距离太近，爆炸伤害提高。
如果分散站位，爆炸伤害降低。
```

### 8.2 涉及模块

```text
Targeting
GameplayEffect
ActiveEffect
Remove Reason
Effect Query
Rule Engine
GameplayTag
GameplayMessage
Area Effect
GameplayCue
```

### 8.3 推荐标签

```text
Ability.Boss.Mark
Boss.Phase.One
Boss.Phase.Two
State.Enraged
Effect.BossMark
Effect.Dispellable
Effect.Undispellable
Effect.Debuff.HealingReduction
Damage.Arcane
Area.Fire
Event.Boss.MarkApplied
Event.Boss.MarkExploded
Event.Effect.Dispelled
GameplayCue.BossMark.Add
GameplayCue.BossMark.Explode
GameplayCue.BossMark.SmallExplode
GameplayCue.FireZone.Spawn
```

### 8.4 推荐 Effect

```text
GE_BossMark
GE_BossMark_HealingReduction
GE_BossMark_Explosion
GE_BossMark_DispelExplosion
GE_BossMark_FireZone
```

### 8.5 推荐 Rule / Condition

```text
BossHealthBelow50Condition
BossHasTag State.Enraged
TargetIsAliveCondition
TargetDoesNotHave Effect.BossMark
MarkedTargetsDistanceCondition
```

### 8.6 关键实现点

```text
Targeting 需要从玩家列表中排除死亡和已有标记目标。
Boss 阶段影响技能配置，可以通过 Tag 或 Rule Engine 决定。
驱散不是简单 Remove，需要传 RemoveReason = Dispel。
自然到期、驱散、死亡清理应触发不同结果。
爆炸伤害依赖多个标记目标之间的距离。
State.Enraged 会覆盖标记的可驱散性。
二阶段爆炸后创建火焰区域。
```

### 8.7 验收点

```text
普通阶段标记 3 人。
二阶段标记 5 人。
死亡玩家不会被选中。
已有标记玩家不会重复选中。
标记期间治疗降低。
普通阶段标记可驱散。
驱散触发小爆炸。
Enraged 状态下标记不可驱散。
爆炸伤害会根据标记玩家距离变化。
二阶段爆炸后留下火焰区域。
Debug Timeline 能显示 MarkApplied、Dispelled、ExplodeReason、PhaseRule。
```

---

## 9. 技能八：时间回溯

### 9.1 需求描述

角色释放技能时记录自身状态。

5 秒后回到释放瞬间的：

```text
位置
生命
Mana
部分 Buff 状态
部分 Cooldown 状态
部分 OwnedTags
```

不可回溯内容：

```text
State.Dead
Effect.Curse
Effect.BossMark
Cooldown.Ultimate
```

提前触发：

```text
回溯等待期间如果角色死亡，立刻触发回溯。
死亡触发的回溯只能恢复 30% 生命。
```

回溯后：

```text
清除所有控制效果。
获得 1 秒无敌。
触发时间扭曲表现。
```

### 9.2 涉及模块

```text
Snapshot
ActiveEffect Snapshot
Attribute Snapshot
GameplayTag Snapshot
Effect Query
GameplayEvent
GameplayMessage
AbilityTask
GameplayCue
Remove Reason
State Restore
Presentation Interpolation
```

### 9.3 推荐标签

```text
Ability.Time.Rewind
State.RewindPending
State.Rewinding
State.Invincible
State.Dead
Effect.Curse
Effect.BossMark
Effect.CrowdControl
Cooldown.Ultimate
Event.Time.SnapshotCreated
Event.Time.RewindTriggered
Event.Time.StateRestored
GameplayCue.TimeRewind.Start
GameplayCue.TimeRewind.Restore
GameplayCue.TimeRewind.End
```

### 9.4 推荐 Effect

```text
GE_TimeRewind_Pending
GE_TimeRewind_Invincible
GE_TimeRewind_ClearCrowdControl
GE_TimeRewind_Cooldown
```

### 9.5 Snapshot 内容

```text
Position
Rotation
Health
Mana
AttributeSnapshot[]
ActiveEffectSnapshot[]
GameplayTagSnapshot[]
CooldownSnapshot[]
SimulationTick
RandomState optional
```

### 9.6 关键实现点

```text
ActiveEffect 必须可快照和恢复。
不是所有 Effect 都能被恢复，需要 Effect Query 排除。
State.Dead 不可简单恢复，但死亡可以触发特殊回溯分支。
位置恢复属于逻辑层瞬移，表现层可以做插值或残影。
回溯后清除控制效果应通过 Effect Query。
无敌通过 Duration GameplayEffect 授予 State.Invincible。
Debug Timeline 需要记录 SnapshotCreated 和 RestoreDiff。
```

### 9.7 验收点

```text
释放时创建状态快照。
5 秒后回到快照位置。
Health / Mana 恢复到快照值。
Curse、BossMark、Ultimate Cooldown 不被恢复。
等待期间死亡会立刻触发回溯。
死亡触发只恢复 30% 生命。
回溯后清除控制效果。
回溯后获得 1 秒无敌。
Debug Timeline 能显示快照前后差异。
```

---

## 10. 横向能力检查清单

### 10.1 如果系统只能实现简单技能

通常只能支持：

```text
按键
播放动画
扣血
冷却
简单 Buff
```

这种系统不一定需要 GAS 架构。

### 10.2 如果系统能实现蓄力火球

说明系统支持：

```text
AbilityTask
SetByCaller
Commit 时机
取消原因
Projectile TargetData
Tag 条件分支
GameplayCue
```

### 10.3 如果系统能实现连招剑技

说明系统支持：

```text
输入缓冲
动画事件窗口
技能分支
取消窗口
Ability 间阻塞和取消
装备/Buff Tag 改变流程
```

### 10.4 如果系统能实现毒雾领域

说明系统支持：

```text
区域 Aura
目标进入离开
周期 Effect
叠层
Effect 替换
来源追踪
环境交互
```

### 10.5 如果系统能实现护盾反击

说明系统支持：

```text
Damage Pipeline
Meta Attribute
ExecutionCalculation
Remove Reason
反弹伤害
临时授予能力或规则
```

### 10.6 如果系统能实现被动天赋

说明系统支持：

```text
Passive Ability
GameplayMessageBus
事件监听
时间窗口计数
规则修改 Cost / Damage
```

### 10.7 如果系统能实现镜像分身

说明系统支持：

```text
多个 ASC 之间的关系
AbilitySpec 复制和过滤
Attribute Capture
Source / Instigator / Causer 区分
归属统计
生命周期绑定
```

### 10.8 如果系统能实现 Boss 标记机制

说明系统支持：

```text
复杂 Targeting
Effect Remove Reason
Rule Engine
多目标空间关系
阶段规则覆盖
驱散和不可驱散
```

### 10.9 如果系统能实现时间回溯

说明系统支持：

```text
Snapshot
ActiveEffect 可序列化
Effect Query
状态恢复
特殊死亡分支
逻辑层和表现层分离
```

---

## 11. 最终深度判断

真正复杂的技能系统需要的不只是“释放技能造成伤害”。

它至少要能稳定表达：

```text
技能是流程，不是一段函数。
Effect 是结果，不只是 Buff。
Tag 是语义，不只是状态名。
Attribute 是聚合值，不只是 float。
输入是意图，不一定立刻执行。
Event 是事实，不是随手 Debug.Log。
Cue 是表现，不应该混进逻辑。
TargetData 是统一目标描述，不应该每个技能自己写。
ActiveEffect 要有生命周期、来源、层数、移除原因。
复杂技能经常需要 Blackboard、Task、MessageBus、Snapshot。
```

如果未来 Unity GAS 框架能优雅地复刻本文档中的多个技能，它就已经具备大型项目能力系统的基础深度。

