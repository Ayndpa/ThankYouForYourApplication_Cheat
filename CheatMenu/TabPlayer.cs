using ResumePlease;
using ResumePlease.Interview;
using UnityEngine;
using XDEngine.Core.Singletons;

namespace CheatMenu
{
    public partial class Plugin
    {
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
            if (GUILayout.Button("最大(100)", _btnStyle, GUILayout.Width(100)))
            {
                gd.BI = 100f; ShowStatus("BI = 100 (最大)");
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

            GUILayout.Space(8);

            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label("修改结算评价", _labelStyle);
            GUILayout.Label("将评价强制设为 A（面试全员正确）", _smallLabel);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("当天评价 → A", _btnStyle))
            {
                // 修改当天进行中的面试记录
                if (SingletonEntity<InterviewManager>.HasInstance())
                {
                    var lm = SingletonEntity<InterviewManager>.Instance.levelManager;
                    if (lm != null)
                    {
                        foreach (var r in lm.interviewRecordQueue)
                        {
                            if (!r.skip && string.IsNullOrEmpty(r.taskId))
                            {
                                r.resultType = InterviewRecord.ResultType.PerfectionHire;
                                r.accurate = true;
                            }
                        }
                    }
                }
                // 修改当天已有结算记录
                var today = gd.settlementHistories.Find(s => s.day == gd.currentDay);
                if (today != null)
                {
                    today.rating = SettlementRatingEnum.A;
                    today.accurateCount = today.totalCount;
                    today.faultCount = 0;
                }
                ShowStatus("当天评价已设为 A");
            }
            if (GUILayout.Button("全部历史 → A", _btnStyle))
            {
                foreach (var s in gd.settlementHistories)
                {
                    s.rating = SettlementRatingEnum.A;
                    s.accurateCount = s.totalCount;
                    s.faultCount = 0;
                }
                ShowStatus("全部 " + gd.settlementHistories.Count + " 天评价已设为 A");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
