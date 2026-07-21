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
| P0 | 透明置顶窗、拖拽、托盘退出 | 待做 |
| P1 | 加载 Spine、播放 idle | 待做 |
| P2 | 状态机（点击/拖拽/睡眠） | 待做 |
| P3 | 多皮肤、缩放、开机自启、点击穿透 | 待做 |

## 环境要求

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Spine 导出资源（与 Runtime 主版本匹配，例如 4.2.x）

## 快速开始

```bash
git clone <repo-url>
cd DesktopPet
dotnet restore
dotnet run --project DesktopPet.csproj
```

## 资源放置

将宠物资源放到：

```text
Assets/Pets/default/
  ├── skeleton.skel   # 或 skeleton.json
  ├── skeleton.atlas
  └── skeleton.png
```

在配置中指定宠物名（默认 `default`）。**Spine 编辑器导出版本必须与引用的 spine-csharp Runtime 版本一致。**

## 项目结构

```text
DesktopPet/
├── Assets/Pets/          # Spine 资源
├── Core/                 # 状态机、配置
├── Spine/                # 加载、更新、WPF 渲染
├── UI/                   # 宠物窗、托盘
└── Services/             # 设置、窗口位置
```

## 架构说明

- **PetWindow**：透明无边框主窗，处理拖拽与点击
- **PetStateMachine**：Idle / Walk / Sleep / Clicked 等状态切换
- **SpineRuntimeHost**：Skeleton 加载与每帧 Update
- **WpfSkeletonRenderer**：将 Spine 网格绘制到 WPF 可显示表面

## 开发约定

- Spine Runtime 与资源导出版本保持一致，升级时同步两边
- 渲染与 UI 解耦：Window 只发事件，不直接操作 Skeleton 内部数据
- 用户设置写到 `%AppData%/DesktopPet/settings.json`（计划）

## 已知限制

- WPF 无官方 Spine 控件，需自实现渲染桥接
- 仅支持 Windows（WPF）

## License

待定
