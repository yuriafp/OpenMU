# TBT server configuration

SQL scripts that turn a Season 6 OpenMU database into the private "classic MU TBT" server.
They change the game configuration in the database only; the server code is unchanged.

| Script | What it does |
|---|---|
| `01-classes-and-resets.sql` | Summoner and Rage Fighter can't be created, their unlock plugins are off, the reset system is on, and the bots are regenerated on the next start. |
| `02-import-97d-items.sql` | Adds the 0.97d items that later went to the Dark Lord as new items for their original classes. The Season 6 Dark Lord items stay as they are. |
| `03-remove-summoner-rf-items.sql` | Summoner and Rage Fighter items no longer drop from monsters, boxes or medals, aren't crafted by the wing mixes, and aren't sold by NPCs. |
| `04-more-monster-spots.sql` | More monsters on the leveling maps. `04-revert-more-monster-spots.sql` undoes it. |
| `05-storm-set.sql` | Adds the 0.97d Storm set for wizards and gladiators, with stats balanced for the wizard sets. |

## New items

All of them copy the Season 6 item with the same 3D model, so the client needs no new models.
Only the MuMain client shows them: its `Data/Items` JSON files need the matching entries
(branch `tbt/97d-items` of the MuMain fork).

| Item | Group / numbers | Classes | Copy of |
|---|---|---|---|
| Skeleton Mace | 2 / 30 | Dark Knight, Magic Gladiator | Battle Scepter |
| Slayer's Mace, Stalker's Mace, Mace of Gimli, Phantom Mace | 2 / 31-34 | Blade Knight, Magic Gladiator | Master, Great, Lord and Great Lord Scepter |
| Mace of the King | 2 / 35 | Blade Knight, Magic Gladiator | Great Lord Scepter, with level 135, 105-117 damage and Cyclone |
| Embroidered set | 7-11 / 100 | Dark Knight | Light Plate set |
| Magus set | 7-11 / 101 | Dark Knight; Magic Gladiator without the helm | Dark Steel set |
| Adamantine set (0.97d) | 7-11 / 102 | Dark Wizard | Dark Master set |
| Storm set | 7-11 / 103 | Dark Wizard; Magic Gladiator without the helm | Adamantine set, with balanced stats (below) |

The maces are knight maces: no scepter bonus, no pet command damage. The new armor sets get the
normal set bonuses of their source set, not its ancient sets.

The Storm set keeps the drop levels of the Season 6 Adamantine set (57-78), between the Legendary
and Grand Soul sets. Its defense and requirements lie between those two sets at that drop level,
instead of the higher Dark Lord values:

| Piece | Drop level | Defense | Strength | Agility |
|---|---|---|---|---|
| Helm | 66 | 23 | 45 | 10 |
| Armor | 78 | 29 | 52 | 13 |
| Pants | 70 | 25 | 50 | 10 |
| Gloves (attack speed 3) | 57 | 16 | 35 | 5 |
| Boots | 60 | 17 | 44 | 5 |

## More monsters

Only automatic monster spawns change; event maps, Kalima, Crywolf, Land of Trials and the quest maps
are left alone.

* Lorencia, Devias, Elvenland: 1.5 times the monsters of each spawn area. Noria already has about
  1000 monsters and is unchanged.
* Dungeon, Lost Tower, Atlans, Tarkan, Icarus, Aida, Kanturu I and III, Swamp of Calmness, Raklion,
  Vulcanus, Karutan 1 and 2: one extra monster per spawn point, somewhere in the 7x7 tiles around it.

The script keeps the original quantities in the `tbt` schema, so the revert script can restore them.

## How to run

Back up the database first, then run the scripts in order and restart the server, which caches the
configuration:

```powershell
docker exec database pg_dump -U postgres -d openmu -Fc -f /tmp/openmu-before-tbt.dump
docker cp database:/tmp/openmu-before-tbt.dump .
Get-Content -Raw tbt-server\sql\01-classes-and-resets.sql | docker exec -i database psql -U postgres -d openmu
docker restart openmu-startup
```

`02-import-97d-items.sql` and `05-storm-set.sql` stop with an error when an item number is already
taken, so they run once per database. The other scripts can run again.
