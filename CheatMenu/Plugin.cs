using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using ResumePlease;
using ResumePlease.DormSystem;
using ResumePlease.Interview;
using ResumePlease.Interview.Document.Fields;
using ResumePlease.Interview.Document.Faults;
using ResumePlease.Interview.Inspect;
using ResumePlease.Interview.InspectPoints;
using ResumePlease.Interview.Level;
using ResumePlease.UI;
using UnityEngine;
using XDEngine.Core.Singletons;

namespace CheatMenu;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    // ── 缩放（基于 1080p 虚拟坐标） ──
    private const float REF_H = 1080f;

    // ── 窗口状态（虚拟坐标） ──
    private bool _showMenu = false;
    private Rect _windowRect = new Rect(40f, 40f, 620f, 820f);
    private int _tab = 0;
    private Vector2 _scrollPos;

    // ── 玩家属性输入 ──
    private string _moneyInput = "1000";
    private string _biInput = "0";
    private string _expInput = "100";
    private string _levelInput = "1";
    private string _dayInput = "1";

    // ── 开关型功能 ──
    private bool _freezeBI = false;
    private bool _freezeOT = false;
    private bool _autoHire = false;
    private bool _speedHack = false;
    private float _speedMultiplier = 3f;

    // ── 自动感谢信 ──
    private bool _autoThankYou = false;
    private bool _autoTrash = false;
    private List<string> _scanLog = new List<string>();
    private string _scanResult = "";
    private float _scanResultTimer = 0f;
    private float _pulseTimer = 0f;
    private bool _lastCharHadFault = false;   // 追踪上一个角色是否有缺陷，防止重复扫描

    // ── 扫描动画延迟激活 ──
    private bool _pendingActivation = false;
    private float _activationTimer = 0f;
    private float _activationDelay = 0.6f;   // 等待感谢信弹出动画播放完毕

    // ── 样式 ──
    private GUISkin _skin;
    private GUISkin _defaultSkin;
    private Font _font;
    private GUIStyle _boxStyle;
    private GUIStyle _btnStyle;
    private GUIStyle _toggleStyle;
    private GUIStyle _fieldStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _statLabel;
    private GUIStyle _smallLabel;

    // ── 状态显示 ──
    private string _statusMsg = "";
    private float _statusTimer = 0f;

    private static readonly string[] TabNames = { "玩家属性", "面试系统", "时间控制", "工具" };

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} 已加载");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
            _showMenu = !_showMenu;

        if (_freezeBI && SingletonEntity<GameSaveManager>.HasInstance())
        {
            var gd = SingletonEntity<GameSaveManager>.Instance.GameData;
            if (gd != null && gd.BI != 0f) gd.BI = 0f;
        }

        if (_freezeOT && SingletonEntity<GameSaveManager>.HasInstance())
        {
            var gd = SingletonEntity<GameSaveManager>.Instance.GameData;
            if (gd != null) gd.consecutiveOTDays = 0;
        }

        if (_speedHack)
            Time.timeScale = _speedMultiplier;

        if (_autoHire && SingletonEntity<InterviewManager>.HasInstance())
        {
            var lm = SingletonEntity<InterviewManager>.Instance.levelManager;
            if (lm != null && lm.currentInterviewRecord != null)
                lm.currentInterviewRecord.resultType = InterviewRecord.ResultType.PerfectionHire;
        }

        // ── 自动感谢信逻辑 ──
        if (_autoThankYou && SingletonEntity<InterviewManager>.HasInstance())
        {
            var lm = SingletonEntity<InterviewManager>.Instance.levelManager;
            if (lm?.currentInterviewCharacter != null)
            {
                var character = lm.currentInterviewCharacter;
                var record = lm.currentInterviewRecord;
                bool hasFault = character.faultModel != null && character.faultModel.HasFault;

                // 新候选人进入时重置追踪
                if (hasFault != _lastCharHadFault)
                    _lastCharHadFault = hasFault;

                // 有缺陷 + 尚未激活检查点 + 对话已完成 → 自动扫描
                if (hasFault && record.pointType == InspectPointType.None
                    && ConversationManager.Instance.conversationFinish)
                {
                    PerformScan(character);
                }

                // 自动拒绝有问题的候选人
                if (_autoTrash && hasFault && record.pointType != InspectPointType.None)
                    record.resultType = InterviewRecord.ResultType.FaultTrashWithInspectPoint;
            }
        }

        // 扫描结果计时
        if (_scanResultTimer > 0f)
        {
            _scanResultTimer -= Time.unscaledDeltaTime;
            if (_scanResultTimer <= 0f) _scanResult = "";
        }

        // 感谢信弹出动画播放完毕后，延迟激活检查点
        if (_pendingActivation)
        {
            _activationTimer -= Time.unscaledDeltaTime;
            if (_activationTimer <= 0f)
            {
                _pendingActivation = false;
                if (SingletonEntity<InterviewManager>.HasInstance())
                {
                    var inspectManager = SingletonEntity<InterviewManager>.Instance.inspectManager;
                    if (inspectManager.waittingTriggerModel != null)
                    {
                        var pt = inspectManager.waittingTriggerModel.pointType;
                        inspectManager.ActivateTriggerPoint(pt);
                        _scanResult = $"✓ 感谢信已激活 [{pt}]";
                        _scanResultTimer = 5f;
                        PlayThankYouSound();
                    }
                }
            }
        }

        if (_statusTimer > 0f)
        {
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f) _statusMsg = "";
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    // ════════════════════════════════════════════
    //  样式构建 — 每帧重建皮肤，完全独立于游戏
    // ════════════════════════════════════════════

    private GUISkin BuildSkin()
    {
        var skin = ScriptableObject.CreateInstance<GUISkin>();
        skin.hideFlags = HideFlags.HideAndDontSave;

        // 字体：优先用缓存的游戏默认字体，其次系统字体，最后 Arial
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16)
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        skin.font = _font;

        Color accent = new Color(0.35f, 0.6f, 0.95f, 1f);

        // ── Window ──
        skin.window = new GUIStyle
        {
            name = "CheatWindow",
            normal =
            {
                background = MakeBorderTex(new Color(0.10f, 0.10f, 0.13f, 0.98f), new Color(0.30f, 0.45f, 0.70f, 0.9f)),
                textColor = new Color(0.8f, 0.8f, 0.9f)
            },
            onNormal =
            {
                background = MakeBorderTex(new Color(0.10f, 0.10f, 0.13f, 0.98f), new Color(0.30f, 0.45f, 0.70f, 0.9f))
            },
            border = new RectOffset(2, 2, 2, 2),
            fontSize = 16,
            padding = new RectOffset(12, 12, 10, 12)
        };

        // ── Box ──
        skin.box = new GUIStyle
        {
            name = "CheatBox",
            normal =
            {
                background = MakeBorderTex(new Color(0.13f, 0.13f, 0.17f, 0.95f), new Color(0.25f, 0.25f, 0.35f, 0.6f)),
                textColor = new Color(0.7f, 0.7f, 0.75f)
            },
            border = new RectOffset(2, 2, 2, 2),
            padding = new RectOffset(16, 16, 12, 12)
        };

        // ── Button ──
        skin.button = new GUIStyle
        {
            name = "CheatButton",
            normal =
            {
                background = MakeBorderTex(new Color(0.28f, 0.30f, 0.38f, 1f), new Color(0.50f, 0.60f, 0.85f, 0.9f)),
                textColor = new Color(0.90f, 0.90f, 0.95f)
            },
            hover =
            {
                background = MakeBorderTex(new Color(0.35f, 0.38f, 0.48f, 1f), new Color(0.55f, 0.70f, 1f, 1f)),
                textColor = Color.white
            },
            active =
            {
                background = MakeBorderTex(new Color(0.30f, 0.45f, 0.70f, 0.8f), accent),
                textColor = Color.white
            },
            border = new RectOffset(2, 2, 2, 2),
            fontSize = 15,
            padding = new RectOffset(14, 14, 8, 8),
            margin = new RectOffset(4, 4, 4, 4)
        };

        // ── Toggle（从默认皮肤复制勾选框纹理，仅覆盖文字色） ──
        var defToggle = new GUIStyle(_defaultSkin.toggle);
        skin.toggle = new GUIStyle(defToggle)
        {
            name = "CheatToggle",
            normal =
            {
                background = defToggle.normal.background,
                textColor = new Color(0.75f, 0.75f, 0.80f)
            },
            onNormal =
            {
                background = defToggle.onNormal.background,
                textColor = new Color(0.85f, 0.85f, 0.90f)
            },
            hover =
            {
                background = defToggle.hover.background,
                textColor = Color.white
            },
            onHover =
            {
                background = defToggle.onHover.background,
                textColor = Color.white
            },
            active =
            {
                background = defToggle.active.background,
            },
            onActive =
            {
                background = defToggle.onActive.background,
            },
            focused =
            {
                background = defToggle.focused.background,
            },
            onFocused =
            {
                background = defToggle.onFocused.background,
            },
            fontSize = 15,
            padding = new RectOffset(26, 8, 6, 6),
            margin = new RectOffset(6, 6, 4, 4)
        };

        // ── TextField ──
        skin.textField = new GUIStyle
        {
            name = "CheatField",
            normal =
            {
                background = MakeBorderTex(new Color(0.14f, 0.14f, 0.18f, 1f), new Color(0.45f, 0.55f, 0.80f, 0.7f)),
                textColor = Color.white
            },
            focused =
            {
                background = MakeBorderTex(new Color(0.14f, 0.14f, 0.18f, 1f), new Color(0.45f, 0.55f, 0.80f, 0.7f)),
                textColor = Color.white
            },
            border = new RectOffset(2, 2, 2, 2),
            fontSize = 15,
            padding = new RectOffset(10, 10, 6, 6),
            margin = new RectOffset(4, 4, 4, 4)
        };

        // ── Label ──
        skin.label = new GUIStyle
        {
            name = "CheatLabel",
            normal = { textColor = new Color(0.85f, 0.85f, 0.9f) },
            fontSize = 15,
            padding = new RectOffset(0, 0, 4, 4)
        };

        // ── HorizontalSlider ──
        skin.horizontalSlider = new GUIStyle
        {
            name = "CheatHSlider",
            normal = { background = MakeBorderTex(new Color(0.20f, 0.20f, 0.25f, 1f), new Color(0.35f, 0.35f, 0.45f, 0.8f)) },
            fixedHeight = 12,
            border = new RectOffset(1, 1, 1, 1)
        };

        skin.horizontalSliderThumb = new GUIStyle
        {
            name = "CheatHSliderThumb",
            normal = { background = MakeBorderTex(accent, accent * 1.2f) },
            hover = { background = MakeBorderTex(accent * 1.1f, Color.white) },
            active = { background = MakeBorderTex(accent * 0.9f, Color.white) },
            fixedHeight = 16,
            fixedWidth = 16
        };

        // ── ScrollView ──
        skin.scrollView = new GUIStyle();

        // ── 自定义样式 ──
        _boxStyle = new GUIStyle(skin.box) { padding = new RectOffset(16, 16, 12, 12) };
        _btnStyle = skin.button;
        _toggleStyle = skin.toggle;
        _fieldStyle = skin.textField;

        _titleStyle = new GUIStyle(skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = accent },
            padding = new RectOffset(0, 0, 12, 8)
        };

        _labelStyle = new GUIStyle(skin.label)
        {
            fontSize = 15,
            normal = { textColor = new Color(0.85f, 0.85f, 0.9f) },
            padding = new RectOffset(0, 0, 4, 4)
        };

        _statLabel = new GUIStyle(skin.label)
        {
            fontSize = 15,
            normal = { textColor = new Color(0.70f, 0.82f, 1f) },
            padding = new RectOffset(0, 0, 4, 4)
        };

        _smallLabel = new GUIStyle(skin.label)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.50f, 0.50f, 0.55f) }
        };

        return skin;
    }

    // ════════════════════════════════════════════
    //  IMGUI 渲染
    // ════════════════════════════════════════════

    private void OnGUI()
    {
        if (!_showMenu) return;

        // 首次调用时缓存游戏默认皮肤（用于复制勾选框等内置纹理）
        if (_defaultSkin == null)
            _defaultSkin = GUI.skin;
        if (_font == null)
            _font = _defaultSkin.font;

        // 每帧重建皮肤 — 彻底隔离游戏皮肤
        if (_skin != null) Destroy(_skin);
        _skin = BuildSkin();
        GUI.skin = _skin;

        // DPI 缩放
        float scale = Screen.height / REF_H;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

        // 窗口外层描边
        Rect br = new Rect(_windowRect.x - 2, _windowRect.y - 2, _windowRect.width + 4, _windowRect.height + 4);
        GUI.color = new Color(0.30f, 0.45f, 0.70f, 0.5f);
        GUI.DrawTexture(br, Texture2D.whiteTexture);
        GUI.color = Color.white;

        _windowRect = GUI.Window(314159, _windowRect, DrawWindow, "");

        GUI.matrix = Matrix4x4.identity;
    }

    private void DrawWindow(int id)
    {
        // 标题栏
        GUILayout.BeginHorizontal();
        GUILayout.Label("简历请收好 · 作弊菜单", _titleStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("✕", _btnStyle, GUILayout.Width(40), GUILayout.Height(30)))
            _showMenu = false;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Tab 栏
        GUILayout.BeginHorizontal();
        for (int i = 0; i < TabNames.Length; i++)
        {
            if (_tab == i) GUI.color = new Color(0.40f, 0.65f, 1f);
            if (GUILayout.Toggle(_tab == i, TabNames[i], _btnStyle))
                _tab = i;
            GUI.color = Color.white;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        // 内容区
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        switch (_tab)
        {
            case 0: DrawPlayerTab(); break;
            case 1: DrawInterviewTab(); break;
            case 2: DrawTimeTab(); break;
            case 3: DrawToolsTab(); break;
        }

        GUILayout.EndScrollView();

        // 状态栏
        if (!string.IsNullOrEmpty(_statusMsg))
        {
            GUILayout.Space(4);
            GUI.color = new Color(0.4f, 0.95f, 0.5f);
            GUILayout.Label("✔ " + _statusMsg, _labelStyle);
            GUI.color = Color.white;
        }

        GUILayout.Label("按 F9 切换菜单 · 拖拽标题栏移动", _smallLabel);

        GUI.DragWindow(new Rect(0, 0, 10000, 44));
    }

    // ════════════════════════════════════════════
    //  Tab 0: 玩家属性
    // ════════════════════════════════════════════

    private void DrawPlayerTab()
    {
        var gd = GetGameData();
        if (gd == null) { DrawNotInGame(); return; }

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("── 当前状态 ──", _labelStyle);
        DrawStat("金钱", gd.money.ToString("N0"));
        DrawStat("精神值 (BI)", gd.BI.ToString("F1") + " / 100");
        DrawStat("等级", gd.level.ToString());
        DrawStat("经验", gd.exp.ToString());
        DrawStat("当前天数", gd.currentDay.ToString());
        DrawStat("连续加班", gd.consecutiveOTDays + " / 5 天");
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("修改金钱", _labelStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("数额:", GUILayout.Width(50));
        _moneyInput = GUILayout.TextField(_moneyInput, _fieldStyle, GUILayout.Width(180));
        if (GUILayout.Button("添加", _btnStyle, GUILayout.Width(80)))
        {
            if (int.TryParse(_moneyInput, out int v)) { gd.money += v; ShowStatus("金钱 +" + v.ToString("N0") + " → " + gd.money.ToString("N0")); }
        }
        if (GUILayout.Button("设置为", _btnStyle, GUILayout.Width(80)))
        {
            if (int.TryParse(_moneyInput, out int v)) { gd.money = v; ShowStatus("金钱 = " + gd.money.ToString("N0")); }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("修改精神值 (BI)", _labelStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("数值:", GUILayout.Width(50));
        _biInput = GUILayout.TextField(_biInput, _fieldStyle, GUILayout.Width(120));
        if (GUILayout.Button("设置", _btnStyle, GUILayout.Width(80)))
        {
            if (float.TryParse(_biInput, out float v)) { gd.BI = Mathf.Clamp(v, 0f, 100f); ShowStatus("BI = " + gd.BI.ToString("F1")); }
        }
        if (GUILayout.Button("归零", _btnStyle, GUILayout.Width(80)))
        {
            gd.BI = 0f; ShowStatus("BI 已归零");
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("修改等级 / 经验", _labelStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("等级:", GUILayout.Width(50));
        _levelInput = GUILayout.TextField(_levelInput, _fieldStyle, GUILayout.Width(60));
        GUILayout.Space(10);
        GUILayout.Label("经验:", GUILayout.Width(50));
        _expInput = GUILayout.TextField(_expInput, _fieldStyle, GUILayout.Width(80));
        GUILayout.Space(10);
        if (GUILayout.Button("应用", _btnStyle, GUILayout.Width(80)))
        {
            if (int.TryParse(_levelInput, out int lv)) gd.level = Mathf.Max(1, lv);
            if (int.TryParse(_expInput, out int ex)) gd.exp = Mathf.Max(0, ex);
            ShowStatus("等级=" + gd.level + "  经验=" + gd.exp);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(8);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("修改当前天数", _labelStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("天数:", GUILayout.Width(50));
        _dayInput = GUILayout.TextField(_dayInput, _fieldStyle, GUILayout.Width(100));
        if (GUILayout.Button("设置", _btnStyle, GUILayout.Width(80)))
        {
            if (int.TryParse(_dayInput, out int d)) { gd.currentDay = Mathf.Max(1, d); ShowStatus("天数 = " + gd.currentDay); }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════
    //  Tab 1: 面试系统
    // ════════════════════════════════════════════

    private void DrawInterviewTab()
    {
        var gd = GetGameData();
        if (gd == null) { DrawNotInGame(); return; }

        bool inInterview = SingletonEntity<InterviewManager>.HasInstance();

        // ── 面试作弊 ──
        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("面试作弊", _labelStyle);

        _autoHire = GUILayout.Toggle(_autoHire, "自动完美录用（当前候选人）", _toggleStyle);

        GUILayout.Space(8);
        GUI.enabled = inInterview;
        if (GUILayout.Button("立即结算当天（跳过剩余面试）", _btnStyle))
        {
            if (inInterview)
            {
                LevelManager.Instance.officeTimer.Finsh();
                ShowStatus("已强制结束当天面试");
            }
        }
        GUI.enabled = true;
        GUILayout.EndVertical();

        // ── 自动感谢信 ──
        GUILayout.Space(10);
        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("── 自动感谢信 ──", _labelStyle);

        _pulseTimer += Time.unscaledDeltaTime;
        float pulse = Mathf.Sin(_pulseTimer * 3f) * 0.5f + 0.5f;
        Color pulseColor = Color.Lerp(new Color(0.35f, 0.6f, 0.95f), new Color(0.5f, 0.8f, 1f), pulse);

        GUI.color = pulseColor;
        _autoThankYou = GUILayout.Toggle(_autoThankYou, "⚡ 自动扫描 + 生成感谢信", _toggleStyle);
        GUI.color = Color.white;

        GUILayout.Space(4);
        _autoTrash = GUILayout.Toggle(_autoTrash, "自动拒绝有问题的候选人", _toggleStyle);

        GUILayout.Space(8);
        GUI.color = pulseColor;
        GUI.enabled = inInterview;
        if (GUILayout.Button("🔍 立即扫描当前候选人", _btnStyle))
        {
            ManualScan();
        }
        GUI.enabled = true;
        GUI.color = Color.white;

        // 扫描日志
        if (_scanLog.Count > 0)
        {
            GUILayout.Space(6);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("── 扫描日志 ──", _smallLabel);

            int start = Mathf.Max(0, _scanLog.Count - 8);
            for (int i = start; i < _scanLog.Count; i++)
            {
                string log = _scanLog[i];
                if (log.StartsWith("  ✓") || log.StartsWith("[DONE]"))
                    GUI.color = new Color(0.4f, 0.95f, 0.5f);
                else if (log.StartsWith("[ERROR]") || log.StartsWith("[INFO]"))
                    GUI.color = new Color(1f, 0.7f, 0.3f);
                else if (log.StartsWith("  ▸"))
                    GUI.color = new Color(0.5f, 0.5f, 0.6f);
                else
                    GUI.color = new Color(0.7f, 0.8f, 1f);

                GUILayout.Label(log, _smallLabel);
            }
            GUI.color = Color.white;
            GUILayout.EndVertical();
        }

        // 扫描结果闪烁
        if (!string.IsNullOrEmpty(_scanResult))
        {
            GUILayout.Space(4);
            GUI.color = _scanResult.StartsWith("✓")
                ? new Color(0.3f, 1f, 0.5f)
                : new Color(1f, 0.4f, 0.4f);
            GUILayout.Label(_scanResult, _labelStyle);
            GUI.color = Color.white;
        }

        GUILayout.EndVertical();

        // ── 累计统计 ──
        GUILayout.Space(10);
        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("── 累计统计 ──", _labelStyle);
        var stats = gd.statistics;
        DrawStat("总面试次数", stats.totalInterviewCount.ToString());
        DrawStat("录用次数", stats.hireInteriviewCount.ToString());
        DrawStat("淘汰次数", stats.trashInterviewCount.ToString());
        DrawStat("正确判断", stats.totalAccurateInterviewCount.ToString());
        DrawStat("错误判断", stats.totalFaultInterviewCount.ToString());
        DrawStat("总收入", stats.totalEarnedMoney.ToString("N0"));
        GUILayout.EndVertical();

        if (gd.settlementHistories.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("── 最近结算记录 ──", _labelStyle);
            int show = Mathf.Min(gd.settlementHistories.Count, 5);
            for (int i = gd.settlementHistories.Count - show; i < gd.settlementHistories.Count; i++)
            {
                var s = gd.settlementHistories[i];
                GUILayout.Label(
                    "第" + s.day + "天  准确:" + s.accurateCount + "/" + s.totalCount + "  收入:" + s.total.ToString("N0") + "  BI+" + s.addBi.ToString("F1"),
                    _smallLabel);
            }
            GUILayout.EndVertical();
        }
    }

    // ════════════════════════════════════════════
    //  Tab 2: 时间控制
    // ════════════════════════════════════════════

    private void DrawTimeTab()
    {
        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("游戏速度", _labelStyle);

        _speedHack = GUILayout.Toggle(_speedHack, "启用速度修改", _toggleStyle);

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        GUILayout.Label("倍率: " + _speedMultiplier.ToString("F1") + "x", GUILayout.Width(120));
        _speedMultiplier = GUILayout.HorizontalSlider(_speedMultiplier, 1f, 10f);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1x", _btnStyle)) { _speedMultiplier = 1f; _speedHack = true; }
        if (GUILayout.Button("2x", _btnStyle)) { _speedMultiplier = 2f; _speedHack = true; }
        if (GUILayout.Button("3x", _btnStyle)) { _speedMultiplier = 3f; _speedHack = true; }
        if (GUILayout.Button("5x", _btnStyle)) { _speedMultiplier = 5f; _speedHack = true; }
        if (GUILayout.Button("10x", _btnStyle)) { _speedMultiplier = 10f; _speedHack = true; }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUI.color = new Color(1f, 0.6f, 0.5f);
        if (GUILayout.Button("恢复原速", _btnStyle))
        {
            _speedHack = false; _speedMultiplier = 1f; Time.timeScale = 1f;
            ShowStatus("已恢复原速");
        }
        GUI.color = Color.white;
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("加班保护", _labelStyle);
        var gd = GetGameData();
        if (gd != null)
        {
            _freezeOT = GUILayout.Toggle(_freezeOT, "冻结连续加班天数（防止过劳结局）", _toggleStyle);
            GUILayout.Label("当前连续加班: " + gd.consecutiveOTDays + " / 5 天（≥5 触发过劳结局）", _smallLabel);
            GUILayout.Space(6);
            if (GUILayout.Button("重置加班天数为 0", _btnStyle))
            {
                gd.consecutiveOTDays = 0;
                ShowStatus("加班天数已重置");
            }
        }
        else { DrawNotInGame(); }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("面试时间", _labelStyle);
        if (SingletonEntity<InterviewManager>.HasInstance())
        {
            var lm = LevelManager.Instance;
            if (lm != null)
            {
                DrawStat("上班时间", lm.startTime.ToString());
                DrawStat("下班时间", lm.endTime.ToString());
                DrawStat("已下班", lm.DayEnd ? "是" : "否");
                DrawStat("在加班", lm.OT ? "是" : "否");

                GUILayout.Space(6);
                GUI.color = new Color(1f, 0.6f, 0.5f);
                if (GUILayout.Button("强制下班（跳过当前时间）", _btnStyle))
                {
                    lm.officeTimer.Finsh();
                    ShowStatus("已强制下班");
                }
                GUI.color = Color.white;
            }
        }
        else { GUILayout.Label("当前不在面试场景", _smallLabel); }
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════
    //  Tab 3: 工具
    // ════════════════════════════════════════════

    private void DrawToolsTab()
    {
        var gd = GetGameData();

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("精神值保护", _labelStyle);
        _freezeBI = GUILayout.Toggle(_freezeBI, "冻结 BI 为 0（防止精神崩溃结局）", _toggleStyle);
        if (gd != null)
        {
            GUILayout.Label("当前 BI: " + gd.BI.ToString("F1") + " / 100", _smallLabel);
            GUILayout.Space(6);
            if (GUILayout.Button("立即将 BI 归零", _btnStyle))
            {
                gd.BI = 0f;
                ShowStatus("BI 已归零");
            }
        }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("快捷操作", _labelStyle);

        if (gd != null)
        {
            if (GUILayout.Button("金钱 +10,000", _btnStyle))
            {
                gd.money += 10000;
                ShowStatus("金钱 +10000 → " + gd.money.ToString("N0"));
            }
            if (GUILayout.Button("金钱 +100,000", _btnStyle))
            {
                gd.money += 100000;
                ShowStatus("金钱 +100000 → " + gd.money.ToString("N0"));
            }
            if (GUILayout.Button("经验 +500", _btnStyle))
            {
                SingletonEntity<GameManager>.Instance.AddExp(500);
                ShowStatus("经验 +500");
            }
            if (GUILayout.Button("等级 +1", _btnStyle))
            {
                gd.level++;
                ShowStatus("等级 → " + gd.level);
            }
        }
        else { DrawNotInGame(); }
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("存档操作", _labelStyle);

        GUI.enabled = SingletonEntity<GameSaveManager>.HasInstance();
        if (GUILayout.Button("立即保存当前存档", _btnStyle))
        {
            try
            {
                SingletonEntity<GameSaveManager>.Instance.CoverCurrentSolt();
                ShowStatus("存档已保存");
            }
            catch (Exception e)
            {
                ShowStatus("保存失败: " + e.Message);
                Logger.LogError("保存失败: " + e);
            }
        }
        GUI.enabled = true;
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("── 插件信息 ──", _labelStyle);
        GUILayout.Label(MyPluginInfo.PLUGIN_NAME + " v" + MyPluginInfo.PLUGIN_VERSION, _smallLabel);
        GUILayout.Label("按 F9 打开/关闭菜单", _smallLabel);
        GUILayout.Label("基于游戏反编译代码制作", _smallLabel);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════════

    private GameData GetGameData()
    {
        try
        {
            if (!SingletonEntity<GameSaveManager>.HasInstance()) return null;
            return SingletonEntity<GameSaveManager>.Instance.GameData;
        }
        catch { return null; }
    }

    private void DrawStat(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _labelStyle, GUILayout.Width(140));
        GUILayout.Label(value, _statLabel);
        GUILayout.EndHorizontal();
    }

    private void DrawNotInGame()
    {
        GUI.color = new Color(1f, 0.7f, 0.3f);
        GUILayout.Label("⚠ 未在游戏中，部分功能不可用", _labelStyle);
        GUI.color = Color.white;
    }

    private void ShowStatus(string msg)
    {
        _statusMsg = msg;
        _statusTimer = 4f;
        Logger.LogInfo(msg);
    }

    // ════════════════════════════════════════════
    //  自动感谢信 — 核心扫描逻辑
    // ════════════════════════════════════════════

    /// <summary>
    /// 反射获取 BasicInspectController 的所有私有触发模型列表。
    /// </summary>
    private BaseTriggerModel[] GetAllTriggerModels(BasicInspectController controller)
    {
        var type = typeof(BasicInspectController);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var models = new List<BaseTriggerModel>();

        string[] fieldNames = { "_triggerModel1", "_triggerModel2", "_triggerModel3",
                                "_triggerModel4", "_customTriggerModel", "_qaTriggerModel" };

        foreach (var fname in fieldNames)
        {
            var list = type.GetField(fname, flags)?.GetValue(controller) as System.Collections.IEnumerable;
            if (list != null)
                foreach (var m in list)
                    if (m is BaseTriggerModel btm) models.Add(btm);
        }

        return models.ToArray();
    }

    /// <summary>
    /// 瞬间遍历所有可检查组合，找到匹配项并自动触发感谢信。
    /// </summary>
    private void PerformScan(CharacterEntity character)
    {
        _scanLog.Clear();
        _scanResult = "";

        var inspectManager = SingletonEntity<InterviewManager>.Instance.inspectManager;
        var basicController = inspectManager.BasicInspectController;
        var faultModel = character.faultModel;

        // 加载所有注册的触发规则
        var allModels = GetAllTriggerModels(basicController);
        _scanLog.Add($"[SCAN] 加载 {allModels.Length} 条检查规则");

        // 收集所有缺陷字段
        var allFaultFields = new List<IDocumentField>();
        foreach (var docFault in faultModel.documentFaultList)
        {
            if (docFault is FaultTemplateDocumentFaultComponent ftc)
                allFaultFields.AddRange(ftc.faultFields);
        }

        var allFaultReqs = faultModel.requirementList;
        _scanLog.Add($"[SCAN] 发现 {allFaultReqs.Count} 个需求缺陷, {allFaultFields.Count} 个字段缺陷");

        bool found = false;

        // ── 第一轮：单需求组合 ──
        _scanLog.Add("── 需求检查 ──");
        foreach (var req in allFaultReqs)
        {
            string tag = $"REQ:{req.type}";
            _scanLog.Add($"  ▸ {tag}");

            if (basicController.TakeInspect(new IDocumentField[0], req))
            {
                _scanLog.Add($"  ✓ 命中 {tag}");
                found = true;
                break;
            }
        }

        // ── 第二轮：字段 + 需求组合 ──
        if (!found && allFaultFields.Count > 0 && allFaultReqs.Count > 0)
        {
            _scanLog.Add("── 字段+需求检查 ──");
            foreach (var field in allFaultFields)
            {
                foreach (var req in allFaultReqs)
                {
                    string tag = $"FIELD+REQ:{field.GetFieldEnum()}+{req.type}";
                    _scanLog.Add($"  ▸ {tag}");

                    if (basicController.TakeInspect(new[] { field }, req))
                    {
                        _scanLog.Add($"  ✓ 命中 {tag}");
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
        }

        // ── 第三轮：双字段组合 ──
        if (!found && allFaultFields.Count >= 2)
        {
            _scanLog.Add("── 双字段检查 ──");
            for (int i = 0; i < allFaultFields.Count && !found; i++)
            {
                for (int j = i + 1; j < allFaultFields.Count; j++)
                {
                    string tag = $"FIELD×2:{allFaultFields[i].GetFieldEnum()}+{allFaultFields[j].GetFieldEnum()}";
                    _scanLog.Add($"  ▸ {tag}");

                    if (basicController.TakeInspect(new[] { allFaultFields[i], allFaultFields[j] }, null))
                    {
                        _scanLog.Add($"  ✓ 命中 {tag}");
                        found = true;
                        break;
                    }
                }
            }
        }

        // ── 第四轮：全局字段单独触发 ──
        if (!found && allFaultFields.Count > 0)
        {
            _scanLog.Add("── 全局字段检查 ──");
            foreach (var field in allFaultFields)
            {
                string tag = $"GLOBAL:{field.GetFieldEnum()}";
                _scanLog.Add($"  ▸ {tag}");

                if (basicController.TakeInspect(new[] { field }, null))
                {
                    _scanLog.Add($"  ✓ 命中 {tag}");
                    found = true;
                    break;
                }
            }
        }

        // ── 结果处理 ──
        if (found && inspectManager.waittingTriggerModel != null)
        {
            var pt = inspectManager.waittingTriggerModel.pointType;
            _scanResult = $"✓ 感谢信已生成 [{pt}]，等待动画...";
            _scanLog.Add($"[DONE] 检查点 {pt} 已触发，等待弹出动画");

            // 不立即激活，而是启动延迟计时器，让感谢信弹出动画先播放
            _pendingActivation = true;
            _activationTimer = _activationDelay;
        }
        else
        {
            _scanResult = "✗ 未找到匹配的检查组合";
            _scanLog.Add("[DONE] 无匹配结果");
        }

        _scanResultTimer = 5f;
    }

    private void ManualScan()
    {
        if (!SingletonEntity<InterviewManager>.HasInstance())
        {
            _scanLog.Clear();
            _scanLog.Add("[ERROR] 当前不在面试场景");
            _scanResult = "✗ 不在面试场景";
            _scanResultTimer = 3f;
            return;
        }

        var lm = SingletonEntity<InterviewManager>.Instance.levelManager;
        if (lm?.currentInterviewCharacter == null)
        {
            _scanLog.Clear();
            _scanLog.Add("[ERROR] 当前没有面试候选人");
            _scanResult = "✗ 没有候选人";
            _scanResultTimer = 3f;
            return;
        }

        if (lm.currentInterviewCharacter.faultModel == null || !lm.currentInterviewCharacter.faultModel.HasFault)
        {
            _scanLog.Clear();
            _scanLog.Add("[INFO] 当前候选人无问题，无需感谢信");
            _scanResult = "候选人无问题";
            _scanResultTimer = 3f;
            return;
        }

        PerformScan(lm.currentInterviewCharacter);
    }

    private void PlayThankYouSound()
    {
        // 游戏音效系统 (SingletonGlobalBehaviour) 的类型约束在插件程序集中无法解析，
        // 因此音效由游戏自身的感谢信激活流程自动播放，无需手动触发。
        Logger.LogInfo("[SFX] 感谢信激活音效已由游戏内置流程播放");
    }

    /// <summary>
    /// 5×5 带边框纹理，外圈 borderColor，内圈 bgColor。
    /// </summary>
    private Texture2D MakeBorderTex(Color bgColor, Color borderColor)
    {
        int s = 5;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = (x == 0 || x == s - 1 || y == 0 || y == s - 1) ? borderColor : bgColor;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
