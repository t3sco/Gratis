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
    public Vector2 ChipDismissButtonCenter { get; private set; }
    public Vector4 ChipBounds { get; private set; }
    private SpotlightRequest? dismissedTarget;
    private string? chipId;
    private bool chipConfirm, chipRequiresTravelActions;

    private void DrawAetheryteChip(IMapWindowHost host)
    {
        Chip = null; ChipBounds = Vector4.Zero;
        var request = ContextTarget(host) ?? StoryTarget(host) ?? (host.Renderer.Spotlight.Active ? host.Renderer.Spotlight.Request : null);
        if (!host.Config.AetheryteChips || request is not { } target || target == dismissedTarget ||
            !host.Config.TravelActions && (IsContextTarget(target) || target.Source == SpotlightSource.Quest) ||
            (!AetheryteSelection.Useful(target.MapId, target.Target, places.Context.MapId, places.Context.Player) && !IsContextTarget(target))) return;
        chipRequiresTravelActions = IsContextTarget(target) || target.Source == SpotlightSource.Quest;
        Chip = AetheryteSelection.Nearest(places.Entries, target.MapId, target.Target, places.Access,
            provider.ActiveAetheryte, available && host.Config.TravelActions);
        if (Chip is not { } choice) return;
        if (chipId != choice.Destination.Id) { chipId = choice.Destination.Id; ChipMessage = ""; chipConfirm = false; }
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        var size = new Vector2(MathF.Min(310 * s, max.X - min.X),
            (ChipMessage.Length > 0 && !chipConfirm ? 144 : 120) * s);
        if (size.X < 230 * s) return;
        ChipBounds = ChromeLayout.Place("nearest-travel", size, host.Config.TravelCardAnchor);
        if (ChipBounds == Vector4.Zero) return;
        var position = ChromeLayout.Position(ChipBounds);
        Panel("##nearest-aetheryte", position, size, () => {
            CardHeading("Nearest aetheryte", choice.Destination.Name,
                choice.Aethernet ? "Aethernet" : places.Access(choice.Destination).Cost is { } gil ? $"{gil:N0} gil" : "Cost unavailable");
            if (PrimaryTravelButton(chipConfirm ? "Confirm teleport" : choice.Aethernet ? "Travel here" : "Teleport", GratisIcons.Compass))
                RequestChip(choice, chipConfirm);
            ChipButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            ImGui.SameLine(0, GratisTheme.Gap * s);
            if (QuietButton("Dismiss")) { dismissedTarget = target; Chip = null; }
            ChipDismissButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            if (ChipMessage.Length > 0 && !chipConfirm) ImGui.TextWrapped(ChipMessage);
        });
    }

    public string RequestChip(AetheryteChoice choice, bool confirmed = false)
    {
        places.Refresh();
        if (chipRequiresTravelActions && !MapDrawContext.Current.Config.TravelActions || !canTravel() || !places.Access(choice.Destination).Attuned || provider.IsBusy)
            return ChipMessage = "Unable to teleport now.";
        ChipMessage = choice.Aethernet ? Request(choice.Destination, confirmed) : places.Teleport(choice.Destination, confirmed);
        chipConfirm = ChipMessage.StartsWith("Confirm ", StringComparison.Ordinal);
        return ChipMessage;
    }
}
