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

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace Gratis.Compat.Extensions;

public static class ConditionExtensions {
    public static bool IsBoundByDuty(this ICondition condition)
        => condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95);

    public static bool IsInCombat(this ICondition condition)
        => condition.Any(ConditionFlag.InCombat);

    public static bool IsBetweenAreas(this ICondition condition)
        => condition.Any(ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51);
}
