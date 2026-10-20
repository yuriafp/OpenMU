-- Classic TBT classes and resets: no Summoner or Rage Fighter, reset system on.
\set ON_ERROR_STOP on
begin;

-- 1. Summoner (20) and Rage Fighter (24) can no longer be created.
update config."CharacterClass" set "CanGetCreated" = false where "Number" in (20, 24);

-- 2. The plugins which unlock them per account are switched off.
update config."PlugInConfiguration" set "IsActive" = false
where "TypeId" in ('2dffd751-7651-4fa1-93d1-9890ca0f04f1', '2dffd752-7652-4fa2-93d2-9890ca0f04f2');

-- 3. Reset system on, with its current (default) configuration.
update config."PlugInConfiguration" set "IsActive" = true
where "TypeId" = '6a9d585d-79d7-4674-b6ea-7e87392fa501';

-- 4. Regenerate the bots on the next start, so none of them are Summoners or Rage Fighters.
update config."PlugInConfiguration"
set "CustomConfiguration" = replace("CustomConfiguration", '"ResetBots": false', '"ResetBots": true')
where "TypeId" = '6f3a2b91-7c4e-4d88-9a1f-2e5c0b7a4d63';

-- Check
select "Number", split_part("Name", '||', 1) as name, "CanGetCreated" from config."CharacterClass" where "Number" in (20, 24);
select "TypeId", "IsActive", position('"ResetBots": true' in coalesce("CustomConfiguration", '')) > 0 as reset_bots
from config."PlugInConfiguration"
where "TypeId" in ('2dffd751-7651-4fa1-93d1-9890ca0f04f1', '2dffd752-7652-4fa2-93d2-9890ca0f04f2',
                   '6a9d585d-79d7-4674-b6ea-7e87392fa501', '6f3a2b91-7c4e-4d88-9a1f-2e5c0b7a4d63');

commit;
