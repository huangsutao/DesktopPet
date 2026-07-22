# DesktopPet

基于 **WPF + Spine** 的 Windows 桌面宠物。无边框透明置顶窗口，播放 Spine 骨骼动画，支持拖拽与点击互动。

## 技术栈

- .NET 9 / WPF
- Spine Runtime（C#，版本需与导出工具一致）
- 渲染：WriteableBitmap 或 SkiaSharp（自研 SkeletonRenderer）
- 系统托盘：NotifyIcon

## 功能规划

| 阶段 | 内容 | 状态 |
|------|------|------|
| P0 | 透明置顶窗、拖拽、托盘退出 | 已完成 |
| P1 | 加载 Spine、播放 idle | 已完成 |
| P2 | 状态机（点击/拖拽/睡眠） | 待做 |
| P3 | 多皮肤、缩放、开机自启、点击穿透 | 待做 |

## 环境要求

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Spine 导出资源（与 Runtime 主版本匹配，例如 4.3.x）

## 快速开始

```bash
git clone <repo-url>
cd DesktopPet
dotnet restore
dotnet run --project DesktopPet.csproj
```

## 资源放置

每个宠物一个目录，**运行时只使用 `export/`**：

```text
Assets/Pets/{petName}/
  ├── export/                 # 会复制到编译输出（程序加载）
  │   ├── *.atlas
  │   ├── *.png
  │   └── *-pro.skel / *.json
  ├── images/                 # 编辑器用，不输出
  └── *.spine                 # 编辑器用，不输出
```

在配置中指定宠物名（默认 `default`）。**Spine 编辑器导出版本必须与引用的 spine-csharp Runtime 版本一致。**

## 项目结构

```text
DesktopPet/
├── Assets/Pets/default/          # Spine 资源（skel/json + atlas + png）
├── Core/                         # PetState / PetStateMachine / PetConfig
├── Spine/                        # SpineRuntimeHost / WpfSkeletonRenderer / AnimationController
├── UI/                           # TrayIconService、Behaviors/
├── Services/                     # SettingsService / WindowPlacementService
├── Resources/                    # 其它静态资源
└── ThirdParty/SpineCSharp/       # 官方 spine-csharp 源码（方案 B，拷入主项目）
```

### 接入 spine-csharp（方案 B）

将官方 [spine-csharp](https://github.com/EsotericSoftware/spine-runtimes/tree/4.3/spine-csharp) 的 `src` 目录下 `.cs` 文件拷贝到：

```text
ThirdParty/SpineCSharp/
```

保持原有子目录结构（例如 `Attachments/`）。主项目 SDK 会自动编译这些源码，无需单独 `ProjectReference`。

WPF 工程已在 `.csproj` 中排除 `ColorMono.cs`（MonoGame/XNA）和 `ColorUnity.cs`（Unity），仅使用 `ColorOther.cs`。

升级 Runtime 时：用对应分支的新 `src` 覆盖 `ThirdParty/SpineCSharp/`，并确认导出资源版本一致。

## 架构说明

- **PetWindow**：透明无边框主窗，处理拖拽与点击
- **PetStateMachine**：Idle / Walk / Sleep / Clicked 等状态切换
- **SpineRuntimeHost**：Skeleton 加载与每帧 Update
- **WpfSkeletonRenderer**：将 Spine 网格绘制到 WPF 可显示表面

## 开发约定

- Spine Runtime 与资源导出版本保持一致，升级时同步两边
- 渲染与 UI 解耦：Window 只发事件，不直接操作 Skeleton 内部数据
- 用户设置写到 `%AppData%/DesktopPet/settings.json`（计划）
- `ThirdParty/SpineCSharp` 为官方源码，尽量少改；业务封装写在 `Spine/`

## 已知限制

- WPF 无官方 Spine 控件，需自实现渲染桥接
- 仅支持 Windows（WPF）

## License

待定
