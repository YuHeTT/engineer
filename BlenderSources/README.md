# BlenderSources —— Blender 源文件（Unity 不导入这里）

这个文件夹存放 **Blender 源文件**（`.blend` / `.blend1`），以及模型的编辑工程。

## 为什么放在 `Assets` 外面

Unity 只会导入 `Assets/` 和 `Packages/` 里的东西。放在项目根目录下的其他文件夹
（比如这里），Unity **完全不会碰**：不导入、不生成 `.meta`、不占用导入时间。

`.blend` 文件如果留在 `Assets/` 里，会带来三个问题：

1. **Unity 导入 `.blend` 必须调用本机安装的 Blender**。没装 Blender 的人，
   每次打开工程都会看到一串 `Blender could not be found` 报错。
2. **不同 Blender 版本转换出的网格可能不一致**。同一个人装了 4.0、另一个人装了 3.6，
   Unity 从同一个 `.blend` 里导出的模型可能不一样，容易出现
   "同一个场景在不同人机器上长得不一样" 这类最难查的问题。
3. 每次 Unity 重新导入都要启动 Blender，明显变慢。

所以：**Unity 用 `Assets/` 里的 `.glb` / `.fbx`，人要用 Blender 改模型时来这里开 `.blend`。**

## 对应关系

这里保留了和 `Assets/可能用到的建模/` 相同的子目录结构：

| 这个文件夹 | `Assets/可能用到的建模/` 里的对应文件 |
|---|---|
| `土地/崇岭阶.blend` | `土地/崇岭阶.glb` |
| `土地plus/*.blend` | `土地plus/*.glb` |
| `房车_Blender精细版_v2/智能农田房车_精细可开合_v2.blend` | 同目录下的 `.glb` / `.fbx` |

## 在 git 里

**这个文件夹会被提交**，队友 clone 下来就能拿到源文件。

只有 `*.blend1` 被 `.gitignore` 排除——那是 Blender 每次保存自动生成的备份，
内容和主文件几乎一样。它的问题不是"大"，而是**每保存一次就往 Git 历史里多塞一份**，
模型改 20 次就是 20 × 16MB。

## 改了模型之后怎么办

`.blend` 改了不会自动同步到 Unity，需要手动重新导出：

1. 在 Blender 里改完，导出为 `.glb`（或 `.fbx`）。
2. 用新文件覆盖 `Assets/可能用到的建模/` 下的同名文件。
3. ⚠️ **覆盖时保持文件名不变**，这样 Unity 会沿用同一个资源 GUID，
   场景里已有的引用不会断。改名 = 新资源 = 引用全丢。
