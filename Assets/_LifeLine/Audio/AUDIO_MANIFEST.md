# LifeLine 音频清单（156 条 · 交付说明）

> 这份文件是给「接手的人」和「AI」看的：每一条音频都写明**放在哪个目录**、**什么时长**、**游戏里干什么用**、**是怎么做出来的**。
> 音频本体在 `audio/`，Unity 工程内镜像在 `Assets/_LifeLine/Resources/Audio/`，两边都是 156 条、一一对应，文件名不要改（代码按文件名找）。

## 一眼看懂的几条规矩

- **格式**：全部 WAV / PCM 16bit；SFX 以单声道为主，结局两条是立体声。采样率保持素材原生值（44.1k 132 条，48k 18 条，24k 5 条，22k 3 条，32k 1 条）。
- **音量**：已全库统一，**SFX 峰值 0.34（≈ −9.4 dBFS）／环境 0.24／音乐 0.40**，逐条前后电平见 `audio_volume_report.csv`。
- **时长**：以本表为准（表的「实测」列是磁盘上真实的长度）。
- **循环**：标 `循环` 的条目首尾电平已对齐，可直接开 AudioSource.loop；其余是一次性播放。
- **命名**：`sfx_敌人/场景_动作.wav`，前缀 `amb_` 是环境底噪，`sfx_end_` 是结局。
- **接入点**：工程里声音层目前几乎为空，唯一现成接缝是 `Assets/_LifeLine/Scripts/Runtime/.../CrewPresentationEvents.cs` 的 `ICrewPresentationSink`；其余槽位按文件名在对应系统里挂 AudioSource 即可。

## 总览

| 组 | 主题 | 条数 | 优先级 | 目录 |
| --- | --- | --- | --- | --- |
| 01 | 制作节奏 · 6 键音符 | 6 | P0 | `SFX/Station` |
| 02 | 制作反馈 | 4 | P0 | `SFX/Station` |
| 03 | 批量制作与校准键 | 4 | P0 | `SFX/Station` |
| 04 | 绞盘 | 3 | P0 | `SFX/Station` |
| 05 | 绳索与货箱 | 5 | P0 | `SFX/World` |
| 06 | 角色动作（CrewPresentationEvent） | 21 | P0 | `SFX/Crew` |
| 07 | 工位与设备 | 28 | P1 | `SFX/Station` |
| 08 | 炮塔与维修共用 | 4 | P1 | `SFX/Station` |
| 09 | 敌人（6 种怪） | 27 | P1 | `SFX/Enemy` |
| 10 | 世界与船体 | 10 | P1 | `SFX/World` |
| 11 | 天气与雷击 | 4 | P0 | `SFX/World` |
| 12 | UI 与系统 | 12 | P1 | `SFX/UI` |
| 13 | 主线 · 电台 | 7 | P1 | `SFX/Station` |
| 14 | 新手引导 | 3 | P1 | `SFX/UI` |
| 15 | 结局 | 2 | P0 | `Music` |
| 16 | 底噪与环境 | 6 | P0 | `SFX/Ambience` |
| 18 | 警报与状态 | 3 | P0 | `SFX/UI` |
| 19 | 拾取与入库 | 6 | P0 | `SFX/World` |
| 20 | P2 变体与打磨 | 1 | P2 | `SFX/Various` |
| **合计** |  | **156** |  |  |

## 01 · 制作节奏 · 6 键音符

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 01-01 | `sfx_craft_note_w.wav` | W 键（C5） | 0.12s / 0.15s |  | 最低音，方向区起点 |
| 01-02 | `sfx_craft_note_a.wav` | A 键（D5） | 0.12s / 0.15s |  |  |
| 01-03 | `sfx_craft_note_s.wav` | S 键（E5） | 0.12s / 0.15s |  |  |
| 01-04 | `sfx_craft_note_d.wav` | D 键（G5） | 0.12s / 0.15s |  |  |
| 01-05 | `sfx_craft_note_q.wav` | Q 键（A5） | 0.12s / 0.15s |  | 铃音，双槽时更易分辨 |
| 01-06 | `sfx_craft_note_e.wav` | E 键（C6） | 0.12s / 0.15s |  | 最高音 |

## 02 · 制作反馈

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 02-01 | `sfx_craft_hit.wav` | 节奏命中 | 0.16s / 0.16s |  | 上扬亮音，与音符提示区分 |
| 02-02 | `sfx_craft_miss.wav` | 节奏失误 | 0.26s / 0.26s |  | 下沉闷响 |
| 02-03 | `sfx_craft_complete.wav` | 制作完成 | 0.60s / 0.60s |  | C-E-G-C 琶音 |
| 02-04 | `sfx_craft_deny.wav` | 材料不足 | 0.34s / 0.34s |  | 配合头顶红字 2.5s |

## 03 · 批量制作与校准键

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 03-01 | `sfx_batch_add.wav` | 批量 +1 | 0.06s / 0.06s |  | 高 tick |
| 03-02 | `sfx_batch_remove.wav` | 批量 −1 | 0.06s / 0.06s |  | 低 tick |
| 03-03 | `sfx_batch_confirm.wav` | 批量确认 | 0.26s / 0.26s |  | 双击铃音 |
| 03-04 | `sfx_qte_press.wav` | 校准键按下 | 0.10s / 0.10s |  | 机械键，Calibrate 键 |

## 04 · 绞盘

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 04-01 | `sfx_winch_loop.wav` | 绞盘手摇循环 | 8.33s / 8.33s | 循环 | 原始实录直用；循环播放 |
| 04-02 | `sfx_winch_success.wav` | 绞盘成功 | 0.34s / 0.34s |  | 复用挂绳 sfx_rope_attach |
| 04-03 | `sfx_winch_fail.wav` | 绞盘失败 | 0.46s / 0.46s |  | 原始素材直用 |

## 05 · 绳索与货箱

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 05-01 | `sfx_cargo_strain_loop.wav` | 绳索紧绷循环 | 2.60s / 2.60s | 循环 | 用户素材 rope pulled very tight 13.60–15.60s，100ms 交叉 + 0.60s 尾静音；开头 0.30s 归零（其后未动）；峰 0.26 |
| 05-02 | `sfx_rope_snap.wav` | 货箱绳断裂 | 0.57s / 0.57s |  | 原始素材直用 |
| 05-03 | `sfx_rope_attach.wav` | 挂绳 | 0.34s / 0.34s |  | 原始素材直用 |
| 05-04 | `sfx_cargo_shove.wav` | 水下推箱 | 0.38s / 0.38s |  | 原始素材直用 |
| 05-05 | `sfx_crate_pack.wav` | 装箱 | 0.35s / 0.35s |  | 货箱 3 格 |

## 06 · 角色动作（CrewPresentationEvent）

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 06-01 | `sfx_crew_step_deck.wav` | 甲板 / 舱内脚步 | 1.20s / 1.20s |  | 你提供的脚印素材取相邻两声（0.64s / 1.24s），步间隔 0.60s；峰 0.20 |
| 06-02 | `sfx_crew_ladder_loop.wav` | 爬梯循环 | 1.40s / 1.40s | 循环 | 你提供的梯子单步 ×2，0.70s 间隔（已回退到昨天那一版）；峰 0.28 |
| 06-03 | `sfx_crew_swim_loop.wav` | 游泳循环 | 1.90s / 1.90s | 循环 | 水声 1.20s（末尾渐变）+ 0.70s 静音，循环 1.90s |
| 06-04 | `sfx_crew_sprint_breath.wav` | 冲刺呼吸 | 1.00s / 1.00s | 循环 | 跑步喘息实录里 2.85–3.85s，首尾落在呼吸间隙；趴到 −20dBFS |
| 06-05 | `sfx_crew_dive_splash.wav` | 入水 | 1.15s / 1.15s |  | 跳水入水水花（原始素材截取） |
| 06-06 | `sfx_crew_board.wav` | 上船 | 1.10s / 1.10s |  | 复用出水水花（原 lunge 截取） |
| 06-07 | `sfx_crew_hurt.wav` | 受击 | 0.32s / 0.32s |  | 布料闷击、无人声；峰值 0.350（−9.1dBFS） |
| 06-08 | `sfx_crew_downed.wav` | 倒地 / 昏厥 | 0.67s / 0.67s |  | 木地板双闷响，身体砸地、无人声；峰值 0.350 |
| 06-09 | `sfx_crew_revived.wav` | 船上扶起 | 0.95s / 0.95s |  | 布料拉拽 / 擦蹭，无人声；峰值 0.300（−10.5dBFS） |
| 06-10 | `sfx_crew_revive_water.wav` | 水下扶起 | 0.90s / 0.90s |  | 水中拉扯的水花，无人声；峰值 0.400（−8.0dBFS） |
| 06-11 | `sfx_crew_revive_loop.wav` | 4 秒扶起循环 | 4.00s / 4.00s | 循环 | 布料持续摩擦循环，峰值 0.220（−13.2dBFS） |
| 06-12 | `sfx_crew_melee_swing.wav` | 近战挥击 | 0.30s / 0.19s |  |  |
| 06-13 | `sfx_crew_melee_flesh.wav` | 近战命中肉体 | 0.25s / 0.49s |  |  |
| 06-14 | `sfx_crew_melee_metal.wav` | 近战命中金属 | 0.30s / 2.58s |  |  |
| 06-15 | `sfx_spear_fire.wav` | 射矛出膛 | 0.35s / 0.67s |  | Fire |
| 06-16 | `sfx_spear_hit_flesh.wav` | 矛命中肉体 | 0.25s / 0.84s |  |  |
| 06-17 | `sfx_spear_hit_metal.wav` | 矛命中金属 | 1.21s / 1.21s |  | 已剪掉开头 0.69s 空段，从撞击起势处开始；峰 0.646 |
| 06-18 | `sfx_spear_recall.wav` | 鱼枪召回 | 0.45s / 0.31s |  |  |
| 06-19 | `sfx_crew_hide_enter.wav` | 进入藏匿 | 0.35s / 0.67s |  |  |
| 06-20 | `sfx_crew_hide_exit.wav` | 离开藏匿 | 0.35s / 1.07s |  |  |
| 06-21 | `sfx_crew_winched_loop.wav` | 被绞盘拖拽循环 | 3.07s / 3.07s | 循环 | 拖行摩擦实录 0.29–3.36s，首尾都在停顿处；峰 0.300 |

## 07 · 工位与设备

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 07-01 | `sfx_station_panel_open.wav` | 工位面板开 | 0.25s / 0.25s |  | CC0 metal_open_01；峰 0.28 |
| 07-02 | `sfx_station_panel_close.wav` | 工位面板关 | 0.22s / 0.21s |  | CC0 metal_close_01；峰 0.28 |
| 07-03 | `sfx_equipment_unlock.wav` | 设备解锁 / 建成 | 0.80s / 0.85s |  | 发电机/维修台/过滤器/探照灯/炮塔/鱼竿 |
| 07-04 | `sfx_upgrade_success.wav` | 升级成功 | 0.56s / 0.56s |  | CC0 metal_slam_01 重落定；峰 0.32 |
| 07-05 | `sfx_upgrade_fail.wav` | 升级失败 | 0.36s / 0.36s |  | CC0 metal_hit_04 哑金属；峰 0.30 |
| 07-06 | `sfx_wheel_loop.wav` | 舵轮循环 | 1.20s / 1.20s | 循环 | 你新换的 old-clockwork-gears-turning（2.97s）：取 0.02–1.22s，450ms 等功率交叉（首尾 RMS 0.0140 / 0.0189，接缝差 0.0029），一圈 5–7 记齿轮咔哒；峰 0.24、rms 0.0162 |
| 07-07 | `sfx_wheel_reverse.wav` | 换向咔哒 | 0.15s / 0.15s |  | CC0 keys_02；峰 0.26 |
| 07-08 | `sfx_light_on.wav` | 探照灯开 | 0.30s / 0.28s |  |  |
| 07-09 | `sfx_light_off.wav` | 探照灯关 | 0.30s / 0.18s |  |  |
| 07-10 | `sfx_light_hum_loop.wav` | 探照灯嗡鸣 | 1.50s / 1.50s | 循环 | Fridge hum 2.00–3.50s，80ms 交叉；峰 0.22 |
| 07-11 | `sfx_fishing_cast.wav` | 抛竿 | 0.35s / 0.35s |  | Cast Fishing Line 0.30–0.65s；峰 0.30 |
| 07-12 | `sfx_fishing_bite.wav` | 鱼讯 | 0.30s / 0.30s |  | 拟饵落水 Plop（游戏用钓具音效 CC0）0–0.30s；峰 0.30 |
| 07-13 | `sfx_fishing_reel_loop.wav` | 收线循环 | 1.00s / 1.00s | 循环 | 老式卷线器快倒线 0.40–1.40s，50ms 交叉；峰 0.26 |
| 07-14 | `sfx_fish_common.wav` | 普通鱼上钩 | 0.60s / 0.60s |  | Rock splash 0–0.60s；峰 0.35 |
| 07-15 | `sfx_fish_rare.wav` | 品质鱼上钩 | 0.89s / 0.89s |  | Pool entry 0.40–1.30s；峰 0.35 |
| 07-16 | `sfx_fish_escape.wav` | 鱼逃脱 | 0.60s / 0.60s |  | Splash 34566 0.20–0.80s；峰 0.32 |
| 07-17 | `sfx_repair_hammer.wav` | 修船敲击 | 0.30s / 0.30s |  | CC0 hammer_01；峰 0.34 |
| 07-18 | `sfx_filter_wire.wav` | 过滤器接线 | 0.39s / 0.39s |  | CC0 metal_hit_02；峰 0.30 |
| 07-19 | `sfx_filter_pressurize.wav` | 过滤器加压 | 0.61s / 0.61s |  | Hand pump 0.60–1.20s；峰 0.30 |
| 07-20 | `sfx_oil_scrub_loop.wav` | 擦油污循环 | 1.20s / 1.20s | 循环 | Sponge scrub 3.00–4.20s；峰 0.22 |
| 07-21 | `sfx_symbol_reveal.wav` | 符号显现 | 0.50s / 0.50s |  | Bell tree 0.30–0.80s；峰 0.30 |
| 07-22 | `sfx_wrong_part.wav` | 选错故障件 | 0.39s / 0.39s |  | CC0 metal_hit_03；峰 0.28 |
| 07-23 | `sfx_engine_start.wav` | 引擎启动 | 1.21s / 1.21s |  | Engine start 0–1.20s；峰 0.40 |
| 07-24 | `sfx_hand_crank_loop.wav` | 手摇发电循环 | 1.20s / 1.20s | 循环 | Ratchet crank 2.00–3.20s；峰 0.24 |
| 07-25 | `sfx_fuel_add.wav` | 加燃料 | 0.41s / 0.41s |  | Pour liquid 0.60–1.00s；峰 0.22 |
| 07-26 | `sfx_eel_discharge.wav` | 装电鳗放电 | 0.70s / 0.70s |  | Bit laser zap 0–0.70s；峰 0.45 |
| 07-27 | `sfx_repair_wire_connect.wav` | 接线成功 | 0.25s / 0.25s |  | CC0 metal_hit_01；峰 0.28 |
| 07-28 | `sfx_repair_complete.wav` | 维修完成 / 通电 | 0.47s / 0.47s |  | CC0 wood_slam_02；峰 0.32 |

## 08 · 炮塔与维修共用

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 08-01 | `sfx_turret_build.wav` | 炮塔建造 | 0.99s / 0.99s |  | Socket Wrench 装配实录；峰 0.28 |
| 08-02 | `sfx_turret_rotate_loop.wav` | 炮塔转向 | 1.20s / 1.20s | 循环 | 复用 04 绞盘铰链循环（取 1.00–2.20s，50ms 交叉）；峰 0.25 |
| 08-03 | `sfx_turret_fire.wav` | 炮塔开火 | 0.61s / 0.61s |  | Cannonfire 原样截 0.6s；峰 0.45 |
| 08-04 | `sfx_repair_fail.wav` | 维修操作失败 | 0.32s / 0.32s |  | CC0 metal_hit_05 金属哑响；峰 0.30 |

## 09 · 敌人（6 种怪）

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 09-01 | `sfx_enemy_shark_detect.wav` | 鲨鱼索敌 | 0.60s / 0.60s |  | 你上传的 shark-fin-sound 取 0–0.60s；峰 0.35 |
| 09-02 | `sfx_enemy_shark_windup.wav` | 鲨鱼蓄力 | 0.80s / 0.80s |  | 你上传的 the-archer-pulls-the-string… 取 0.20–1.00s 弓弦拉紧段（0.05s RMS 0.0042→0.0145 渐强，自带"越绷越紧"的走势），+5.8dB 到峰 0.30；头 20ms / 尾 100ms 淡出 |
| 09-03 | `sfx_enemy_shark_lunge.wav` | 鲨鱼突进 | 0.50s / 0.50s |  | 你上传的 shark-attacks-a-diver 取 1.15–1.65s 冲击段；峰 0.35 |
| 09-04 | `sfx_enemy_shark_bite.wav` | 鲨鱼咬中 | 0.50s / 0.50s |  | 你上传的 shark-hit-with-fin 取 0–0.50s；峰 0.35 |
| 09-05 | `sfx_enemy_shark_flee.wav` | 鲨鱼受击逃跑 | 0.70s / 0.70s |  | 沿用 shark-attacks-a-diver 原素材：改为 0.80–1.50s，正好含 1.20s 那记冲击峰（0.02s 包络 0.39→1.00→0.12）与之后的衰减尾，比前段更"有事件"；峰 0.27，头 20ms / 尾 120ms 淡 |
| 09-06 | `sfx_enemy_shark_death.wav` | 鲨鱼死亡 | 0.90s / 0.90s |  | 你上传的 entity-ravager-death-1（3.15s，原峰 1.057 已削顶）：取前 0.9s 并降频 ×0.80（≈ -3.9 半音，时长不变），质心 1204→1066Hz、200–800Hz 低频占比 0.32→0.39；峰 0.35 |
| 09-07 | `sfx_enemy_bird_flap_loop.wav` | 海鸟振翅 | 0.50s / 0.50s | 循环 | 你上传的 heavy-falcon-wing-flaps（4.25s，7 次扇动）第 1 次（0.10s，相对力度 0.77）：双峰"扑-扑"尾较长；0.20s 扇动 + 0.30s 静音 = 0.50s，首尾静音可循环 |
| 09-08 | `sfx_enemy_bird_flap_loop_02.wav` | 海鸟振翅 变体2 | 0.50s / 0.50s | 循环 | 同素材第 5 次（2.44s，相对力度 0.89）：单主峰、衰减快，听感更"脆"；0.20s 扇动 + 0.30s 静音 |
| 09-09 | `sfx_enemy_bird_flap_loop_03.wav` | 海鸟振翅 变体3 | 0.50s / 0.50s | 循环 | 同素材第 6 次（2.84s，相对力度 1.00，最重）：三段起伏，翼风拖得最长 |
| 09-10 | `sfx_enemy_bird_scream.wav` | 海鸟尖叫 | 0.50s / 0.50s |  | 你上传的 agitated-birds-screeching（14.95s）取 11.44–11.94s：11.54s 那记最长的尖叫（原峰 0.896），前面半段还在爬升、后面 11.8s 起全静，收得最干净；峰 0.34、rms 0.081 |
| 09-11 | `sfx_enemy_bird_dive.wav` | 海鸟俯冲 | 0.60s / 0.60s |  | 用户合集「前 3 秒鸟叫段」1.86–2.28s：只留第一声尖叫（2.00s 峰 0.456，起手 0.06→1.00 渐强后收）；2.30s 起的第二声已削掉，后 0.18s 补静音，总长仍 0.60s；头 10ms / 尾 30ms 淡；峰 0.33、rms 0.049 |
| 09-12 | `sfx_enemy_bird_peck.wav` | 海鸟啄击 | 0.25s / 0.25s |  | 用户合集 5.06–5.31s：5.08s（峰 0.707、质心 1.2kHz）与 5.20s（峰 0.444）两记短啄，正好连啄两下；头 5ms / 尾 50ms 淡；峰 0.30、rms 0.030 |
| 09-13 | `sfx_enemy_bird_repel.wav` | 海鸟被驱离 | 0.60s / 0.60s |  | 你上传的 agitated-birds-screeching-and-flapping-in-cages（14.95s）取 2.40–3.00s：2.42s 还挂着前一声的尾，2.62–2.78s 是一记完整尖叫 + 拍翅（原峰 0.87），2.83s 后自然衰减；峰 0.32、rms 0.063 |
| 09-14 | `sfx_enemy_bird_armor_hit.wav` | 装甲鸟金属碰撞 | 0.60s / 0.60s |  | 你上传的 heavy-hollow-metal-clang（1.79s）取 0.06–0.66s：砍掉 0.66s 之后那 1.1s 空余韵，只留"撞上去 + 两记回荡"；峰 0.32、rms 0.044 |
| 09-15 | `sfx_enemy_bird_death.wav` | 海鸟死亡 | 0.80s / 0.80s |  | 用户合集「前 3 秒鸟叫段」0.34–0.78s：只留第一声叫（0.44s 峰 0.262）；0.84s 起的第二声已削掉，后 0.36s 补静音，总长仍 0.80s；头 10ms / 尾 30ms 淡；峰 0.35、rms 0.041 |
| 09-16 | `sfx_enemy_piranha_swarm_loop.wav` | 食人鱼群游 | 4.00s / 4.00s | 循环 | 用户合集 2.00–6.00s（4 秒），两端再各取 500ms 做等功率交叉，接缝处电平 0.043 / 0.047 基本持平；比 1.5s 版长了 2.7 倍，一圈内有 2 次自然的强弱起伏（0.51→1.00→0.60）；峰 0.33、rms 0.063 |
| 09-17 | `sfx_enemy_piranha_bite.wav` | 食人鱼咬击 | 0.30s / 0.30s |  | 用户合集 8.47–8.77s：8.52s / 8.55s 两记咬合脉冲（峰 0.933 / 0.967，质心 2.5–3.1kHz）正好合成"一口咬"；头 8ms / 尾 60ms 淡；峰 0.35 |
| 09-18 | `sfx_enemy_piranha_death.wav` | 食人鱼死亡 | 0.50s / 0.50s |  | 用户合集 9.60–10.10s：挣扎低鸣段，质心从 3.5kHz 一路掉到 900Hz（9.84s 有一记 0.952 峰），末尾自然停息；头 5ms / 尾 100ms 淡；峰 0.35 |
| 09-19 | `sfx_enemy_shadow_pressure_loop.wav` | 深渊之影压迫 | 6.00s / 6.00s | 循环 | 用户合集 0.00–6.00s 原样；尾部 350ms 余弦渐出到 0，正好接上开头 0.15s 的静音起，接缝差 0.000000。听感是「尾巴渐弱→静→重新渐起」，可无限循环；峰 0.22、rms 0.072 |
| 09-20 | `sfx_enemy_shadow_bite.wav` | 影撕咬 | 0.50s / 0.50s |  | 同素材 1.10–1.60s（含 1.20s 那记 1.00 峰值），音高 ×1.10（+1.4 半音）让咬合更脆；峰 0.35 |
| 09-21 | `sfx_enemy_shadow_lunge.wav` | 影突进 | 0.60s / 0.60s |  | 同素材 0.55–1.15s，正是低吼由弱冲到最强的段（0.02s 包络 0.5→1.0）；峰 0.35 |
| 09-22 | `sfx_enemy_shadow_projectile.wav` | 水弹发射 | 0.50s / 0.50s |  | 用户合集 13.945–14.445s：13.953s 起连续 6 记高频爆响（>2kHz 占比 0.15–0.35、峰 0.820）= 水弹射出与水花，后 250ms 余弦渐弱收尾；峰 0.34、rms 0.093 |
| 09-23 | `sfx_enemy_shadow_warning.wav` | 水弹落点预警 | 2.20s / 2.20s |  | 新合集（10.03s）里前一声就是 0.00–0.12s 那记短敲（0.02s 达 0.46、60ms 收完），等幅复制 7 次，间隔按 0.50→0.42→0.35→0.29→0.24→0.20 逐次缩短（越敲越密），最后一记落在 2.00s、之后留 0.08s 静音；峰 0.33 |
| 09-24 | `sfx_enemy_shadow_hit.wav` | 水弹命中 | 0.50s / 0.50s |  | 同合集后面那一声 4.00–4.50s：4.00s 起手 0.58 → 4.09s 冲到 1.00 → 到 4.50s 仍是 0.76 的密集冲击，尾 100ms 余弦淡出；峰 0.38（全组最响） |
| 09-25 | `sfx_enemy_shadow_exit.wav` | 影退场 | 5.04s / 5.04s |  | 你新换的素材（原为 M4A 容器，已解码成正规 wav）：5.04s 全段未裁剪、48kHz 立体声；音量已归一到峰 0.34、rms 0.047 |
| 09-26 | `sfx_enemy_bird_scream_02.wav` | 海鸟尖叫 变体2 | 0.50s / 0.50s |  | 同素材 5.19–5.69s：5.49s 那记（原峰 0.900）起手是安静的、前 0.15s 渐强再炸开，收尾 5.6s 后迅速回落；峰 0.34、rms 0.075 |
| 09-27 | `sfx_enemy_bird_scream_03.wav` | 海鸟尖叫 变体3 | 0.50s / 0.50s |  | 同素材 3.86–4.36s：3.91s 那记最响（原峰 0.997），整段 0.5s 电平几乎不落（连续嘶叫），听感最"吵"的一版；峰 0.34、rms 0.079 |

## 10 · 世界与船体

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 10-01 | `sfx_cabinet_open.wav` | 柜子开启 | 0.40s / 0.78s |  |  |
| 10-02 | `sfx_hatch_open.wav` | 陷阱门开 | 0.50s / 0.31s |  |  |
| 10-03 | `sfx_hatch_close.wav` | 陷阱门关 | 0.50s / 0.31s |  |  |
| 10-04 | `sfx_cargo_land.wav` | 货箱落地 | 0.50s / 1.07s |  |  |
| 10-05 | `sfx_hull_breach_loop.wav` | 船体裂口循环 | 1.50s / 8.33s | 循环 |  |
| 10-06 | `sfx_flooding_loop.wav` | 进水循环 | 1.50s / 3.20s | 循环 |  |
| 10-07 | `sfx_low_power_alert.wav` | 低电提示 | 0.62s / 0.62s |  | 原有 0.313s 的一记提示音，原样拼接重复一遍 = 0.62s（第二声紧贴第一声尾巴，中间无补静音）；峰 0.34、rms 0.071 |
| 10-08 | `sfx_engine_damage_alert.wav` | 引擎损坏警报 | 0.80s / 0.22s |  |  |
| 10-09 | `sfx_night_fall.wav` | 入夜提示 | 1.00s / 0.22s |  |  |
| 10-10 | `sfx_day_refresh.wav` | 日切换刷新 | 1.00s / 0.22s |  |  |

## 11 · 天气与雷击

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 11-01 | `sfx_lightning_warning.wav` | 雷击倒计时 | 2.00s / 2.00s |  | 你上传的 electric-shock-by-high-voltage-wire（7.45s/48kHz）拆两用：底=1.45–1.95s 无爆发段做 0.50s 等功率无缝单元平铺 2s（持续滋滋）；电击=0.95–1.40s 一记 0.45s，在 0.00/0.85/1.32/1.60 叠入，间隔 0.85→0.47→0.28 逐级加快；峰 0.33，末记接落雷 |
| 11-02 | `sfx_lightning_strike.wav` | 落雷 | 1.00s / 1.00s |  | 你上传的 thunder-single-hit（8.35s）取 0.00–1.00s 起爆段，含 0.70s 那记二次炸响；峰 0.45，尾 150ms 淡出 |
| 11-03 | `sfx_thunder_near.wav` | 近雷 | 2.50s / 2.50s |  | 你上传的 thunderstorm-and-pouring-rain 取 37.30–39.80s（第二声雷，最响）；非雷响的雨声已归零，只保留 0.60–2.02s 雷声本体；峰 0.45 |
| 11-04 | `sfx_thunder_far.wav` | 远雷 | 3.00s / 3.00s |  | 同一素材取 0.20–3.20s（第一声雷，偏闷偏轻）；非雷响的雨声已归零，只保留 0.38–1.68s 雷声本体；峰 0.38 |

## 12 · UI 与系统

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 12-01 | `sfx_ui_click.wav` | UI 点击 | 0.09s / 0.09s |  | Kenney click1；峰 0.25 |
| 12-02 | `sfx_ui_hover.wav` | UI 悬停 | 0.06s / 0.06s |  | Kenney rollover2；很轻，峰 0.12 |
| 12-03 | `sfx_ui_back.wav` | 返回 | 0.07s / 0.07s |  | Kenney switch12；峰 0.18 |
| 12-04 | `sfx_ui_deny.wav` | 禁用 / 队列满 | 0.26s / 0.26s |  | 合成下行双音 700→466Hz；峰 0.26 |
| 12-05 | `sfx_ui_toggle.wav` | 开关切换 | 0.21s / 0.21s |  | Kenney switch7；峰 0.22 |
| 12-06 | `sfx_ui_slider.wav` | 滑杆 | 0.09s / 0.09s |  | Kenney mouserelease1；峰 0.15 |
| 12-07 | `sfx_ui_tab.wav` | 页签切换 | 0.06s / 0.06s |  | Kenney click2；峰 0.20 |
| 12-08 | `sfx_save_manual.wav` | 手动存档 | 0.40s / 0.40s |  | Kenney switch21 确认感；峰 0.28 |
| 12-09 | `sfx_save_auto.wav` | 自动存档 | 0.28s / 0.28s |  | Kenney switch24 较轻；峰 0.20 |
| 12-10 | `sfx_save_fail.wav` | 存档失败 | 0.50s / 0.50s |  | 合成下行三音 520→392→311Hz；峰 0.26 |
| 12-11 | `sfx_online_join.wav` | 联机加入 | 0.60s / 0.60s |  | 合成上行四音琶音；峰 0.26 |
| 12-12 | `sfx_online_fail.wav` | 联机连接失败 | 0.60s / 0.60s |  | 合成下行三音 440→330→247Hz；峰 0.26 |

## 13 · 主线 · 电台

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 13-01 | `sfx_radio_static_loop.wav` | 电台杂音 | 3.50s / 3.50s | 循环 | 你上传的 radio-noise-interference 取 1.00–4.50s，100ms 交叉 + 首尾 3ms 归零；峰 0.28，质心 2238Hz |
| 13-02 | `sfx_quest_stage.wav` | 主线阶段推进 | 0.10s / 0.10s |  | Kenney click1 原样，0.103s 一记短"嗒"（质心 3764Hz）；峰 0.26 |
| 13-03 | `sfx_radio_antenna_done.wav` | 天线完成 | 0.09s / 0.09s |  | Kenney rollover3 原样，0.086s 双段"叮-叮"收尾（5163Hz）；峰 0.26 |
| 13-04 | `sfx_radio_receiver_stage.wav` | 接收器阶段 | 0.09s / 0.09s |  | Kenney click3 原样，0.089s 带延迟小尾（4581Hz）；峰 0.26 |
| 13-05 | `sfx_radio_amplifier_stage.wav` | 放大器阶段 | 0.07s / 0.07s |  | Kenney switch12 原样，0.068s 清脆"哒"（6518Hz）；峰 0.26 |
| 13-06 | `sfx_departure_horn.wav` | 启程汽笛 | 2.00s / 2.45s |  |  |
| 13-07 | `sfx_beacon_loop.wav` | 救援信标 | 2.00s / 2.00s | 循环 | 你上传的 sabotage-siren（43.73s）取 0.00–2.00s：素材本身每 2.00s 重复一记警笛，首尾都落在静音段，循环无缝；峰 0.28 |

## 14 · 新手引导

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 14-01 | `sfx_tutorial_step.wav` | 引导步骤推进 | 0.30s / 0.22s |  | 8 步，别太抢 |
| 14-02 | `sfx_tutorial_notice.wav` | 首次事件提醒 | 0.50s / 0.22s |  | 海鸥/夜潜/雷雨 |
| 14-03 | `sfx_tutorial_complete.wav` | 引导完成 | 1.68s / 1.68s |  | 原文件 3.36s，砍掉中间之后的后半段，只留前 1.68s（0.02s 静起 → 0.20s 起音阶上行、0.55s 处最亮）；尾 120ms 淡出；峰 0.34、rms 0.057 |

## 15 · 结局

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 15-01 | `sfx_end_win.wav` | 胜利 | 3.00s / 4.02s |  | 立体声 |
| 15-02 | `sfx_end_fail.wav` | 失败 | 3.00s / 1.99s |  | 立体声 |

## 16 · 底噪与环境

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 16-01 | `amb_ocean_ship_loop.wav` | 海面 + 船体底噪 | 15.00s / 15.00s | 循环 | 你上传的 weak-waves（80.26s）取 57.10–72.50s，400ms 等功率交叉；峰 0.22，质心 3426Hz（低频占 12%） |
| 16-02 | `amb_underwater_loop.wav` | 水下底噪 | 10.00s / 10.00s | 循环 | CC0 水下气泡（fs#493072）取 1.30–11.70s，400ms 等功率交叉；峰 0.20，质心 970Hz（低频占 71%，配 800Hz 低通刚好） |
| 16-03 | `amb_rain_loop.wav` | 雨声 | 21.00s / 21.00s | 循环 | 你上传的 thunderstorm-and-pouring-rain（62.80s）取 15.50–36.90s 纯雨段（两声雷之间，不含 14.0s 小轰与 37.5s 雷），400ms 等功率交叉、无首尾归零；峰 0.22，质心 5983Hz |
| 16-04 | `amb_cave_loop.wav` | 洞穴回响 | 10.00s / 10.00s | 循环 | CC0 洞穴滴水（fs#381418）取 54.10–64.50s，400ms 等功率交叉；峰 0.22，质心 1803Hz（10s 内约 2–3 次/秒） |
| 16-05 | `amb_night_wind_loop.wav` | 夜风 | 10.00s / 10.00s | 循环 | CC0 风穿缝（fs#829000）取 24.30–34.70s，400ms 等功率交叉；峰 0.20，质心 860Hz（低频占 66%） |
| 16-06 | `amb_distant_birds.wav` | 远处海鸟 / 鲸鸣 | 3.00s / 3.00s |  | 你上传的 herring-gulls（79.01s）取开头第一段叫声 1.95–4.95s，不循环；峰 0.28，质心 2371Hz |

## 18 · 警报与状态

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 18-01 | `sfx_alert_oxygen_low.wav` | 低氧警报 | 1.00s / 1.00s | 循环 | 你上传的 liecio-water-bubbles（34.04s）取 12.00–12.60s 一段气泡串（0.05s 帧起伏 0.15→0.63→0.30）+ 0.40s 静音，1s 循环 = "咕嘟→停"的心律；峰 0.26 |
| 18-02 | `sfx_alert_hull_low.wav` | 船体告急 | 0.80s / 0.80s |  | 你上传的 aviation-alarm（2.48s）取 0.10–0.90s，三声急促双音 beep，末尾 130ms 淡出让第三声渐弱收口；峰 0.35 |
| 18-03 | `sfx_alert_power_low.wav` | 电力告急 | 0.62s / 0.62s |  | 与 10-07 同一条 0.313s 提示音，原样拼接重复一遍 = 0.62s；峰 0.34、rms 0.071 |

## 19 · 拾取与入库

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 19-01 | `sfx_pickup_generic.wav` | 拾取通用 | 0.30s / 0.30s |  | 你上传的 item-pickup（0.53s）取能量最高的连续 0.30s（0.06–0.36s，丢掉头尾低电平）；峰 0.28 |
| 19-02 | `sfx_pickup_wood.wav` | 木材拾取 | 0.36s / 0.36s |  | 按你的要求改用 19-01 同一素材（item-pickup 0.53s）取 0.06–0.42s：与通用同音色，只多留 0.06s 尾；峰统一 0.28 |
| 19-03 | `sfx_pickup_metal.wav` | 金属拾取 | 0.40s / 0.40s |  | 同上，取 item-pickup 0.06–0.46s（比木材多含 0.39s 那记轻的第二击）；原 metallic-thud 的刺耳撞击峰已弃用；峰 0.28 |
| 19-04 | `sfx_pickup_fiber.wav` | 纤维拾取 | 0.30s / 0.30s |  | 你上传的 fallen-flowers-picked-up（1.61s）取能量最高的 0.30s（0.17–0.47s，包络 0.55→1.00→0.93）；峰 0.24 |
| 19-05 | `sfx_pickup_fish.wav` | 鱼获拾取 | 0.50s / 0.50s |  | 你上传的 fishing_bobber-retrieve（0.31s）整段保留，后补 0.19s 静音凑满 0.50s（素材短于目标，不做拉伸）；峰 0.30 |
| 19-06 | `sfx_bank_deposit.wav` | 入库 | 0.70s / 0.70s |  | 同上，用 item-pickup 整条 0.00–0.53s + 补 0.17s 静音凑满 0.70s（素材短于槽位，不拉伸）；峰统一 0.28 |

## 20 · P2 变体与打磨

| 编号 | 文件 | 用途 | 时长(标/实测) | 循环 | 取材与制作 |
| --- | --- | --- | --- | --- | --- |
| 20-01 | `sfx_crew_step_deck_02.wav` | 脚步 变体 2 | 1.20s / 1.20s |  | 与主步同一条左右脚循环，Unity 侧随机 pitch ±4% 使用 |

## 附：这份清单之外的东西

- `audio/_alt/`：旧版与回退件，含 `_vol_backup/`（音量统一前的全部原件），**不要进 Unity**。
- `audio/demo/`：试听长条，不占槽位。
- `audio_volume_report.csv`：每条的统一前后峰值 / rms。
- `AUDIO_SOURCES.md`：素材来源与制作口径（23 节，含每版的取舍原因）。
- `preview.html`：试听与体检页（浏览器打开）。
