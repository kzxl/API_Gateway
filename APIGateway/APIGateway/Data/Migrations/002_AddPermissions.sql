-- Migration script for Permission System
-- Run this to add permission tables

-- Permissions table
CREATE TABLE IF NOT EXISTS Permissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Resource TEXT NOT NULL,
    Action TEXT NOT NULL,
    Description TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_permissions_name ON Permissions(Name);
CREATE INDEX IF NOT EXISTS idx_permissions_resource_action ON Permissions(Resource, Action);

-- RolePermissions table
CREATE TABLE IF NOT EXISTS RolePermissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Role TEXT NOT NULL,
    PermissionId INTEGER NOT NULL,
    FOREIGN KEY (PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE,
    UNIQUE(Role, PermissionId)
);

CREATE INDEX IF NOT EXISTS idx_rolepermissions_role ON RolePermissions(Role);

-- UserPermissions table
CREATE TABLE IF NOT EXISTS UserPermissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    PermissionId INTEGER NOT NULL,
    IsGranted INTEGER NOT NULL DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (PermissionId) REFERENCES Permissions(Id) ON DELETE CASCADE,
    UNIQUE(UserId, PermissionId)
);

CREATE INDEX IF NOT EXISTS idx_userpermissions_userid ON UserPermissions(UserId);

-- Add account lockout fields to Users table
ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER DEFAULT 0;
ALTER TABLE Users ADD COLUMN LockedUntil DATETIME;
ALTER TABLE Users ADD COLUMN LastFailedLogin DATETIME;

-- Seed default permissions
INSERT OR IGNORE INTO Permissions (Name, Resource, Action, Description) VALUES
('routes.read', 'routes', 'read', 'View routes'),
('routes.write', 'routes', 'write', 'Create/update routes'),
('routes.delete', 'routes', 'delete', 'Delete routes'),
('clusters.read', 'clusters', 'read', 'View clusters'),
('clusters.write', 'clusters', 'write', 'Create/update clusters'),
('clusters.delete', 'clusters', 'delete', 'Delete clusters'),
('users.read', 'users', 'read', 'View users'),
('users.write', 'users', 'write', 'Create/update users'),
('users.delete', 'users', 'delete', 'Delete users'),
('permissions.read', 'permissions', 'read', 'View permissions'),
('permissions.write', 'permissions', 'write', 'Manage permissions'),
('logs.read', 'logs', 'read', 'View logs'),
('logs.delete', 'logs', 'delete', 'Delete logs'),
('metrics.read', 'metrics', 'read', 'View metrics');

-- Grant all permissions to Admin role
INSERT OR IGNORE INTO RolePermissions (Role, PermissionId)
SELECT 'Admin', Id FROM Permissions;

-- Grant read-only permissions to User role
INSERT OR IGNORE INTO RolePermissions (Role, PermissionId)
SELECT 'User', Id FROM Permissions WHERE Action = 'read';
