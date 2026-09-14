using System;
using System.Linq;
using UnityEngine;

namespace ZCJ.Shiploader
{
    [DisallowMultipleComponent]
    public sealed class ShiploaderDemoUI : MonoBehaviour
    {
        [SerializeField] private ShiploaderRigController rig;
        [SerializeField] private ShiploaderEffectsController effects;
        [SerializeField] private ShiploaderOrbitCamera orbitCamera;
        [SerializeField] private ShiploaderRemoteCoordinator remoteCoordinator;

        private HatchCoverController[] hatchCovers = Array.Empty<HatchCoverController>();
        private int selectedHatch;
        private Vector2 scrollPosition;
        private Vector2 remoteScrollPosition;
        private float cancelConfirmUntil;
        private float resetConfirmUntil;
        private Font runtimeFont;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle valueStyle;
        private bool controlsExpanded;
        private ShiploaderFleetController Fleet => GetComponent<ShiploaderFleetController>();
        public void ExpandControls() { controlsExpanded = true; }
        public void ToggleOperationPanel() { remoteCollapsed = !remoteCollapsed; }
        private bool detailsExpanded;
        private bool remoteCollapsed;
        private bool lastWalking, savedControlsExpanded, savedRemoteCollapsed;
        private GUISkin presentationSkin;
        private float PanelWidth => Mathf.Min(320f, Screen.width * 0.3f);
        private Rect ToolbarRect => new(16f, 16f, Mathf.Min(740f, Screen.width - PanelWidth - 48f), 154f);
        private Rect ControlsRect => new(16f, 178f, 330f, Mathf.Max(100f, Screen.height - 194f));
        private Rect RemoteRect => new(Screen.width - PanelWidth - 16f, 16f, PanelWidth,
            remoteCollapsed ? 48f : detailsExpanded ? Screen.height - 32f : Mathf.Min(500f, Screen.height - 32f));

        public void Configure(
            ShiploaderRigController rigController,
            ShiploaderEffectsController effectsController,
            ShiploaderOrbitCamera cameraController)
        {
            Configure(rigController, effectsController, cameraController, null);
        }

        public void Configure(
            ShiploaderRigController rigController,
            ShiploaderEffectsController effectsController,
            ShiploaderOrbitCamera cameraController,
            ShiploaderRemoteCoordinator coordinator)
        {
            rig = rigController;
            effects = effectsController;
            orbitCamera = cameraController;
            remoteCoordinator = coordinator;
            orbitCamera?.BindWorkingRig(rig);
            RefreshHatches();
        }

        private void Awake()
        {
            orbitCamera?.BindWorkingRig(rig);
            RefreshHatches();
        }

        private void OnEnable()
        {
            RefreshHatches();
        }

        private void OnGUI()
        {
            if (rig == null || rig.Config == null)
            {
                return;
            }

            GUISkin originalSkin = GUI.skin;
            EnsureStyles();
            bool walking = orbitCamera != null && orbitCamera.Walkthrough != null && orbitCamera.Walkthrough.Active;
            if (walking && !lastWalking)
            {
                savedControlsExpanded = controlsExpanded; savedRemoteCollapsed = remoteCollapsed;
                controlsExpanded = false; remoteCollapsed = true;
            }
            else if (!walking && lastWalking)
            {
                controlsExpanded = savedControlsExpanded; remoteCollapsed = savedRemoteCollapsed;
            }
            lastWalking = walking;
            DrawViewToolbar();
            orbitCamera?.SetInputBlocks(ToolbarRect, controlsExpanded ? ControlsRect : Rect.zero,
                remoteCoordinator != null ? RemoteRect : Rect.zero);
            if (controlsExpanded) DrawManualPanel();
            else if (Fleet != null) Array.Clear(Fleet.UiInput, 0, 5);
            DrawRemotePanel();
            GUI.skin = originalSkin;
        }

        private void DrawViewToolbar()
        {
            GUILayout.BeginArea(ToolbarRect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            CameraButton("港口总览", ShiploaderCameraPreset.Perspective);
            CameraButton("港区俯视", ShiploaderCameraPreset.PortTop);
            CameraButton("作业跟随", ShiploaderCameraPreset.Work);
            CameraButton("落料近景", ShiploaderCameraPreset.Discharge);
            var walk = orbitCamera != null ? orbitCamera.Walkthrough : null;
            if (GUILayout.Button(walk != null && walk.Active ? "退出漫游" : "手柄漫游", GUILayout.Height(28f)))
                walk?.Toggle();
            if (GUILayout.Button(controlsExpanded ? "收起控制" : "设备控制", GUILayout.Height(28f)))
            { if (Fleet != null && !controlsExpanded) Fleet.Manual(); else controlsExpanded = !controlsExpanded; }
            GUILayout.EndHorizontal();
            if (walk != null && walk.Active)
            {
                GUILayout.Label(walk.HasGamepad ? "第一人称漫游 · Xbox 手柄已连接" : "手柄未连接 · 可使用 WASD 行走", sectionStyle);
                GUILayout.Label("左摇杆：行走　右摇杆：转向　RT：加速");
                GUILayout.Label(walk.ClimbHint);
                GUILayout.Label("Y：返回起点　B / Start：退出　请保持 Game 窗口焦点");
                GUILayout.EndArea();
                return;
            }
            if (Fleet != null) {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ LB", GUILayout.Width(65))) Fleet.Next(-1);
                GUILayout.Label(Fleet.SelectionText, sectionStyle);
                if (GUILayout.Button("RB ▶", GUILayout.Width(65))) Fleet.Next(1);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (int.Parse(remoteCoordinator.MachineId) <= 3) {
                    if (GUILayout.Button("← 左侧船")) _ = remoteCoordinator.SelectVesselAsync("Vessel-R");
                    if (GUILayout.Button("右侧船 →")) _ = remoteCoordinator.SelectVesselAsync("Vessel-L");
                }
                GUILayout.Label("A：装船/继续　B：暂停　X：手动　Y：视角");
                GUILayout.EndHorizontal();
                GUILayout.Label("Menu：作业面板　F8 / 点击手柄漫游：进入漫游");
                GUILayout.EndArea(); return;
            }
            GUILayout.Label("01 号装船机 · " + (orbitCamera != null && !orbitCamera.Following
                ? "自由观察（点击作业跟随可返回）" : "当前观察设备"), sectionStyle);
            GUILayout.Label("右键拖动环绕 · 滚轮缩放 · 详情按需展开");
            GUILayout.Label(!string.IsNullOrEmpty(walk?.Hint) ? walk.Hint : "Start / F8：进入手柄漫游（Game 窗口）");
            GUILayout.EndArea();
        }

        private void DrawManualPanel()
        {
            Rect panel = ControlsRect;
            GUILayout.BeginArea(panel, GUI.skin.box);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("黄骅 SL15 装船机", titleStyle);
            GUILayout.Label("Unity 精细化可运动模型 · 1 unit = 1 m");
            GUILayout.Space(8f);

            GUILayout.Label("固定视角", sectionStyle);
            GUILayout.BeginHorizontal();
            CameraButton("透视", ShiploaderCameraPreset.Perspective);
            CameraButton("主视", ShiploaderCameraPreset.Front);
            CameraButton("侧视", ShiploaderCameraPreset.Side);
            CameraButton("俯视", ShiploaderCameraPreset.Top);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("五轴控制", sectionStyle);
            if (Fleet != null) {
                bool enabled = GUI.enabled;
                if (!remoteCoordinator.IsManual && GUILayout.Button("暂停自动任务并进入手动（X）")) Fleet.Manual();
                GUI.enabled = enabled && remoteCoordinator.IsManual;
                var pose = rig.Pose;
                string[] names = { "大车行走", "臂架回转", "臂架俯仰", "臂架伸缩", "溜筒回转" };
                float[] values = { pose.travel, pose.slew, pose.boomLuff, pose.boomExtension, pose.chuteRotate };
                for (int i = 0; i < 5; i++) {
                    GUILayout.Label($"{names[i]}：{values[i]:F2}");
                    GUILayout.BeginHorizontal();
                    bool minus = GUILayout.RepeatButton("− 按住", GUILayout.Height(28));
                    bool plus = GUILayout.RepeatButton("+ 按住", GUILayout.Height(28));
                    Fleet.UiInput[i] = (plus ? 1 : 0) - (minus ? 1 : 0);
                    GUILayout.EndHorizontal();
                }
                GUI.enabled = enabled;
                GUILayout.Label("左摇杆：行走 / 回转\n右摇杆上下：俯仰\nLT / RT：缩回 / 伸出\n十字键上下：溜筒回转\n松开停止；切换设备后摇杆回中再操作。");
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }
            bool manualLocked = remoteCoordinator != null && remoteCoordinator.ManualControlLocked;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !manualLocked;
            ShiploaderPose nextPose = rig.Pose;
            SL15ModelConfig config = rig.Config;
            nextPose.travel = DrawAxis("大车行走", nextPose.travel, config.travelRange, "m");
            nextPose.slew = DrawAxis("臂架回转", nextPose.slew, config.slewRange, "°");
            nextPose.boomLuff = DrawAxis("臂架俯仰", nextPose.boomLuff, config.luffRange, "°");
            nextPose.boomExtension = DrawAxis("臂架伸缩", nextPose.boomExtension, config.extensionRange, "m");
            nextPose.chuteRotate = DrawAxis("溜筒回转", nextPose.chuteRotate, config.chuteRotateRange, "°");
            if (!nextPose.Approximately(rig.Pose))
            {
                rig.ApplyPose(nextPose);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("复位姿态", GUILayout.Height(28f)))
            {
                rig.ResetPose();
            }
            GUILayout.Label($"关键点误差 {rig.GetMaximumKeyPointError():F4} m", valueStyle);
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
            if (manualLocked)
            {
                GUILayout.Label("一键装船任务活动中，五轴手动控制已锁定。", valueStyle);
            }

            GUILayout.Space(8f);
            GUILayout.Label("动态演示", sectionStyle);
            if (effects != null)
            {
                GUI.enabled = previousEnabled && !manualLocked;
                bool belt = GUILayout.Toggle(effects.BeltRunning, "皮带运行");
                bool coal = GUILayout.Toggle(effects.CoalFlowEnabled, "煤流粒子");
                if (belt != effects.BeltRunning)
                {
                    effects.SetBeltRunning(belt);
                }
                if (coal != effects.CoalFlowEnabled)
                {
                    effects.SetCoalFlow(coal);
                }
                GUI.enabled = previousEnabled;
            }

            DrawHatchControls();
            GUILayout.Space(8f);
            GUILayout.Label("说明：右键拖动旋转透视相机，滚轮缩放。模型为仿真视觉模型，不用于制造或真实设备控制。", GUI.skin.label);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

        }

        private float DrawAxis(string label, float value, AxisRange range, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(88f));
            GUILayout.Label($"{value:F2} {unit}", valueStyle, GUILayout.Width(82f));
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(value, range.min, range.max, GUILayout.Height(18f));
        }

        private void CameraButton(string label, ShiploaderCameraPreset cameraPreset)
        {
            bool active = orbitCamera != null && orbitCamera.Preset == cameraPreset;
            Color previous = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(1f, 0.55f, 0.12f);
            }

            if (GUILayout.Button(label, GUILayout.Height(28f)) && orbitCamera != null)
            {
                orbitCamera.SetPreset(cameraPreset);
            }
            GUI.backgroundColor = previous;
        }

        private void DrawHatchControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("舱盖控制", sectionStyle);
            if (hatchCovers.Length == 0)
            {
                GUILayout.Label("未找到舱盖控制器");
                return;
            }

            selectedHatch = Mathf.Clamp(selectedHatch, 0, hatchCovers.Length - 1);
            HatchCoverController hatch = hatchCovers[selectedHatch];
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(42f)))
            {
                selectedHatch = (selectedHatch - 1 + hatchCovers.Length) % hatchCovers.Length;
            }
            GUILayout.Label(hatch.DisplayName, valueStyle);
            if (GUILayout.Button("▶", GUILayout.Width(42f)))
            {
                selectedHatch = (selectedHatch + 1) % hatchCovers.Length;
            }
            GUILayout.EndHorizontal();

            hatch = hatchCovers[selectedHatch];
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && (remoteCoordinator == null || !remoteCoordinator.ManualControlLocked);
            if (GUILayout.Button(hatch.IsOpen ? "关闭当前舱盖" : "打开当前舱盖", GUILayout.Height(28f)))
            {
                if (remoteCoordinator != null)
                {
                    _ = remoteCoordinator.SetHatchOpenAsync(hatch, !hatch.IsOpen);
                }
                else
                {
                    hatch.Toggle();
                }
            }
            GUI.enabled = previousEnabled;
        }

        private void DrawRemotePanel()
        {
            if (remoteCoordinator == null)
            {
                return;
            }

            Rect panel = RemoteRect;
            GUILayout.BeginArea(panel, GUI.skin.box);
            if (GUILayout.Button(remoteCollapsed ? "展开作业面板" : "收起作业面板", GUILayout.Height(28f)))
                remoteCollapsed = !remoteCollapsed;
            if (remoteCollapsed) { GUILayout.EndArea(); return; }
            remoteScrollPosition = GUILayout.BeginScrollView(remoteScrollPosition);
            GUILayout.Label("一键装船", titleStyle);

            Color previousColor = GUI.color;
            GUI.color = remoteCoordinator.IsConnected
                ? new Color(0.45f, 1f, 0.65f)
                : new Color(1f, 0.55f, 0.4f);
            GUILayout.Label($"仿真服务：{remoteCoordinator.ConnectionText}", sectionStyle);
            GUI.color = previousColor;

            LoadingPlanEnvelopeDto envelope = remoteCoordinator.LoadingPlan;
            if (envelope?.plan != null)
            {
                string valid = envelope.validation?.valid == true ? "校验通过" : "校验失败";
                GUILayout.Label($"{envelope.plan.vesselName} · {envelope.plan.name}");
                GUILayout.Label($"计划：{envelope.plan.totalTargetKg:N0} kg · {valid}");
                if (detailsExpanded) GUILayout.Label("固定演示倍率：机械 12× · 装载 600× · 排水 14s");
            }
            else
            {
                GUILayout.Label("正在读取默认配载计划……");
            }

            LoadingTaskDto task = remoteCoordinator.CurrentTask;
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawActionButton(
                "一键装船",
                remoteCoordinator.IsConnected && !remoteCoordinator.IsBusy &&
                envelope?.validation?.valid == true && task == null && !remoteCoordinator.NeedsSide,
                () => _ = remoteCoordinator.StartLoadingTaskAsync());
            DrawActionButton(
                "暂停",
                task != null && task.LocksManualControl && task.status != "paused" && !remoteCoordinator.IsBusy,
                () => _ = remoteCoordinator.PauseLoadingTaskAsync());
            DrawActionButton(
                "恢复",
                task?.status == "paused" && !remoteCoordinator.IsBusy,
                () => _ = remoteCoordinator.ResumeLoadingTaskAsync());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            string cancelLabel = Time.realtimeSinceStartup <= cancelConfirmUntil ? "再次点击终止" : "终止";
            DrawActionButton(
                cancelLabel,
                task != null && task.LocksManualControl && !remoteCoordinator.IsBusy,
                () =>
                {
                    if (Time.realtimeSinceStartup <= cancelConfirmUntil)
                    {
                        cancelConfirmUntil = 0f;
                        _ = remoteCoordinator.CancelLoadingTaskAsync();
                    }
                    else
                    {
                        cancelConfirmUntil = Time.realtimeSinceStartup + 5f;
                    }
                });
            string resetLabel = Time.realtimeSinceStartup <= resetConfirmUntil ? "再次点击重置" : "重置";
            DrawActionButton(
                resetLabel,
                task != null && task.IsTerminal && !remoteCoordinator.IsBusy,
                () =>
                {
                    if (Time.realtimeSinceStartup <= resetConfirmUntil)
                    {
                        resetConfirmUntil = 0f;
                        _ = remoteCoordinator.ResetLoadingTaskAsync();
                    }
                    else
                    {
                        resetConfirmUntil = Time.realtimeSinceStartup + 5f;
                    }
                });
            GUILayout.EndHorizontal();

            DrawTaskProgress(task);
            if (!string.IsNullOrEmpty(remoteCoordinator.ErrorMessage))
            {
                GUILayout.Space(6f);
                GUILayout.Label(
                    $"错误 [{remoteCoordinator.ErrorCode}]\n{remoteCoordinator.ErrorMessage}",
                    GUI.skin.box);
            }
            GUILayout.Space(6f);
            if (detailsExpanded) GUILayout.Label("仿真用途：Unity 仅展示 FastAPI 真值，不用于真实 PLC、稳性或装船决策。",
                GUI.skin.label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTaskProgress(LoadingTaskDto task)
        {
            GUILayout.Space(10f);
            GUILayout.Label("任务进度", sectionStyle);
            if (task == null)
            {
                GUILayout.Label("当前无任务，五个目标舱为 0%。");
                return;
            }

            GUILayout.Label($"状态：{TranslateStatus(task.status)} · {TranslatePhase(task.currentPhase)}");
            var activeBatch = task.batchProgress?.FirstOrDefault(item => item.batchNo == task.currentBatchNo);
            GUILayout.Label($"当前船舱：{activeBatch?.holdNo ?? "待分配"}");
            GUILayout.Label($"轮次/批次：{task.currentRoundNo?.ToString() ?? "-"} / {task.currentBatchNo?.ToString() ?? "-"}");
            GUILayout.Label($"总吨位：{task.loadedTotalKg:N0} / {task.targetTotalKg:N0} kg");
            DrawProgressBar(task.Progress, $"{task.Progress:P1}");
            if (task.status == "waiting_drainage")
            {
                GUILayout.Label($"排水倒计时：{task.drainageRemainingDemoS:F1} s");
            }
            if (!string.IsNullOrEmpty(task.pauseReason))
            {
                GUILayout.Label($"暂停原因：{task.pauseReason}");
            }

            if (GUILayout.Button(detailsExpanded ? "收起各舱进度与时间线" : "展开各舱进度与时间线", GUILayout.Height(28f)))
                detailsExpanded = !detailsExpanded;
            if (!string.IsNullOrEmpty(task.failureCode) || !string.IsNullOrEmpty(task.failureMessage))
                GUILayout.Label($"任务错误 [{task.failureCode}] {task.failureMessage}", GUI.skin.box);
            if (!detailsExpanded) return;
            GUILayout.Label("五舱进度", sectionStyle);
            foreach (LoadingHoldProgressDto hold in task.holdProgress ?? Enumerable.Empty<LoadingHoldProgressDto>())
            {
                GUILayout.Label($"{hold.holdNo} ({hold.holdId})  {hold.loadedKg:N0}/{hold.targetKg:N0} kg");
                DrawProgressBar(hold.Progress, $"{hold.Progress:P0}");
            }

            GUILayout.Label("12 批时间线", sectionStyle);
            foreach (LoadingBatchProgressDto batch in task.batchProgress ?? Enumerable.Empty<LoadingBatchProgressDto>())
            {
                bool current = batch.batchNo == task.currentBatchNo;
                string marker = current ? "▶" : batch.status == "completed" ? "●" : "○";
                GUILayout.Label($"{marker} 第 {batch.batchNo:00} 批 · 第 {batch.roundNo} 轮 · {batch.holdNo} · {TranslateStatus(batch.status)}");
            }

            if (!string.IsNullOrEmpty(task.failureCode) || !string.IsNullOrEmpty(task.failureMessage))
            {
                GUILayout.Label($"任务错误 [{task.failureCode}] {task.failureMessage}", GUI.skin.box);
            }
        }

        private static void DrawProgressBar(float progress, string label)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);
            Rect fill = new(rect.x + 2f, rect.y + 2f,
                Mathf.Max(0f, (rect.width - 4f) * Mathf.Clamp01(progress)), rect.height - 4f);
            Color previous = GUI.color;
            GUI.color = new Color(0.96f, 0.46f, 0.08f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, label, GUI.skin.label);
        }

        private static void DrawActionButton(string label, bool enabled, Action action)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            if (GUILayout.Button(label, GUILayout.Height(28f)))
            {
                action();
            }
            GUI.enabled = previous;
        }

        private static string TranslateStatus(string status)
        {
            return status switch
            {
                "prechecking" => "预检",
                "positioning" => "定位",
                "loading" => "装载",
                "waiting_drainage" => "排水等待",
                "paused" => "已暂停",
                "verifying" => "校验",
                "completed" => "已完成",
                "cancelled" => "已终止",
                "failed" => "失败",
                "pending" => "待执行",
                _ => status ?? "-",
            };
        }

        private static string TranslatePhase(string phase)
        {
            return phase switch
            {
                "path_target_luff" => "调整臂架高度",
                "path_target_slew" => "回转至目标船舱",
                "path_target_extension" => "调整臂架伸缩",
                "path_travel" => "大车行走定位",
                "path_safe_luff" => "抬臂至安全高度",
                "path_safe_retract" => "收回伸缩臂",
                "path_safe_slew" => "安全回转",
                "path_final_approach" => "对准落料位置",
                "path_planning" => "规划定位路径",
                "belt_starting" => "启动输送带",
                "loading_cargo" => "正在装载",
                "drainage_wait" => "等待排水",
                "final_verification" => "检查装载结果",
                "validate" => "检查作业计划",
                "precheck_failed" => "作业预检未通过",
                "paused" => "作业已暂停",
                "completed" => "作业已完成",
                "cancelled" => "作业已终止",
                "failed" => "作业失败",
                "loading" => "正在装载",
                "waiting_drainage" => "等待排水",
                null or "" => "等待作业",
                _ => "执行作业步骤",
            };
        }

        private void RefreshHatches()
        {
            hatchCovers = (remoteCoordinator != null && remoteCoordinator.IsFleet ? remoteCoordinator.Covers : FindObjectsByType<HatchCoverController>(FindObjectsSortMode.None))
                .OrderBy(item => item.VesselId)
                .ThenBy(item => item.HoldId)
                .ToArray();
            selectedHatch = Mathf.Clamp(selectedHatch, 0, Mathf.Max(0, hatchCovers.Length - 1));
        }

        private void CreateStyles()
        {
            if (runtimeFont == null)
            {
                runtimeFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 16);
                if (runtimeFont == null)
                {
                    runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = runtimeFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.97f, 1f) },
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                font = runtimeFont,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.9f, 1f) },
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                font = runtimeFont,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) },
            };
            GUI.skin.font = runtimeFont;
        }

        private void EnsureStyles()
        {
            if (presentationSkin == null)
            {
                presentationSkin = Instantiate(GUI.skin);
                presentationSkin.label.normal.textColor = new Color(0.9f, 0.94f, 1f);
                presentationSkin.label.wordWrap = true;
                var background = new Texture2D(1, 1);
                background.SetPixel(0, 0, new Color(0.035f, 0.065f, 0.1f, 0.94f));
                background.Apply();
                presentationSkin.box.normal.background = background;
                presentationSkin.box.padding = new RectOffset(12, 12, 10, 10);
            }
            GUI.skin = presentationSkin;
            if (titleStyle == null || sectionStyle == null || valueStyle == null)
            {
                CreateStyles();
            }
            GUI.skin.font = runtimeFont;
        }

        private void OnDestroy()
        {
            if (presentationSkin != null)
            {
                Destroy(presentationSkin.box.normal.background);
                Destroy(presentationSkin);
            }
            if (runtimeFont != null && runtimeFont.name != "LegacyRuntime") Destroy(runtimeFont);
        }
    }
}
