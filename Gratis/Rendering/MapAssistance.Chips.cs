using System;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance
{
    public AetheryteChoice? Chip { get; private set; }
    public Vector2 ChipButtonCenter { get; private set; }
    public string ChipMessage { get; private set; } = "";
    private string? chipId;
    private bool chipConfirm;

    private void DrawAetheryteChip(IMapWindowHost host)
    {
        Chip = null;
        var request = ContextTarget(host) ?? StoryTarget(host) ?? (host.Renderer.Spotlight.Active ? host.Renderer.Spotlight.Request : null);
        if (!host.Config.AetheryteChips || request is not { } target ||
            (!AetheryteSelection.Useful(target.MapId, target.Target, places.Context.MapId, places.Context.Player) && !IsContextTarget(target))) return;
        Chip = AetheryteSelection.Nearest(places.Entries, target.MapId, target.Target, places.Access,
            provider.ActiveAetheryte, available && host.Config.TravelActions);
        if (Chip is not { } choice) return;
        if (chipId != choice.Destination.Id) { chipId = choice.Destination.Id; ChipMessage = ""; chipConfirm = false; }
        var s = MapDrawContext.Current.GlobalScale;
        var size = new Vector2(MathF.Min(430 * s, ImGui.GetWindowWidth() * (host.Config.MapCircle ? .68f : .85f)),
            (ChipMessage.Length > 0 && !chipConfirm ? 104 : 84) * s);
        if (size.X < 230 * s) return;
        var position = ConstrainPanel(ImGui.GetWindowPos() + new Vector2((ImGui.GetWindowWidth() - size.X) / 2,
            (provider.IsBusy ? 142 : 94) * s), size);
        Panel("##nearest-aetheryte", position, size, () => {
            using (MapDrawContext.Current.GetChromeFont(11 * s, ChromeFontRole.Chip)?.Push())
                ImGui.TextColored(GratisTheme.Secondary, "NEAREST AETHERYTE");
            using (MapDrawContext.Current.GetChromeFont(14 * s, ChromeFontRole.Title)?.Push())
                ImGui.TextUnformatted(choice.Destination.Name + " (attuned)");
            var cost = places.Access(choice.Destination).Cost;
            if (ActionButton(chipConfirm ? "Confirm teleport" : choice.Aethernet ? "Travel here" : "Teleport", GratisIcons.Compass))
                RequestChip(choice, chipConfirm);
            ChipButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            ImGui.SameLine(); ImGui.TextDisabled(choice.Aethernet ? "Aethernet" : cost is { } gil ? $"{gil:N0} gil" : "Cost unavailable");
            if (ChipMessage.Length > 0 && !chipConfirm) ImGui.TextDisabled(ChipMessage);
        });
    }

    public string RequestChip(AetheryteChoice choice, bool confirmed = false)
    {
        places.Refresh();
        if (!canTravel() || !places.Access(choice.Destination).Attuned || provider.IsBusy)
            return ChipMessage = "Unable to teleport now.";
        ChipMessage = choice.Aethernet ? Request(choice.Destination, confirmed) : places.Teleport(choice.Destination, confirmed);
        chipConfirm = ChipMessage.StartsWith("Confirm ", StringComparison.Ordinal);
        return ChipMessage;
    }
}
