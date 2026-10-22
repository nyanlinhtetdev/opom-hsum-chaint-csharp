-- Migration: Granular Permission Separation for Users, Invitations, Notifications, and Space Members
-- Date: 2026-10-22
-- Target Database: MySQL 8.0+ / InnoDB

USE hsumchaint_db;

START TRANSACTION;

-- 1. Insert new system permissions
INSERT INTO `Permission` (`name`, `is_deleted`) VALUES
    ('Users.Update', 0),
    ('Invitation.View', 0),
    ('Invitation.Respond', 0),
    ('Notification.View', 0),
    ('Notification.Manage', 0)
ON DUPLICATE KEY UPDATE `is_deleted` = 0;

-- 2. Map system permissions to Role_Permission

-- System Admin (Role 1): Full access to all permissions
INSERT INTO `Role_Permission` (`role_id`, `permission_id`, `is_deleted`)
SELECT 1, p.`id`, 0 
FROM `Permission` p 
WHERE p.`is_deleted` = 0
ON DUPLICATE KEY UPDATE `is_deleted` = 0;

-- Monk (Role 2): Add profile update, invitations, and notifications
INSERT INTO `Role_Permission` (`role_id`, `permission_id`, `is_deleted`)
SELECT 2, p.`id`, 0 
FROM `Permission` p 
WHERE p.`name` IN ('Users.Update', 'Invitation.View', 'Invitation.Respond', 'Notification.View', 'Notification.Manage')
  AND p.`is_deleted` = 0
ON DUPLICATE KEY UPDATE `is_deleted` = 0;

-- Normal User (Role 3): Add profile update, invitations, and notifications
INSERT INTO `Role_Permission` (`role_id`, `permission_id`, `is_deleted`)
SELECT 3, p.`id`, 0 
FROM `Permission` p 
WHERE p.`name` IN ('Users.Update', 'Invitation.View', 'Invitation.Respond', 'Notification.View', 'Notification.Manage')
  AND p.`is_deleted` = 0
ON DUPLICATE KEY UPDATE `is_deleted` = 0;

-- 3. Insert new space permission
INSERT INTO `Space_Permission` (`name`, `description`, `is_deleted`) VALUES
    ('Space.ViewMembers', 'View monastery space members list', 0)
ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `is_deleted` = 0;

-- 4. Map Space.ViewMembers to Space Owner (1) and Space Admin (2)
INSERT INTO `Space_Role_Permission` (`space_role_id`, `space_permission_id`, `is_deleted`)
SELECT 1, sp.`id`, 0 
FROM `Space_Permission` sp 
WHERE sp.`name` = 'Space.ViewMembers' AND sp.`is_deleted` = 0
ON DUPLICATE KEY UPDATE `is_deleted` = 0;

INSERT INTO `Space_Role_Permission` (`space_role_id`, `space_permission_id`, `is_deleted`)
SELECT 2, sp.`id`, 0 
FROM `Space_Permission` sp 
WHERE sp.`name` = 'Space.ViewMembers' AND sp.`is_deleted` = 0
ON DUPLICATE KEY UPDATE `is_deleted` = 0;

COMMIT;
