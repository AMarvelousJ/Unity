using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZCJ.Shiploader
{
    [Serializable]
    public sealed class ShiploaderAxisStateDto
    {
        [JsonProperty("current")] public float current;
        [JsonProperty("target")] public float target;
        [JsonProperty("running")] public bool running;
    }

    [Serializable]
    public sealed class ShiploaderBeltStateDto
    {
        [JsonProperty("running")] public bool running;
        [JsonProperty("speed_mps")] public float speedMps;
    }

    [Serializable]
    public sealed class LoadingBatchProgressDto
    {
        [JsonProperty("batch_no")] public int batchNo;
        [JsonProperty("round_no")] public int roundNo;
        [JsonProperty("hold_no")] public string holdNo;
        [JsonProperty("hold_id")] public string holdId;
        [JsonProperty("target_kg")] public long targetKg;
        [JsonProperty("loaded_kg")] public long loadedKg;
        [JsonProperty("status")] public string status;
        [JsonProperty("column")] public int column;
        [JsonProperty("row")] public int row;

        [JsonIgnore]
        public float Progress => targetKg <= 0 ? 0f : Math.Min(1f, loadedKg / (float)targetKg);
    }

    [Serializable]
    public sealed class LoadingHoldProgressDto
    {
        [JsonProperty("hold_no")] public string holdNo;
        [JsonProperty("hold_id")] public string holdId;
        [JsonProperty("target_kg")] public long targetKg;
        [JsonProperty("loaded_kg")] public long loadedKg;

        [JsonIgnore]
        public float Progress => targetKg <= 0 ? 0f : Math.Min(1f, loadedKg / (float)targetKg);
    }

    [Serializable]
    public sealed class LoadingTaskDto
    {
        [JsonProperty("task_id")] public string taskId;
        [JsonProperty("plan_id")] public string planId;
        [JsonProperty("status")] public string status;
        [JsonProperty("mode")] public string mode;
        [JsonProperty("current_round_no")] public int? currentRoundNo;
        [JsonProperty("current_batch_no")] public int? currentBatchNo;
        [JsonProperty("current_phase")] public string currentPhase;
        [JsonProperty("loaded_total_kg")] public long loadedTotalKg;
        [JsonProperty("target_total_kg")] public long targetTotalKg;
        [JsonProperty("batch_progress")] public List<LoadingBatchProgressDto> batchProgress = new();
        [JsonProperty("hold_progress")] public List<LoadingHoldProgressDto> holdProgress = new();
        [JsonProperty("drainage_remaining_demo_s")] public float drainageRemainingDemoS;
        [JsonProperty("pause_reason")] public string pauseReason;
        [JsonProperty("failure_code")] public string failureCode;
        [JsonProperty("failure_message")] public string failureMessage;

        [JsonIgnore]
        public float Progress => targetTotalKg <= 0
            ? 0f
            : Math.Min(1f, loadedTotalKg / (float)targetTotalKg);

        [JsonIgnore]
        public bool IsTerminal => status is "completed" or "cancelled" or "failed";

        [JsonIgnore]
        public bool LocksManualControl => !string.IsNullOrEmpty(taskId) && !IsTerminal;
    }

    [Serializable]
    public sealed class SimulationSnapshotDto
    {
        [JsonProperty("fleet_configured")] public bool? fleetConfigured;
        [JsonProperty("selected_vessel")] public string selectedVessel;
        [JsonProperty("sim_time")] public double simTime;
        [JsonProperty("sim_paused")] public bool simPaused;
        [JsonProperty("axis_states")] public Dictionary<string, ShiploaderAxisStateDto> axisStates = new();
        [JsonProperty("belt_state")] public ShiploaderBeltStateDto beltState = new();
        [JsonProperty("loading_task")] public LoadingTaskDto loadingTask;

        public bool TryGetPose(out ShiploaderPose pose)
        {
            pose = default;
            if (axisStates == null ||
                !axisStates.TryGetValue("travel", out ShiploaderAxisStateDto travel) ||
                !axisStates.TryGetValue("slew", out ShiploaderAxisStateDto slew) ||
                !axisStates.TryGetValue("boom_luff", out ShiploaderAxisStateDto luff) ||
                !axisStates.TryGetValue("boom_extension", out ShiploaderAxisStateDto extension) ||
                !axisStates.TryGetValue("chute_rotate", out ShiploaderAxisStateDto chute))
            {
                return false;
            }

            pose = new ShiploaderPose(
                travel.current,
                slew.current,
                luff.current,
                extension.current,
                chute.current);
            return true;
        }
    }

    [Serializable]
    public sealed class LoadingPlanDto
    {
        [JsonProperty("plan_id")] public string planId;
        [JsonProperty("name")] public string name;
        [JsonProperty("vessel_id")] public string vesselId;
        [JsonProperty("vessel_name")] public string vesselName;
        [JsonProperty("total_target_kg")] public long totalTargetKg;
    }

    [Serializable]
    public sealed class LoadingPlanValidationDto
    {
        [JsonProperty("valid")] public bool valid;
        [JsonProperty("calculated_total_kg")] public long calculatedTotalKg;
        [JsonProperty("errors")] public List<LoadingValidationIssueDto> errors = new();
    }

    [Serializable]
    public sealed class LoadingValidationIssueDto
    {
        [JsonProperty("code")] public string code;
        [JsonProperty("message")] public string message;
    }

    [Serializable]
    public sealed class LoadingPlanEnvelopeDto
    {
        [JsonProperty("plan")] public LoadingPlanDto plan;
        [JsonProperty("validation")] public LoadingPlanValidationDto validation;
    }

    [Serializable]
    public sealed class SceneHoldDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("cover_state")] public string coverState;
    }

    [Serializable]
    public sealed class SceneVesselDto
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("holds")] public List<SceneHoldDto> holds = new();
    }

    [Serializable]
    public sealed class SceneConfigDto
    {
        [JsonProperty("revision")] public int revision;
        [JsonProperty("vessels")] public List<SceneVesselDto> vessels = new();
    }

    [Serializable]
    public sealed class LoadingResetResponseDto
    {
        [JsonProperty("task")] public LoadingTaskDto task;
        [JsonProperty("snapshot")] public SimulationSnapshotDto snapshot;
    }

    [Serializable]
    public sealed class SimulationClockStatusDto
    {
        [JsonProperty("step_s")] public float stepS;
        [JsonProperty("active_clients")] public int activeClients;
        [JsonProperty("running")] public bool running;
        [JsonProperty("sim_paused")] public bool simPaused;
    }
}
