# 《一线生机》美术资产

Unity 工程路径为 `Assets/_LifeLine/Art/`。按用途分为 `Characters` 船员、`Ship` 船体、`Stations` 船上设备、`Environment` 环境、`Items` 道具、`Enemies` 敌人、`Effects` 特效、`UI` 界面。每类用 `StaticSprites/` 放独立静态 PNG，用 `SpriteSheets/` 放按动作划分的逐帧图组。

## 首批像素素材

- 船长和潜水员各有站立、倒地两张静态姿势。两人的走路与游泳各自有独立图组，分别放在 `Characters/SpriteSheets/<角色>/<动作>/`。每个动作四张 PNG，按 `_01` 至 `_04` 顺序播放，循环片段的采样率为 8 FPS。
- 船体为甲板下的船舱剖面。设备图是独立的船舵、绞盘、总控等 13 个站点。水下首批包含货箱、遗失背包、散落木材／金属／纤维／鱼获／燃油、可打捞过滤器、藏匿柜、活板门和两种墙面。
- 使用内置 ImageGen。以像素概念图的明度、暖橙雨衣、青绿潜水服和黄色识别色为参考。角色提示词指定正侧面和同一个动作的四个连续姿势，静态提示词指定正侧／正视独立道具、透明底、清晰像素色块。图组按格切片，依据主体 alpha 收边，保留原始透明通道。

## 替换方法

1. 对同一个部件或帧，原位换 PNG 并保留 `.meta`。新增动作放在该角色的 `SpriteSheets/<动作>/`，用零填充序号命名，并在对应 Animator Controller 连接片段。可用 Unity Animation 窗口改变帧序、速度或关键帧。
2. 角色的 `Pixel Body` 在原 `VisualRoot` 下，Animator 由 `CrewPresentation` 驱动。设备、船体和货箱通过各自 Prefab 中的 SpriteRenderer 换图。水下道具、墙面和藏匿柜在 `Environment/UnderwaterSprites.asset` 换引用，过滤器用 `Prefabs/World/FilterSalvageVisual.prefab`。
3. 导入器默认用 Sprite、Point、无 mipmap、无压缩。替换不同尺寸的素材时，核对脚底基线、动作中心、设备交互点和画面层级。美术保持与现有 Collider、绞盘物理锚点和门的状态同步。

菜单 `LifeLine/Art/Create Artwork Folders` 补建目录，`LifeLine/Art/Install First Pixel Art Pass` 将首批素材接到现有 Prefab 和 Gameplay 场景。后续美术提交可在现有引用上直接迭代，已安装的控制器、SpriteRenderer 与水下美术配置会保留编辑值。

## 船桨、鱼叉、渔枪与动态背景

- 船长和潜水员各有 `Paddle`、`Harpoon`、`Speargun` 三套船上动作，以及相应的 `PaddleSwim`、`HarpoonSwim`、`SpeargunSwim` 水下动作。每个目录四帧，只画该目录对应的动作。独立船桨、鱼叉、渔枪和发射矛头在 `Items/StaticSprites/Weapons/`。
- `CrewPresentation` 用现有近战／发射事件驱动角色 Animator。走路用不同的启动、停止阈值和短过渡，步态按头部和脚底对齐。水中朝向平滑跟随实际游动方向。近战的判定仍由现有扇形逻辑计算。
- 海鸥和变异海鸥各有 `Fly`、`Swoop` 两组。食人鱼、鲨鱼和白化鲨各有 `Swim`、`Bite` 两组。深渊之影有 `Swim`、`Bite`、`Dash`、`WaterShot` 四组。位于 `Enemies/SpriteSheets/<种类>/<动作>/`。预制体上的 `PixelEnemyView` 引用对应帧和播放速度，由敌人实际追击、预备、攻击状态决定画面。
- `Ship/StaticSprites/Backdrop/` 存天空、云、浪花和水面波纹。水下远景在 `Environment/StaticSprites/Backdrop/`，独立海草、珊瑚、沉没路灯、建筑残骸、断梁、拱门、岩石和气泡在同类目录的 `Decor/`。各海域的远景平滑过渡，画面随潜水深度逐渐变暗。
- `Environment/OceanBackdrops.asset` 提供图片、远景宽度、波浪与云层速度，以及傍晚、夜间、风暴色调。水下房间按地图种子布置独立装饰。水生植物可摆动，装饰仅由 SpriteRenderer 显示。
- 缩略船水线以上使用 `Environment/StaticSprites/Backdrop/` 中的 `WaterlineSky.png`、`WaterlineDuskSky.png` 和 `WaterlineNightSky.png`。贴图按原始比例横向滚动，夜间潜水遮罩以水线为界。甲板在 `Ship/StaticSprites/Backdrop/` 使用 `CoastSky.png`、`CoastSkyDusk.png` 和 `CoastSkyNight.png`，对应白天、黄昏和夜间，生成的海浪和远景云层也随时段调色。
- 这些 3:1 天空图由内置 ImageGen 制作。水线天空的提示指定灰蓝白昼、珊瑚色余晖和带星光的深蓝夜色，三张图都保持稀疏低云、无地平线及可横向循环。甲板变体保留白天的海岸线、岩拱、云和构图，黄昏用暖橙余晖和紫云，夜间用深蓝星空和月光云缘。`OceanBackdrops.asset` 可改视野高度、云速、颜色和贴图。已有工程可用 `LifeLine/Art/Install Sky Backdrops` 绑定新图。

素材由内置 ImageGen 生成。角色每张源图明确指定一项动作的四个连贯姿势，船上用站姿设定，水下用横向潜水姿势设定。敌人分别出移动与攻击图组。背景的生成提示指定浅滩植被、沉没城区与空旷深海，并把可复用景物单独放在透明底图组。

安装菜单为 `LifeLine/Art/Install Ocean, Weapons and Enemies`。角色控制器和敌人预制体已有引用时，可直接换帧或调节参数。

## 梯子、缩略船与角色尺寸校准

- 船梯贴图为 `Ship/StaticSprites/CabinLadder.png`。船长和潜水员的 `Climb/` 各自包含四帧与独立 Clip。播放速度跟随实际攀爬速度，下降时倒播，挂在梯子上停住时保持姿势。
- 缩略船在 `Ship/SpriteSheets/SurfaceBob/`，四帧使用同一个画布与原点。水上的小船和水下屏幕边缘的船位标记共用这组图。波浪的上下浮动也作用于绳索的可见挂点。
- 打捞柜用 `Items/StaticSprites/SalvageCabinet.png` 的正面平视素材，藏匿柜继续引用 `Environment/StaticSprites/HideLocker.png`。浅滩远景 `Shallows.png` 由内置 ImageGen 重新处理为整幅水下画面。梯子与柜子提示词指定无透视正视形状，缩略船与角色图组各指定四个连续姿势并保持透明背景。
- 人物用 1.8 米站姿建立基准，水下帧按帽子或潜水头罩匹配尺度。敌人使用身体或喙的参考点。尺寸与枢轴写入每张 Sprite 的导入设置，角色动画只切换 Sprite。异尺寸换图时，在 Sprite 导入器核对 `Pixels Per Unit` 和 `Pivot`，或者运行 `LifeLine/Art/Install Ship Details and Align Sprite Poses` 重新校准这套像素资源。
- 水下晕厥使用 `Characters/StaticSprites/<角色>_WaterFaint.png`。用内置 ImageGen 从该角色的 `Swim_01` 局部改画闭眼与放松的嘴，保持水平游姿和潜水装备。表情图沿用 `Swim_01` 的物理尺寸与枢轴，晕厥时保留当前水下朝向。
- 两人可以同时攀爬同一架梯子，任何一人仍在攀爬时舱口保持打开。近景重叠按模式调整绘制顺序，本地船长在前，联机视角由本机操控角色在前。

## 海况与水下美术

- `LifeLine/Art/Install Marine Polish` 绑定城区现代楼体远景、电鳗发电机与深渊之影四帧 `BasicBite`。城区远景保留同一日夜时间轴。发电机素材同时用于打捞物和船上已安装的电鳗机。
- 墙体重新使用规则灰蓝砖缝和低对比暖灰岩层。两张材质按世界坐标每 3.2 米循环。深海礁石与残拱使用更大、低对比的远景层。
- Sprite 导入清理低透明度底雾和游离角落的残留。深渊之影各动画以可见轮廓中心对齐枢轴，并按共同宽度注册，动作高度在巡游参考的 94%–109% 之间。换帧后运行 `LifeLine/Art/Register All Shadow Frames` 更新预制体和导入数据。
- 雷雨夜雨丝由远景着色器滚动。`AreaWarningView` 使用统一的深色外圈与倒计时内圈，可在预制体换色及替换命中贴图。雷电在预警中心播 0.36 秒，出击点固定。
- 本批内置 ImageGen 提示指定正视重复混凝土砖面、低对比岩层、带透明底的浸水现代楼体轮廓、带玻璃鳗舱的电机，以及同体型同朝向的四帧快速咬合。

## 航海界面

- 在 `Scenes/MainMenu.unity` 的 `MainMenuCanvas` 编辑主菜单、设置和换绑。在 `Scenes/Gameplay.unity` 的 `GameplayCanvas` 编辑暂停、设备面板、引导卷轴和分屏视口。未激活的面板可在编辑模式临时激活查看。
- `Prefabs/UI/VoyageStatusCard.prefab` 共用于两个视口。`ActionButton.prefab` 和 `BackpackSlot.prefab` 提供实际的列表行和物品格。房间与载入界面分别位于 `OnlineRoomPanel.prefab` 和 `OnlineLoadingCanvas.prefab`。查看房间预制体时可切换 `Home`、`Create`、`Join` 或 `Room` 子页。
- 在各 `Image` 的 `Source Image` 字段替换图片。黄铜边框、羊皮纸、图标和时钟位于 `UI/StaticSprites/Resources/LifeLineUI/Nautical/`，边框使用九宫格拉伸。场景与预制体直接引用 Sprite。
- `LifeLine/UI/Install Authored Nautical Interface` 用于给仍是旧占位 UI 的工程做一次编辑器迁移。已经迁移的场景可直接保存并提交，日常换图和调布局从 Unity Inspector 完成。

## 甲板与水下地图

- 在 `Prefabs/Ship/Ship.prefab` 的 `VisualRoot/Pixel Hull` 替换船体 Sprite。甲板两侧和梯口由独立 BoxCollider 承载，船体贴图边缘对齐甲板站立面，舱内地面与舱底碰撞面也各有对齐点。
- 水下墙体、洞穴和海床外观在 `Prefabs/World/UnderwaterArt/`。通过预制体的 MeshRenderer 材质更换贴图。普通废墟、洞穴墙、洞穴背板和海床的平铺大小与色调可分别调整。
- 在 Gameplay 场景选择 `Underwater Panorama` 的 `Backdrop texture plane`。它引用的 `UnderwaterPanorama.mat` 可直接替换浅滩、沉没城市和深水三张全景图。蓝色色调和平铺参数也在该材质。水线上方的黄昏与夜景在 `OceanBackdrops.asset`，同一资源里的水草、珊瑚、深海岩石为房间装饰的可选 Sprite。
- `UnderwaterSprites.asset` 可更换打捞物和地图预制体，`HideLocker.prefab` 里可直接换藏匿柜的 Sprite。每日地图会按种子继续布置房间和物资，布置时引用这些资源。
