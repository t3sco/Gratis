/*
    Copyright (C) 2022  MidoriKami

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Linq;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace Gratis.Compat.CommandManager;

public class CommandHandler {
    public required string ActivationPath { get; init; }
    public required CommandHandlerDelegate Delegate { get; init; }
    public bool Hidden { get; init; }
    public int CommandLength => ActivationPath.Split("/").Prepend("/").Count(part => !part.IsNullOrEmpty());

    public bool CommandMatches(IPluginLog log, params string[] commandParts) {
        var thisCommandParts = ActivationPath.Split("/").Prepend("/").Where(part => !part.IsNullOrEmpty()).ToArray();

        return commandParts.Length == CommandLength
            && Enumerable.Range(0, CommandLength).All(index => thisCommandParts[index] == commandParts[index]);
    }
}