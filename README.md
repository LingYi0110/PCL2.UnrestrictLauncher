# PCL2.UnrestrictLauncher

`PCL2.UnrestrictLauncher` 是一个基于 Harmony 的第三方运行时补丁启动器。它通过在内存中动态应用补丁，恢复 Plain Craft Launcher 2 中的 Minecraft 下载、整合包安装及相关界面功能，无需直接修改原程序文件。

---

## 功能特性

- **恢复 Minecraft 版本列表**：恢复原版游戏版本列表的加载与浏览入口。
- **恢复整合包文件安装**：恢复手动选择外部整合包文件并执行安装的入口。
- **恢复整合包安装分发**：恢复整合包识别后的后续分发与安装逻辑。
- **恢复版本设置页面原版卡片**：在版本修改页面中恢复原版信息卡片的正常显示。
- **恢复整合包资源详情安装**：在整合包资源详情页的版本列表中恢复直接安装入口，同时保留“另存为”功能。
- **非侵入式运行时补丁**：采用 Harmony 在程序载入内存后应用 IL Transpiler 补丁，不改写磁盘上的原版 PCL 可执行文件。

---

## 运行环境与兼容性

### 运行需求

- **运行库**：.NET Framework 4.8.1
- **目标程序**：PCL 2.99.0.0

### 兼容性说明

本启动器的工作原理依赖于目标 PCL 二进制中的类名、方法签名及 IL 指令特征。不同版本之间的代码结构变动可能导致补丁定位失败，因此**不承诺兼容所有历史或未来发布的 PCL 版本**。

---

## 下载与使用

### 使用步骤

1. 前往项目的 [Releases 页面](https://github.com/) 下载最新发布的 `PCL2.UnrestrictLauncher.exe`。
2. 将 `PCL2.UnrestrictLauncher.exe` 放置在 PCL 程序所在的同级目录下。
3. 确保目标 PCL 程序的文件名为默认的 `Plain Craft Launcher 2.exe`（若名称不同，参见下方[配置说明](#配置)）。
4. 直接运行 `PCL2.UnrestrictLauncher.exe` 启动游戏启动器。

### 目录结构示例

```text
PCL/
├── Plain Craft Launcher 2.exe    # 目标 PCL 主程序（用户自行提供）
├── PCL2.UnrestrictLauncher.exe   # 补丁启动器程序
└── config.ini                    # 配置文件（首次运行自动生成）
```

---

## 配置

启动器首次运行或找不到配置文件时，会在同级目录下自动生成 `config.ini`：

```ini
[Launcher]
PatchFile=Plain Craft Launcher 2.exe
```

### 配置项说明

| 配置项 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `PatchFile` | `Plain Craft Launcher 2.exe` | 指定要加载并打补丁的 PCL 程序路径。 |

- **路径支持**：支持相对路径（相对于启动器所在目录）和绝对路径。
- **缺省与重置**：如果该项为空白或配置缺失，启动器将自动使用默认值 `Plain Craft Launcher 2.exe`。
- **安全检查**：`PatchFile` 不能指向 `PCL2.UnrestrictLauncher.exe` 自身，否则启动时将抛出异常并阻止循环拉起。

---

## 工作原理

启动器通过在运行时将自身挂载为目标进程的引导宿主，完成依赖修补与补丁注入后再唤醒目标 PCL：

1. **资源解包与引导**：启动器入口（`Bootstrap`）从自身内嵌资源中解压并载入 `0Harmony.dll`，在 JIT 编译器介入前挂载程序集解析器，确保单文件独立运行。
2. **目标载入与环境隔离**：依据 `config.ini` 中的配置路径加载目标程序集，设置工作目录与原生 DLL 搜索路径（`SetDllDirectory`），并挂载嵌入式资源解析器以正确处理 PCL 的内嵌依赖（如 `MeloongCore`、`PCLCS`、`Newtonsoft.Json` 等）。
3. **运行时环境修复（移植自 BepInEx）**：
   - 参考并移植了 [BepInEx.NET.Framework.Launcher](https://github.com/BepInEx/BepInEx) 的启动引导与运行时修复机制（`RuntimeFixes` / `AssemblyFixes`）。
   - 通过 Harmony 前置补丁拦截 `Assembly.GetEntryAssembly()`，使其正确返回目标 PCL 程序集而非补丁启动器自身，保证依赖入口程序集的逻辑行为一致。
   - 补丁拦截并缓存 `Assembly.Load(string)` 与 `Assembly.Load(byte[])`，避免重复加载相同程序集导致类型冲突或多副本错误。
4. **Harmony 业务补丁**：利用 Harmony 针对目标程序集执行 `PatchAll`，通过 Transpiler 针对 5 处关键业务逻辑进行针对性的 IL 指令替换与流控制修改。
5. **移交主入口执行**：环境与补丁准备完毕后，启动器反射调用目标程序的入口点（`Main`），完整保留目标程序的日志流、全局异常捕获和应用生命周期。

---

## 从源码构建

### 构建准备

- **操作系统**：Windows 10 / 11
- **开发工具**：Visual Studio 2022（或更高版本），或安装有 .NET Framework 4.8.1 Developer Pack 的 MSBuild 工具链。
- **依赖管理**：[NuGet CLI](https://www.nuget.org/downloads) 或 Visual Studio 内置 NuGet 包管理器。

### 构建步骤

1. **克隆仓库**：
   ```bash
   git clone https://github.com/your-username/PCL2.UnrestrictLauncher.git
   cd PCL2.UnrestrictLauncher
   ```

2. **还原 NuGet 依赖**：
   ```bash
   nuget restore PCL2.UnrestrictLauncher.sln
   ```

3. **编译构建**：
   ```bash
   msbuild PCL2.UnrestrictLauncher.sln /p:Configuration=Release /p:Platform="Any CPU"
   ```

4. **产物输出**：
   编译生成的独立可执行文件位于：
   ```text
   PCL2.UnrestrictLauncher/bin/Release/PCL2.UnrestrictLauncher.exe
   ```
   由于 Harmony 依赖已在编译期作为嵌入资源打入程序，发布时只需分发单个 `.exe` 文件即可。

---

## 问题反馈

如果您在 PCL 上遇到了补丁报错或功能失效，欢迎提交 [Issues](https://github.com/)。

为便于快速定位问题，提交反馈时请尽量提供：
1. 补丁启动器版本。
2. 目标 PCL 的具体版本（或其文件 SHA-256 校验码）。
3. 操作系统版本及 .NET Framework 运行时环境。
4. 完整的复现流程与控制台/日志截取内容。

> **安全提示**：在提交日志文本或截图前，请务必核对并剔除其中的令牌、账号密码、个人私有目录等敏感隐私信息。

---

## 许可证与致谢

### 许可证与版权说明

- 本项目代码基于 **LGPL-2.1-or-later** 许可证分发。
- **Harmony 许可**：项目嵌入并使用了 [Harmony](https://github.com/pardeike/Harmony)（MIT 许可证），其版权与许可声明予以单独保留。
- **BepInEx 代码移植**：本项目启动引导中的 `RuntimeFixes` 与入口程序集修补逻辑移植/参考自 [BepInEx](https://github.com/BepInEx/BepInEx)（具体来自于 `BepInEx.NET.Framework.Launcher`），遵循 LGPL-2.1 许可证。
- **目标程序声明**：本启动器仅为外置挂载补丁容器，**不包含、不分发且未获得 Plain Craft Launcher 2 任何二进制程序或受版权保护素材的授权**，用户的原版 PCL 须自行遵守其原作者规定的相关协议。

### 致谢与第三方开源库

- [Plain Craft Launcher 2](https://github.com/Hex-Dragon/PCL2)：优秀简洁的 Minecraft 启动器。
- [Harmony](https://github.com/pardeike/Harmony)：提供在运行时对 .NET 方法进行挂钩与 IL 重写的强大底层库。
- [BepInEx](https://github.com/BepInEx/BepInEx)：提供了可靠的 .NET Framework 程序启动引导与运行时修复思路（`BepInEx.NET.Framework.Launcher`）。
