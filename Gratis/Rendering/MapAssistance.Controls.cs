#if GRATIS_PREVIEW
using ImGuiNET;
#else
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    private readonly SectionHeaderCache travelHeading = new();
    private void CardHeading(string label, string name, string detail, string subtitle = "") =>
        SectionHeader.Travel(travelHeading, label, name, detail, subtitle, GratisTheme.ChromeScale);

    internal static bool QuietButton(string text) =>
        global::Gratis.Rendering.ActionButton.Draw(text, text, new(ActionFaces.Quiet));

    internal static bool ActionButton(string text, char icon) =>
        global::Gratis.Rendering.ActionButton.Draw(text, text, new(ActionFaces.Action, Icon: icon));

    internal static bool PrimaryTravelButton(string text, char icon) =>
        global::Gratis.Rendering.ActionButton.Draw(text, text, new(ActionFaces.PrimaryTravel, Icon: icon));
}
