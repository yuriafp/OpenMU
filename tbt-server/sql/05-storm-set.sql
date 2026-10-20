-- Adds the 0.97d Storm set (groups 7-11, number 103) for Dark Wizard, Soul Master and Grand Master,
-- and Magic Gladiator and Duel Master except the helm. It has the looks of the Season 6 Adamantine set,
-- which stays a Dark Lord set. Its stats are balanced for the wizard sets instead of copied: drop levels
-- of the Season 6 Adamantine set, which fall between the Legendary and Grand Soul sets, and defense and
-- requirements in between those two sets at that drop level.
-- Re-running fails on purpose if the item number is already taken.
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



-- Sets the balanced stats of a cloned piece.
create function pg_temp.set_stats(p_group int, p_number int, p_durability int, p_defense real, p_attack_speed real,
                                  p_strength int, p_agility int) returns void language plpgsql as $$
declare
  item_id uuid;
begin
  select "Id" into strict item_id from config."ItemDefinition" where "Group" = p_group and "Number" = p_number;
  update config."ItemDefinition" set "Durability" = p_durability where "Id" = item_id;

  update config."ItemBasePowerUpDefinition" b set "BaseValue" = p_defense
  from config."AttributeDefinition" a
  where a."Id" = b."TargetAttributeId" and b."ItemDefinitionId" = item_id and split_part(a."Designation", '||', 1) = 'Base Defense';
  if p_attack_speed is not null then
    update config."ItemBasePowerUpDefinition" b set "BaseValue" = p_attack_speed
    from config."AttributeDefinition" a
    where a."Id" = b."TargetAttributeId" and b."ItemDefinitionId" = item_id and split_part(a."Designation", '||', 1) = 'Attack Speed Any';
  end if;

  update config."AttributeRequirement" r set "MinimumValue" = p_strength
  from config."AttributeDefinition" a
  where a."Id" = r."AttributeId" and r."ItemDefinitionId" = item_id and split_part(a."Designation", '||', 1) = 'Total Strength Requirement Value';
  update config."AttributeRequirement" r set "MinimumValue" = p_agility
  from config."AttributeDefinition" a
  where a."Id" = r."AttributeId" and r."ItemDefinitionId" = item_id and split_part(a."Designation", '||', 1) = 'Total Agility Requirement Value';
end $$;

-- Dark Wizard = 0, Soul Master = 2, Grand Master = 3, Magic Gladiator = 12, Duel Master = 13.
select pg_temp.clone_item( 7, 26, 103, 'Storm Helm',   array[0, 2, 3],         null, false, null, null, null);
select pg_temp.clone_item( 8, 26, 103, 'Storm Armor',  array[0, 2, 3, 12, 13], null, false, null, null, null);
select pg_temp.clone_item( 9, 26, 103, 'Storm Pants',  array[0, 2, 3, 12, 13], null, false, null, null, null);
select pg_temp.clone_item(10, 26, 103, 'Storm Gloves', array[0, 2, 3, 12, 13], null, false, null, null, null);
select pg_temp.clone_item(11, 26, 103, 'Storm Boots',  array[0, 2, 3, 12, 13], null, false, null, null, null);
select pg_temp.clone_set_bonus(26, 103, 'Storm');

--                           group durability defense speed strength agility
select pg_temp.set_stats( 7, 103, 56, 23, null, 45, 10);
select pg_temp.set_stats( 8, 103, 56, 29, null, 52, 13);
select pg_temp.set_stats( 9, 103, 56, 25, null, 50, 10);
select pg_temp.set_stats(10, 103, 56, 16, 3,    35,  5);
select pg_temp.set_stats(11, 103, 56, 17, null, 44,  5);

-- Check, next to the sets around it.
select i."Group" as g, i."Number" as n, split_part(i."Name", '||', 1) as item, i."DropLevel" as dlvl, i."Durability" as dur,
  (select string_agg(split_part(a."Designation", '||', 1) || '=' || b."BaseValue", ', ' order by a."Designation")
     from config."ItemBasePowerUpDefinition" b join config."AttributeDefinition" a on a."Id" = b."TargetAttributeId"
     where b."ItemDefinitionId" = i."Id" and b."BaseValue" <> 0) as powerups,
  (select string_agg(split_part(a."Designation", '||', 1) || '=' || r."MinimumValue", ', ' order by a."Designation")
     from config."AttributeRequirement" r join config."AttributeDefinition" a on a."Id" = r."AttributeId"
     where r."ItemDefinitionId" = i."Id") as requirements,
  (select string_agg(c."Number"::text, ',' order by c."Number") from config."ItemDefinitionCharacterClass" j
     join config."CharacterClass" c on c."Id" = j."CharacterClassId" where j."ItemDefinitionId" = i."Id") as classes,
  (select count(*) from config."ItemDefinitionItemOptionDefinition" o where o."ItemDefinitionId" = i."Id") as options,
  (select count(*) from config."ItemDefinitionItemSetGroup" s where s."ItemDefinitionId" = i."Id") as set_groups
from config."ItemDefinition" i
where i."Group" between 7 and 11 and i."Number" in (3, 18, 103)
order by i."Group", i."DropLevel";

commit;
