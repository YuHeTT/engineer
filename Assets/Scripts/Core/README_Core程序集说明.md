# ---------------------------------------------------------------------------
#  Engineer.Core 程序集说明
# ---------------------------------------------------------------------------
#  【这是本工程最重要的一个文件之一】
#
#  "noEngineReferences": true  的含义：
#      本程序集【不引用 UnityEngine 和 UnityEditor】。
#      也就是说这里的代码里写一句 using UnityEngine; 就会【编译报错】。
#
#  为什么必须这样：
#    1. 数值内核必须能脱离 Unity 独立运行 —— 单元测试、批量验证、
#       纯控制台跑几千组参数，都不需要启动编辑器。
#    2. 编译期强制，而不是靠"大家记得别引 UnityEngine"这种口头约定。
#       一旦有人加进来，CI / 本地编译立刻失败，问题当场暴露。
#    3. 保证数值逻辑不会被 MonoBehaviour 的生命周期（Awake/Update/
#       OnDestroy 的执行时机）搞乱。
#
#  因此本目录下【不允许】出现：
#      using UnityEngine;      using UnityEditor;
#      MonoBehaviour / ScriptableObject / Mathf / Debug.Log / Vector3 …
#
#  需要 Unity 类型的地方放在：
#      Assets/Scripts/Config/    （ScriptableObject 包装、导入器）
#      Assets/Scripts/Gameplay/  （农场、机器人、相机）
#      Assets/Scripts/UI/        （状态机、各屏）
#
#  验证方式（在仓库根目录执行，必须零结果）：
#      grep -rn "UnityEngine" Assets/Scripts/Core/
# ---------------------------------------------------------------------------
