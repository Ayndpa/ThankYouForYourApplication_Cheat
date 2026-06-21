using System.Collections.Generic;
using System.Reflection;
using ResumePlease;
using ResumePlease.Interview;
using ResumePlease.Interview.Document.Fields;
using ResumePlease.Interview.Document.Faults;
using ResumePlease.Interview.Inspect;
using ResumePlease.Interview.InspectPoints;
using ResumePlease.Interview.Level;
using UnityEngine;
using XDEngine.Core.Singletons;

namespace CheatMenu
{
    public partial class Plugin
    {
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
    }
}
