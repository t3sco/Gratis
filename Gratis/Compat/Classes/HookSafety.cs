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
using System.Diagnostics;
using System.Reflection;
using Dalamud.Plugin.Services;

namespace Gratis.Compat.Classes;

public static class HookSafety {
    public static void ExecuteSafe(Action action, IPluginLog log, string? message = null) {
        try {
            action();
        }
        catch (Exception exception) {
            if (new StackTrace().GetFrame(1) is { } trace) {
                var callingClass = trace.GetMethod()?.DeclaringType;
                var callingName = trace.GetMethod()?.Name;

                log.Error($"Exception Source: {Assembly.GetCallingAssembly().GetName().Name} :: "
                    + $"{callingClass} :: {callingName}");
            }

            log.Error(exception, message ?? "Caught Exception Safely");
        }
    }
}