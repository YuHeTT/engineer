# 找到"田变灰"的直接原因：小车 FBX 里混进了影棚场景

> 日期：2026-10-15
> 你的判断是对的：**问题确实在 fbx 文件里**。

---

## 一、确认结果：FBX 里混进了 Blender 的整套影棚展示环境

我直接解剖了 `reference_rover_lux3d_wheels.fbx`（二进制 FBX v7400，
用"`名字\x00\x01类型`"的格式提取出 294 条对象记录）。

### 里面的 **139 个 Model** 里，有 5 个**不是小车**

```
   Studio floor                  ← ★★★ 影棚地面（灰色大平面）
   Large studio softbox          ← 影棚柔光箱
   Front fill                    ← 影棚补光灯
   Rear edge light               ← 影棚轮廓灯
   Three quarter reference view  ← 影棚相机
```

### 材质列表也印证了这一点（14 个材质）

```
   Warm gray studio        ← ★ "暖灰影棚" —— 典型的大背景板/地面材质
   Designer_Logic_PBR
   Ceramic white coated armor   ← 以下是车身真实材质
   Midnight navy chassis
   Black rubber
   …
```

### NodeAttribute（也就是灯与相机）

```
   Front fill                    ← 灯
   Large studio softbox          ← 灯
   Rear edge light               ← 灯
   Camera.001                    ← 相机
```

**结论**：这位作者导出时，把 Blender 里的**展示场景（影棚地面 + 三点打光 + 相机）**
一起导进了 FBX。`Studio floor` 是一块相当大的平面（几米到几十米量级），
用 `Warm gray studio` 材质 —— **摆在田里就是一大块灰板，把土壤和玉米全挡住。**

**这就是"田地还在、但就是灰色"的直接原因。**

---

## 二、我做的处理：代码里自动清理（不用等重新导入）

新增 `RoverModelCleaner.cs`，在小车实例化后**立刻删掉影棚物件**。

### 清理规则（两条都要满足，避免误删零件）

| 规则 | 说明 |
|---|---|
| ① **按名字** | 命中 `studio` / `softbox` / `reference view` / `backdrop` / `edge light` / `fill light` |
| ② **按尺寸** | 该物体的水平包围盒 > **"小车本体水平尺寸" × 1.6**，或者"又扁又宽"（Y < 水平尺寸的 15%） |

> 为什么必须两条一起判：`Front fill` 这种名字，也可能是车头的某个填充件。
> 只有尺寸也明显超出车体，才说明它是影棚环境。

### 执行顺序（重要）

```
Instantiate(roverPrefab)
  → RoverModelCleaner.Clean()   ← 先清理（此时量尺寸最准）
  → 应用 roverScale = 0.25      ← 再缩放
```

### 会打印日志

```
[农场] 小车模型清理：清理了 2 个：Studio floor（12.0×0.1×12.0 m）、Large studio softbox（3.2×3.2×3.2 m）
```

---

## 三、同时保留的两项修复（即使你重新导入 FBX 也有用）

| 修复 | 说明 |
|---|---|
| `roverScale = 0.25` | 原始模型 9.2 × 7.0 × 2.8 m，和田块（12 × 9 m）差不多大。缩到约 2.3 m 长才和一株玉米相称 |
| 相机跟车参数 | 改用固定俯角 32° + 高 4.5 / 后撤 6，不再和俯视参数混用 |

---

## 四、给美术的重新导出建议（如果他要重导）

导出时**只选小车本体**，不要带上影棚环境。具体：

```
在 Blender 里：
  1. 切到 Outliner，找到这几个对象并【取消勾选导出】或直接删除：
       Studio floor
       Large studio softbox
       Front fill
       Rear edge light
       Three quarter reference view
     （它们通常在同一个 Collection，比如 "Studio" / "Lighting" / "Camera"）
  2. 导出 FBX 时勾上 "Selected Objects Only"，只选车身与四个轮子
  3. 顺手确认一下单位与缩放：当前导出结果是 9.2 m 长，
     如果实际车长应该是 2.3 m 左右，说明 Blender 里单位设成了 cm 或缩放没应用
```

**如果新的 FBX 干净了**，`RoverModelCleaner` 会自动"无事可做"（打印"没有需要清理的影棚物件"），
不需要改代码。

---

## 五、请验证

Play 一次，看 Console 是否有：

```
[农场] 小车模型清理：清理了 2 个：Studio floor（…）、Large studio softbox（…）
```

然后确认：

1. **田不再是灰的**（灰板被删掉了）
2. 按 `W` 车能动、相机跟着走
3. 田里的玉米和土壤清楚可见

如果 `Studio floor` 没被删掉（日志里没出现），说明它的名字在 Unity 里被改了
（比如导入后变成 `Studio_floor` 或带前缀）。把日志里那行 `小车模型清理：…` 发我，
或者直接告诉我导入后 Hierarchy 里那块灰板叫什么名字，我调整匹配规则。

---

## 六、本轮改动

| 文件 | 改动 |
|---|---|
| `RoverModelCleaner.cs` | **新增** —— 按"名字 + 尺寸"双条件清理影棚地面/灯光/相机 |
| `FarmCruiseController.cs` | `BuildRover` 里先清理再缩放；新增 `cleanStudioProps` 开关；补回 `roverScale` |

`Engineer.UI` 现 **32 个源文件**，五个程序集编译通过。
