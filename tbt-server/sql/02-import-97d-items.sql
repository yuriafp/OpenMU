-- Adds the 0.97d items which later became Dark Lord items as new items for their original classes.
-- Each new item is a copy of the Season 6 Dark Lord item with the same 3D model; the Season 6
-- items stay unchanged. Re-running fails on purpose if an item number is already taken.
\set ON_ERROR_STOP on
begin;

create function pg_temp.clone_item(
  p_group int, p_src int, p_new int, p_name text, p_classes int[],
  p_skill_number int,   -- null: no skill
  p_as_mace boolean,    -- turn a Dark Lord scepter into a knight mace
  p_drop_level int,     -- null: keep the source value
  p_dmg_min real,       -- null: keep the source value
  p_dmg_max real)       -- null: keep the source value
returns uuid language plpgsql as $$
declare
  src config."ItemDefinition"%rowtype;
  new_id uuid := gen_random_uuid();
  slot_id uuid;
  skill_id uuid;
  is_scepter constant uuid := '8650de3b-be11-458e-b352-4046ce264402';
  is_mace constant uuid := '47bd42d1-2e27-407b-92b5-925f418c30de';
  scepter_rise constant uuid := 'fb374862-d360-4ff0-ab88-2c170e6a9f85';
  dmg_min_attr constant uuid := '1ac59d93-6a52-4e88-8201-5f125a63b2a1';
  dmg_max_attr constant uuid := 'ec0d70be-839c-4bad-9fc5-1ad7438c75f8';
begin
  select * into strict src from config."ItemDefinition" where "Group" = p_group and "Number" = p_src;
  if exists (select 1 from config."ItemDefinition" where "Group" = p_group and "Number" = p_new) then
    raise exception 'item %/% already exists', p_group, p_new;
  end if;

  slot_id := src."ItemSlotId";
  if p_as_mace then
    -- the slot of a one-handed knight mace (Morning Star) instead of the scepter slot
    select "ItemSlotId" into strict slot_id from config."ItemDefinition" where "Group" = 2 and "Number" = 1;
  end if;
  if p_skill_number is not null then
    select "Id" into strict skill_id from config."Skill" where "Number" = p_skill_number;
  end if;

  insert into config."ItemDefinition" (
    "Id", "ItemSlotId", "ConsumeEffectId", "SkillId", "GameConfigurationId", "Number", "Width", "Height",
    "DropsFromMonsters", "IsAmmunition", "IsBoundToCharacter", "Name", "DropLevel", "MaximumItemLevel",
    "Durability", "Group", "Value", "MaximumSockets", "PetExperienceFormula", "StorageLimitPerCharacter",
    "MaximumDropLevel", "IsQuestItem", "IsDroppable", "IsPersonalStoreSellable", "IsRepairable",
    "IsSellableToNpc", "IsStorable", "IsTradable")
  values (
    new_id, slot_id, src."ConsumeEffectId", skill_id, src."GameConfigurationId", p_new, src."Width", src."Height",
    src."DropsFromMonsters", src."IsAmmunition", src."IsBoundToCharacter", p_name,
    coalesce(p_drop_level, src."DropLevel"), src."MaximumItemLevel",
    src."Durability", src."Group", src."Value", src."MaximumSockets", src."PetExperienceFormula", src."StorageLimitPerCharacter",
    src."MaximumDropLevel", src."IsQuestItem", src."IsDroppable", src."IsPersonalStoreSellable", src."IsRepairable",
    src."IsSellableToNpc", src."IsStorable", src."IsTradable");

  insert into config."ItemBasePowerUpDefinition" ("Id", "TargetAttributeId", "BonusPerLevelTableId", "ItemDefinitionId", "BaseValue", "AggregateType")
  select gen_random_uuid(),
         case when p_as_mace and b."TargetAttributeId" = is_scepter then is_mace else b."TargetAttributeId" end,
         b."BonusPerLevelTableId", new_id,
         case when b."TargetAttributeId" = dmg_min_attr and p_dmg_min is not null then p_dmg_min
              when b."TargetAttributeId" = dmg_max_attr and p_dmg_max is not null then p_dmg_max
              else b."BaseValue" end,
         b."AggregateType"
  from config."ItemBasePowerUpDefinition" b
  where b."ItemDefinitionId" = src."Id"
    and not (p_as_mace and b."TargetAttributeId" = scepter_rise);

  insert into config."AttributeRequirement" ("Id", "AttributeId", "GameMapDefinitionId", "ItemDefinitionId", "SkillId", "SkillId1", "MinimumValue")
  select gen_random_uuid(), r."AttributeId", r."GameMapDefinitionId", new_id, r."SkillId", r."SkillId1", r."MinimumValue"
  from config."AttributeRequirement" r where r."ItemDefinitionId" = src."Id";

  insert into config."ItemDefinitionCharacterClass" ("ItemDefinitionId", "CharacterClassId")
  select new_id, c."Id" from config."CharacterClass" c where c."Number" = any (p_classes);

  insert into config."ItemDefinitionItemOptionDefinition" ("ItemDefinitionId", "ItemOptionDefinitionId")
  select new_id, o."ItemOptionDefinitionId" from config."ItemDefinitionItemOptionDefinition" o where o."ItemDefinitionId" = src."Id";

  return new_id;
end $$;

-- Copies the set bonus groups (full set defense and defense rate) of a Season 6 armor set
-- to a new set; the bonus options themselves are shared, as between the Season 6 sets.
create function pg_temp.clone_set_bonus(p_src int, p_new int, p_label text) returns void language plpgsql as $$
declare
  g record;
  new_group uuid;
begin
  for g in
    select distinct s.*
    from config."ItemSetGroup" s
    join config."ItemOfItemSet" x on x."ItemSetGroupId" = s."Id"
    join config."ItemDefinition" i on i."Id" = x."ItemDefinitionId"
    where i."Group" between 7 and 11 and i."Number" = p_src
      -- normal set bonuses only, no ancient sets
      and not exists (select 1 from config."ItemOfItemSet" a where a."ItemSetGroupId" = s."Id" and a."AncientSetDiscriminator" <> 0)
  loop
    new_group := gen_random_uuid();
    insert into config."ItemSetGroup" ("Id", "GameConfigurationId", "Name", "AlwaysApplies", "CountDistinct", "MinimumItemCount", "SetLevel", "OptionsId")
    values (new_group, g."GameConfigurationId",
            p_label || ' ' || regexp_replace(split_part(g."Name", '||', 1), '^\S+ ', ''),
            g."AlwaysApplies", g."CountDistinct", g."MinimumItemCount", g."SetLevel", g."OptionsId");

    insert into config."ItemOfItemSet" ("Id", "ItemSetGroupId", "ItemDefinitionId", "BonusOptionId", "AncientSetDiscriminator")
    select gen_random_uuid(), new_group, n."Id", x."BonusOptionId", x."AncientSetDiscriminator"
    from config."ItemOfItemSet" x
    join config."ItemDefinition" i on i."Id" = x."ItemDefinitionId"
    join config."ItemDefinition" n on n."Group" = i."Group" and n."Number" = p_new
    where x."ItemSetGroupId" = g."Id" and i."Number" = p_src;

    insert into config."ItemDefinitionItemSetGroup" ("ItemDefinitionId", "ItemSetGroupId")
    select n."Id", new_group
    from config."ItemDefinition" n
    where n."Group" between 7 and 11 and n."Number" = p_new
      and exists (select 1 from config."ItemOfItemSet" x where x."ItemSetGroupId" = new_group and x."ItemDefinitionId" = n."Id");
  end loop;
end $$;

-- Maces (group 2): Dark Knight = 4, Blade Knight = 6, Blade Master = 7, Magic Gladiator = 12, Duel Master = 13.
select pg_temp.clone_item(2,  8, 30, 'Skeleton Mace',    array[4, 6, 7, 12, 13], null, true, null, null, null);
select pg_temp.clone_item(2,  9, 31, 'Slayer''s Mace',   array[6, 7, 12, 13],    null, true, null, null, null);
select pg_temp.clone_item(2, 10, 32, 'Stalker''s Mace',  array[6, 7, 12, 13],    null, true, null, null, null);
select pg_temp.clone_item(2, 11, 33, 'Mace of Gimli',    array[6, 7, 12, 13],    null, true, null, null, null);
select pg_temp.clone_item(2, 12, 34, 'Phantom Mace',     array[6, 7, 12, 13],    null, true, null, null, null);
-- No Season 6 item uses its model; just below the Phantom Mace, as in 0.97d, with Cyclone.
select pg_temp.clone_item(2, 12, 35, 'Mace of the King', array[6, 7, 12, 13],    22,   true, 135,  105, 117);

-- Embroidered set (Light Plate models), Dark Knight.
select pg_temp.clone_item( 7, 25, 100, 'Embroidered Helm',   array[4, 6, 7], null, false, null, null, null);
select pg_temp.clone_item( 8, 25, 100, 'Embroidered Armor',  array[4, 6, 7], null, false, null, null, null);
select pg_temp.clone_item( 9, 25, 100, 'Embroidered Pants',  array[4, 6, 7], null, false, null, null, null);
select pg_temp.clone_item(10, 25, 100, 'Embroidered Gloves', array[4, 6, 7], null, false, null, null, null);
select pg_temp.clone_item(11, 25, 100, 'Embroidered Boots',  array[4, 6, 7], null, false, null, null, null);
select pg_temp.clone_set_bonus(25, 100, 'Embroidered');

-- Magus set (Dark Steel models), Dark Knight; Magic Gladiator except the helm.
select pg_temp.clone_item( 7, 27, 101, 'Magus Mask',   array[4, 6, 7],         null, false, null, null, null);
select pg_temp.clone_item( 8, 27, 101, 'Magus Armor',  array[4, 6, 7, 12, 13], null, false, null, null, null);
select pg_temp.clone_item( 9, 27, 101, 'Magus Pants',  array[4, 6, 7, 12, 13], null, false, null, null, null);
select pg_temp.clone_item(10, 27, 101, 'Magus Gloves', array[4, 6, 7, 12, 13], null, false, null, null, null);
select pg_temp.clone_item(11, 27, 101, 'Magus Boots',  array[4, 6, 7, 12, 13], null, false, null, null, null);
select pg_temp.clone_set_bonus(27, 101, 'Magus');

-- Adamantine set of 0.97d (Dark Master models), Dark Wizard = 0, Soul Master = 2, Grand Master = 3.
select pg_temp.clone_item( 7, 28, 102, 'Adamantine Helm',   array[0, 2, 3], null, false, null, null, null);
select pg_temp.clone_item( 8, 28, 102, 'Adamantine Armor',  array[0, 2, 3], null, false, null, null, null);
select pg_temp.clone_item( 9, 28, 102, 'Adamantine Pants',  array[0, 2, 3], null, false, null, null, null);
select pg_temp.clone_item(10, 28, 102, 'Adamantine Gloves', array[0, 2, 3], null, false, null, null, null);
select pg_temp.clone_item(11, 28, 102, 'Adamantine Boots',  array[0, 2, 3], null, false, null, null, null);
select pg_temp.clone_set_bonus(28, 102, 'Adamantine 97d');

-- Check
select i."Group", i."Number", i."Name", i."DropLevel", i."Width" || 'x' || i."Height" as size,
  (select split_part(s."Name", '||', 1) from config."Skill" s where s."Id" = i."SkillId") as skill,
  (select string_agg(c."Number"::text, ',' order by c."Number") from config."ItemDefinitionCharacterClass" j join config."CharacterClass" c on c."Id" = j."CharacterClassId" where j."ItemDefinitionId" = i."Id") as classes,
  (select count(*) from config."ItemBasePowerUpDefinition" b where b."ItemDefinitionId" = i."Id") as powerups,
  (select count(*) from config."AttributeRequirement" r where r."ItemDefinitionId" = i."Id") as reqs,
  (select count(*) from config."ItemDefinitionItemOptionDefinition" o where o."ItemDefinitionId" = i."Id") as options,
  (select count(*) from config."ItemDefinitionItemSetGroup" g where g."ItemDefinitionId" = i."Id") as set_groups
from config."ItemDefinition" i
where (i."Group" = 2 and i."Number" between 30 and 35) or (i."Group" between 7 and 11 and i."Number" between 100 and 102)
order by i."Number", i."Group";

commit;
