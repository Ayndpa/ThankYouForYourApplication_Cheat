using System;
using ResumePlease;
using UnityEngine;
using XDEngine.Core.Singletons;

namespace CheatMenu
{
    public partial class Plugin
    {
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
    }
}
