using UnityEngine;

/// Puts the elevation colliders back to ground state.
///
/// The elevation triggers toggle shared scene colliders as you climb, and that
/// state has no way of unwinding itself if you leave the plateau without walking
/// back through an exit - dying up there being the obvious case. Respawning at
/// ground level while the mountain colliders are still disabled lets you walk
/// straight through cliffs.
///
/// Collider state is per-client (see Elevation_entre), so this is called on the
/// owning client only.
public static class ElevationReset
{
    public static void ToGround()
    {
        foreach (Elevation_entre entry in
                 Object.FindObjectsByType<Elevation_entre>(FindObjectsSortMode.None))
            Apply(entry.mountaincolliders, entry.boundarycolliders);

        foreach (Elevation_exit exit in
                 Object.FindObjectsByType<Elevation_exit>(FindObjectsSortMode.None))
            Apply(exit.mountaincolliders, exit.boundarycolliders);
    }

    static void Apply(Collider2D[] mountains, Collider2D[] boundaries)
    {
        if (mountains != null)
            foreach (Collider2D m in mountains)
                if (m != null) m.enabled = true;      // cliffs solid again

        if (boundaries != null)
            foreach (Collider2D b in boundaries)
                if (b != null) b.enabled = false;     // plateau edges off
    }
}
