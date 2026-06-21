using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ResumePlease;
using ResumePlease.Interview;
using ResumePlease.Interview.Document.Fields;
using ResumePlease.Interview.Document.Faults;
using ResumePlease.Interview.Inspect;
using ResumePlease.Interview.InspectPoints;
using ResumePlease.Interview.Level;
using ResumePlease.Interview.Level.Requirements;
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
        /// 通过反射获取 BasicInspectController 的指定私有触发模型列表。
        /// </summary>
        private List<T> GetTriggerModelList<T>(BasicInspectController controller, string fieldName)
        {
            var field = typeof(BasicInspectController).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (field?.GetValue(controller) as List<T>) ?? new List<T>();
        }

        /// <summary>
        /// 通过反射调用 BasicInspectController 的私有 TriggerInspectPoint 方法。
        /// </summary>
        private bool TriggerInspectPointViaReflection(BasicInspectController controller, BaseTriggerModel model)
        {
            var method = typeof(BasicInspectController).GetMethod("TriggerInspectPoint",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                _scanLog.Add("[ERROR] 无法找到 TriggerInspectPoint 方法");
                return false;
            }
            method.Invoke(controller, new object[] { model });
            return true;
        }

        /// <summary>
        /// 直接匹配触发模型并触发感谢信。
        /// 匹配逻辑与游戏 BasicInspectController.TakeInspect 的内部路由一致，但绕过路由直接匹配。
        /// </summary>
        private void PerformScan(CharacterEntity character)
        {
            _scanLog.Clear();
            _scanResult = "";

            if (!SingletonEntity<InterviewManager>.HasInstance())
            {
                _scanLog.Add("[ERROR] 当前不在面试场景");
                _scanResult = "✗ 不在面试场景";
                _scanResultTimer = 3f;
                return;
            }

            var inspectManager = SingletonEntity<InterviewManager>.Instance.inspectManager;
            var basicController = inspectManager.BasicInspectController;
            var faultModel = character.faultModel;

            // ── 收集缺陷字段和需求 ──
            var allFaultFields = new List<IDocumentField>();
            foreach (var docFault in faultModel.documentFaultList)
            {
                if (docFault is FaultTemplateDocumentFaultComponent ftc)
                    allFaultFields.AddRange(ftc.faultFields);
            }
            var allFaultReqs = faultModel.requirementList;

            _scanLog.Add($"[SCAN] {allFaultReqs.Count} 个需求缺陷, {allFaultFields.Count} 个字段缺陷");

            // ── 加载各触发模型列表 ──
            var customModels = GetTriggerModelList<CustomFieldTriggerModel>(basicController, "_customTriggerModel");
            var twoFieldModels = GetTriggerModelList<TwoFieldTriggerModel>(basicController, "_triggerModel1");
            var fieldReqModels = GetTriggerModelList<FieldAndRequirementTriggerModel>(basicController, "_triggerModel2");
            var reqModels = GetTriggerModelList<RequirementTriggerModel>(basicController, "_triggerModel3");
            var globalModels = GetTriggerModelList<GlobalFieldTriggerModel>(basicController, "_triggerModel4");

            _scanLog.Add($"[SCAN] 规则: Custom={customModels.Count} TwoField={twoFieldModels.Count} " +
                         $"FieldReq={fieldReqModels.Count} Req={reqModels.Count} Global={globalModels.Count}");

            // ── 全局系统字段（游戏中 GlobalFieldTriggerModel.IsFieldTrigger 匹配的目标） ──
            var lm = SingletonEntity<InterviewManager>.Instance.levelManager;
            var globalFields = new List<IDocumentField>();
            if (lm.currentDateDocumentField != null) globalFields.Add(lm.currentDateDocumentField);
            if (lm.currentInterviewCharacterHeadImg != null) globalFields.Add(lm.currentInterviewCharacterHeadImg);

            BaseTriggerModel matched = null;
            string matchedTag = "";

            // ── 优先级 1: CustomFieldTriggerModel（游戏中最先检查） ──
            // 游戏中 TakeInspect 对每个字段/需求组合调用 Check(fields, requirement)
            if (matched == null && customModels.Count > 0)
            {
                _scanLog.Add("── Custom 检查 ──");

                // 先尝试 CheckCharacter（快速路径）
                foreach (var m in customModels)
                {
                    if (m.CheckCharacter(character))
                    {
                        matched = m;
                        matchedTag = $"CUSTOM_CHAR:{m.pointType}";
                        break;
                    }
                }

                // 再尝试 Check(fields, requirement) 的所有组合
                if (matched == null)
                {
                    // 构建所有要尝试的字段组合
                    var customFieldSets = new List<(IDocumentField[] fields, string tag)>();
                    customFieldSets.Add((new IDocumentField[0], "CUSTOM:∅"));
                    foreach (var f in allFaultFields)
                        customFieldSets.Add((new[] { f }, $"CUSTOM:{f.GetFieldEnum()}"));
                    for (int i = 0; i < allFaultFields.Count; i++)
                        for (int j = i + 1; j < allFaultFields.Count; j++)
                            customFieldSets.Add((new[] { allFaultFields[i], allFaultFields[j] },
                                $"CUSTOM:{allFaultFields[i].GetFieldEnum()}+{allFaultFields[j].GetFieldEnum()}"));
                    // 包含全局系统字段的组合
                    foreach (var gf in globalFields)
                        foreach (var ff in allFaultFields)
                            customFieldSets.Add((new[] { ff, gf }, $"CUSTOM:{ff.GetFieldEnum()}+Global"));

                    // 尝试每个字段组合 × 每个需求（含 null）
                    var reqsToTry = new List<BaseRequirement>(allFaultReqs);
                    reqsToTry.Add(null); // null requirement 也要尝试

                    foreach (var (fields, tag) in customFieldSets)
                    {
                        foreach (var req in reqsToTry)
                        {
                            foreach (var m in customModels)
                            {
                                if (m.Check(fields, req))
                                {
                                    matched = m;
                                    matchedTag = tag + (req != null ? $"+{req.type}" : "");
                                    break;
                                }
                            }
                            if (matched != null) break;
                        }
                        if (matched != null) break;
                    }
                }

                if (matched != null)
                    _scanLog.Add($"  ✓ 命中 {matchedTag}");
            }

            // ── 优先级 2: 双字段路径（GlobalField 优先于 TwoField） ──
            if (matched == null && allFaultFields.Count > 0)
            {
                _scanLog.Add("── 双字段检查 ──");

                // 构建字段对：缺陷字段两两组合 + 缺陷字段与全局系统字段的组合
                var fieldPairs = new List<(IDocumentField f1, IDocumentField f2, string tag)>();

                // 缺陷字段两两组合
                for (int i = 0; i < allFaultFields.Count; i++)
                {
                    for (int j = i + 1; j < allFaultFields.Count; j++)
                    {
                        var tag = $"F×F:{allFaultFields[i].GetFieldEnum()}+{allFaultFields[j].GetFieldEnum()}";
                        fieldPairs.Add((allFaultFields[i], allFaultFields[j], tag));
                    }
                }

                // 缺陷字段 × 全局系统字段
                foreach (var gf in globalFields)
                {
                    foreach (var ff in allFaultFields)
                    {
                        var tag = $"F×G:{ff.GetFieldEnum()}+Global";
                        fieldPairs.Add((ff, gf, tag));
                    }
                }

                foreach (var (f1, f2, tag) in fieldPairs)
                {
                    // 至少一个字段是 fault（游戏的 TakdInspect(field1, field2) 要求）
                    if (!f1.IsFault && !f2.IsFault) continue;

                    // GlobalField 优先检查
                    foreach (var gm in globalModels)
                    {
                        if (gm.IsFieldTrigger(f1) || gm.IsFieldTrigger(f2))
                        {
                            _scanLog.Add($"  ✓ 命中 GlobalField: {tag}");
                            matched = gm;
                            matchedTag = tag;
                            break;
                        }
                    }
                    if (matched != null) break;

                    // TwoField 检查（两个字段类型必须相同）
                    if (f1.GetType() == f2.GetType())
                    {
                        foreach (var tf in twoFieldModels)
                        {
                            if (tf.fieldType == f1.GetType())
                            {
                                _scanLog.Add($"  ✓ 命中 TwoField: {tag}");
                                matched = tf;
                                matchedTag = tag;
                                break;
                            }
                        }
                    }
                    if (matched != null) break;
                }
            }

            // ── 优先级 3: 字段 + 需求路径（GlobalField 优先于 FieldAndRequirement） ──
            if (matched == null && allFaultFields.Count > 0 && allFaultReqs.Count > 0)
            {
                _scanLog.Add("── 字段+需求检查 ──");

                // 包含缺陷字段和全局系统字段
                var searchFields = new List<IDocumentField>(allFaultFields);
                searchFields.AddRange(globalFields);

                foreach (var field in searchFields)
                {
                    foreach (var req in allFaultReqs)
                    {
                        var tag = $"F+R:{field.GetFieldEnum()}+{req.type}";
                        _scanLog.Add($"  ▸ {tag}");

                        // GlobalField 优先检查
                        foreach (var gm in globalModels)
                        {
                            if (gm.requirement != null && gm.IsFieldTrigger(field) && gm.requirement == req)
                            {
                                _scanLog.Add($"  ✓ 命中 GlobalField: {tag}");
                                matched = gm;
                                matchedTag = tag;
                                break;
                            }
                        }
                        if (matched != null) break;

                        // FieldAndRequirement 检查
                        foreach (var fr in fieldReqModels)
                        {
                            if (fr.fieldType == field.GetType() && fr.requirement == req)
                            {
                                _scanLog.Add($"  ✓ 命中 FieldReq: {tag}");
                                matched = fr;
                                matchedTag = tag;
                                break;
                            }
                        }
                        if (matched != null) break;
                    }
                    if (matched != null) break;
                }
            }

            // ── 优先级 4: 纯需求路径 ──
            // 游戏的 GetWarningLabelId 只检查需求就返回匹配，说明 FieldAndRequirement 模型
            // 也可以仅通过需求匹配（不需要字段缺陷存在）。这里复刻该行为。
            if (matched == null && allFaultReqs.Count > 0)
            {
                _scanLog.Add("── 纯需求检查 ──");
                foreach (var req in allFaultReqs)
                {
                    var tag = $"REQ:{req.type}";
                    _scanLog.Add($"  ▸ {tag}");

                    // 先检查 RequirementTriggerModel
                    foreach (var rm in reqModels)
                    {
                        if (rm.requirement == req)
                        {
                            _scanLog.Add($"  ✓ 命中 Requirement: {tag}");
                            matched = rm;
                            matchedTag = tag;
                            break;
                        }
                    }
                    if (matched != null) break;

                    // 再检查 FieldAndRequirementTriggerModel（仅按需求匹配，与 GetWarningLabelId 一致）
                    foreach (var fr in fieldReqModels)
                    {
                        if (fr.requirement == req)
                        {
                            _scanLog.Add($"  ✓ 命中 FieldReq(仅需求): {tag}");
                            matched = fr;
                            matchedTag = tag;
                            break;
                        }
                    }
                    if (matched != null) break;

                    // 最后检查 GlobalFieldTriggerModel（仅按需求匹配）
                    foreach (var gm in globalModels)
                    {
                        if (gm.requirement == req)
                        {
                            _scanLog.Add($"  ✓ 命中 GlobalReq(仅需求): {tag}");
                            matched = gm;
                            matchedTag = tag;
                            break;
                        }
                    }
                    if (matched != null) break;
                }
            }

            // ── 结果处理 ──
            if (matched != null)
            {
                if (TriggerInspectPointViaReflection(basicController, matched)
                    && inspectManager.waittingTriggerModel != null)
                {
                    var pt = inspectManager.waittingTriggerModel.pointType;
                    _scanResult = $"✓ 感谢信已生成 [{pt}]，等待动画...";
                    _scanLog.Add($"[DONE] 检查点 {pt} 已触发 ({matchedTag})");

                    _pendingActivation = true;
                    _activationTimer = _activationDelay;
                }
                else
                {
                    _scanResult = "⚠ 匹配成功但触发失败";
                    _scanLog.Add("[ERROR] TriggerInspectPoint 调用失败或 waittingTriggerModel 为空");
                }
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
