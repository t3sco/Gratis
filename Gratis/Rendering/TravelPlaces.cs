using System.Collections.Generic;
using Gratis.Controllers;

namespace Gratis.Rendering;

public interface ITravelPlaces {
    IEnumerable<PlaceEntry> Entries { get; }
    PlaceSearchContext Context { get; }
    void Refresh();
    PlaceAccess Access(PlaceEntry place);
    string Teleport(PlaceEntry place, bool confirmed);
    void Show(PlaceEntry place);
}