# QTE表现与美术替换

更新：2026-11-17。制作、修船使用HTML原型的移动键帽轨道；绞盘、手摇发电使用同一套圆环判定视图。只更新表现，费用、操作键、制作时间、牵引量、发电间隔和成功判定仍由原玩法脚本负责。

## 当前显示

- 制作台、普通及快速修船：有厚度与边框的键帽沿深色轨道移动，金线位于轨道75%，半透明绿色区间表示实际判定窗口。船长默认显示W/A/S/D，潜水员显示对应方向箭头贴图；成功绿底深色字、失败红底浅色字。
- 绞盘：从顶部起点顺时针旋转的金色指针、绿色判定弧、中央回收进度与当前近战/QTE键帽。基础及T2判定增益直接反映在绿色弧宽度；T3电动绞盘隐藏手动键帽与判定指针，保留回收进度。
- 手摇发电：同样的圆环，绿色弧覆盖当前generatorWindow，中央显示电力与该角色的发力按键。默认3秒一圈；命中变绿、有效失误变红，冷却中反复按键不伪造成功反馈。燃油机仍保留该手摇圆环，电鳗无限供能时关闭手摇视图。
- 图形均为UGUI Image，非文字画圆；圆环使用等宽高适配，在本地两人面板中保持正圆。钓鱼和引擎清污继续使用各自原有进度条。

## 美术修改入口

路径相对于Assets/_LifeLine：

| 文件/目录 | 修改内容 |
| --- | --- |
| Data/UI/QteVisualTheme.asset | 全局共享皮肤SO：所有Sprite引用、颜色、字体、键帽尺寸、轨道显示参数 |
| Prefabs/UI/QteStationView.prefab | 两位角色共用的完整UI预制体：轨道、键帽、圆环、中心区与指针层级 |
| Textures/UI/QTE/ | 可直接替换的PNG：Keycap/KeycapSuccess/KeycapFailure、RhythmLane、HitLine、RingTrack、RingFace、Needle、Hub、ArrowUp/Left/Down/Right |
| Scripts/Runtime/UI/QTE/ | QteStationView纯表现脚本和QteVisualTheme数据类型 |
| Scripts/Editor/QteUiBuilder.cs | 首次资源创建、旧Gameplay显式迁移；保留现有美术和皮肤数值 |

替换PNG时保留原文件和.meta可维持引用，也可导入新Sprite后直接拖入皮肤SO。键帽使用九宫格Image，默认边框18px；更换图时在Sprite Editor设置合适Border。圆环、扇区和指针使用正方形透明PNG、中心枢轴(.5,.5)。RingTrack原图为完整环形遮罩，绿色弧由同一Sprite径向裁切；从12点方向顺时针计算。Needle原图朝12点方向，不要将旋转动画烘焙到贴图里。

默认颜色来自HTML：深蓝轨道、薄荷绿判定区、金色指针。外围设备面板继续用明亮背景。艺术家可以替换中心背景、指针、枢轴和三种反馈键帽，无须改制作Manager或绞盘代码。

## 当前键位与自定义键帽

视图读取各角色CrewInputReader.RuntimeActions绑定的effectivePath，不固定写W或SPACE。重绑为I会立即显示I；潜水员重绑方向键后也显示新键。字母是贴图键帽上的动态字形，可适配任意改绑。

如需整张带字按键贴图，在皮肤SO的bindingArtwork增加条目：controlPath（如<Keyboard>/space、<Keyboard>/i）、sprite、showLabel、size。贴图自带键名时关闭showLabel，避免叠字；size为零使用默认尺寸，正数XY可单独指定长空格键等尺寸。空Sprite自动回退通用键帽，不会留白。方向箭头另有四张独立图；字体可通过keyFont整体替换。

## 展示参数和真实判定

默认keySize=48×48，长键自动加宽到1.7倍；laneHitPosition=.75，laneTravelPerCycle=.65。音符位置=判定线+(作业elapsed−nextCue)/craftCueInterval×移动比例；判定区宽度=2×实际QTE半窗口/间隔×移动比例，因此到达金线的时刻与真实判定中心一致。制作台升级、鱼类增益扩大窗口时图形同步扩大。

圆环phase=玩法周期归一化值，旋转角度=−360×phase；判定弧起点=−360×(center−width/2)，径向fillAmount=width。绞盘宽度直接读取WinchQteWidth等效值；发电区间默认.7–.9。更换图不能改变真实判定窗口。发电反馈持续时间由皮肤feedbackSeconds控制（默认.65秒），其他反馈沿用玩法中的既有持续时间。

## 已有场景安装与测试

新建基础场景时GameProjectBuilder自动挂接。旧项目在保存人工场景改动后执行LifeLine/UI/Install HTML Style QTE (Preserve Existing Art)。命令备份Gameplay到Documentation/UIBackups，再给两个站点面板实例化共享预制体；已有qteView引用保留，不覆盖现有PNG、SO和预制体。不要强制重建整个Gameplay。

新增6项编辑测试检查：音符到线与判定边界、W→I改绑、潜水员箭头与独立K键、圆环顶点/顺时针角度、完整自定义键帽与电动隐藏、三态反馈颜色。实际Gameplay已检查制作/修船并列键帽、绞盘/发电并列圆环、改绑立即更新、双方各命中一次，以及中英显示。
