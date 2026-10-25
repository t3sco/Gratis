using System;
using System.Linq;
using System.Reflection;
using Gratis.Data;

namespace Gratis.Rendering;

public sealed class TweakDefinition {
    public string Id { get; }
    public string Group { get; }
    public char Icon { get; }
    public string Name { get; }
    public string Description { get; }
    public string ConfigPath { get; }
    public bool Default { get; }
    public bool Locked => ConfigPath == nameof(MapDrawingConfig.PvpMapOnly);
    public string? ConfigureSection { get; }
    public string? ConfigureGroup { get; }
    public string? IntegrationNote { get; }
    private readonly FieldInfo[] fields;

    internal TweakDefinition(string id, string group, char icon, string name, string description, string path,
        string? section, string? configureGroup, string? integrationNote) {
        Id = id;
        Group = group;
        Icon = icon;
        Name = name;
        Description = description;
        ConfigPath = path;
        ConfigureSection = section;
        ConfigureGroup = configureGroup;
        IntegrationNote = integrationNote;
        var type = typeof(SystemConfig);
        fields = path.Split('.').Select(part => {
            var field = type.GetField(part) ?? throw new ArgumentException($"Unknown tweak binding: {path}");
            type = field.FieldType;
            return field;
        }).ToArray();
        if (type != typeof(bool) && type != typeof(InterfaceAnimationMode))
            throw new ArgumentException($"Tweaks must be independent toggles: {path}");
        Default = Get(new SystemConfig());
    }

    public bool Get(SystemConfig config) {
        object? owner = config;
        foreach (var field in fields) {
            if (owner is null)
                return Default;
            owner = field.GetValue(owner);
        }
        return owner is InterfaceAnimationMode motion ? motion != InterfaceAnimationMode.Off : (bool)owner!;
    }

    public void Set(SystemConfig config, bool value) {
        if (Locked)
            value = true;
        object owner = config;
        foreach (var field in fields.AsSpan(0, fields.Length - 1)) {
            var next = field.GetValue(owner);
            if (next is null) {
                next = Activator.CreateInstance(field.FieldType)!;
                field.SetValue(owner, next);
            }
            owner = next;
        }
        fields[^1].SetValue(owner,
            fields[^1].FieldType == typeof(InterfaceAnimationMode) ? value ? InterfaceAnimationMode.Full
                : InterfaceAnimationMode.Off : value);
        if (ConfigPath == nameof(MapDrawingConfig.TravelActions))
            config.TravelActionsInitialized = true;
    }

    public bool IsModified(SystemConfig config) => ConfigPath == "InterfaceAnimations"
        ? config.InterfaceAnimations != InterfaceAnimationMode.Full : Get(config) != Default;
    public void Reset(SystemConfig config) => Set(config, Default);

    internal int Rank(string search) {
        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rank = 0;
        foreach (var word in words) {
            if (Name.Equals(word, StringComparison.OrdinalIgnoreCase))
                rank += 100;
            else if (Name.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                rank += 60;
            else if (Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                rank += 40;
            else if (Id.Contains(word, StringComparison.OrdinalIgnoreCase)
                || ConfigPath.Contains(word, StringComparison.OrdinalIgnoreCase))
                rank += 20;
            else if ((Group + " " + Description + " " + IntegrationNote).Contains(word,
                StringComparison.OrdinalIgnoreCase))
                rank += 10;
            else
                return -1;
        }
        return rank;
    }
}