-- Migration script for Authentication system
-- Run this after the application creates the initial database

-- Add new tables for Refresh Tokens and User Sessions

CREATE TABLE IF NOT EXISTS RefreshTokens (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    Token TEXT NOT NULL UNIQUE,
    ExpiresAt DATETIME NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    CreatedByIp TEXT,
    RevokedAt DATETIME,
    RevokedByIp TEXT,
    ReplacedByToken TEXT,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_refreshtokens_token ON RefreshTokens(Token);
CREATE INDEX IF NOT EXISTS idx_refreshtokens_userid ON RefreshTokens(UserId);

CREATE TABLE IF NOT EXISTS UserSessions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    SessionId TEXT NOT NULL UNIQUE,
    AccessTokenJti TEXT NOT NULL,
    RefreshToken TEXT,
    IpAddress TEXT,
    UserAgent TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    LastActivityAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ExpiresAt DATETIME NOT NULL,
    RevokedAt DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_sessions_sessionid ON UserSessions(SessionId);
CREATE INDEX IF NOT EXISTS idx_sessions_jti ON UserSessions(AccessTokenJti);
CREATE INDEX IF NOT EXISTS idx_sessions_userid ON UserSessions(UserId);
