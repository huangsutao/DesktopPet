using System.Windows;

// WPF 主题资源查找位置（本项目 UI 文案主要来自 Locales JSON + 运行时注入的 ResourceDictionary，
// 一般不依赖主题专用字典；保留 SourceAssembly 以便将来若放入 Themes/Generic.xaml 仍可被找到）。
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            // 主题相关字典：无（不按系统主题切 ResourceDictionary）
    ResourceDictionaryLocation.SourceAssembly   // 通用字典：在本程序集内查找（如 Themes/Generic.xaml）
)]
