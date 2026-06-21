using ResumePlease;
using ResumePlease.Interview;
using ResumePlease.Interview.Level;
using UnityEngine;
using XDEngine.Core.Singletons;

namespace CheatMenu
{
    public partial class Plugin
    {
        // ════════════════════════════════════════════
        //  Tab 2: 时间控制
        // ════════════════════════════════════════════

        private void DrawTimeTab()
        {
            // ── 时间暂停 ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("时间暂停", _labelStyle);
            bool newPause = GUILayout.Toggle(_pauseGame, "暂停游戏时间", _toggleStyle);
            if (newPause != _pauseGame)
            {
                _pauseGame = newPause;
                if (!_pauseGame) Time.timeScale = _speedHack ? _speedMultiplier : 1f;
            }
            GUILayout.Label("暂停时游戏内时间完全冻结，UI动画不受影响", _smallLabel);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // ── 游戏速度 ──
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("游戏速度", _labelStyle);

            _speedHack = GUILayout.Toggle(_speedHack, "启用速度修改", _toggleStyle);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("倍率: " + _speedMultiplier.ToString("F2") + "x", GUILayout.Width(120));
            _speedMultiplier = GUILayout.HorizontalSlider(_speedMultiplier, 0.1f, 10f);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.1x", _btnStyle)) { _speedMultiplier = 0.1f; _speedHack = true; _pauseGame = false; }
            if (GUILayout.Button("0.25x", _btnStyle)) { _speedMultiplier = 0.25f; _speedHack = true; _pauseGame = false; }
            if (GUILayout.Button("0.5x", _btnStyle)) { _speedMultiplier = 0.5f; _speedHack = true; _pauseGame = false; }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x", _btnStyle)) { _speedMultiplier = 1f; _speedHack = true; _pauseGame = false; }
            if (GUILayout.Button("2x", _btnStyle)) { _speedMultiplier = 2f; _speedHack = true; _pauseGame = false; }
            if (GUILayout.Button("3x", _btnStyle)) { _speedMultiplier = 3f; _speedHack = true; _pauseGame = false; }
            if (GUILayout.Button("5x", _btnStyle)) { _speedMultiplier = 5f; _speedHack = true; _pauseGame = false; }
            if (GUILayout.Button("10x", _btnStyle)) { _speedMultiplier = 10f; _speedHack = true; _pauseGame = false; }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUI.color = new Color(1f, 0.6f, 0.5f);
            if (GUILayout.Button("恢复原速", _btnStyle))
            {
                _speedHack = false; _pauseGame = false; _speedMultiplier = 1f; Time.timeScale = 1f;
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
    }
}
