-- Reverts 04-more-monster-spots.sql: original quantities back, extra spawn areas removed.
\set ON_ERROR_STOP on
begin;

update config."MonsterSpawnArea" s set "Quantity" = b."Quantity"
from tbt.spawn_quantity_backup b
where b."Id" = s."Id";

delete from config."MonsterSpawnArea" x
using config."MonsterSpawnArea" s
where x."Id" = md5('tbt-spot:' || s."Id"::text)::uuid;

drop table tbt.spawn_quantity_backup;
drop schema tbt;

commit;
