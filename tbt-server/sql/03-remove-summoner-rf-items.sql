-- Summoner and Rage Fighter can't be created, so their items should not drop, be sold or be crafted.
\set ON_ERROR_STOP on
begin;

create temp table sum_rf_items on commit drop as
select i."Id"
from config."ItemDefinition" i
where exists (select 1 from config."ItemDefinitionCharacterClass" j where j."ItemDefinitionId" = i."Id")
  and not exists (select 1 from config."ItemDefinitionCharacterClass" j join config."CharacterClass" c on c."Id" = j."CharacterClassId"
                  where j."ItemDefinitionId" = i."Id" and c."Number" not in (20, 22, 23, 24, 25));

-- 1. No monster drops.
update config."ItemDefinition" set "DropsFromMonsters" = false
where "Id" in (select "Id" from sum_rf_items) and "DropsFromMonsters";

-- 2. Not in the boxes and medals (Box of Kundun +1..+5, Silver and Gold Medal).
delete from config."ItemDropItemGroupItemDefinition" where "ItemDefinitionId" in (select "Id" from sum_rf_items);

-- 3. Not a result of the wing mixes, unless that would leave a mix without results.
delete from config."ItemCraftingResultItem" r
where r."ItemDefinitionId" in (select "Id" from sum_rf_items)
  and exists (select 1 from config."ItemCraftingResultItem" o
              where o."SimpleCraftingSettingsId" = r."SimpleCraftingSettingsId"
                and o."ItemDefinitionId" not in (select "Id" from sum_rf_items));

-- 4. Not sold by NPC merchants.
delete from data."Item" it
where it."DefinitionId" in (select "Id" from sum_rf_items)
  and it."ItemStorageId" in (select "MerchantStoreId" from config."MonsterDefinition" where "MerchantStoreId" is not null);

-- Check: nothing left.
select
  (select count(*) from config."ItemDefinition" where "Id" in (select "Id" from sum_rf_items) and "DropsFromMonsters") as still_dropping,
  (select count(*) from config."ItemDropItemGroupItemDefinition" where "ItemDefinitionId" in (select "Id" from sum_rf_items)) as in_boxes,
  (select count(*) from config."ItemCraftingResultItem" where "ItemDefinitionId" in (select "Id" from sum_rf_items)) as craft_results,
  (select count(*) from data."Item" it where it."DefinitionId" in (select "Id" from sum_rf_items)
     and it."ItemStorageId" in (select "MerchantStoreId" from config."MonsterDefinition" where "MerchantStoreId" is not null)) as in_stores;

commit;
