-- Migration: Split Space.ManageMembers into Space.InviteMember, Space.UpdateMemberRole, Space.RemoveMember

START TRANSACTION;

-- 1. Remove mappings for the legacy Space.ManageMembers permission
DELETE FROM `Space_Role_Permission` 
WHERE `space_permission_id` IN (SELECT `id` FROM `Space_Permission` WHERE `name` = 'Space.ManageMembers');

-- 2. Delete the legacy Space.ManageMembers permission
DELETE FROM `Space_Permission` WHERE `name` = 'Space.ManageMembers';

-- 3. Insert the three granular permissions
INSERT INTO `Space_Permission` (`name`, `description`, `is_deleted`, `created_at`, `updated_at`)
VALUES
  ('Space.InviteMember', 'Invite new members to the monastery space', 0, NOW(), NOW()),
  ('Space.UpdateMemberRole', 'Update an existing member\'s space role', 0, NOW(), NOW()),
  ('Space.RemoveMember', 'Remove a member from the monastery space', 0, NOW(), NOW());

-- 4. Map the new permissions to Owner (space_role_id = 1) and Admin (space_role_id = 2)
INSERT INTO `Space_Role_Permission` (`space_role_id`, `space_permission_id`, `is_deleted`)
SELECT 1, sp.`id`, 0
FROM `Space_Permission` sp
WHERE sp.`name` IN ('Space.InviteMember', 'Space.UpdateMemberRole', 'Space.RemoveMember');

INSERT INTO `Space_Role_Permission` (`space_role_id`, `space_permission_id`, `is_deleted`)
SELECT 2, sp.`id`, 0
FROM `Space_Permission` sp
WHERE sp.`name` IN ('Space.InviteMember', 'Space.UpdateMemberRole', 'Space.RemoveMember');

COMMIT;
