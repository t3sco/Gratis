using System.Numerics;
using System.Text.Json;
using Gratis.Compat.Configuration;
using Gratis.Controllers;
using Gratis.Data;
using Gratis.Rendering;
using Gratis.Windows;
using ImGuiNET;

namespace Gratis.Preview;

internal static class QuestLogPhaseOneVerification {
    private static void Require(bool value, string message) => MarkerVisibilityVerification.Require(value, message);
    public static void Run() {
        var config = JsonSerializer.Deserialize<SystemConfig>("{}", Configuration.SerializerOptions)!;
        Require(!config.QuestLogReplaceJournal && !config.QuestLogShowLocked, "Old configs default to native Journal and no future quests");
        config.QuestLogReplaceJournal = config.QuestLogShowLocked = true;
        var saved = JsonSerializer.Deserialize<SystemConfig>(JsonSerializer.Serialize(config, Configuration.SerializerOptions),
            Configuration.SerializerOptions)!;
        Require(saved.QuestLogReplaceJournal && saved.QuestLogShowLocked, "New settings round trip");
        SettingsLayout.Reset(saved, "Map");
        Require(!saved.QuestLogReplaceJournal && !saved.QuestLogShowLocked, "Catalog reset restores both opt-outs");
        JournalState();

        var directory = Path.GetFullPath("_artifacts/work/questlog-phase1");
        Directory.CreateDirectory(directory);
        using (var driver = new ScriptedFrameDriver(Program.QuestLogConfig(1100, 800),
            new FrameScript { Width = 1100, Height = 800, QuestLog = true, Palette = true })) {
            double time = 0;
            void Step(Vector2? mouse = null, bool down = false, ImGuiKey[]? keys = null) =>
                driver.Step(new(time += .04, Mouse: mouse ?? new(-1000), Left: down, Keys: keys));
            void Settle() { for (var i = 0; i < 12; i++) Step(); }
            void Click(Vector4 b) {
                Require(b.Z > b.X && b.W > b.Y, "Clickable quest control has bounds");
                var mouse = new Vector2((b.X + b.Z) / 2, (b.Y + b.W) / 2);
                Step(mouse); Step(mouse, true); Step(mouse); Settle();
            }
            var panel = driver.Context.QuestLog;
            var fixture = (QuestLogFixture)driver.Context.QuestSource!;
            Settle();
            Require(panel.Tab == QuestLogTab.Accepted && panel.Results.Count == 8, "Accepted is the initial tab");
            driver.Save(Path.Combine(directory, "tabs-accepted.png"));
            Click(panel.TabBounds[1]);
            Require(panel.Tab == QuestLogTab.Completed && panel.Results.Single().Completed, "Completed tab uses history independently of acceptance");
            driver.Save(Path.Combine(directory, "tabs-completed.png"));
            Click(panel.Rows.First().Bounds);
            Require(panel.DetailOpen, "History details can be selected");
            Settle();
            driver.Save(Path.Combine(directory, "completed-detail.png"));
            Click(panel.DetailCloseBounds);
            Click(panel.TabBounds[2]);
            Require(!panel.DetailOpen && panel.Results.Count == 1 && panel.Results.All(q => q.ClientKnown), "Tab switching closes details and hides unknown future names");
            driver.Save(Path.Combine(directory, "tabs-available.png"));
            driver.Config.QuestLogShowLocked = true;
            Settle();
            Require(panel.Results.Count == 2, "Spoiler opt-in exposes future quests");
            Click(panel.Rows.Single(r => r.Id == 66003).Bounds);
            Require(panel.DetailOpen, "Explicit spoiler opt-in permits future quest detail");
            driver.Config.QuestLogShowLocked = false;
            Step();
            Require(!panel.DetailOpen && panel.DetailBounds == Vector4.Zero, "Spoiler opt-out immediately removes future detail without an exit fade");
            Settle();
            Require(panel.Results.Count == 1 && QuestLogModel.Filter(fixture.Snapshot, "Future", QuestLogTab.Unaccepted).Length == 0,
                "Spoiler policy applies before search and updates live");
            Require(QuestLogModel.Filter(fixture.Snapshot, "", QuestLogTab.Completed, repeatableOnly: true).Length == 1,
                "Repeatable chip filters historical completion");
            var mirror = new JournalMirrorState();
            var closes = 0;
            void NativeClose() {
                if (mirror.LocalClose() != JournalMirrorAction.CloseNative) return;
                closes++;
                Require(mirror.Show(true, true) == JournalMirrorAction.None, "Frame close blocks re-entrant native show");
                mirror.Hide(driver.Config.QuestLogPinned);
                mirror.EndClose(true, driver.Config.QuestLogPinned);
            }
            panel.Closed += NativeClose;
            void OpenMirrored() {
                mirror.Show(true, true);
                panel.OpenFromJournal();
                Settle();
            }
            OpenMirrored();
            Click(panel.CloseBounds);
            Require(closes == 1 && !panel.IsOpen, "Quest Log X closes the native Journal once");
            OpenMirrored();
            Step(keys: [ImGuiKey.Escape]); Step();
            Require(closes == 2 && !panel.IsOpen, "Quest Log Escape closes the native Journal once");
            OpenMirrored();
            Click(panel.Layout.Toggle);
            Require(closes == 3 && !panel.IsOpen, "Quest Log toggle closes the native Journal once");
            OpenMirrored();
            mirror.Hide(false);
            panel.CloseFromJournal();
            Require(closes == 3 && !panel.IsOpen, "Native hide mirrors without a recursive native close");
            driver.Config.QuestLogPinned = true;
            OpenMirrored();
            mirror.Hide(true);
            panel.CloseFromJournal();
            Require(panel.IsOpen && closes == 3, "Pinned log survives native PreHide");
            driver.Config.QuestLogPinned = false;
            panel.Closed -= NativeClose;
            driver.Config.QuestLogOpenWithMap = false;
            panel.OnClose(); panel.OpenFromJournal(); panel.OnOpen(driver.Config);
            Require(panel.IsOpen, "Journal request survives map-open initialization when open-with-map is off");
            panel.Toggle();
            Require(!panel.IsOpen, "Second toggle closes unpinned log");
            driver.Config.QuestLogPinned = true;
            panel.OpenFromJournal(); panel.Toggle();
            Require(panel.IsOpen, "Pinned log survives toggle");
        }
        Program.QuestJournalCards(directory);
        Console.WriteLine("Quest Log phase 1: Journal mirroring/close guard/fallback/pin, config migration/reset, card toggle, tabs, spoiler gating and PNGs verified.");
    }
    private static void JournalState() {
        var state = new JournalMirrorState();
        Require(state.Show(false, true) == JournalMirrorAction.Restore && !state.Mirroring,
            "Opt-out leaves the native Journal visible");
        Require(state.Show(true, false) == JournalMirrorAction.Restore && !state.Mirroring,
            "Unavailable map falls back to the native Journal");
        Require(state.Show(true, true) == (JournalMirrorAction.OpenLog | JournalMirrorAction.PushOffscreen)
            && state.Mirroring, "Native show opens the log and takes ownership of its presentation");
        Require(state.Draw(true, true, true) == JournalMirrorAction.PushOffscreen, "Each native draw stays offscreen");
        Require(state.Hide(false) == JournalMirrorAction.CloseLog && !state.Mirroring, "Native hide closes the unpinned log");
        Require(state.Draw(true, true, true) == JournalMirrorAction.PushOffscreen, "Native fade stays hidden");
        Require(state.Draw(true, false, true) == JournalMirrorAction.PushOffscreen, "Close fade stays offscreen after the Gratis map closes");
        Require(state.Draw(true, true, false) == JournalMirrorAction.Restore, "Completed fade restores the native root");
        state.Show(true, true);
        Require(state.Hide(true) == JournalMirrorAction.None, "Native hide preserves a pinned log");
        state.Show(true, true);
        Require(state.LocalClose() == JournalMirrorAction.CloseNative && state.ClosingNative, "Local close begins guarded native close");
        Require(state.Show(true, true) == JournalMirrorAction.None && state.LocalClose() == JournalMirrorAction.None,
            "Re-entrant show and close cannot reopen or recurse");
        Require(state.Hide(false) == JournalMirrorAction.None, "Our native PreHide does not close the panel recursively");
        state.EndClose(true, false);
        Require(!state.ClosingNative && !state.NativeVisible, "Successful close releases guard and native visibility");
        state.Show(true, true);
        state.LocalClose();
        Require(state.EndClose(false, false) == JournalMirrorAction.Restore && !state.Mirroring,
            "Rejected native close restores a reachable native window");
        state.Show(true, true);
        Require(state.Draw(false, true, true) == JournalMirrorAction.Restore && !state.Mirroring,
            "Opt-out, logout, PvP and dispose restore immediately");
        state.Show(true, true);
        Require(state.Draw(true, false, true) == (JournalMirrorAction.CloseNative | JournalMirrorAction.CloseLog
            | JournalMirrorAction.PushOffscreen) && state.ClosingNative,
            "Losing the Gratis map closes the native Journal without restoring it");
        Require(state.Draw(true, false, true) == JournalMirrorAction.PushOffscreen,
            "A pending native close cannot restore or close recursively");
        state.EndClose(true, false);
        Require(state.Draw(true, false, true) == JournalMirrorAction.PushOffscreen,
            "Map loss keeps the native close fade offscreen");
        state.Draw(true, false, false);
        state.Show(true, true, openedMap: true);
        Require(state.Show(true, true) == JournalMirrorAction.PushOffscreen && state.OpenedMap,
            "Repeated PreShow preserves map ownership");
        Require(state.Hide(false) == (JournalMirrorAction.CloseLog | JournalMirrorAction.CloseMap),
            "Journal hidden while mirroring closes the map when we opened it");
        Require(state.Hide(false) == JournalMirrorAction.None,
            "Repeated PreHide cannot close the map twice");
        state.Show(true, true, openedMap: true);
        Require(state.Hide(true) == JournalMirrorAction.None, "Pinned log preserves the map on native hide");
        state.Show(true, true, openedMap: true);
        Require(state.LocalClose() == (JournalMirrorAction.CloseNative | JournalMirrorAction.CloseMap),
            "Closing our log also closes a map opened for it");
        state.EndClose(true, false);
    }

}
