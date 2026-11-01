#if !GRATIS_PREVIEW
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Gratis.Compat.CommandManager;

namespace Gratis.Rendering;

internal sealed class LensCommand : global::System.IDisposable {
    [PluginService] private IChatGui ChatGui { get; set; } = null!;
    private readonly CommandHandler command;
    public LensCommand() {
        Service.PluginInterface.Inject(this);
        command = new CommandHandler {
            ActivationPath = "/lens", Delegate = args => {
                var config = System.SystemConfig;
                var request = string.Join(' ', args).Trim();
                if (string.Equals(request, "cycle", global::System.StringComparison.OrdinalIgnoreCase)) {
                    Lenses.Cycle(config);
                    Data.SystemConfig.Save();
                }
                else if (request.Length > 0) {
                    var lens = Lenses.Find(config, request);
                    if (string.Equals(lens.Name, request, global::System.StringComparison.Ordinal)) {
                        config.ActiveLens = lens.Name;
                        Data.SystemConfig.Save();
                    }
                }
                var model = MapDrawContext.Current.Visibility;
                ChatGui.Print($"[Gratis] lens={config.ActiveLens}; effective={model.EffectiveLens}; "
                    + $"auto={config.LensAuto}; tint={config.LensTintsMap}; peek={config.PeekKey} "
                    + $"({model.Peek}); cycleKey={config.LensCycleKey}; "
                    + $"custom={config.CustomLenses?.Count ?? 0}. Priority markers can only be hidden "
                    + "by an explicit category mute.");
                ChatGui.Print("[Gratis] " + DutyDiagnostics());
                for (var i = 0; i < MarkerVisibility.GroupCount; i++)
                    ChatGui.Print($"[Gratis] {MarkerVisibility.Names[i]}: {model.State((MarkerGroup)i)}, "
                        + $"count={model.Counts[i]}, filter={model.Overrides[i]?.ToString() ?? "lens"}");
            }
        };
        System.CommandManager.RegisterCommand(command);
    }

    internal static string DutyDiagnostics() {
        var condition = Service.Condition;
        return $"territory={Service.ClientState.TerritoryType} inDutyForLens={MapDrawContext.Current.LensInDuty} "
            + $"boundByDuty={condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]} "
            + $"boundByDuty56={condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56]} "
            + $"boundByDuty95={condition[global::Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty95]} "
            + $"pvp={Service.ClientState.IsPvP} sizeFactor={MapDrawContext.Current.MapSizeFactor:0.##} "
            + $"nativeMarkers/event/gathering={MarkerCounts()}";
    }

    private static unsafe string MarkerCounts() {
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        return agent == null ? "n/a"
            : $"{agent->MapMarkerCount}/{agent->EventMarkers.Count}/{agent->MiniMapGatheringMarkers.Length}";
    }

    public void Dispose() => System.CommandManager.RemoveCommand(command);
}
#endif