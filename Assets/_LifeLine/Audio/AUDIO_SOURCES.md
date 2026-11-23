
> **2026-11-24 注**：本文多处提到的 `_free_sources/` 素材候选库（582 MB / 344 个文件）已按要求整体删除，文中这些路径仅作历史溯源记录，文件已不在磁盘上。已采用的音频全部在 `audio/SFX/**` 与 `audio/Music/**`（共 156 条），并在 `audio/_alt/_vol_backup/` 保留了音量统一前的原始件。
# 04/05 免费音效素材来源与用法对照

2026-11-20 · 为 LifeLine 绞盘/绳索两组找的免费素材。**已下载的那包在 `cc0_wood_metal/`（CC0，可商用可再分发）**，其余是链接与许可说明。

## 一、已下载：CC0 木质 foley 100 条

- 包名：`100 CC0 metal and wood SFX`
- 作者：rubberduck ｜ 2018-07-04 ｜ **CC0 1.0**
- 来源：https://opengameart.org/content/100-cc0-metal-and-wood-sfx
- 位置：`_free_sources/cc0_wood_metal/`（100 个 ogg + `LICENSE.txt`）
- 说明：真实录制的木头敲、裂、断、吱呀，正好覆盖绞盘与绳索需要的“木质受力”层。原始是 ogg，入 Unity 前转 44.1k/16bit/mono wav。

## 二、九个槽位怎么用（先当“素材层”，再拼）

| 槽位 | 推荐素材 | 用法 |
| --- | --- | --- |
| 04-01 `sfx_winch_loop` 1.7s 循环 | `wood_squeak_01/02` + `wood_hammer_01` + `wood_hit_01` | 吱呀当连续底，木槌声切成 1.7s 内的固定节奏当齿咬，收尾交叉淡化 |
| 04-02 `sfx_winch_success` 0.55s | `wood_slam_02` + `wood_hit_07` + `wood_squeak_02` 尾 | 重木闷响落地 + 亮一点的回弹 + 绳绷紧短吱呀 |
| 04-03 `sfx_winch_fail` 0.56s | `wood_cracking_02` + `wood_falling_03` + `wood_slam_04` | 开裂起头、木料回落、低闷砸收尾 |
| 04-04 `sfx_winch_engage` 0.20s | `wood_hit_05` + `wood_cracking_01` 头 0.1s | 单下干脆咬合，短、干、近 |
| 05-01 `sfx_cargo_strain_loop` 2.6s | `user_rope/the-sound-of-a-rope-being-pulled-very-tight.mp3`（用户提供，17.49s / 44.1k / mono / 整体质心 525Hz） | v7：取 13.60–15.60s（素材里最稳的持续受力段）+ 100ms 首尾交叉，2.00s 循环 + 0.60s 尾静音（前 40ms 淡出），增益到峰 0.26；素材开头 0–1.2s 是最大一次拉扯（不适合循环），中段为断续小拉扯，尾部 14.4s 后是稳态摩擦 |
| 05-02 `sfx_rope_snap` 0.40s | `wood_breaking_02` + `wood_cracking_03` + 噪声鞭梢 | 木裂做主体，补一层高频撕裂当绳断 |
| 05-03 `sfx_rope_attach` 0.30s | `wood_hit_01` + `wood_squeak_02` 短尾 | 木头咬合 + 绳收紧的一点摩擦 |
| 05-04 `sfx_cargo_shove` 0.45s | `wood_slam_01` 低通 + 气泡/水推（合成） | 水下 muffled 砸 + 刮擦，整体 600~800Hz 以下 |
| 05-05 `sfx_crate_pack` 0.35s | `wood_hit_03` + `wood_close_01` + `wood_misc_05` | 放东西、合盖、内部轻碰 |

> 这包没有“绳索”实录，绳子的纤维感要靠 `wood_squeak` 变调 + 噪声层补；水下推箱的水声也要合成。

## 三、其他免费来源（按优先级）

1. **Unity Asset Store｜Wooden Fence Destruction (Free Sample Pack)** — InspectorJ Audio，免费，Standard EULA。
   https://assetstore.unity.com/packages/audio/sound-fx/wooden-fence-destruction-free-sample-pack-144362
   5MB，木栅栏破坏/断裂，最适合 `rope_snap`、`crate_pack` 的碎木层。可直接在 Unity Package Manager 里领。
2. **Sonniss GDC Game Audio Bundle** — 全库免费、可商用、免署名（但许可禁止把原始音效当素材包再分发）。
   https://sonniss.com/gameaudiogdc/ ｜ 2026 版 https://gdc.sonniss.com/
   7.47GB 分 5 卷，里面 wood creak / rope / winch / cargo foley 都有，适合一次下齐长期用。
3. **Freesound（需登录，逐条看许可）**
   - Wood Creak 01（KVV_Audio，96kHz/24bit，14s）：https://freesound.org/people/KVV_Audio/sounds/796507/
   - Rope under tension（gear_clinkz，58s）：https://freesound.org/people/gear_clinkz/sounds/547862/
   - Whip Crack 01（CGEffex，48k）：https://freesound.org/people/CGEffex/sounds/93100/ —— 做绳断的鞭梢层
4. **Mechanical Gears SFX Vol.01** — meloraw，免费 21 条，含 wooden ratchets 与 crane winch。
   https://meloraw.com/sfx-packs/mechanical-gears-sfx-vol-01/
5. **Shapeforms Audio Free Sounds** — 180 条免费、免署名、可商用，偏 UI/打击/科幻，可补 `crate_pack` 的物件声。
   https://shapeforms.itch.io/shapeforms-audio-free-sfx ｜ https://www.shapeforms.com/shop/p/shapeforms-audio-free-sounds （115MB 整包）

## 四、许可红线

- CC0（本包）：随便用、可改、可再分发，不用署名。
- Unity Asset Store 免费包：受 Standard EULA 约束，只能在本项目/团队内用，**不能把原始文件再分发**。
- Sonniss：可商用、免署名，但**不能把音效原样打包转给他人**；团队内协作用可以。
- Freesound：逐条看 CC0 / CC-BY，CC-BY 要署名，下载需账号。

## 五、06 角色动作：脚步实录（2026-11-22）

**现用（v4）**：用户新提供的 `E:\下载\the-sound-of-a-man39s-footsteps-on-dry-ground-in-shoes.mp3`（6.02s、44.1k 单声道、峰值 0.125），已原样存到 `_free_sources/user_step/`。

- 处理链路：原样解码（本身 44.1k，无需重采样）→ 取相邻两声（起音 0.64s 与 1.24s，各前留 0.02s 静默底）→ 首尾 40ms 交叉 → 整体增益到峰值 0.200，成品 **1.200s**，步间隔 **0.60s**（约 100 步/分）。
- 两声取自同一段连续录音，左右脚本身就不同，不再用两条单步文件拼接。
- `sfx_crew_step_deck.wav` 与 `_02.wav` 内容完全相同（MD5 一致），变体仍靠 Unity 端随机 pitch ±4% 做变化。
- 检测结果：成品内起音在 0.02s 与 0.62s，尾部 50ms 电平 0.00002（落在静默里），接缝无爆音。
- 只做截取、40ms 交叉、统一增益；没有 EQ、压限、拉伸。

**上一版（v3，已替换）**：`走路1.wav`（左脚）+ `走路2.wav`（右脚）各取 0.30s，左右放在 0.00 / 0.583s，总长 1.167s、103 步/分。早期三版（`walk_user.wav` / `_v2` / `_v3`）仍在 `_free_sources/user_walk/` 可回退。

以下为之前的选材记录，均退为备选：

| 源文件 | 原页 | 用时 | 用途 |
| --- | --- | --- | --- |
| `tennis_8878.mp3`（0.169s） | https://free-sound-effects.net/sound/8878-footsteps-tennis-shoes-hollow-wood | 已停用 | 普通运动鞋单步，用户改用自备走路音 |
| `tennis_8879.mp3`（0.335s） | https://free-sound-effects.net/sound/8879-footsteps-tennis-shoes-hollow-wood | 未用 | 备选：同系列第二条，低频 71%、更闷更长 |
| `shoes_single_8827.mp3`（0.415s） | https://free-sound-effects.net/sound/8827-footsteps-shoes-single-step-hollow-wood | 未用 | 备选：单步但低频 68%，偏闷 |
| `shoe_step_8818.mp3`（0.119s） | https://free-sound-effects.net/sound/8818-footsteps-shoe-step-hollow-wood | 未用 | 备选：太短（0.12s）且高频 22%，偏脆 |
| `dress_8691.mp3`（0.383s） | https://free-sound-effects.net/sound/8691-footsteps-dress-shoe-step-hollow | 未用 | 备选：皮鞋，低频厚、峰值 0.976 |
| `workboots_solid_9031.mp3`（11.79s） | https://free-sound-effects.net/sound/9031-footsteps-work-boots-solid-wood-surface-walk | 已弃用 | 工靴踩实木，用户反馈“换普通音色”后退下 |
| `workboots_slow_24424.mp3`（27.64s） | https://free-sound-effects.net/sound/24424-footsteps-work-boots-solid-wood-surface-slow-walk | 未用 | 备选：更慢更沉，适合负重/重落脚 |
| `hardsole_single_8739.mp3`（0.48s） | https://free-sound-effects.net/sound/8739-footsteps-hard-sole-shoes-single-step-hollow-wood | 已弃用 | 硬底鞋踩空心木，试听判定“太硬太脆” |
| `cowboy_single_8679.mp3`（0.40s） | https://free-sound-effects.net/sound/8679-footsteps-cowboy-boots-wood-single-step | 已弃用 | 皮靴单步，高频 22%，更脆 |
| `cowboy_slowwalk_8682.mp3`（5.66s） | https://free-sound-effects.net/sound/8682-footsteps-cowboy-boots-wood-walk-slow | 未用 | 备选 |
| `creaky_8684.mp3`（4.00s） | https://free-sound-effects.net/sound/8684-footsteps-creaky-wood | 未用 | 甲板吱呀，适合环境细节，不适合常规步频 |

成品一律 44.1k/16bit/mono wav，过零点截取，没有 EQ、压缩、淡化或叠层。

## 六、06 爬梯循环（2026-11-22）

**现用（v2，已回退）**：用户 2026-11-22 提供的单步 `梯子踩踏声_04.wav`（0.31s、44.1k 单声道、峰值 1.000），原样存在 `_free_sources/user_ladder/ladder_step_user_04.wav`。

`sfx_crew_ladder_loop.wav` 的构造：把这个单步原封不动放两次，步头在 0.00s 与 0.70s，循环长 **1.40s**，最后 0.09s 落在该步尾部的近静默段；整体增益到峰值 0.280（−11.1 dBFS）。循环首尾相接时，第二步到下一轮第一步也正好 0.70s，步频完全均匀（约 86 步/分），接缝在静默里。两步是同一段波形，没有电平差。

**已停用（2026-11-23 试作，已回退）**：v3~v7 用 `sound-a-man-climbs-a-ladder.mp3` 做过多种变体 —— 分别取第 2、3 / 3、4 / 4、5 次踩踏，步距 0.98 / 0.93→0.76 / 0.80 / 0.66，单步最短截到 0.19s 并叠加音高 +8% 与 70Hz 高通。脚本 `_local_ladder_seg45.py`、`_local_ladder_seg34_fast.py`、`_local_ladder_seg34_crisp.py` 保留，需要时可再跑。素材仍在 `_free_sources/user_step/`。

未做 EQ、压限、拉伸。

未做 EQ、压限、拉伸。

早期木楼梯实录仍保留作比较：`stairs_24419.mp3`（7.50s，https://free-sound-effects.net/sound/24419-footsteps-on-wooden-stairs ）前两步 0–1.20s，峰值 0.300。备用 `stairs_24418.mp3`（15.50s）。

## 七、06 游泳循环（2026-11-22）

`sfx_crew_swim_loop.wav` 用的是已下载水声 `fse_water/water_pool_entry_38454.mp3`（7.86s）里最平稳的 1.2 秒：0.42–1.62s。

- 选这一段是因为 0.4s 之后是入池后持续的水花段，再往后（约 2.4s 起）已经衰减成近乎静音；这一段头尾 100ms 电平 0.113 / 0.121，最接近。
- 切点各自吸到 ±15ms 内绝对值最小的样本（head 0.0001），末尾 0.20s 做余弦淡出（tail 0.0000）。
- 成品 1.899s（水声 1.199s + 0.70s 静音）、44.1k/16bit/mono、峰值 0.300（−10.5dBFS）；只做截取、增益、尾部渐变与补静音，没 EQ、压限。

> 前一条用 `rock_splash_30809`（2.40–3.60s）的版本已被替换，源文件仍在目录里可回退。

## 八、06 冲刺呼吸（2026-11-22）

`sfx_crew_sprint_breath.wav` 取自 free-sound-effects 的 `Breath Running Male`（16.18s 连续跑步喘息，峰值 0.873）。

- 详情页 https://free-sound-effects.net/sound/18146-breath-running-male
- 直链 https://free-sound-effects.net/mp3/03/free-sound-1674931609.mp3
- 已存 `_free_sources/fse_breath/breath_running_male_18146.mp3`
- 取 2.85–3.85s 的 1.0 秒：首 60ms RMS 0.017、尾 60ms 0.010，都落在两次喘之间的气口上，所以循环接缝是“呼吸间隙接间隙”，不会硬切。
- 成品 1.000s、44.1k/16bit/mono、峰值 **0.100（−20dBFS）**；只做截取与增益。
- 建议用法：冲刺开始时按 loop 播放、停止/松开时停；音量比脚步再低 6dB 左右，只做氧气压力的提示。

> 这条实录是“连续粗重呼吸”，不是强节奏的喘气；如果之后要更戏剧化的“每步一喘”，可换 `Breathe Fast Male`（sound/18151）。

## 九、06 受击（2026-11-22）

`sfx_crew_hurt.wav` 用 free-sound-effects 的 `Punch Body Cloth`（0.49s，布料包裹的闷击，**无人物叫声**）。

- 详情页 https://free-sound-effects.net/sound/12910-punch-body-cloth
- 直链 https://free-sound-effects.net/mp3/03/free-sound-1674878647.mp3
- 已存 `_free_sources/fse_impact/body_cloth_12910.mp3`
- 取 0–0.322s，峰值 0.350（−9.1dBFS），44.1k/16bit/mono；只做截取与增益。
- 频谱以 200Hz 以下为主（57%），中频 11%、2k 以上 13%，是“闷在衣服里”的撞击，不会像拳拳到肉的脆响。

同页另存的三条备选（都在 `_free_sources/fse_impact/`）：

| 文件 | 时长 | 特征 | 适合 |
| --- | --- | --- | --- |
| `body_low_crunch_30107.mp3` | 0.48s | 80% 能量在 200Hz 以下，更沉 | 重击 / 鲨咬拖拽 |
| `punch_sub_12941.mp3` | 0.19s | 89% 低频、几乎无高频 | 纯低频冲击（可做水下受击） |
| `flesh_6798.mp3` | 5.09s | 39% 高频，拍击感强、偏“打脸” | 需要“脆”的场合 |

## 十、06 倒地 / 昏厥（2026-11-22）

`sfx_crew_downed.wav` 用 `Bodyfall Wood Double Thud`（0.67s，木地板双闷响，身体砸地）。

- 详情页 https://free-sound-effects.net/sound/6631-bodyfall-wood-double-thud
- 直链 https://free-sound-effects.net/mp3/03/free-sound-1674747349.mp3
- 已存 `_free_sources/fse_impact/bodyfall_wood_double_6631.mp3`
- 整段保留 0.670s，峰值 0.350（−9.1dBFS），44.1k/16bit/mono；只做截取与增益。
- 频谱 200Hz 以下占 50%、中频 18%、2k 以上 17%，是“人砸在木甲板上的两声闷响”，不含任何语音。

同批备选（`_free_sources/fse_impact/`）：`human_body_fall_10572.mp3`（1.15s，67% 低频，更绵长，适合慢镜昏厥）、`bodyfall_wood_messy_17880.mp3`（0.80s，更散）、`bodyfall_mat_clothes_17879.mp3`（2.82s，布料慢落，适合拖拽倒地）。

## 十一、06 扶起三条（2026-11-22）

全部不含人声。

| 槽位 | 文件 | 素材 | 取用 | 时长 / 峰值 |
| --- | --- | --- | --- | --- |
| 06-09 船上扶起 | `sfx_crew_revived.wav` | `fse_cloth/cotton_wipes_7721.mp3`（布衬衫手擦 1.19s） | 0–0.949s | 0.95s / 0.300（−10.5dBFS） |
| 06-10 水下扶起 | `sfx_crew_revive_water.wav` | `fse_water/splash_34566.mp3`（4.62s） | 0.19–1.09s | 0.90s / 0.400（−8.0dBFS） |
| 06-11 4 秒扶起循环 | `sfx_crew_revive_loop.wav` | `fse_cloth/cloth_movement_7716.mp3`（4.65s） | 0.10–4.00s + 0.005s 静音 | 4.000s / 0.220（−13.2dBFS） |

- 06-09 的 `cotton_wipes` 是干燥布料的擦蹭，像抓着衣服把人拽起来。
- 06-10 的水花取自 `splash_34566` 前段（0.5s 附近有一次强水花），听感是水中拉扯 + 出水，和"上船"用的 `lunge_38507` 不是同一条，不会撞。
- 06-11 循环首尾都是 0.0000，布料的持续摩擦在 4 秒内完成一轮起落。
- 三条都只做截取、首尾近零吸附、整体增益；`cloth_movement` 全程无语音。
- 备选：`fse_cloth/jeans_sequence_8518.mp3`（38.19s，牛仔布料动作序列，可再切更"挣扎"的版本）、`nylon_struggle_8519.mp3`（17.37s，极轻，仅适合很近的贴身摩擦）。

## 十二、06 被绞盘拖拽循环（2026-11-22）

`sfx_crew_winched_loop.wav` 用 `Fire Hose, Hose Being Dragged`（6.97s，软管在甲板上被拖行的连续摩擦，无语音）。

- 详情页 https://free-sound-effects.net/sound/27-fire-hose-hose-being-dragged
- 直链 https://free-sound-effects.net/mp3/01/free-sound-effects-AA2G39A6.mp3
- 已存 `_free_sources/fse_drag/fire_hose_dragged_27.mp3`
- 取 0.29–3.36s（这一段是两段停顿之间的连续拖行），3.069s 循环；循环首尾都落在拖行的停顿点上（head 0.0000 / tail 0.0070），接缝不会硬断。
- 源电平只有 peak 0.118，整体增益 2.5 倍到 0.300（−10.5dBFS），只做截取与增益。
- 听感是"布/皮在木板或湿甲板上被拽着走"，跟 04 组手摇绞盘（`chain_winch_596`）不是一条，不会被认成绞盘本身在转。

备选（`_free_sources/fse_drag/`）：`leather_pull_creak_11237.mp3`（1.46s，皮革拉紧吱呀，峰值 0.814，可在拖拽开始瞬间叠一下“绳绷紧”）、`rope_pulls_nylon_13215.mp3`（11.90s，绳穿尼龙，很轻，适合近距离绳摩擦细节）。

## 十三、UI 与系统 12 条（2026-11-22）

素材包：**Kenney UI Audio**（50 个 wav，CC0 1.0，无需署名）。原始页面 https://kenney.nl/assets/ui-audio ，这里用的是 GitHub 镜像 https://github.com/Calinou/kenney-ui-audio ，压缩包留在 `_free_sources/kenney_ui/kenney_ui_audio.zip`，解压在 `_free_sources/kenney_ui/extract/`。

| 槽位 | 文件 | 素材 | 时长 / 峰值 |
| --- | --- | --- | --- |
| 12-01 UI 点击 | `sfx_ui_click.wav` | Kenney `click1` 截 0.09s | 0.090s / 0.250 |
| 12-02 UI 悬停 | `sfx_ui_hover.wav` | Kenney `rollover2` | 0.060s / 0.120 |
| 12-03 返回 | `sfx_ui_back.wav` | Kenney `switch12` | 0.068s / 0.180 |
| 12-04 禁用 / 队列满 | `sfx_ui_deny.wav` | **合成**：700→466Hz 双音下行 | 0.260s / 0.260 |
| 12-05 开关切换 | `sfx_ui_toggle.wav` | Kenney `switch7` | 0.210s / 0.220 |
| 12-06 滑杆 | `sfx_ui_slider.wav` | Kenney `mouserelease1` | 0.086s / 0.150 |
| 12-07 页签切换 | `sfx_ui_tab.wav` | Kenney `click2` | 0.057s / 0.200 |
| 12-08 手动存档 | `sfx_save_manual.wav` | Kenney `switch21` | 0.400s / 0.280 |
| 12-09 自动存档 | `sfx_save_auto.wav` | Kenney `switch24` | 0.280s / 0.200 |
| 12-10 存档失败 | `sfx_save_fail.wav` | **合成**：520→392→311Hz 下行三音 | 0.500s / 0.260 |
| 12-11 联机加入 | `sfx_online_join.wav` | **合成**：523→659→784→1046Hz 上行琶音 | 0.600s / 0.260 |
| 12-12 联机连接失败 | `sfx_online_fail.wav` | **合成**：440→330→247Hz 下行三音 | 0.600s / 0.260 |

- 9 条直接取自 Kenney 原样，只做截取与整体增益，没有改音色。
- 4 条（禁用 / 存档失败 / 联机加入 / 失败）在素材包里没有对应的“负向提示”，改为合成：正弦 + 二次谐波 + 3ms 淡入 + 指数衰减，音色故意压得很轻，和 Kenney 的机械咔哒做分层。
- 联机两条是占位音，P2P 尚未接入（`IGameTransport` 只有接口），接入后按真实 lobby 流程再调。README 里没有“联机”素材需求，如果之后要换真人声提示，另开一条。

## 十四、08 炮塔与维修共用 4 条（2026-11-23）

全部实录 / 原样使用，没有合成。

| 槽位 | 文件 | 素材 | 处理 | 时长 / 峰值 |
| --- | --- | --- | --- | --- |
| 08-01 炮塔建造 | `sfx_turret_build.wav` | `fse_turret/socket_wrench_3366.mp3`（气动套筒扳手 3.30s） | 0.05–1.04s 截取 | 0.993s / 0.280 |
| 08-02 炮塔转向 | `sfx_turret_rotate_loop.wav` | **复用 04 已定稿的 `sfx_winch_loop.wav`**（`chain_winch_596` 链条/铰链循环） | 取 1.00–2.20s，50ms 首尾交叉 | 1.200s / 0.250 |
| 08-03 炮塔开火 | `sfx_turret_fire.wav` | `fse_turret/cannonfire_7059.mp3`（实炮 3.78s） | 0–0.61s 截取 | 0.608s / 0.450 |
| 08-04 维修失败 | `sfx_repair_fail.wav` | `cc0_wood_metal/metal_hit_05.ogg`（0.33s 金属哑响） | 整段 | 0.319s / 0.300 |

- 素材页：https://free-sound-effects.net/sound/3366-sfx-tools-socket-wrench 、`/sound/13512-servo-moving-truck-long` 、`/sound/7059-cannonfire`
- 转向循环改用 04 组已定稿的绞盘/链条铰链音（同一份 `chain_winch_596`），取 1.2s 并在首尾做 50ms 线性交叉，避免循环“咔”；伺服长音（`servo_long_13512.mp3`）仍留在 `fse_turret/` 里，若之后想要更“电动”的炮塔可回退。
- 开火这条峰值放到 0.450（−6.9dBFS），是炮塔类里最响的一档，配合水面/船体的尾音。
- 07 工位与设备还剩 25 条（28 槽中 `equipment_unlock`、`light_on`、`light_off` 已有），素材方向：CC0 木金属包 + free-sound-effects 的机械/水声/鱼线实录，逐条挑，不用合成。

**关于 Minecraft**：MC 原版音乐与音效属 Mojang/Microsoft 版权，不能直接提取使用；需要“MC 味”只能找气质相近的免费曲目（C418 式极简钢琴/环境底噪），或自己做。免费候选可看 OpenGameArt 的 CC-BY 环境钢琴（如 https://opengameart.org/content/ambient-piano ，作者 tcarisland，CC-BY 4.0，需署名）与 Kitty Lamp Studio 的免费钢琴包（https://kittylampstudio.itch.io/piano-music-pack ，免费下载，商用前看页面说明）。


## 十五、07 工位与设备 25 条（2026-11-23）

全部实录 / 原样使用，没有合成。素材来自 CC0 木金属包（`cc0_wood_metal/`）与 free-sound-effects（`fse_station/`、`fse_water/`、`fse_step/`）。

| 槽位 | 文件 | 素材 | 处理 | 时长 / 峰值 |
| --- | --- | --- | --- | --- |
| 07-01 面板开 | `sfx_station_panel_open.wav` | `cc0_wood_metal/metal_open_01.ogg` | 0–0.25s | 0.251s / 0.280 |
| 07-02 面板关 | `sfx_station_panel_close.wav` | `cc0_wood_metal/metal_close_01.ogg` | 0–0.22s | 0.215s / 0.280 |
| 07-04 升级成功 | `sfx_upgrade_success.wav` | `cc0_wood_metal/metal_slam_01.ogg` | 0–0.70s（源仅 0.56s） | 0.562s / 0.320 |
| 07-05 升级失败 | `sfx_upgrade_fail.wav` | `cc0_wood_metal/metal_hit_04.ogg` | 0–0.50s（源仅 0.36s） | 0.362s / 0.300 |
| 07-06 舵轮循环 | `sfx_wheel_loop.wav` | `fse_step/creaky_8684.mp3`（木甲板吱呀 4.00s） | 0.40–1.60s，首尾 100ms 交叉 | 1.200s / 0.240 |
| 07-07 换向咔哒 | `sfx_wheel_reverse.wav` | `cc0_wood_metal/keys_02.ogg` | 0–0.15s | 0.149s / 0.260 |
| 07-10 探照灯嗡鸣 | `sfx_light_hum_loop.wav` | `fse_station/hum_24526.mp3`（冰箱嗡鸣 7.44s） | 2.00–3.50s，80ms 交叉 | 1.500s / 0.220 |
| 07-11 抛竿 | `sfx_fishing_cast.wav` | `fse_station/cast_19446.mp3`（0.90s） | 0.30–0.65s | 0.352s / 0.300 |
| 07-12 鱼讯 | `sfx_fishing_bite.wav` | `fse_fishing/853279_lure_plop_game_hq.mp3`（拟饵落水，作者 el_boss，游戏用钓具音效） | 0–0.30s，末尾 30ms 淡出 | 0.300s / 0.300 |
| 07-13 收线循环 | `sfx_fishing_reel_loop.wav` | `fse_fishing/716635_old_reel_fast_rewind_hq.mp3`（老式卷线器快速倒线 3.93s，作者 AudioPapkin） | 0.40–1.40s，50ms 交叉 | 1.000s / 0.260 |
| 07-14 普通鱼 | `sfx_fish_common.wav` | `fse_water/rock_splash_30809.mp3` | 0–0.60s | 0.599s / 0.350 |
| 07-15 品质鱼 | `sfx_fish_rare.wav` | `fse_water/water_pool_entry_38454.mp3` | 0.40–1.30s | 0.893s / 0.350 |
| 07-16 鱼逃脱 | `sfx_fish_escape.wav` | `fse_water/splash_34566.mp3` | 0.20–0.80s | 0.599s / 0.320 |
| 07-17 修船敲击 | `sfx_repair_hammer.wav` | `cc0_wood_metal/hammer_01.ogg` | 0–0.30s | 0.300s / 0.340 |
| 07-18 过滤器接线 | `sfx_filter_wire.wav` | `cc0_wood_metal/metal_hit_02.ogg` | 0–0.40s | 0.390s / 0.300 |
| 07-19 过滤器加压 | `sfx_filter_pressurize.wav` | `fse_station/handpump_824.mp3` | 0.60–1.20s | 0.606s / 0.300 |
| 07-20 擦油污循环 | `sfx_oil_scrub_loop.wav` | `fse_station/scrub_14666.mp3`（海绵擦洗 36.54s） | 3.00–4.20s，交叉 | 1.200s / 0.220 |
| 07-21 符号显现 | `sfx_symbol_reveal.wav` | `fse_station/chime_335.mp3`（风铃树） | 0.30–0.80s | 0.496s / 0.300 |
| 07-22 选错故障件 | `sfx_wrong_part.wav` | `cc0_wood_metal/metal_hit_03.ogg` | 0–0.45s（源仅 0.39s） | 0.392s / 0.280 |
| 07-23 引擎启动 | `sfx_engine_start.wav` | `fse_station/engine_start_22966.mp3`（2.90s） | 0–1.20s | 1.208s / 0.400 |
| 07-24 手摇发电循环 | `sfx_hand_crank_loop.wav` | `fse_station/ratchet_crank_11369.mp3`（11.52s） | 2.00–3.20s，交叉 | 1.200s / 0.240 |
| 07-25 加燃料 | `sfx_fuel_add.wav` | `fse_station/pour_760.mp3`（倒液 5.17s） | 0.60–1.00s | 0.410s / 0.220 |
| 07-26 装电鳗放电 | `sfx_eel_discharge.wav` | `fse_station/zap_5838.mp3`（1.07s） | 0–0.70s | 0.695s / 0.450 |
| 07-27 接线成功 | `sfx_repair_wire_connect.wav` | `cc0_wood_metal/metal_hit_01.ogg` | 0–0.40s（源仅 0.25s） | 0.252s / 0.280 |
| 07-28 维修完成 | `sfx_repair_complete.wav` | `cc0_wood_metal/wood_slam_02.ogg` | 0–1.00s（源仅 0.47s） | 0.469s / 0.320 |

- 素材页：`/sound/22966-engine-start-engine`、`/sound/19446-cast-fishing-line`、`/sound/11369-little-ratchet-crank`、`/sound/760-coffee-pouring-liquid`、`/sound/24526-fridge-operating-hum`、`/sound/14666-sponge-scrubbing-various`、`/sound/824-pump-hand-pump-tire-pump`、`/sound/23-fire-hose-hose-being-reeled-in`、`/sound/5838-bit-laser-zap`、`/sound/335-chimes-bell-tree`
- 舵轮循环最初用 `wood_squeak_01.ogg`，但该文件只有 0.478s、能量集中在 0–0.30s，取到 0.50–1.70s 是静音（成品峰值 0.000）。改用 `creaky_8684` 木甲板吱呀（4.00s，有声段在前 1.9s），取 0.40–1.60s，舵轮的“吱呀—顿—再吱呀”节奏更接近转舵。
- `metal_slam_02.ogg` 文件损坏（libsndfile 打不开），维修完成改用 `wood_slam_02.ogg`。
- 5 条 CC0 源比目标时长短（升级成功 / 失败、选错件、接线成功、维修完成），成品按源长，页面标实际时长。
- 鱼讯与收线循环改用 freesound 上的真实钓具素材（原 `fse_station/reel_23.mp3` 是消防水带收卷，当鱼线用偏笨重）：本条两条均为 **CC0 公共领域**，用 hq 预览（44.1kHz stereo）转单声道，没有做任何滤波。`fse_fishing/` 里另有 18 条候选（fly reel、spinning reel、casting、rod swoosh 等），要换随时可替。
- 全部 44.1kHz / 16bit / mono，写入 `audio/SFX/Station/` 与 `Assets/_LifeLine/Resources/Audio/SFX/Station/`。

## 十六、16 底噪与环境 6 条（2026-11-23，v4：海面/海鸥用用户素材，雨声去雷，长循环）

v4 的三处改动：海面与海鸥换成用户上传的两条素材；雨声原来的 `rainwood_256258` 是雷雨录音（峰值达中位数 39 倍、19.9% 的帧超过 3 倍中位），换掉；循环拉长到 15s，避免听出周期。

| 槽 | 成品 | 素材 | 截取 | 成品 |
| --- | --- | --- | --- | --- |
| 16-01 海面 + 船体底噪 | `amb_ocean_ship_loop.wav` | `user_amb/weak-waves-on-the-shore-of-a-calm-sea.mp3`（用户上传，80.26s） | 57.10–72.30s，200ms 交叉 | 15.000s / 0.220，质心 3425Hz |
| 16-02 水下底噪 | `amb_underwater_loop.wav` | `amb_cand2/bub_493072_1929421.mp3`（fs#493072，30.00s） | 1.30–11.50s，200ms 交叉 | 10.000s / 0.200，质心 963Hz，<500Hz 占 71% |
| 16-03 雨声 | `amb_rain_loop.wav` | `amb_cand/rain_537762_11976230.mp3`（fs#537762，82.2s，质心 1494Hz，峰值 2.38 倍中位） | 29.70–44.90s，200ms 交叉 | 15.000s / 0.220，质心 2038Hz（窗口峰值仅 1.33 倍中位，无雷/无爆响） |
| 16-04 洞穴回响 | `amb_cave_loop.wav` | `amb_cand2/drip_381418_3530854.mp3`（fs#381418，65.62s） | 54.10–64.30s，200ms 交叉 | 10.000s / 0.220，质心 1801Hz（约 2–3 次滴水/秒） |
| 16-05 夜风 | `amb_night_wind_loop.wav` | `amb_cand2/howl_829000_11519060.mp3`（fs#829000，107.68s） | 24.30–34.50s，200ms 交叉 | 10.000s / 0.200，质心 856Hz，<500Hz 占 66% |
| 16-06 远处海鸟 / 鲸鸣 | `amb_distant_birds.wav` | `user_amb/herring-gulls.mp3`（用户上传，79.01s） | 69.20–72.20s（两声叫），不循环 | 3.000s / 0.280，质心 1577Hz |

- 去雷判据：0.2s 帧算 RMS，取全条中位数作基准；窗口内峰值必须 ≤3 倍中位（雷声、阵风这类爆响会远超），再按低频占比挑段。原雷雨素材 `rainwood_256258` 的 3.0–7.6s、41–52s、439–456s 等段峰值达 12–39 倍中位，都是雷。
- 重复感：4s→10s→15s，海面与雨各 15s（约 1.29MB），水下/洞穴/风 10s，鸟叫 3s 一次性；循环接缝 200ms 交叉 + 首尾 15ms 归零。
- 用户素材归档在 `user_amb/`；CC0 候选仍留在 `amb_cand`、`amb_cand2` 可回退。
- 全部 44.1kHz / 16bit / mono，写入 `audio/SFX/Ambience/` 与 `Assets/_LifeLine/Resources/Audio/SFX/Ambience/`。

## 十七、2026-11-23 补充：雨声改用雷雨实录，开头雷声拆成 11 组两条

用户上传 `thunderstorm-and-pouring-rain.mp3`（62.80s，44.1kHz stereo）。逐 0.5s 扫描：0.5–1.5s 第一声雷（相对电平 0.95/0.72）、14.0s 一次小轰（0.49）、37.5–39.5s 第二声雷（1.00）、其余为稳定雨声（0.20–0.26），60s 后收尾。

| 槽 | 成品 | 截取 | 成品规格 |
| --- | --- | --- | --- |
| 16-03 雨声 | `amb_rain_loop.wav` | 20.00–35.20s 纯雨段，200ms 交叉 | 15.000s / 0.220，质心 5979Hz |
| 11-03 近雷 | `sfx_thunder_near.wav` | 37.30–39.80s（第二声雷，最响） | 2.500s / 0.450，质心 3712Hz，尾 60ms 淡出 |
| 11-04 远雷 | `sfx_thunder_far.wav` | 0.20–3.20s（第一声雷，偏闷偏轻） | 3.000s / 0.380，质心 3675Hz，尾 60ms 淡出 |

- 雨段选的 20–35s 正好在两声雷之间（14.0s 之后、37.5s 之前），窗口内无非雨爆响；雷段保留原始整段衰减，未做低通/高通。
- 顺带废弃了上一版的 CC0 雨声 `rain_537762`（仍留在 `amb_cand/` 里可回退）。
- 写入 `audio/SFX/Ambience/`、`audio/SFX/World/` 与 `Assets/_LifeLine/Resources/Audio/SFX/` 对应目录；素材归档 `user_amb/thunderstorm-and-pouring-rain.mp3`。

## 十八、2026-11-23 循环接缝修复（等功率交叉）

雨声试听反馈"首尾循环有点突兀"。原因是接缝算法，不是素材：

- 旧做法用 `0.5(1-cos t)` 与 `0.5(1+cos t)` 做线性交叉，两路增益和为 1；对不相关噪声，能量按 `a²+b²` 叠加，交叉中点只剩 0.5，也就是 **−3dB 的 200ms 能量凹陷**，每次循环都会掉一下。
- 另外旧版在首尾各做 15ms 余弦归零，等于在循环点又压出一个 30ms 的静音坑。
- 两者叠加，就是听出来的"顿一下"。

新做法：

- **等功率交叉**：交叉增益用 `sin`/`cos`（`fi²+fo²=1`），噪声能量全程恒定，无凹陷。
- **取消首尾归零**：等功率交叉后 `y[0]=seg[n]`、`y[-1]=seg[n-1]` 本来就是相邻采样，样本级天然连续，不需要再压零。
- 交叉长度 200ms → **400ms**；雨声窗口加长到 **21.00s**（15.50–36.90s），减少同一段起伏的重复频率。

重建后的接缝校验（`|y[0]−y[-1]|`）：rain 0.00055、ocean 0.00296、underwater 0.00159、cave 0.01022、wind 0.00043；首尾 200ms 的 RMS 基本持平（rain 0.0240 vs 0.0258）。第五节（第十七节）里雨声的 15s/20–35.2s 记录已被本节取代。

## 十九、2026-11-23 落雷 1.00s + 近/远雷把非雷响雨声归零

**11-02 落雷**：新增素材 `E:\下载\thunder-single-hit.mp3`（8.347s，峰值 1.118）。取 `0.00–1.00s`：0.0–0.5s 是第一记主炸（0.1s 帧相对电平 0.85/1.00/0.58/0.33），0.7s 还有一记二次炸响（0.92），1s 处余响仍有 0.084，所以末 150ms 余弦淡出收口。峰值归一到 0.450，输出 `sfx_lightning_strike.wav`。

**近雷 / 远雷去雨**：原来两条直接截自雷雨实录，雷响前后垫着持续的雨声。做法是**按雷事件区间门控**，不是逐帧硬门：

1. 10ms 帧 RMS，阈值取峰值帧的 0.18 倍；
2. 低于阈值的连续段若小于 0.35s 就合并回事件里（否则雷尾衰减会穿过阈值、被切成碎片）；
3. 取首尾超阈帧作为事件区间，区间内原样保留，区间外全部置 0；
4. 区间两端 40ms 余弦淡入、120ms 淡出，避免硬切咔哒。

结果：`sfx_thunder_near.wav` 保留 56.8%，有声区间 0.60–2.02s；`sfx_thunder_far.wav` 保留 43.3%，有声区间 0.38–1.68s（远雷那记二次回响也在区间内）。发声段连续、无碎片化，雷响之外是绝对静音。

脚本 `_local_thunder_gate_build2.py`（仓库根），可重跑。

## 二十、2026-11-23 深渊之影 7 条：一条低吼素材切出整套

新素材 `E:\下载\deep-rumbling-monster-growl-3bf1629d.mp3`：2.972s / 44.1k / 峰 0.791 / rms 0.3122（非常饱）/ 质心 683Hz / <200Hz 占 49%。结构是标准的"渐强—持稳—骤降"：0–1.20s 从 0.35 涨到 1.00，1.20–2.75s 稳在 0.76–0.94，2.90s 掉到 0.18。

一条素材要支撑 6 种行为，靠**窗口 + 增益 + 尾淡**区分，只有两处动了音高：

| 槽位 | 时长 | 取材 | 处理 |
| --- | --- | --- | --- |
| 压迫循环 | 2.00s | 0.55–2.55 | 200ms 等功率交叉（唯一做循环的一条） |
| 影撕咬 | 0.50s | 1.10–1.60 | 音高 ×1.10（+1.4 半音），咬合更脆 |
| 影突进 | 0.60s | 0.55–1.15 | 正好是渐强段，自带冲刺走势 |
| 水弹发射 | 0.50s | 2.32–2.82 | 音高 ×1.05 稍薄，当"噗" |
| 落点预警 | 2.20s | 0.00–2.20 | 全程渐强当倒计时，尾 120ms 淡 |
| 水弹命中 | 0.50s | 1.16–1.66 | 从峰顶起、90ms 快收，峰 0.38 最响 |
| 影退场 | 1.50s | 1.40–2.90 | 满档衰减到静，尾 200ms 淡 |

**变调的坑**：`pitch ×1.10` 如果照老写法"取 N 长的素材段、重采样到 N/1.1、再截前 N"，长度只有 N/1.1（0.45s），比槽位短。正确是**源段取 N×ratio，重采样回 N**：音高升、时长不变。第一版 bite 输出 0.45s、projectile 0.48s，改成这个算法后 7 条全部精确。

归一化峰值 0.30–0.38，rms 0.125–0.179。素材本身 rms 0.31 极饱，归一后完全不需要额外增益。脚本 `_local_shadow_build2.py`（仓库根），可重跑。


**v3（同一轮追加）**：压迫循环按用户要求"降音量 + 首尾相连 + 首尾和中间都渐弱"重做：源段不变（0.55–2.55s、200ms 等功率交叉），叠 sin² 呼吸包络 `m = (0.60+0.40·sin²(πt/L))·(0.88+0.12·sin²(2πt/L))`。两条 sin² 的谷都落在 t=0/1.00/2.00，于是**接缝与正中间各一处渐弱**，峰落在 0.50/1.50；谷底斜率为 0，接缝两侧电平与变化率一致，听不出接头。峰值 0.30→**0.20**，rms 0.128→0.075，起伏 4.5dB。脚本 `_local_shadow_pressure_v3.py`。


## 二十一、2026-11-24 用户合集：深渊之影压迫 6s 循环 + 水弹射出

新素材 `E:\下载\游戏敌人_深渊之影_的音效合集，按顺序排列，每个音效之间留0.wav`：24.985s / 48k / 峰 0.969 / rms 0.187，是用户自己按序排好的深渊之影音效串。

**分段为什么不能靠静音阈值**：按 10ms 帧 RMS + 3% 峰值阈值切，整条只切出 2 段——段与段之间没有纯静音，只有电平凹陷（6.00–6.55s 掉到相对 0.09–0.25，之后 6.66s 又跳回 0.83）。所以第二段起点是 6.55s 而不是 6.00s，其余段落边界同样靠包络人工判读。

**水弹怎么定的（已更正）**：第一版按"全条唯一宽带突发"判到 20.375s，用户指出实际在 13–14s 之间——更正做法是先按**段落低谷**定段边界，再在段内找特征帧。13.883–13.903s 有一段 rms 0.03–0.04 的低谷，新段从 13.908s 起；13.953s 起连续 6 记宽带爆响（13.953 / 13.963 / 13.973 / 13.988 / 14.008 / 14.038，>2kHz 占比 0.148–0.348，质心 1327–2220Hz，峰 0.820），前后帧质心都只有 60–120Hz。窗口取 13.945–14.445s，避开 13.90–13.94 的过门低吼，头 8ms 淡入、尾 250ms 余弦渐弱，峰 0.34、rms 0.093。20.375s 那处只是另一段低吼里的孤立尖峰，不是水弹。

**压迫 6s 循环的处理（已更正）**：0–6.00s 原样，尾部 **350ms 余弦渐出到 0**，不再做等功率交叉——开头 0–0.15s 本身就是静音，渐出到 0 正好接上，接缝 |y0−y−1| = **0.000000**（head100ms rms 0.00001、tail20ms rms 0.00019）。听感是"尾弱 → 静 → 渐起"。峰值归一 0.22、rms 0.072，不再叠 sin² 调制——这条素材自身起伏已有 0.15–1.00 的动态，再叠就过头了。

脚本 `_local_comp_probe*.py`（探测）、`_local_comp_build2.py`（出片定稿）。

## 二十二、2026-11-24 用户合集：食人鱼群游/咬击/死亡（09-16..18）

新素材 `E:\下载\游戏敌人_食人鱼_的音效合集，按顺序排列，每个音效之间留0.wav`：11.981s / 48k / 峰 0.967 / rms 0.130。这条的特征很极端——**质心 8071Hz、>2kHz 占 0.934**，基本是"嘶嘶"的高频水声，不是低吼。

**结构**（靠包络低谷 + 频谱走势判，同样没有静音间隔）：

| 区间 | 内容 |
| --- | --- |
| 0.00–0.58s | 静音起 |
| 0.58–8.46s | 高频群游层，质心稳在 8.0–8.8kHz |
| 8.46–10.75s | 低频事件层，质心 1627→994→728Hz 逐段下移 |
| 10.75–11.24s | 低谷 rms rel 0.02 |
| 11.24s 后 | 归零 |

层内还有 8.46–8.52、9.01–9.07 两处 50ms 低谷可当子段边界。

**三条**：群游取 6.00–7.50s（0.25s 包络 0.32/0.26/0.28/0.29/0.27/0.26，全条最平稳）+ 250ms 等功率交叉，峰 0.38；咬击取 8.47–8.77s，含 8.52s（峰 0.933）与 8.55s（峰 0.967）两记咬合；死亡取 9.60–10.10s，质心 3.5k→900Hz 的挣扎低鸣（9.84s 有 0.952 峰）。

**判据的一个坑**：对 8kHz 质心的素材，`|y0−y−1|` 完全不能当接缝判据——高频相邻样本天然差 0.04 以上（该素材第一版量到 0.037，看着吓人其实正常）。这种素材只能靠等功率交叉 + 短时包络对比（head50/tail50 rms 0.056/0.043）判断。

脚本 `_local_piranha_probe*.py`、`_local_piranha_build.py`。


**群游改版（同一轮）**：1.50s 被用户判为"太短、loop 突兀"，改为 **4.00s**——源段 2.00–6.00s，两端各 500ms 等功率交叉（占 12.5%，1.5s 版只有 250ms）。上一版取的是"最平稳"的 6.00–7.50s，恰恰因为太稳，1.5 秒内几乎没有事件，接缝反而显眼；这次换到起伏更丰富的段，一圈两次强弱（0.10s 包络 0.51→1.00→0.60），循环点藏进动态里。音量随之从峰 0.38/rms 0.044 收到 **0.33/0.063**（换段后素材本身更响）。


## 二十三、2026-11-24 用户合集：海鸟俯冲/啄击/死亡（09-11/12/15）

新素材 `E:\下载\游戏敌人_海鸟_的音效合集，按顺序排列，每个音效之间留0.wav`：7.988s / 48k / 峰 0.915 / rms 0.089。这条**终于是真的有静音间隔**——多处低谷 minrel 0.00（食人鱼和深渊之影都没有），所以能直接按包络切段。

**结构**：

| 区间 | 内容 |
| --- | --- |
| 0.34–3.06s | 6 记高频鸟叫，质心 2500–3500Hz、hi2k 0.85–1.00，每记约 0.4s、间隔 0.1–0.2s |
| 3.08–4.42s | 低频挣扎层，质心先由 2900 骤降到 365Hz（3.60s 有 0.915 峰），之后在 300–1700 区间密集脉冲 |
| 4.46–5.60s | 零星短脉冲（4.66s 0.819、4.80s 0.681、5.08s 0.707、5.20s 0.444） |
| 5.60s 后 | 归零 |

**三条切法**：俯冲取 3.06–3.66s（质心 3000→365H 的整段下坠，尾端撞地峰 0.915，正好"由远及近再压低"）；啄击取 5.06–5.31s（5.08s + 5.20s 两记短啄）；死亡取 3.66–4.46s（紧接俯冲不重叠，密集低频挣扎）。脚本 `_local_bird_build.py`。


**俯冲/死亡改版（同一轮）**：用户要求"11 和 15 用前 3 秒的素材"——第一版取的是 3.06–4.46s 的低频挣扎层，改回 0.34–3.06s 的 6 记鸟叫区。俯冲取 **1.86–2.46s**（两记连续尖叫：2.00s 主峰 0.456 + 2.40s 第二声，起手 0.06→1.00 渐强，听感是"连叫扑下"）；死亡取 **0.34–1.14s**（前两记叫，0.44s 峰 0.262、0.96s 峰 0.287，末尾自然弱到 0.22 再淡出）。两条不重叠。鸟叫各记之间本身有 0.12–0.2s 间隙，0.6s 窗口必然"一声叫一段空"，俯冲靠两记连叫把这个空盖住了。啄击（5.06–5.31s）未动。


## 二十四、2026-11-24 19 拾取与入库：目标翻倍 + 用户素材「优先删低电平」裁到长度

用户要求把 6 个目标时长翻倍（0.15→0.30、0.18→0.36、0.20→0.40、0.15→0.30、0.25→0.50、0.35→0.70），并用他此前传过但一直没用的素材截到对应长度，**优先删掉 dB 低的部分**。

| 槽 | 目标 | 素材（`E:\下载`） | 原长 | 取材 | 峰 |
| --- | --- | --- | --- | --- | --- |
| 通用 | 0.30s | freesound_community-item-pickup-37089 | 0.53s | 0.06–0.36s | 0.28 |
| 木材 | 0.36s | broken-wooden-object | 8.33s | 4.48–4.84s | 0.28 |
| 金属 | 0.40s | sumaga123-metallic-thud-447690 | 1.97s | 0.76–1.16s | 0.32 |
| 纤维 | 0.30s | sound-fallen-flowers-picked-up-from-the-floor | 1.61s | 0.17–0.47s | 0.24 |
| 鱼获 | 0.50s | entity-fishing_bobber-retrieve-1 | 0.31s | 整段 + 补静音 0.19s | 0.30 |
| 入库 | 0.70s | item-bundle-insert-1 | 0.67s | 整段 + 补静音 0.03s | 0.30 |

**「优先删低电平」怎么实现**：第一版按 20ms 帧 RMS 全局升序删帧，结果 broken-wood 删出 **7 个拼接缝**——那条素材本身能量很碎（0.00 平了 1.4 秒，真正的撞击在 1.45–2.1s 与 5.0–5.9s 几处），逐帧删等于把碎片硬拼起来。改成**滑动窗口找能量最高的连续 dur 段**（10ms hop、累积平方和求窗内能量），等价于把窗口外 dB 最低的部分整块丢掉，且拼接缝为 0。短于目标的（鱼获、入库）只补静音，不拉伸——用户明确排斥拉伸变声。

脚本 `_local_pickup_probe.py`、`_local_pickup_build.py`。

**19-03 金属拾取改版（同日补）**：用户"取能量低的那些，最高音太刺耳了"。metallic-thud 的 20ms 包络显示全条只有一个 attack——0.76s 的 1.00，紧接着 0.77/0.51/0.42 指数衰减到 1.5s 后 0.02–0.04；前 0.72s 与后 0.5s 都是 rel 0.02–0.09 的底噪。所以刺耳的就是那记撞击头。改为 0.90–1.30s（纯衰减余响，窗口内最大帧只 rel 0.24），增益 ×3.08 抬到峰 0.26、rms 0.053，包络 1.58→0.22（/rms）平滑下滑，没有单帧尖峰。选段规则：在「窗口内最大帧 ≤ 阈值」的 0.40s 窗里挑平均电平最高者，即"最饱满但不尖"的平坦段；阈 0.15 的那档 at 1.02–1.42 meanrel 只 0.062 太虚，取 0.25 档。

**19 组统一音色（同日补）**：用户"除了渔获拾取和纤维拾取，其他拾取都用拾取通用这个音效"。即木材、金属、入库三条全部改用 item-pickup 同一素材，只有 19-04 纤维（fallen-flowers）和 19-05 鱼获（bobber-retrieve）保留原素材。item-pickup 的 10ms 包络：0.00–0.05 静音、0.06–0.30 主体（0.23s 达 1.00）、0.31–0.38 掉到 0.06–0.20、**0.39 冒一记轻的第二击 0.63**、0.42 之后归零。取法：木材 0.36 ← 0.06–0.42（尾在 0.03 自然落，不用淡出）；金属 0.40 ← 0.06–0.46（多含那记第二击、尾是静音）；入库 0.70 ← 整条 0.53 + 补 0.17 静音。三条峰统一 0.28，与 19-01 持平，避免同一音色在不同槽位上响度打架。broken-wooden-object、metallic-thud、item-bundle-insert 三条素材就此弃用。
