# engineer —— Unity 团队协作说明

> **Unity 版本：2022.3.62f2c1**
> 请所有人统一用这个版本打开工程。版本不一致时 Unity 会静默重写场景和预制体，
> 产生大量看不懂的 diff，是团队协作里最难排查的一类问题。

---

## 一、克隆之后怎么打开

1. 用 GitHub Desktop 克隆仓库到本地。
   路径建议**短、纯英文、无空格**，例如 `D:\work\engineer`。
   路径太深或含中文，Unity 偶尔会在导入和烘焙时出问题。
2. 打开 **Unity Hub → Add → Add project from disk**，选中仓库根目录
   （就是同时能看到 `Assets`、`Packages`、`ProjectSettings` 的那一层）。
3. 首次打开 Unity 会重新生成 `Library/`（几个 GB 的缓存），耐心等它跑完。

### 为什么 `Library/` 不在仓库里

`Library/` 是 Unity 把 `Assets` 里的资源加工后的**缓存**：导入结果、压缩纹理、
烘焙光照、脚本编译产物。它有两个特点：

- **可以完全重新生成**：只要有 `Assets` + `ProjectSettings` + `Packages`，Unity 就能重算出来；
- **每台机器都不一样**：里面含有本机绝对路径、时间戳、机器码。

把它提交上去，等于把 220MB 的临时产物塞进仓库，而且两个人一提交就冲突。
所以 `.gitignore` 忽略了它，`Logs/`、`UserSettings/`、`Temp/`、`obj/` 同理。

**判断标准很简单：这个文件是"人做出来的"，还是"机器算出来的"？只提交前者。**

---

## 二、两条必须遵守的铁律

### 铁律 1：`.meta` 和资源本体必须一起提交

Unity 内部**不用路径找资源，用 GUID 找**。GUID 就存在 `.meta` 文件里，
场景和预制体里记的是 GUID。

| 情况 | 后果 |
|---|---|
| 只提交 `.meta`，不提交资源 | 队友那边只有"身份证"没有"本人"，所有引用变成 `Missing` |
| 只提交资源，不提交 `.meta` | 队友的 Unity 会**重新分配一个 GUID**，引用全部断裂，且无法自动修复 |

所以：**要么两个都提交，要么两个都不提交。** 绝不单独提交 `.meta`。

`.gitignore` 里专门写了一行 `!/[Aa]ssets/**/*.meta`，就是为了防止以后有人
不小心加了笼统的 `*.meta` 规则，把资源的"身份证"误杀。

### 铁律 2：动手前先 Pull，收工后立刻 Push

`.unity` 场景和 `.prefab` 预制体是文本格式（本项目已开启 **Force Text** 序列化），
Git 理论上能合并，但两个人同时改同一个场景依然极易出问题。

**分工建议：按场景 / 按模块分，不要两人同时打开同一个场景。**
改完立刻提交推送，把冲突窗口压到最小。

---

## 三、一次性配置：Unity 场景智能合并（建议每人都做）

普通文本合并看不懂 Unity 的 YAML，会把两个版本混成一份语法错误的场景文件。
Unity 自带 `UnityYAMLMerge.exe`，能理解场景结构，按对象的增删改做真正的三方合并。

**每台机器各自执行一次**（注意把路径换成你自己机器上的 Unity 安装位置）：

```powershell
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver '"D:/Unity/2022.3.62f2c1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

> 在 PowerShell 里必须用**单引号**包住第二条命令的后半段，
> 否则 `$BASE` 会被 PowerShell 当成空变量吃掉。
> 在 cmd 或 Git Bash 里把外层换成双引号。

验证是否配置成功：

```powershell
git config --get merge.unityyamlmerge.driver
```

**没配置也不会报错**——`.gitattributes` 只是声明"用这个合并器"，
某台机器没配时 Git 会自动退回普通文本合并。所以这件事可以慢慢补，不影响别人。
但如果有人配了有人没配，出冲突时行为会不一致，**建议全员都配上**。

---

## 四、日常协作流程（GitHub Desktop）

```
Pull（拉取最新）  →  在 Unity 里干活  →  关掉 Unity  →  在 GitHub Desktop 里 Commit  →  Push
```

**务必先关 Unity 再提交。** Unity 运行时会在后台不断改写 `Library/` 和
`UserSettings/`，虽然它们已被忽略，但 `.meta` 和场景文件也可能在保存瞬间被改写，
提交到一半的内容容易出错。

Commit 信息建议写清楚"改了什么、为什么"，例如：

- `新增：玩家移动脚本 PlayerController`
- `修复：玉米模型贴图丢失`
- `调整：把主摄像机移到场景中央`

---

## 五、仓库里都装了什么

| 路径 | 是否提交 | 说明 |
|---|---|---|
| `Assets/` | ✅ | 所有美术资源、脚本、场景；`.meta` 一起提交 |
| `Packages/` | ✅ | 依赖清单，`manifest.json` + `packages-lock.json` 必须提交 |
| `ProjectSettings/` | ✅ | 工程配置（物理、输入、图形、质量……），必须提交 |
| `Library/` | ❌ | Unity 缓存，可重建 |
| `Logs/` `Temp/` `obj/` | ❌ | 运行日志和编译中间产物 |
| `UserSettings/` | ❌ | 每个人的编辑器布局和偏好 |
| `*.sln` `*.csproj` | ❌ | Unity / VS 自动生成，每人不同 |
| `*.blend1` | ❌ | Blender 自动备份，每保存一次就多一份 |
| `Unity界面.docx` | ✅ | 设计文档，属于"人做出来的"东西 |

---

## 六、如果以后仓库变得很大

当前素材约 140MB，GitHub 完全承受得住（单文件上限 100MB，仓库警告线 1GB）。
但 **Git 会永久保存二进制文件的每一个版本**——模型改 20 次，历史里就有 20 份。

如果将来素材总量超过 500MB，或者模型会反复迭代，就该换成 **Git LFS**：
它把大文件本体存在别处，仓库里只留下一个小指针，历史不会膨胀。
届时再迁移即可，现在不需要。
