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

using System;

namespace Gratis.Compat.CommandManager;

public enum ToggleCommandSubCommand {
    Enable,
    Disable,
    Toggle,
}

public class ToggleCommandHandler {
    public required string BaseActivationPath { get; init; }
    public required CommandHandlerDelegate EnableDelegate { get; init; }
    public required CommandHandlerDelegate DisableDelegate { get; init; }
    public required CommandHandlerDelegate ToggleDelegate { get; init; }
    public bool Hidden { get; set; }
    public bool UseShowHideText { get; set; }

    public CommandHandler GetCommandHandler(ToggleCommandSubCommand subCommand)
        => new CommandHandler {
            Delegate = subCommand switch {
                ToggleCommandSubCommand.Enable => EnableDelegate,
                ToggleCommandSubCommand.Disable => DisableDelegate,
                ToggleCommandSubCommand.Toggle => ToggleDelegate,
                _ => throw new ArgumentOutOfRangeException(nameof(subCommand), subCommand, null),
            },
            ActivationPath = BaseActivationPath + subCommand switch {
                ToggleCommandSubCommand.Enable => UseShowHideText ? "/show" : "/enable",
                ToggleCommandSubCommand.Disable => UseShowHideText ? "/hide" : "/disable",
                ToggleCommandSubCommand.Toggle => "/toggle",
                _ => throw new ArgumentOutOfRangeException(nameof(subCommand), subCommand, null),
            },
            Hidden = Hidden,
        };
}
