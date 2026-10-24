using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace KamiLib.Classes;

public struct UserRegex {
	private const RegexOptions DefaultOptions = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Multiline | RegexOptions.NonBacktracking;

	[JsonIgnore] private string internalText = string.Empty;

	public string Text {
		get => internalText;
		set => UpdateText(value);
	}

	[JsonIgnore] public Regex? Regex { get; set; }

	public UserRegex() { }

	public UserRegex(string text, RegexOptions regex = DefaultOptions)
		=> UpdateText(text, regex);

	public UserRegex(Regex? regex)
		=> UpdateText(regex);

	public void UpdateText(string text, RegexOptions options = DefaultOptions) {
		this.internalText = text;
		Regex = BuildRegex(text, options);
	}

	public void UpdateText(Regex? regex) {
		this.internalText = regex?.ToString() ?? string.Empty;
		Regex = regex;
	}

	public bool Match(string text) => Text.Length == 0 || (Regex?.IsMatch(text) ?? text.Contains(Text, StringComparison.OrdinalIgnoreCase));

	private static Regex? BuildRegex(string text, RegexOptions options) {
		if (text.Length == 0)
			return null;

		try {
			return new Regex(text, options | RegexOptions.Compiled);
		}
		catch {
			return null;
		}
	}

	public static bool DrawRegexInput(string label, ref UserRegex regex, string hint = "", Action? tooltip = null, float width = 0, uint failureColor = 0, RegexOptions options = DefaultOptions) {
		if (width != 0)
			ImGui.SetNextItemWidth(width);
		var text = regex.Text;

		bool change;
		var  drawBorder = failureColor != 0 && text.Length > 0 && regex.Regex == null;
		using (var _ = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale, drawBorder)) {
			using var color  = ImRaii.PushColor(ImGuiCol.Border, failureColor, drawBorder);
			change = ImGui.InputTextWithHint(label, hint, ref text, 256, ImGuiInputTextFlags.EnterReturnsTrue);
		}

		if (tooltip != null && ImGui.IsItemHovered())
			tooltip();

		if (text != regex.Text)
			regex.UpdateText(text, options);

		return change;
	}
}