using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using ResumePlease;
using ResumePlease.Interview;
using ResumePlease.Interview.InspectPoints;
using ResumePlease.Interview.Level;
using UnityEngine;
using XDEngine.Core.Singletons;

namespace CheatMenu
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        private static Harmony _harmony;

        // ── 输入拦截（鼠标在作弊窗口上时屏蔽游戏点击） ──
        internal static bool BlockMouseInput = false;

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
        private bool _pauseGame = false;
        private float _speedMultiplier = 3f;

        // ── 自动感谢信 ──
        private bool _autoThankYou = false;
        private bool _autoTrash = false;
        private List<string> _scanLog = new List<string>();
        private string _scanResult = "";
        private float _scanResultTimer = 0f;
        private float _pulseTimer = 0f;
        private bool _lastCharHadFault = false;

        // ── 扫描动画延迟激活 ──
        private bool _pendingActivation = false;
        private float _activationTimer = 0f;
        private float _activationDelay = 0.6f;

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

        // ════════════════════════════════════════════
        //  生命周期
        // ════════════════════════════════════════════

        private void Awake()
        {
            Logger = base.Logger;
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID + ".inputblock");
            _harmony.PatchAll(typeof(InputBlockPatch));
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} 已加载");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
                _showMenu = !_showMenu;

            // 每帧检测鼠标是否在作弊窗口区域内（在游戏处理输入之前）
            if (_showMenu)
            {
                float sc = Screen.height / REF_H;
                var mp = Input.mousePosition;
                mp.y = Screen.height - mp.y; // Y 翻转为 GUI 坐标
                BlockMouseInput = _windowRect.Contains(new Vector2(mp.x / sc, mp.y / sc));
            }
            else
            {
                BlockMouseInput = false;
            }

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

            if (_pauseGame)
                Time.timeScale = 0f;
            else if (_speedHack)
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
            GUILayout.Label("感谢你的投递 · 作弊菜单", _titleStyle, GUILayout.ExpandWidth(true));
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
    }
}
