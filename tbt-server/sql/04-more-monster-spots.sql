-- More monsters on the leveling maps, to make leveling (and leveling again after a reset) faster.
--  * Maps with spawn areas (Lorencia, Devias, Elvenland): 1.5x the quantity of each area.
--    Noria already has about 1000 monsters and is left as is.
--  * Maps with single spawn points: one extra monster per point, in a 7x7 area around it.
--    The server only picks walkable tiles outside the safe zone for an area.
-- Only automatic spawns of monsters (ObjectKind 0, SpawnTrigger 0); no events, Kalima, Crywolf or quest maps.
-- Reversible with 04-revert-more-monster-spots.sql. Idempotent.
\set ON_ERROR_STOP on
begin;

create schema if not exists tbt;
create table if not exists tbt.spawn_quantity_backup ("Id" uuid primary key, "Quantity" smallint not null);

create temp view leveling_spawns as
select s.*, m."Number" as map
from config."MonsterSpawnArea" s
join config."GameMapDefinition" m on m."Id" = s."GameMapId"
join config."MonsterDefinition" d on d."Id" = s."MonsterDefinitionId"
where d."ObjectKind" = 0 and s."SpawnTrigger" = 0;

-- 1. Spawn areas on the starter maps: 0 Lorencia, 2 Devias, 51 Elvenland.
insert into tbt.spawn_quantity_backup ("Id", "Quantity")
select "Id", "Quantity" from leveling_spawns
where map in (0, 2, 51) and not ("X1" = "X2" and "Y1" = "Y2")
on conflict ("Id") do nothing;

update config."MonsterSpawnArea" s set "Quantity" = round(b."Quantity" * 1.5)
from tbt.spawn_quantity_backup b
where b."Id" = s."Id";

-- 2. Spawn points: 1 Dungeon, 4 Lost Tower, 7 Atlans, 8 Tarkan, 10 Icarus, 33 Aida, 37 Kanturu I,
--    38 Kanturu III, 56 Swamp of Calmness, 57 Raklion, 63 Vulcanus, 80 Karutan 1, 81 Karutan 2.
insert into config."MonsterSpawnArea"
  ("Id", "MonsterDefinitionId", "GameMapId", "X1", "Y1", "X2", "Y2", "Direction", "Quantity", "SpawnTrigger", "WaveNumber", "MaximumHealthOverride")
select md5('tbt-spot:' || s."Id"::text)::uuid, s."MonsterDefinitionId", s."GameMapId",
       greatest(s."X1" - 3, 0), greatest(s."Y1" - 3, 0), least(s."X1" + 3, 255), least(s."Y1" + 3, 255),
       s."Direction", 1, s."SpawnTrigger", s."WaveNumber", s."MaximumHealthOverride"
from leveling_spawns s
where s.map in (1, 4, 7, 8, 10, 33, 37, 38, 56, 57, 63, 80, 81) and s."X1" = s."X2" and s."Y1" = s."Y2"
on conflict ("Id") do nothing;

-- Check: monsters per changed map.
select l.map, split_part(m."Name", '||', 1) as map_name, sum(l."Quantity") as monsters,
       count(*) filter (where l."Id" in (select md5('tbt-spot:' || o."Id"::text)::uuid from config."MonsterSpawnArea" o)) as extra_areas
from leveling_spawns l join config."GameMapDefinition" m on m."Id" = l."GameMapId"
where l.map in (0, 2, 51, 1, 4, 7, 8, 10, 33, 37, 38, 56, 57, 63, 80, 81)
group by l.map, m."Name" order by l.map;

commit;
