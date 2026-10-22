-- Migration: Space Roles, Permissions, and Member Role Isolation
-- Date: 2026-10-22
-- Target Database: MySQL 8.0+ / InnoDB
-- Description: Creates Space_Role, Space_Permission, and Space_Role_Permission tables.
--              Migrates Monastery_Member and Invitation tables to reference Space_Role.
--              Deprecates space roles from the system-level Role table.

USE hsumchaint_db;

-- 1. Create Space_Role Table
CREATE TABLE IF NOT EXISTS Space_Role (
    id INT NOT NULL AUTO_INCREMENT,
    name VARCHAR(50) NOT NULL,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY UX_Space_Role_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Create Space_Permission Table
CREATE TABLE IF NOT EXISTS Space_Permission (
    id INT NOT NULL AUTO_INCREMENT,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY UX_Space_Permission_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Create Space_Role_Permission Table
CREATE TABLE IF NOT EXISTS Space_Role_Permission (
    id INT NOT NULL AUTO_INCREMENT,
    space_role_id INT NOT NULL,
    space_permission_id INT NOT NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    PRIMARY KEY (id),
    UNIQUE KEY UX_Space_Role_Permission (space_role_id, space_permission_id),
    KEY IX_Space_Role_Permission_role_id (space_role_id),
    KEY IX_Space_Role_Permission_permission_id (space_permission_id),
    CONSTRAINT FK_Space_Role_Permission_Space_Role_role_id
        FOREIGN KEY (space_role_id) REFERENCES Space_Role (id)
        ON DELETE CASCADE,
    CONSTRAINT FK_Space_Role_Permission_Space_Permission_permission_id
        FOREIGN KEY (space_permission_id) REFERENCES Space_Permission (id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. Seed Default Space Roles
INSERT INTO Space_Role (id, name, description, is_deleted) VALUES
    (1, 'Owner', 'Full control of monastery space (Monk creator only)', 0),
    (2, 'Admin', 'Monastery manager/trustee (Edit info, manage members & donations)', 0),
    (3, 'Editor', 'Accountant/helper (Record donations, schedule events)', 0),
    (4, 'Viewer', 'General member/donor (View space info and schedules, request donation)', 0)
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    description = VALUES(description),
    is_deleted = 0;

-- 5. Seed Default Space Permissions
INSERT INTO Space_Permission (id, name, description, is_deleted) VALUES
    (1, 'Space.View', 'View monastery space details and announcements', 0),
    (2, 'Space.Update', 'Update monastery name, description, and contact info', 0),
    (3, 'Space.Delete', 'Delete the monastery space (Owner only)', 0),
    (4, 'Space.InviteMember', 'Invite new members to the monastery space', 0),
    (5, 'Space.UpdateMemberRole', 'Update an existing member\'s space role', 0),
    (6, 'Space.RemoveMember', 'Remove a member from the monastery space', 0),
    (7, 'Space.TransferOwnership', 'Transfer ownership to another monk (Owner only)', 0),
    (8, 'Donation.View', 'View donations inside this monastery space', 0),
    (9, 'Donation.Create', 'Submit donation requests to this monastery', 0),
    (10, 'Donation.Review', 'Accept, reject, or review donation requests', 0),
    (11, 'Donation.Schedule', 'Schedule pickup/dropoff times for donations', 0),
    (12, 'Donation.Cancel', 'Cancel scheduled or accepted donations', 0)
ON DUPLICATE KEY UPDATE
    name = VALUES(name),
    description = VALUES(description),
    is_deleted = 0;

-- 6. Seed Space Role Permissions
-- Owner (Role 1): Full access to all space permissions
INSERT INTO Space_Role_Permission (space_role_id, space_permission_id, is_deleted)
SELECT 1, id, 0 FROM Space_Permission WHERE is_deleted = 0
ON DUPLICATE KEY UPDATE is_deleted = 0;

-- Admin (Role 2): Manage space info, members, and donations (excluding Delete and TransferOwnership)
INSERT INTO Space_Role_Permission (space_role_id, space_permission_id, is_deleted)
SELECT 2, id, 0 FROM Space_Permission WHERE name IN (
    'Space.View',
    'Space.Update',
    'Space.InviteMember',
    'Space.UpdateMemberRole',
    'Space.RemoveMember',
    'Donation.View',
    'Donation.Create',
    'Donation.Review',
    'Donation.Schedule',
    'Donation.Cancel'
) AND is_deleted = 0
ON DUPLICATE KEY UPDATE is_deleted = 0;

-- Editor (Role 3): Manage and schedule donations
INSERT INTO Space_Role_Permission (space_role_id, space_permission_id, is_deleted)
SELECT 3, id, 0 FROM Space_Permission WHERE name IN (
    'Space.View',
    'Donation.View',
    'Donation.Create',
    'Donation.Review',
    'Donation.Schedule'
) AND is_deleted = 0
ON DUPLICATE KEY UPDATE is_deleted = 0;

-- Viewer (Role 4): View details and submit donation requests
INSERT INTO Space_Role_Permission (space_role_id, space_permission_id, is_deleted)
SELECT 4, id, 0 FROM Space_Permission WHERE name IN (
    'Space.View',
    'Donation.View',
    'Donation.Create'
) AND is_deleted = 0
ON DUPLICATE KEY UPDATE is_deleted = 0;

-- 7. Migrate Monastery_Member Table
-- Add space_role_id column if not exists
SET @member_col_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Monastery_Member'
      AND COLUMN_NAME = 'space_role_id'
);

SET @sql_add_member_col = IF(@member_col_exists = 0,
    'ALTER TABLE Monastery_Member ADD COLUMN space_role_id INT NULL AFTER monastery_space_id;',
    'SELECT "space_role_id already exists in Monastery_Member";'
);
PREPARE stmt FROM @sql_add_member_col;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Backfill data into space_role_id
-- If is_owner = 1, assign Owner (id = 1)
UPDATE Monastery_Member SET space_role_id = 1 WHERE is_owner = 1 AND (space_role_id IS NULL OR space_role_id = 0);

-- If role_id column still exists, backfill from legacy role_id
SET @legacy_role_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Monastery_Member'
      AND COLUMN_NAME = 'role_id'
);

SET @sql_backfill_member = IF(@legacy_role_exists > 0,
    'UPDATE Monastery_Member SET space_role_id = CASE
        WHEN is_owner = 1 THEN 1
        WHEN role_id = 4 THEN 3
        WHEN role_id = 5 THEN 4
        ELSE 2
    END WHERE space_role_id IS NULL OR space_role_id = 0;',
    'UPDATE Monastery_Member SET space_role_id = 4 WHERE space_role_id IS NULL OR space_role_id = 0;'
);
PREPARE stmt FROM @sql_backfill_member;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Ensure all rows have a valid space_role_id
UPDATE Monastery_Member SET space_role_id = 4 WHERE space_role_id IS NULL;

-- Drop legacy foreign key and column if exists, add new FK and index
SET @fk_member_exists = (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Monastery_Member'
      AND CONSTRAINT_NAME = 'FK_Monastery_Member_Role_role_id'
);

SET @sql_drop_member_fk = IF(@fk_member_exists > 0,
    'ALTER TABLE Monastery_Member DROP FOREIGN KEY FK_Monastery_Member_Role_role_id;',
    'SELECT "Legacy FK_Monastery_Member_Role_role_id does not exist";'
);
PREPARE stmt FROM @sql_drop_member_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql_drop_member_role_col = IF(@legacy_role_exists > 0,
    'ALTER TABLE Monastery_Member DROP COLUMN role_id;',
    'SELECT "role_id column does not exist in Monastery_Member";'
);
PREPARE stmt FROM @sql_drop_member_role_col;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Enforce NOT NULL and FK to Space_Role
ALTER TABLE Monastery_Member MODIFY COLUMN space_role_id INT NOT NULL;

SET @fk_space_role_member_exists = (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Monastery_Member'
      AND CONSTRAINT_NAME = 'FK_Monastery_Member_Space_Role_space_role_id'
);

SET @sql_add_member_space_fk = IF(@fk_space_role_member_exists = 0,
    'ALTER TABLE Monastery_Member
        ADD CONSTRAINT FK_Monastery_Member_Space_Role_space_role_id
            FOREIGN KEY (space_role_id) REFERENCES Space_Role (id)
            ON DELETE RESTRICT,
        ADD KEY IX_Monastery_Member_space_role_id (space_role_id);',
    'SELECT "FK_Monastery_Member_Space_Role_space_role_id already exists";'
);
PREPARE stmt FROM @sql_add_member_space_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 8. Migrate Invitation Table
SET @inv_col_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Invitation'
      AND COLUMN_NAME = 'space_role_id'
);

SET @sql_add_inv_col = IF(@inv_col_exists = 0,
    'ALTER TABLE Invitation ADD COLUMN space_role_id INT NULL AFTER invited_by_id;',
    'SELECT "space_role_id already exists in Invitation";'
);
PREPARE stmt FROM @sql_add_inv_col;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Backfill Invitation space_role_id from legacy role column if exists
SET @legacy_inv_role_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Invitation'
      AND COLUMN_NAME = 'role'
);

SET @sql_backfill_inv = IF(@legacy_inv_role_exists > 0,
    'UPDATE Invitation SET space_role_id = CASE
        WHEN role IN (1, 2) THEN 2
        WHEN role = 4 THEN 3
        WHEN role = 5 THEN 4
        ELSE 4
    END WHERE space_role_id IS NULL OR space_role_id = 0;',
    'UPDATE Invitation SET space_role_id = 4 WHERE space_role_id IS NULL OR space_role_id = 0;'
);
PREPARE stmt FROM @sql_backfill_inv;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE Invitation SET space_role_id = 4 WHERE space_role_id IS NULL;

-- Drop legacy role column in Invitation if exists
SET @sql_drop_inv_role_col = IF(@legacy_inv_role_exists > 0,
    'ALTER TABLE Invitation DROP COLUMN role;',
    'SELECT "role column does not exist in Invitation";'
);
PREPARE stmt FROM @sql_drop_inv_role_col;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Enforce NOT NULL and FK to Space_Role on Invitation
ALTER TABLE Invitation MODIFY COLUMN space_role_id INT NOT NULL;

SET @fk_inv_space_role_exists = (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Invitation'
      AND CONSTRAINT_NAME = 'FK_Invitation_Space_Role_space_role_id'
);

SET @sql_add_inv_fk = IF(@fk_inv_space_role_exists = 0,
    'ALTER TABLE Invitation
        ADD CONSTRAINT FK_Invitation_Space_Role_space_role_id
            FOREIGN KEY (space_role_id) REFERENCES Space_Role (id)
            ON DELETE RESTRICT,
        ADD KEY IX_Invitation_space_role_id (space_role_id);',
    'SELECT "FK_Invitation_Space_Role_space_role_id already exists";'
);
PREPARE stmt FROM @sql_add_inv_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 9. Cleanup System-level Role Table
-- Soft-delete 'Editor' (4) and 'Viewer' (5) from System Role table so they cannot be assigned as system roles
UPDATE Role SET is_deleted = 1 WHERE name IN ('Editor', 'Viewer');
